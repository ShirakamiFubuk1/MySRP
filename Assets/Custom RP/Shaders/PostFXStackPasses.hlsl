#ifndef CUSTOM_POST_FX_PASSED_INCLUDED
#define CUSTOM_POST_FX_PASSED_INCLUDED

bool _BloomBicubicUpsampling;
float _BloomIntensity;
float4 _BloomThreshold;

TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
TEXTURE2D(_ColorGradingLUT);
// SAMPLER(sampler_linear_clamp);
// SAMPLER(sampler_point_clamp);

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

float4 _PostFXSource_TexelSize;
float4 _ColorAdjustments;
float4 _ColorFilter;
float4 _WhiteBalance;
float4 _SplitToningShadows;
float4 _SplitToningHighlights;
float4 _ChannelMixerRed;
float4 _ChannelMixerGreen;
float4 _ChannelMixerBlue;
float4 _SMHShadows;
float4 _SMHMidtones;
float4 _SMHHighlights;
float4 _SMHRange;
float4 _ColorGradingLUTParameters;

bool _ColorGradingLUTInLogC;
bool _UsePointSampler;
bool _CopyBicubic;

float4 GetSourceTexelSize()
{
    return _PostFXSource_TexelSize;
}

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

float Luminance(float3 color, bool useACES)
{
    return useACES ? AcesLuminance(color) : Luminance(color);
}

float3 ColorGradePostExposure(float3 color)
{
    return color * _ColorAdjustments.x;
}

float3 ColorGradingContrast(float3 color, bool useACES)
{
    color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
    color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
    return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}

float3 ColorGradeColorFilter(float3 color)
{
    return color * _ColorFilter.rgb;
}

float3 ColorGradingHueShift(float3 color)
{
    color = RgbToHsv(color);
    float hue = color.x + _ColorAdjustments.z;
    color.x = RotateHue(hue,0.0,1.0);

    return HsvToRgb(color);
}

float3 ColorGradingSaturation(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    return (color - luminance) * _ColorAdjustments.w + luminance;
}

float3 ColorGradeWhiteBalance(float3 color)
{
    color = LinearToLMS(color);
    color *= _WhiteBalance.rgb;
    return LMSToLinear(color);
}

float3 ColorGradeSplitToning(float3 color, bool useACES)
{
    color = PositivePow(color,1.0 / 2.2);
    float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
    float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
    float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);
    color = SoftLight(color,shadows);
    color = SoftLight(color,highlights);
    return PositivePow(color,2.2);
}

float3 ColorGradingChannelMixer(float3 color)
{
    return mul(
        float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb),
        color
    );
}

float3 ColorGradingShadowsMidtonesHighlights (float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
    float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
    float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
    return
        color * _SMHShadows.rgb * shadowsWeight +
        color * _SMHMidtones.rgb * midtonesWeight +
        color * _SMHHighlights.rgb * highlightsWeight;
}

// 默认情况下Blit命令会绘制一个由两个三角形组成的quad平面,覆盖整个屏幕空间
// 但我们只用一个三角形就能获得同样的结果,同时可以作为优化项
// 甚至不需要发送给GPU一个三角形,直接程序化生成一个即可
// 虽然从两个三角形变成一个三角形只是从六个顶点减少为三个
// 当时默认两个三角形的情况在屏幕对角线上,由于接触到三角形的边界会出现锯齿
// 因此接近对角线的地方会渲染两次,效率低下且对画面显示会造成一定的影响
// 创建一个vertexPass,只有一个vertexId作为输入参数,使用uint类型和SV_VertexID识别符
Varyings DefaultPassVertex(uint vertexID : SV_VertexID)
{
    Varyings output;
    // 需要注意这个三角形就三个顶点
    // 顶点ID小于等于1可以把2号顶点放在x=3.0,ID等于1可以把1放在y=3.0,ID为零的在原点
    // 覆盖x:-1到1,y:-1到1
    output.positionCS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0 , 1.0
    );
    // U使用0,0,2,V使用0,2,0,覆盖U:0-1,V:0-1
    output.screenUV = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0
    );
    // 如果该值小于零说明V轴向下,需要反转
    if(_ProjectionParams.x < 0.0)
    {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }
    return output;
}

// 创建一个GetSource函数来复制采样回来的颜色,使用线性钳制采样器来采样
float4 GetSource(float2 screenUV)
{
    // 由于我们的buffer用不到Mip,所以使用带Mip的采样器并将Mip设定为0
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource,sampler_linear_clamp,screenUV,0);
}

float4 GetSource2(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource2,sampler_linear_clamp,screenUV,0);
}

float4 GetSourceBicubic(float2 screenUV)
{
    // 使用Core RP中的SampleTexture2DBicubic来构建该函数,后两个参数一般用1.0,0.0
    return SampleTexture2DBicubic(
        TEXTURE2D_ARGS(_PostFXSource,sampler_linear_clamp),screenUV,
        _PostFXSource_TexelSize.zwxy,1.0,0.0
    );
}

// 因为两次滤波可以拆开,所以只用9*2次滤波即可
// 此处时Horizontal模糊
float4 BloomHorizontalPassFragment(Varyings input):SV_TARGET
{
    float3 color = 0.0;
    float offsets[] = {
        -4.0 , -3.0 , -2.0 , -1.0 , 0.0 , 1.0 , 2.0 , 3.0 , 4.0
    };
    // 权重源自帕斯卡三角形.
    // 对于适当的9x9高斯滤波器,我们会选择三角形的第九行,即 1 8 28 56 70 56 28 8 1.
    // 但这个采样会使样本边缘的贡献太弱从而看不出来.因此我们向下移动到13行并切断其边缘.
    // 得到66 220 495 792 495 220 66.这些数字的总和是4070,因此将每个出自除以它得到权重
    float weights[] = {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622
    };
    for(int i = 0; i < 9; i++)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
        color += GetSource(input.screenUV + float2(offset,0.0)).rgb * weights[i];
    }
    return float4(color,1.0);
}

// 纵向采样,从垂直采样中获得材质
float4 BloomVerticalPassFragment(Varyings input):SV_TARGET
{
    // 由于横向和纵向都用高斯采样的开销较大
    // 第二步的纵向采样可以用双线性滤波采样之前高斯采样的样本点并使用合适的偏移值来替代
    // 这样采样次数就从9次变为5次,总次数变为14次
    // 在第一步横向采样中无法使用的原因是我们获得给高斯采样的pyramid就是用双线性滤波获得的2x2格子,无法重复操作
    float3 color = 0.0;
    float offsets[] = {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
    };
    float weights[] = {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };
    for(int i = 0; i < 5; i++)
    {
        // 使用y方向的texel大小
        float offset = offsets[i] * GetSourceTexelSize().y;
        color += GetSource(input.screenUV + float2(0.0,offset)).rgb * weights[i];
    }
    return float4(color,1.0);
}

float3 ApplyBloomThreshold(float3 color)
{
    float brightness = Max3(color.r,color.g,color.b);
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft,0.0,_BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    float contribution = max(soft,brightness - _BloomThreshold.x);
    contribution /= max(brightness,0.00001);

    return color * contribution;
}

float4 BloomAddPassFragment (Varyings input) : SV_TARGET
{
    float3 lowRes;
    if(_BloomBicubicUpsampling)
    {
        // 如果使用了Bicubic采样则lowRes走这条路
        // 虽然Bicubic表现更好,但是一次需要采样四个加上权重的样本或单个样本
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float4 highRes = GetSource2(input.screenUV);

    return float4(lowRes * _BloomIntensity + highRes.rgb , highRes.a);
}

float4 BloomScatterPassFragment (Varyings input) : SV_TARGET
{
    float3 lowRes;
    if(_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float3 highRes = GetSource2(input.screenUV).rgb;

    // 和Add方式只有最后混合模式不一样
    return float4(lerp(highRes,lowRes,_BloomIntensity),1.0);
}

float4 BloomScatterFinalPassFragment (Varyings input) : SV_TARGET
{
    float3 lowRes;
    if(_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float4 highRes = GetSource2(input.screenUV);
    // 这个函数使散射的一个副本,只有将缺失的光添加到低分辨率通道中不一样.
    // 方法是添加高分辨率的光,然后减去它应用了Bloom阈值的版本
    // 这并不是一个完美的解决方案——他不是一个加权平均值,忽略了fireflies褪色损失的光线——
    // 但是效果足够接近,且并未给原始图像增加光
    lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);
    
    return float4(lerp(highRes.rgb,lowRes,_BloomIntensity),highRes.a);
}

float4 CopyPassFragment(Varyings input):SV_TARGET
{
    return GetSource(input.screenUV);
}


float4 BloomPrefilterPassFragment(Varyings input) : SV_TARGET
{
    float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);

    return float4(color,1.0);
}

// 淡化fireflies最直接的方法时将预处理通道的2x2采样器增加到一个6x6的大型滤波器(3x3个2x2滤波器)
// 我们可以用九个样本来做到这点,再取平均值之前将Bloom阈值分别应用于每个样本
float4 BloomPrefilterFirefliesPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float weightSum = 0.0;
    // 因为我们在预处理的半分辨率之后会执行高斯模糊,所以我们可以跳过和中心相连的四个样本,从九个减少到五个
    float2 offsets[] = {
        float2(0.0, 0.0),
        float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0)
        //float2(-1.0, 0.0), float2(1.0, 0.0), float2(0.0, -1.0), float2(0.0, 1.0)
    };
    for (int i = 0; i < 5; i++) {
        float3 c =
            GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
        c = ApplyBloomThreshold(c);
        // 这还不足以解决问题,因为非常明亮的像素只是散布在更大的区域上.
        // 为了淡化fireflies,我们将根据颜色的亮度使用加权平均值.
        // 颜色的luminance是他能感知到的亮度,使用Color.hlsl中的Luminance函数来实现
        float w = 1.0 / (Luminance(c) + 1.0);
        color += c * w;
        weightSum += w;
    }
    // 最后我们将样本综合除以这些权重的综合.这可以让fireflies的高光有效分散到其他样本中.
    // 如果其他样本颜色较深,萤火虫就会褪色
    color /= weightSum;
    return float4(color, 1.0);
}

// 因为颜色分级发生在色调映射之前,所以新建一个函数只将颜色分量限制为60
float3 ColorGrade (float3 color, bool useACES = false)
{
    // color = min(color,60);
    color = ColorGradePostExposure(color);
    color = ColorGradeWhiteBalance(color);
    color = ColorGradingContrast(color, useACES);
    color = ColorGradeColorFilter(color);
    color = max(color,0.0);
    color = ColorGradeSplitToning(color, useACES);
    color = ColorGradingChannelMixer(color);
    color = max(color,0.0);
    color = ColorGradingShadowsMidtonesHighlights(color, useACES);
    color = ColorGradingHueShift(color);
    color = ColorGradingSaturation(color, useACES);
    return max(useACES ? ACEScg_to_ACES(color) : color, 0.0);
}

float3 GetColorGradedLUT(float2 uv, bool useACES = false)
{
    float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
    return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}

float4 ColorGradingNonePassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV);
    // color.rgb = ColorGrade(color.rgb);

    return float4(color, 1.0);
}

float4 ColorGradingReinhardPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV);
    // // 由于精度限制,对于非常大的值可能出现错误.出于同样的原因,
    // // 非常大的值最终出现在1处的时间比无穷大早得多
    // // 因此需要在执行色调映射之前收紧颜色范围
    // // 限制为60可以避免我们支持的所有模式出现任何潜在问题
    // color.rgb = ColorGrade(color.rgb);
    color /= color + 1.0;

    return float4(color, 1.0);
}

float4 ColorGradingNeutralPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV);
    // color.rgb = ColorGrade(color.rgb);
    color = NeutralTonemap(color);

    return float4(color, 1.0);
}

float4 ColorGradingACESPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV, true);
    // color.rgb = ColorGrade(color.rgb, true);
    color = AcesTonemap(color);

    return float4(color, 1.0);
}

float3 ApplyColorGradingLUT(float3 color)
{
    if(_UsePointSampler)
    {
        return ApplyLut2D(
            TEXTURE2D_ARGS(_ColorGradingLUT, sampler_point_clamp),
            saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color),
            _ColorGradingLUTParameters.xyz
        );        
    }
    else
    {
        return ApplyLut2D(
        TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
        saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color),
        _ColorGradingLUTParameters.xyz
        );     
    }
}

float4 ApplyColorGradingPassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);

    return color;
}

float4 FinalPassFragmentRescale(Varyings input) : SV_TARGET
{
    if(_CopyBicubic)
    {
        return GetSourceBicubic(input.screenUV);
    }
    else
    {
        return GetSource(input.screenUV);
    }
}

#endif
