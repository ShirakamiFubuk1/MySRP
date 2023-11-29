#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

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

GI GetGI(float2 lightmapUV)
{
    GI gi;
    gi.diffuse = SampleLightmap(lightmapUV);

    return gi;
}

#endif