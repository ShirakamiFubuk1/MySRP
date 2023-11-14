#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface
{
    
    float alpha;
    float metallic;
    float smoothness;
    float3 normal;
    float3 color;
    float3 viewDirection;
};

#endif