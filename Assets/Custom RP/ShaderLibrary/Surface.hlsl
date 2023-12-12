#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface
{
    float alpha;
    float metallic;
    float occlusion;
    float smoothness;
    float fresnelStrength;
    float depth;
    float dither;
    float3 normal;
    float3 interpolatedNormal;
    float3 color;
    float3 viewDirection;
    float3 position;
};

#endif