#ifndef CUSTOM_META_PASS_INCLUDED
#define CUSTOM_META_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

//使用我们的Meta Pass所有的间接光将消失
//由于间接光漫反射会通过表面反射，因此会受到物体表面的漫反射影响。
//Unity使用特殊的Meta通道来确定烘焙时的反射光。MetaPass用于控制表面的颜色

//Meta Pass可用于生成不同的数据。请求的内容通过该标志传输
bool4 unity_MetaFragmentControl;

float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

struct Attributes
{
    float3 positionOS:POSITION;
    float2 baseUV:TEXCOORD0;
    float2 lightMapUV:TEXCOORD1;
};

struct Varyings
{
    float4 positionCS:SV_POSITION;
    float2 baseUV:VAR_BASE_UV;
};

Varyings MetaPassVertex(Attributes input)
{
    Varyings output;

    //使用光照贴图的UV坐标
    input.positionOS.xy =
        input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    //实际上除非Unity明确需要贴图的z坐标，一般是没啥用的
    //我们自己的Meta Pass将使用相同的虚拟赋值FLT_MIN
    input.positionOS.z = input.positionOS.z > 0.0 ? FLT_MIN : 0.0;
    output.positionCS = TransformWorldToHClip(input.positionOS);
    output.baseUV = TransformBaseUV(input.baseUV);

    return output;
}

float4 MetaPassFragment(Varyings input):SV_TARGET{
    InputConfig config = GetInputConfig(input.baseUV);
    float4 base = GetBase(config);
    Surface surface;
    //初始化surface
    ZERO_INITIALIZE(Surface,surface);
    surface.color = base.rgb;
    surface.metallic = GetMetallic(config);
    surface.smoothness = GetSmoothness(config);
    BRDF brdf = GetBRDF(surface);
    float4 meta = 0.0;
    //因为light和shadow取决于brdf，故需要知道表面的漫反射率，因此需要获得brdf数据
    //如果unity_MetaFragmentControl.x被设置，将brdf的RGB返回，A设置为1.0
    if(unity_MetaFragmentControl.x)
    {
        meta = float4(brdf.diffuse,1.0);
        //由于Unity的meta通道也通过添加粗糙度和高管都乘积的一般来提升效果
        //因为镜面反射和漫反射也会传递间接光
        meta.rgb += brdf.specular * brdf.roughness * 0.5;
        //使用Unity提供的数据对rgb进行处理
        meta.rgb = min(PositivePow(meta.rgb,unity_OneOverOutputBoost),unity_MaxOutputValue);
    }
    //自发光通过单独的通道进行烘焙
    //当unity_MetaFragmentControl.y被设置时,将返回Emission的RGB,A用1.0
    else if(unity_MetaFragmentControl.y)
    {
        meta = float4(GetEmission(config),1.0);
    }
    
    return meta;
}

#endif
