#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

#if defined(LIGHTMAP_ON)
    #define GI_ATTRIBUTE_DATA float2 lightmapUV : TEXCOORD1;
    #define GI_VARYINGS_DATA float2 lightmapUV : VAR_LIGHT_MAP_UV;
    //多行宏定义使用反斜杠来换行
    #define TRANSFER_GI_DATA(input,output) \
        output.lightmapUV = input.lightmapUV * \
        unity_LightmapST.xy + unity_LightmapST.zw;
    #define GI_FRAGMENT_DATA(input) input.lightmapUV
#else
    #define GI_ATTRIBUTE_DATA
    #define GI_VARYINGS_DATA
    #define TRANSFER_GI_DATA(input,output)
    #define GI_FRAGMENT_DATA(input) 0.0
#endif

struct GI
{
    float3 diffuse;
};

float3 SampleLightmap(float2 lightmapUV)
{
    #if defined(LIGHTMAP_ON)
        return SampleSingleLightmap(
			TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightmapUV,
			float4(1.0, 1.0, 0.0, 0.0),
			#if defined(UNITY_LIGHTMAP_FULL_HDR)
				false,
			#else
				true,
			#endif
			float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
            );
    #else
        return 0.0;
    #endif
}

float3 SampleLightProbe (Surface surfaceWS)
{
#if defined(LIGHTMAP_ON)
    return 0.0;
#else
    //通过unity_ProbeVolumeParams来与Unity通信
    if(unity_ProbeVolumeParams.x)
    {
        //首先采样体积,通过SampleProbeVolumeSH4
        //对LPPV进行采样需要对体积空间进行变换,以及其他计算,提及纹理采样和球谐波的应用.
        //在这种情况下,仅应用L1球谐波,因此结果不太精确,但可能会在单个物体的表面上有所不同
        return SampleProbeVolumeSH4(
            //传递贴图和采样器
            TEXTURE3D_ARGS(unity_ProbeVolumeSH,samplerunity_ProbeVolumeSH),
            //世界位置和世界法线
            surfaceWS.position,surfaceWS.normal,
            unity_ProbeVolumeWorldToObject,
            unity_ProbeVolumeParams.y,unity_ProbeVolumeParams.z,
            unity_ProbeVolumeMin.xyz,unity_ProbeVolumeSizeInv.xyz
        );
    }
    else
    {
        float4 coefficients[7];
        coefficients[0] = unity_SHAr;
        coefficients[1] = unity_SHAg;
        coefficients[2] = unity_SHAb;
        coefficients[3] = unity_SHBr;
        coefficients[4] = unity_SHBg;
        coefficients[5] = unity_SHBb;
        coefficients[6] = unity_SHC;
        return max(0.0,SampleSH9(coefficients,surfaceWS.normal));        
    }

#endif
}

GI GetGI(float2 lightmapUV,Surface surfaceWS)
{
    GI gi;
    gi.diffuse = SampleLightmap(lightmapUV) + SampleLightProbe(surfaceWS);

    return gi;
}

#endif