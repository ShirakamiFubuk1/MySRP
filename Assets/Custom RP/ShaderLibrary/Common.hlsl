#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

//Mask数据实例化，防止打破instancing
//遮挡数据可以自动实例化，但只有在定义时才会这样做.因此在Include之前需要定义它
#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
    #define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

float Square (float v)
{
    return v * v;
}

float DistanceSquared(float3 pA,float3 pB)
{
    return dot(pA-pB,pA-pB);
}

void ClipLOD(float2 positionCS,float fade)
{
// 如果CrossFade处于活动状态,则根据淡入淡出因子减去抖动图案
#if defined(LOD_FADE_CROSSFADE)
    // 使用程序生成的Dither来替代
    float dither = InterleavedGradientNoise(positionCS.xy,0);
    // float dither = (positionCS.y % 32) / 32;
    // 因为存在负的fadeFactor,所以需要取反来获得正确效果
    clip(fade + (fade<0.0?dither:-dither));
#endif
}

float3 DecodeNormal(float4 sample, float scale)
{
    #if defined(UNITY_NO_DXT5nm)
        return normalize(UnpackNormalRGB(sample,scale));
    #else
        return normalize(UnpackNormalmapRGorAG(sample,scale));
    #endif
}

float3 NormalTangentToWorld(float3 normalTS,float3 normalWS,float4 tangentWS)
{
    float3x3 tangentToWorld =
        CreateTangentToWorld(normalWS,tangentWS.xyz,tangentWS.w);
    return TransformTangentToWorld(normalTS,tangentToWorld);
}

#endif