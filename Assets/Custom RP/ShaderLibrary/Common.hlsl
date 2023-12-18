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
        // 如果NormalMap还没有变化,UNITY_NO_DXT5nm则会被定义
        // 我们就可以使用UnpackNormalRGB来解码
        return normalize(UnpackNormalRGB(sample,scale));
    #else
        // 反之使用UnpackNormalmapRGorAG来解码
        // 由于Unity 2022中不会自动normalize,在此处加上normalize
        return normalize(UnpackNormalmapRGorAG(sample,scale));
    #endif
}

float3 NormalTangentToWorld(float3 normalTS,float3 normalWS,float4 tangentWS)
{
    // 由于纹理环绕几何体,因此他们在对象空间和世界空间的方向并不同一.因此,存储法线的空间会曲线以匹配几何体的曲面
    // 唯一不变的是空间与表面相切的方向,这就是为什么被称为切线空间.
    // 此空间的Y轴沿着切向垂直方向向上与曲面法线匹配.除此之外还要有个与曲面相切的X轴.
    // 如果有这两个数据就可以从中生成Z正向轴
    // 由于切线空间的X轴不是固定的,因此必须将其定义为Vertex数据的一部分.
    // 这个数值使用float4存储,它的XYZ定义了他在对象空间中的方向,W分量为-1或者1,用于控制Z轴指向的方向.
    // 这用于反转具有双边对称的网格的法线贴图(大部分动物都有),因此可以只用一侧贴图,从而将所需纹理大小减半
    float3x3 tangentToWorld =
        CreateTangentToWorld(normalWS,tangentWS.xyz,tangentWS.w);
    return TransformTangentToWorld(normalTS,tangentToWorld);
}

#endif