#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

//体积数据存储在3D纹理中.通过宏添加它的采集器状态
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

//定义宏刚开始定义为0,如果定义了LIGHTMAP_ON我们需要添加lightMapUV对应的结构,
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
    ShadowMask shadowMask;
};

//该函数在使用光照贴图时调用,否则返回0,它用来设置漫反射光
float3 SampleLightmap(float2 lightmapUV)
{
    #if defined(LIGHTMAP_ON)
        return SampleSingleLightmap(
            //12 LightMap采样器,3 UV,4 平移缩放
			TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightmapUV,
			float4(1.0, 1.0, 0.0, 0.0),
			//用于指示光照贴图是否被压缩
			#if defined(UNITY_LIGHTMAP_FULL_HDR)
				false,
			#else
				true,
			#endif
			//指导解码
			float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
            );
    #else
        return 0.0;
    #endif
}

//对光照探针进行采样。需要一个方向，故提供surfaceWS
////如果此对象正在使用光照贴图，则返回0，否则进行下一步判断
float3 SampleLightProbe (Surface surfaceWS)
{
#if defined(LIGHTMAP_ON)
    return 0.0;
#else
    //通过unity_ProbeVolumeParams来与Unity通信，如果设置了它，就会对体积进行采样
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
            //转换矩阵
            unity_ProbeVolumeWorldToObject,
            //y,z分量,min和size-inv数据的xyz部分
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

float4 SampleBakedShadows(float2 lightMapUV,Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        return SAMPLE_TEXTURE2D(unity_ShadowMask,samplerunity_ShadowMask,lightMapUV);
    #else
        if(unity_ProbeVolumeParams.x)
        {
            return SampleProbeOcclusion(
                TEXTURE3D_ARGS(unity_ProbeVolumeSH,samplerunity_ProbeVolumeSH),
                surfaceWS.position,unity_ProbeVolumeWorldToObject,
                unity_ProbeVolumeParams.y,unity_ProbeVolumeParams.z,
                unity_ProbeVolumeMin.xyz,unity_ProbeVolumeSizeInv.xyz
            );
        }
        else
        {
            return unity_ProbesOcclusion;            
        }
    #endif
}

GI GetGI(float2 lightMapUV,Surface surfaceWS)
{
    GI gi;
    //分别加上LightMap和LightProbe
    gi.diffuse = SampleLightmap(lightMapUV) + SampleLightProbe(surfaceWS);
    //由于shadowMask需要烘焙到GI里面，故在这里也需要初始化
    gi.shadowMask.always = false;
    gi.shadowMask.distance = false;
    gi.shadowMask.shadows = 1.0;

    #if defined(_SHADOW_MASK_ALWAYS)
        gi.shadowMask.always = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV,surfaceWS);
    #elif (_SHADOW_MASK_DISTANCE)
        gi.shadowMask.distance = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV,surfaceWS);
    #endif
    
    return gi;
}

#endif