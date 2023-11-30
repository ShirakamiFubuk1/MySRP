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

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
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
    float4x4 _DirectionalShadowMatrices
        [MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
CBUFFER_END

struct DirectionalShadowData
{
    float strength;
    float normalBias;
    int tileIndex;
};

struct ShadowMask
{
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

//计算混合效果
float FadedShadowStrength(float distance,float scale,float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;

    //初始化shadowMask
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
    if(i==_CascadeCount)
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

float FilterDirectionalShadow(float3 positionSTS)
{
    //只在定义时采样一次，其余时间只用调用
    #if defined(DIRECTIONAL_FILTER_SETUP)
        float weights[DIRECTIONAL_FILTER_SAMPLES];
        float2 positions[DIRECTIONAL_FILTER_SAMPLES];
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

float GetBakedShadow(ShadowMask mask)
{
    float shadow = 1.0;
    if(mask.distance)
    {
        shadow = mask.shadows.r;
    }
    return shadow;
}

float MixBakedAndRealtimeShadows(ShadowData global,float shadow,float strength)
{
    float baked = GetBakedShadow(global.shadowMask);
    if(global.shadowMask.distance)
    {
        shadow = lerp(baked,shadow,global.strength);
        return lerp(1.0,shadow,strength);
    }

    return lerp(1.0,shadow,strength * global.strength);
}

float GetCascadedShadow(
    DirectionalShadowData directional,ShadowData global,Surface surfaceWS)
{
    //通过沿着表面法线方向乘以texel贴片和固定便宜获得法线偏移
    float3 normalBias = surfaceWS.normal *
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
        normalBias = surfaceWS.normal *
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
    if(directional.strength <= 0.0)
    {
        shadow = 1.0;
    }
    else
    {
        shadow = GetCascadedShadow(directional,global,surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global,shadow,directional.strength);
    }

    //阴影的衰减因子是一个0-1的值,如果片段完全被遮蔽则为0，没有被遮挡则为1，0-1表示部分被遮蔽
    //当光的阴影强度降低为0时，衰减就不受其影响而为1
    //故最终的衰减应该为1和采样到的阴影进行线性插值
    return shadow;
}

#endif