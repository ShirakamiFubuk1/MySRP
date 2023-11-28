#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

struct GI
{
    float3 diffuse;
};

GI GetGI(float2 lightMapUV)
{
    GI gi;
    gi.diffuse = float3(lightMapUV,0.0);

    return gi;
}

#endif