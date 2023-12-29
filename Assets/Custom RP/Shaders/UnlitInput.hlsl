#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_MaskMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _CutOff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	// UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	// UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct InputConfig
{
	float2 baseUV;
	float4 color;
};

InputConfig GetInputConfig(float2 baseUV,float2 detailUV = 0.0)
{
	InputConfig c;
	c.baseUV = baseUV;
	c.color = 1.0;

	return c;
}

float2 TransformBaseUV (float2 baseUV) {
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase (InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	return map * color * c.color;
}

float GetCutOff (InputConfig c) {
	return INPUT_PROP(_CutOff);
}

float GetMetallic (InputConfig c) {
	return 0.0;
}

float GetSmoothness (InputConfig c) {
	return 0.0;
}

float GetFresnel(InputConfig c)
{
	return 0.0;
}

float3 GetEmission(InputConfig c)
{
	return GetBase(c).rgb;
}

float4 GetMask(InputConfig c)
{
	return SAMPLE_TEXTURE2D(_MaskMap,sampler_BaseMap,c.baseUV);
}

float GetFinalAlpha(float alpha)
{
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

#endif