#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_DistortionMap);
SAMPLER(sampler_BaseMap);
SAMPLER(sampler_DistortionMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistortionStrength)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistortionBlend)
	UNITY_DEFINE_INSTANCED_PROP(float, _CutOff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	// UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	// UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct InputConfig
{
	Fragment fragment;
	float2 baseUV;
	float4 color;
	float3 flipbookUVB;
	bool flipbookBlending;
	bool nearFade;
	bool softParticles;
};

// SS = Screen Spcae
InputConfig GetInputConfig(float4 positionSS, float2 baseUV,float2 detailUV = 0.0)
{
	InputConfig c;
	c.fragment = GetFragment(positionSS);
	c.baseUV = baseUV;
	// 再输入类型中添加一个颜色,默认为白色
	c.color = 1.0;
	c.flipbookUVB = false;
	c.flipbookBlending = false;
	c.nearFade = false;
	c.softParticles = false;

	return c;
}

float2 TransformBaseUV (float2 baseUV) {
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase (InputConfig c) {
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	if(c.flipbookBlending)
	{
		baseMap = lerp(
			baseMap, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.flipbookUVB.xy),
			c.flipbookUVB.z
		);
	}
	if(c.nearFade)
	{
		// 相机附近的淡入淡出是通过降低片段的alpha值来完成的
		// 衰减的比例为片段的深度减去_NearFadeDistance/_NearFadeRange
		// 由于nearAttenuation有可能是负的所以需要saturate一下
		float nearAttenuation = (c.fragment.depth - INPUT_PROP(_NearFadeDistance)) /
			INPUT_PROP(_NearFadeRange);
		baseMap.a *= saturate(nearAttenuation);
	}
	if(c.softParticles)
	{
		float depthDelta = c.fragment.bufferDepth - c.fragment.depth;
		float nearAttenuation = (depthDelta - INPUT_PROP(_SoftParticlesDistance)) /
			INPUT_PROP(_SoftParticlesRange);
		baseMap.a *= saturate(nearAttenuation);
	}
	float4 BaseColor = INPUT_PROP(_BaseColor);
	return baseMap * BaseColor * c.color;
}

float2 GetDistortion(InputConfig c)
{
	float4 rawMap = SAMPLE_TEXTURE2D(_DistortionMap,sampler_DistortionMap,c.baseUV);
	if(c.flipbookBlending)
	{
		rawMap = lerp(
			rawMap, SAMPLE_TEXTURE2D(_DistortionMap,sampler_DistortionMap,c.flipbookUVB.xy),
			c.flipbookUVB.z
		);
	}
	return DecodeNormal(rawMap, INPUT_PROP(_DistortionStrength)).xy;
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

float GetFinalAlpha(float alpha)
{
	// 当_ZWrite设置为1时为1,反之使用提供的alpha
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

float GetDistortionBlend(InputConfig c)
{
	return INPUT_PROP(_DistortionBlend);
}

#endif