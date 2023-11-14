#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

struct BRDF
{
    float roughness;
    float3 diffuse;
    float3 specular;
};

BRDF GetBRDF(Surface surface)
{
    BRDF brdf;
    
    brdf.diffuse = surface.color;
    brdf.specular = 0.0;
    brdf.roughness = 1.0;

    return brdf;
}

#endif