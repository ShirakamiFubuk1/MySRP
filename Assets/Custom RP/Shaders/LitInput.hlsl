#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

#define  INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_NormalMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_DetailMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _CutOff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
	UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct InputConfig
{
	float2 baseUV;
	float2 detailUV;
	bool useMask;
	bool useDetail;
};

// 内联定义一个detailUV,以防没有定义该函数导致无法使用
InputConfig GetInputConfig(float2 baseUV,float2 detailUV = 0.0)
{
	InputConfig c;
	c.baseUV = baseUV;
	c.detailUV = detailUV;
	c.useMask = false;
	c.useDetail = false;
	
	return c;
}

float2 TransformBaseUV (float2 baseUV) {
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float2 TransformDetailUV (float2 detailUV) {
	float4 detailST = INPUT_PROP(_DetailMap_ST);
	return detailUV * detailST.xy + detailST.zw;
}

float4 GetDetail(InputConfig c)
{
	if(c.useDetail)
	{
		float4 map = SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,c.detailUV);
		// 将参数面板上的0~1转换到-1~1范围
		return map * 2.0 - 1.0;		
	}
	return 0.0;
}

float4 GetMask(InputConfig c)
{
	if(c.useMask)
	{
		return SAMPLE_TEXTURE2D(_MaskMap,sampler_BaseMap,c.baseUV);		
	}
	return 1.0;
}

float4 GetBase (InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP(_BaseColor);

	if(c.useDetail)
	{
		// 因为只有R通道会影响albedo,将颜色推向黑色或白色
		// lerp使用detail的绝对值来控制0或1插值颜色,这只会影响albedo,不会影响base的alpha通道
		float detail = GetDetail(c).r * INPUT_PROP(_DetailAlbedo);
		// 获取detail的mask并应用
		float mask = GetMask(c).b;
		//map += detail;
		// 由于我们在线性空间中应用修改,导致增亮效果比变暗效果更强.
		// 在伽马空间执行此操作将更好的在视觉上均匀分布.
		// 我们可以通过插值albedo的平方根然后再平方再来近似这一点.
		map.rgb = lerp(sqrt(map.rgb),detail < 0.0 ? 0.0 : 1.0,abs(detail) * mask);
		map.rgb *= map.rgb;		
	}
	
	return map * color;
}

float GetCutOff (InputConfig c) {
	return INPUT_PROP(_CutOff);
}

float GetMetallic (InputConfig c) {
	float metallic = INPUT_PROP(_Metallic);
	// 应用Mask图
	metallic *= GetMask(c).r;
	return metallic;
}

float GetSmoothness (InputConfig c) {
	float smoothness = INPUT_PROP(_Smoothness);
	smoothness *= GetMask(c).a;

	if(c.useDetail)
	{
		// 使用类似Basemap的操作
		float detail = GetDetail(c).b * INPUT_PROP(_DetailSmoothness);
		float mask = GetMask(c).b;
		smoothness =
			lerp(smoothness,detail < 0.0 ? 0.0 : 1.0,abs(detail) * mask);
	}

	return smoothness;
}

float GetFresnel(InputConfig c)
{
	return INPUT_PROP(_Fresnel);
}

float3 GetEmission(InputConfig c)
{
	float4 map = SAMPLE_TEXTURE2D(_EmissionMap,sampler_BaseMap,c.baseUV);
	float4 color = INPUT_PROP(_EmissionColor);

	return map.rgb * color.rgb;
}

float GetOcclusion(InputConfig c)
{
	float strength = INPUT_PROP(_Occlusion);
	float occlusion = GetMask(c).g;
	occlusion = lerp(occlusion,1.0,strength);
	return occlusion;
}

float3 GetNormalTS(InputConfig c)
{
	float4 map = SAMPLE_TEXTURE2D(_NormalMap,sampler_BaseMap,c.baseUV);
	float scale = INPUT_PROP(_NormalScale);
	float3 normal = DecodeNormal(map,scale);

	if(c.useDetail)
	{
		// 不把detailMap和detailNormal合起来的原因
		// 虽然HDRP里面的DetailMap定义中x是Albedo,y是Smoothness,zw为Normal
		// 这样效率更高,但是生成Mipmap更难,在生成时应该将向量与其他数据通道区别对待,这时Unity的纹理导入期无法做到的
		// 此外,Unity在淡入淡出Mipmap时会忽略alpha通道,因此该通道中的数据不会正确淡入淡出
		// 因此HDRP这种做法需要自己生产mip映射,无论是Unity外部还是使用脚本.
		// 即使如此我们也需要手动解码正常的数据,而不是以来UnpackNormalmapRGorAG
		map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailMap, c.detailUV);
		scale = INPUT_PROP(_DetailNormalScale) * GetMask(c).b;
		float3 detail = DecodeNormal(map, scale);
		normal = BlendNormalRNM(normal, detail);
	}

	return normal;
}

float GetFinalAlpha(float alpha)
{
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

#endif