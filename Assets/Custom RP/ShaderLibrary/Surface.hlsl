#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface
{
    float alpha;
    float metallic;
    float smoothness;
    float depth;
    float dither;
    float3 normal;
    float3 color;
    float3 viewDirection;
    float3 position;
};

#endif