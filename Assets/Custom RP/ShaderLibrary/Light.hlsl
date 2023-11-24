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

//通过将包含级联信息的级联索引添加到光源的阴影片段和偏移量，通过对应的便宜连来选择对应的片段
DirectionalShadowData GetDirectionalShadowData(int lightIndex,ShadowData shadowData)
{
    //获取对应光的阴影数据,x为shadow strength,y为偏移量，z为bias
    DirectionalShadowData data;
    //将原有强度乘以strength以剔除对应级联范围以外的阴影
    data.strength = _DirectionalLightShadowData[lightIndex].x * shadowData.strength;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;

    return data;
}

Light GetDirectionalLight(int index,Surface surfaceWS,ShadowData shadowData)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;
    //检索阴影数据并将其用于光照的衰减
    DirectionalShadowData dirShadowData = GetDirectionalShadowData(index,shadowData);
    light.attenuation = GetDirectionalShadowAttenuation(dirShadowData,shadowData,surfaceWS);
    //更直观的看到各级Cascade的范围.
    //light.attenuation = shadowData.cascadeIndex * 0.25;

    return light;
}

#endif