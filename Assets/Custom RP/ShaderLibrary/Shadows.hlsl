#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

//使用HLSL文件中定义的函数
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#if defined(_OTHER_PCF3)
    #define OTHER_FILTER_SAMPLES 4
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
    #define OTHER_FILTER_SAMPLES 9
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
    #define OTHER_FILTER_SAMPLES 16
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
//因为只有一种合适的方法可以对阴影贴图进行采样,因此显示定义一个采样器
//而不是依赖Unity的宏TEXTURE2D_SHADOWSAMPLER_CMP为纹理采样器推断状态
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
    int _CascadeCount;
    //float _ShadowDistance;
    float4 _ShadowAtlasSize;
    float4 _ShadowDistanceFade;
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    //将级联数据添加到_CustomShadows中的缓冲区中
    float4 _CascadeData[MAX_CASCADE_COUNT];
    float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
    float4x4 _DirectionalShadowMatrices
        [MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
    float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
CBUFFER_END

struct DirectionalShadowData
{
    float strength;
    float normalBias;
    int tileIndex;
    //由于shadowMask有四个通道,因此最多可以支持四个混合光源
    //烘焙时最重要的灯获得红色通道,第二盏灯获得绿色通道,以此类推
    //当混合模式灯光超过四个,Unity会将前四个以外的所有混合模式灯光转换为完全烘焙的灯光
    //除了直接光之外其他光源类型影响范围有限,因此可以将同一通道用于多个光源
    //由于灯光顺序在运行中可能会发生变化,因为灯光可能更改或者禁用,故不能依赖灯光顺序
    //因此我们通过检索光源的shadowMaskChannel索引shadowMask的RGBA通道
    //当灯光不使用shadowMask通过设置返回-1
    int shadowMaskChannel;
};

struct ShadowMask
{
    bool always;
    bool distance;
    float4 shadows;
};

struct ShadowData
{
    int cascadeIndex;
    float strength;
    float cascadeBlend;
    ShadowMask shadowMask;
};

struct OtherShadowData
{
    float strength;
    int tileIndex;
    bool isPoint;
    int shadowMaskChannel;
    float3 lightPositionWS;
    float3 lightDirectionWS;
    float3 spotDirectionWS;
};

static const float3 pointShadowPlanes[6] = {
    float3(-1.0,0.0,0.0),
    float3(1.0,0.0,0.0),
    float3(0.0,-1.0,0.0),
    float3(0.0,1.0,0.0),
    float3(0.0,0.0,-1.0),
    float3(0.0,0.0,1.0)
};

//计算混合效果
float FadedShadowStrength(float distance,float scale,float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;

    //初始化shadowMask,默认使用alwasy
    data.shadowMask.always = true;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;
    data.cascadeBlend = 1.0;
    //不再将strength初始化为0,而是根据FadedShadowStrength提供的衰减值判断
    data.strength = FadedShadowStrength(
        surfaceWS.depth,_ShadowDistanceFade.x,_ShadowDistanceFade.y
        );
    int i;
    for(i = 0;i<_CascadeCount;i++)
    {
        //w为该Cascade球的半径平方
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSpr = DistanceSquared(surfaceWS.position,sphere.xyz);
        //如果点离球心的距离小于cascade球半径平方则在当前级联球内
        if(distanceSpr<sphere.w)
        {
            //直接使用球半径平方倒数作为参数，以做优化项
            float fade = FadedShadowStrength(
                distanceSpr,_CascadeData[i].x,_ShadowDistanceFade.z
                );
            if(i==_CascadeCount-1)
            {
                data.strength *= fade;
            }
            else
            {
                data.cascadeBlend = fade;
            }
            break;
        }
    }
    //不在阴影范围内
    // 需要确定全局阴影强度在Cascade循环之后是正确的
    if(i == _CascadeCount && _CascadeCount > 0)
    {
        data.strength=0;
    }
    ////当使用抖动混合时，如果不在上一个级联中，且混合值小于抖动值，则跳转到下一个级联
    #if defined(_CASCADE_BLEND_DITHER)
        else if(data.cascadeBlend < surfaceWS.dither)
        {
            i +=1;
        }
    #endif
    //在不适用软混合时，将级联混合设置为0，来剔除这个分支
    #if !defined(_CASCADE_BLEND_SOFT)
        data.cascadeBlend = 1.0;
    #endif
    //获得当前片段对应的Cascade级数
    data.cascadeIndex = i;
    return data;
}

float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    //对阴影贴图进行采样.
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas,SHADOW_SAMPLER,positionSTS);
}

float SampleOtherShadowAtlas(float3 positionSTS,float3 bounds)
{
    positionSTS.xy = clamp(positionSTS.xy,bounds.xy,bounds.xy + bounds.z);
    //对阴影贴图进行采样.
    return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas,SHADOW_SAMPLER,positionSTS);
}


float FilterDirectionalShadow(float3 positionSTS)
{
    //只在定义时采样一次，其余时间只用调用
    #if defined(DIRECTIONAL_FILTER_SETUP)
        real weights[DIRECTIONAL_FILTER_SAMPLES];
        real2 positions[DIRECTIONAL_FILTER_SAMPLES];
        float4 size = _ShadowAtlasSize.yyxx;
        //1 xy表示texelsize zw表示整个贴图尺寸，2 原始采样位置，34 输出每个部分的权重和位置
        DIRECTIONAL_FILTER_SETUP(size,positionSTS.xy,weights,positions);
        float shadow = 0;
        for(int i = 0;i < DIRECTIONAL_FILTER_SAMPLES;i++)
        {
            shadow += weights[i] * SampleDirectionalShadowAtlas(
                float3(positions[i].xy,positionSTS.z)
                );
        }
        return shadow;
    #else
        return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}

float FilterOtherShadow(float3 positionSTS,float3 bounds)
{
    //只在定义时采样一次，其余时间只用调用
    #if defined(OTHER_FILTER_SETUP)
    real weights[OTHER_FILTER_SAMPLES];
    real2 positions[OTHER_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.wwzz;
    //1 xy表示texelsize zw表示整个贴图尺寸，2 原始采样位置，34 输出每个部分的权重和位置
    OTHER_FILTER_SETUP(size,positionSTS.xy,weights,positions);
    float shadow = 0;
    for(int i = 0;i < OTHER_FILTER_SAMPLES;i++)
    {
        shadow += weights[i] * SampleOtherShadowAtlas(
            float3(positions[i].xy,positionSTS.z),bounds
            );
    }
    return shadow;
#else
    return SampleOtherShadowAtlas(positionSTS,bounds);
#endif
}

float GetBakedShadow(ShadowMask mask,int channel)
{
    float shadow = 1.0;
    if(mask.always || mask.distance)
    {
        //达到最大距离则切换为shadowMask阴影，根据channel判断
        if(channel >= 0)
        {
            shadow = mask.shadows[channel];
        }
    }
    return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel , float strength)
{
    if (mask.always || mask.distance) {
        return lerp(1.0, GetBakedShadow(mask, channel), strength);
    }
    return 1.0;
}

float MixBakedAndRealtimeShadows(
    ShadowData global,float shadow,int shadowMaskChannel,float strength)
{
    float baked = GetBakedShadow(global.shadowMask,shadowMaskChannel);
    if(global.shadowMask.always)
    {
        //阴影必须通过全局强度进行控制改变强度,然后选取baked和shadow的最小值来组合两种阴影
        //然后再用灯光的阴影强度应用合并后的阴影
        shadow = lerp(1.0,shadow,global.strength);
        shadow = min(baked,shadow);

        return lerp(1.0,shadow,strength);
    }
    if(global.shadowMask.distance)
    {
        //基于全局强度在烘焙阴影和实时阴影之间进行插值,然后应用光源的阴影强度
        shadow = lerp(baked,shadow,global.strength);
        return lerp(1.0,shadow,strength);
    }

    //当不适用shadowMask时仅仅是把强度组合并应用于实时阴影
    return lerp(1.0,shadow,strength * global.strength);
}

//拆分GetDirectionalShadowAttenuation的功能防止高耦合
float GetCascadedShadow(
    DirectionalShadowData directional,ShadowData global,Surface surfaceWS)
{
    //通过沿着表面法线方向乘以texel贴片和固定便宜获得法线偏移
    float3 normalBias = surfaceWS.interpolatedNormal *
        (directional.normalBias * _CascadeData[global.cascadeIndex].y);
    //影子空间位置
    float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex],
        float4(surfaceWS.position + normalBias, 1.0)
    ).xyz;
    //采样阴影
    float shadow =  FilterDirectionalShadow(positionSTS);
    //在检索第一个阴影值之后检查cascadeBlend是否小于1，小于则处于过渡区
    //还必须从下一个级联中采样，并在两个值中进行插值
    if(global.cascadeBlend < 1.0)
    {
        normalBias = surfaceWS.interpolatedNormal *
            (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
        positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1],
            float4(surfaceWS.position + normalBias,1.0)
            ).xyz;
        //混合第一层第二层之间的阴影
        shadow = lerp(
            FilterDirectionalShadow(positionSTS),shadow,global.cascadeBlend
        );
    }
    return shadow;
}

//计算此处被光照遮蔽的程度
float GetDirectionalShadowAttenuation(
    DirectionalShadowData directional,ShadowData global, Surface surfaceWS)
{
#if !defined(_RECEIVE_SHADOWS)
    return 1.0;
#endif

    float shadow;
    //检测组合强度是否小于0,若小于则直接返回烘焙阴影并跳过实时阴影采样
    if(directional.strength * global.strength <= 0.0)
    {
        shadow = GetBakedShadow(global.shadowMask,directional.shadowMaskChannel,abs(directional.strength));
    }
    else
    {
        shadow = GetCascadedShadow(directional,global,surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global,shadow,directional.shadowMaskChannel,directional.strength);
    }

    //阴影的衰减因子是一个0-1的值,如果片段完全被遮蔽则为0，没有被遮挡则为1，0-1表示部分被遮蔽
    //当光的阴影强度降低为0时，衰减就不受其影响而为1
    //故最终的衰减应该为1和采样到的阴影进行线性插值
    return shadow;
}

float GetOtherShadow(OtherShadowData other,ShadowData global,Surface surfaceWS)
{
    float tileIndex = other.tileIndex;
    float3 lightPlane = other.spotDirectionWS;
    if(other.isPoint)
    {
        float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
        tileIndex += faceOffset;
        lightPlane = pointShadowPlanes[faceOffset];
    }
    float4 tileData = _OtherShadowTiles[tileIndex];
    float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
    float distanceToLightPlane = dot(surfaceToLight,lightPlane);
    float3 normalBias = surfaceWS.interpolatedNormal * (distanceToLightPlane * tileData.w);
    float4 positionSTS = mul(
        _OtherShadowMatrices[tileIndex],
        float4(surfaceWS.position + normalBias,1.0));
    return FilterOtherShadow(positionSTS.xyz / positionSTS.w,tileData.xyz);
}

float GetOtherShadowAttenuation(OtherShadowData other,ShadowData global,Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
    return 1.0;
    #endif

    float shadow;
    if(other.strength * global.strength <= 0.0)
    {
        shadow = GetBakedShadow(
            global.shadowMask,other.shadowMaskChannel,abs(other.strength));
    }
    else
    {
        shadow = GetOtherShadow(other,global,surfaceWS);
        shadow = MixBakedAndRealtimeShadows(
            global,shadow,other.shadowMaskChannel,other.strength);
    }
    return shadow;
}

#endif