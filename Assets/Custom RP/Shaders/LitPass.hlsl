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
    float3 positionOS : POSITION;
    //GI在Attribute中对应的宏，来开启对应的功能
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 baseUV : VAR_BASE_UV;
    float3 normalWS : VAR_NORMAL;
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
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);

    return output;
}

float4 LitPassFragment(Varyings input):SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
// #if defined(LOD_FADE_CROSSFADE)
//     return -unity_LODFade.x;
// #endif
    ClipLOD(input.positionCS.xy,unity_LODFade.x);

    // float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.baseUV);
    // float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_BaseColor);
    float4 base = GetBase(input.baseUV);
    
#if defined(_CLIPPING)
    clip(base.a - GetCutOff(input.baseUV));
#endif    

    Surface surface = (Surface)0;   

    surface.position = input.positionWS;
    surface.normal = normalize(input.normalWS);
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(input.baseUV);
    surface.smoothness = GetSmoothness(input.baseUV);
    surface.fresnelStrength = GetFresnel(input.baseUV);
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    //视图空间和世界空间深度值是相同的，因为只进行了旋转和平移
    surface.depth = -TransformWorldToView(input.positionWS).z;
    //通过该函数使用给定的屏幕空间XY的位置，生成旋转的平铺抖动图案
    surface.dither = InterleavedGradientNoise(input.positionCS.xy,0);

#if defined(_PREMULTIPLY_ALPHA)
    BRDF brdf = GetBRDF(surface,true);
#else
    BRDF brdf = GetBRDF(surface);
#endif

    //输出从unity中获得的数据，如光照贴图等
    GI gi = GetGI(GI_FRAGMENT_DATA(input),surface,brdf);
    float3 color = GetLighting(surface,brdf,gi);
    color += GetEmission(input.baseUV);
    
    return float4(color,surface.alpha);
}

#endif