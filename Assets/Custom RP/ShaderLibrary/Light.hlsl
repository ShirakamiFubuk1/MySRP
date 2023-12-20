#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

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

    int _OtherLightCount;
    float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

OtherShadowData GetOtherShadowData(int lightIndex)
{
    OtherShadowData data;
    data.strength = _OtherLightShadowData[lightIndex].x;
    data.tileIndex = _OtherLightShadowData[lightIndex].y;
    data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
    data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
    data.lightPositionWS = 0.0;
    data.lightDirectionWS = 0.0;
    data.spotDirectionWS = 0.0;

    return data;
}

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
    //data.strength = _DirectionalLightShadowData[lightIndex].x * shadowData.strength;
    //如果不使用shadowMask也不使用shadowCasetr,则需要通过将strength改为负数来关闭shadowMask采集
    //故此时data.shrength有概率为负数,使用时需要使用绝对值
    data.strength = _DirectionalLightShadowData[lightIndex].x;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    //获得脚本端的数据
    data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;

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

int GetOtherLightCount()
{
    return _OtherLightCount;
}

Light GetOtherLight(int index, Surface surfaceWS,ShadowData shadowData)
{
    Light light;
    light.color = _OtherLightColors[index].rgb;
    float3 position = _OtherLightPositions[index].xyz;
    float3 ray = position - surfaceWS.position;
    light.direction = normalize(ray);
    // 计算衰减
    float distanceSqr = max(dot(ray,ray),0.00001);
    // 将灯光范围包含在衰减中
    float rangeAttenuation =
        Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w)));
    float4 spotAngles = _OtherLightSpotAngles[index];
    float3 spotDirection = _OtherLightDirections[index].xyz;
    // 应用聚光灯衰减
    float spotAttenuation = Square(
        saturate(dot(spotDirection,light.direction)
        * spotAngles.x + spotAngles.y));
    OtherShadowData otherShadowData = GetOtherShadowData(index);
    // 由于这三个数据是从光源获得,需要在GetOtherLight中赋值
    otherShadowData.lightPositionWS = position;
    otherShadowData.lightDirectionWS = light.direction;
    otherShadowData.spotDirectionWS = spotDirection;
    light.attenuation = GetOtherShadowAttenuation(otherShadowData,shadowData,surfaceWS) *
        spotAttenuation * rangeAttenuation / distanceSqr;

    return light;
}

#endif