#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4

struct Light
{
    float attenuation;
    float3 color;
    float3 direction;
};

CBUFFER_START(_CustomLight)
	//float4 _DirectionalLightColor;
	//float4 _DirectionalLightDirection;
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

Light GetDirectionalLight(int index)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;

    return light;
}

int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int lightIndex)
{
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[lightIndex].x;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y;

    return data;
}

Light GetDirectionalLight(int index,Surface surfaceWS)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;
    DirectionalShadowData shadowData = GetDirectionalShadowData(index);
    light.attenuation = GetDirectionalShadowAttenuation(shadowData,surfaceWS);

    return light;
}

#endif