#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface
{
    float alpha;
    float metallic;
    float smoothness;
    float2 normal;
    float3 color;
};

#endif