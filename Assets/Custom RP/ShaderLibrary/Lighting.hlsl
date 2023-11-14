#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 IncomingLight (Surface surface,Light light)
{
    return saturate(dot(surface.normal,light.direction))*light.color;
}

float3 GetLighting (Surface surface, BRDF brdf,Light light)
{
    //非PBR
    //return IncomingLight(surface,light) * surface.color * brdf.diffuse;
    //PBR
    return IncomingLight(surface,light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting (Surface surface, BRDF brdf)
{ 
    float3 color = 0.0;
    for(int i = 0;i<GetDirectionalLightCount();i++)
    {
        //叠加多个光照颜色
        color += GetLighting(surface,brdf,GetDirectionalLight(i));
    }
    return color;
}

#endif