#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

// #include "../ShaderLibrary/Common.hlsl"
//
// TEXTURE2D(_BaseMap);
// SAMPLER(sampler_BaseMap);
//
// UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
//     UNITY_DEFINE_INSTANCED_PROP(float4,_BaseMap_ST)
//     UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)
//     UNITY_DEFINE_INSTANCED_PROP(float,_CutOff)
// UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes
{
    float2 baseUV : TEXCOORD0;
    float3 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 baseUV : VAR_BASE_UV;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ShadowCasterPassVertex(Attributes input)
{
    Varyings output;

    //获得Instance的ID
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    //float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_BaseMap_ST);    
    output.positionCS = TransformWorldToHClip(positionWS);

//通过定义UNITY_REVERSED_Z,防止部分区域在屏幕外时被裁减导致阴影也被裁剪出现shadow pancake问题
#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z,output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z,output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    
    output.baseUV = TransformBaseUV(input.baseUV);
    
    return output;
}

void ShadowCasterPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipLOD(input.positionCS.xy,unity_LODFade.x);

    // float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.baseUV);
    // float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_BaseColor);
    float4 base = GetBase(input.baseUV);
    
#if defined(_SHADOWS_CLIP)
    clip(base.a - GetCutOff(input.baseUV));
#elif defined(_SHADOWS_DITHER)
    float dither = InterleavedGradientNoise(input.positionCS.xy,0);
    clip(base.a - dither);
#endif    
}

#endif