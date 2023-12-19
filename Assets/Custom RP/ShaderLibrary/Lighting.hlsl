#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 IncomingLight (Surface surface,Light light)
{
    return saturate(dot(surface.normal,light.direction)*light.attenuation)*light.color;
}

float3 GetLighting (Surface surface, BRDF brdf,Light light)
{
    //非PBR
    //return IncomingLight(surface,light) * surface.color * brdf.diffuse;
    //PBR
    return IncomingLight(surface,light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting (Surface surfaceWS, BRDF brdf,GI gi)
{
    //将影子数据传递给GetLighting
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;
    //Debug项,用于观察shadowMaskData
    //return gi.shadowMask.shadows.rgb;

    //直接引用IndirectBRDF而不是在此计算间接光,传递正确的高光
    float3 color = IndirectBRDF(surfaceWS,brdf,gi.diffuse,gi.specular);
    for(int i = 0;i<GetDirectionalLightCount();i++)
    {
        //将surface信息传递给GetDirectionalLight
        Light light = GetDirectionalLight(i,surfaceWS,shadowData);
        //叠加多个光照颜色
        color += GetLighting(surfaceWS,brdf,light);
    }
    #if !defined(LIGHTS_PER_OBJECT)
    #if defined(LIGHTS_PER_OBJECT)
        for(int j = 0;j < min(unity_LightData.y,8);j++)
        {
            int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
            Light light = GetOtherLight(lightIndex,surfaceWS,shadowData);
            color += GetLighting(surfaceWS,brdf,light);
        }
    #else
        for(int j = 0;j<GetOtherLightCount();j++)
        {
            Light light = GetOtherLight(j,surfaceWS,shadowData);
            color += GetLighting(surfaceWS,brdf,light);
        }
    #endif

    
    return color;
}

#endif