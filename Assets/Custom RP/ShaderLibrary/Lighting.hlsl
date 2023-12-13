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
    
    float3 color = IndirectBRDF(surfaceWS,brdf,gi.diffuse,gi.specular);
    for(int i = 0;i<GetDirectionalLightCount();i++)
    {
        //将surface信息传递给GetDirectionalLight
        Light light = GetDirectionalLight(i,surfaceWS,shadowData);
        //叠加多个光照颜色
        color += GetLighting(surfaceWS,brdf,light);
    }
    for(int j = 0;j<GetOtherLightCount();j++)
    {
        Light light = GetOtherLight(j,surfaceWS,shadowData);
        color += GetLighting(surfaceWS,brdf,light);
    }
    
    return color;
}

#endif