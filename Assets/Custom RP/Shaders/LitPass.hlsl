#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

//#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

// CBUFFER_START(UnityPerMaterial)
//     float4 _BaseColor;
// CBUFFER_END

// TEXTURE2D(_BaseMap);
// SAMPLER(sampler_BaseMap);

// UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
//     UNITY_DEFINE_INSTANCED_PROP(float4,_BaseMap_ST)
//     UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)
//     UNITY_DEFINE_INSTANCED_PROP(float,_CutOff)
//     UNITY_DEFINE_INSTANCED_PROP(float,_Metallic)
//     UNITY_DEFINE_INSTANCED_PROP(float,_Smoothness)
// UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes
{
    float2 baseUV : TEXCOORD0;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float3 positionOS : POSITION;
    //GI在Attribute中对应的宏，来开启对应的功能
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 baseUV : VAR_BASE_UV;
    float2 detailUV : VAR_DETAIL_UV;
    float3 normalWS : VAR_NORMAL;
#if defined(_NORMAL_MAP)
    float4 tangentWS : VAR_TANGENT;
#endif
    float3 positionWS : VAR_POSITION;
    float4 positionCS : SV_POSITION;
    //GI在VARYINGS中对应的宏
    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex(Attributes input)
{
    Varyings output;

    //获得Instance的ID
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,output);
    //获得GI数据,即LightMaps数据，并转换数据
    TRANSFER_GI_DATA(input,output);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    //float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_BaseMap_ST);    
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.baseUV = TransformBaseUV(input.baseUV);
#if defined(_DETAIL_MAP)
    output.detailUV = TransformDetailUV(input.baseUV);
#endif
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_NORMAL_MAP)
    output.tangentWS =
        float4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w);
#endif
    
    return output;
}

float4 LitPassFragment(Varyings input):SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
// //Unity的unity_LODFade的x变量包含淡入淡出的因子,y变量也是相同因子但是变为十六步
// #if defined(LOD_FADE_CROSSFADE)
// //fadeOut值从1减到0,所以超过所有LOD等级的物品会变黑,故取反来让超过0的不变黑
// //请注意有两个LOD级别的对象不会与自己发生CrossFade
//     return -unity_LODFade.x;
// #endif
    // 调用函数而不是直接返回unity_LODFade.x
    ClipLOD(input.positionCS.xy,unity_LODFade.x);

    // float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.baseUV);
    // float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_BaseColor);
    InputConfig config = GetInputConfig(input.baseUV,input.detailUV);
#if defined(_MASK_MAP)
    config.useMask = true;
#endif
#if defined(_DETAIL_MAP)
    config.useDetail = true;
#endif
    float4 base = GetBase(config);
    
#if defined(_CLIPPING)
    clip(base.a - GetCutOff(config));
#endif    

    Surface surface = (Surface)0;   

    surface.position = input.positionWS;
#if defined(_NORMAL_MAP)
    surface.normal =
        NormalTangentToWorld(GetNormalTS(config),input.normalWS,input.tangentWS);
    // 当使用法线贴图时通过切线空间获得法线数据时,使用此项来给阴影的bias使用
    surface.interpolatedNormal = input.normalWS;
#else
    surface.normal = normalize(input.normalWS);
    surface.interpolatedNormal = surface.normal;
#endif
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(config);
    surface.occlusion = GetOcclusion(config);
    surface.smoothness = GetSmoothness(config);
    surface.fresnelStrength = GetFresnel(config);
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    //视图空间和世界空间深度值是相同的，因为只进行了旋转和平移
    surface.depth = -TransformWorldToView(input.positionWS).z;
    //通过该函数使用给定的屏幕空间XY的位置，生成旋转的平铺抖动图案
    surface.dither = InterleavedGradientNoise(input.positionCS.xy,0);
    surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

#if defined(_PREMULTIPLY_ALPHA)
    BRDF brdf = GetBRDF(surface,true);
#else
    BRDF brdf = GetBRDF(surface);
#endif

    //输出从unity中获得的数据，如光照贴图等
    GI gi = GetGI(GI_FRAGMENT_DATA(input),surface,brdf);
    float3 color = GetLighting(surface,brdf,gi);
    color += GetEmission(config);
    
    return float4(color, GetFinalAlpha(surface.alpha));
}

#endif