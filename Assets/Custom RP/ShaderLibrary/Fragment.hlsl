#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED

// 因为采样深度材质需要使用片段的UV坐标,这存储在ScreenSpace中
// 可以通过用位置除以屏幕像素尺寸,通过_ScreenParams
TEXTURE2D(_CameraColorTexture);
TEXTURE2D(_CameraDepthTexture);

// 接收我们修改过的相机尺寸
float4 _CameraBufferSize;

struct Fragment
{
    float2 positionSS;
    // 将屏幕片段UV和深度添加到Fragment中
    float2 screenUV;
    float depth;
    float bufferDepth;
};

Fragment GetFragment (float4 positionSS)
{
    Fragment f;
    f.positionSS = positionSS.xy;
    // 在此处应用_CameraBufferSize代替_ScreenParams
    // _ScreenParams的后两个值包含的是相机尺寸分之一加上1,在此处用不着
    f.screenUV = f.positionSS * _CameraBufferSize.xy;
    f.depth = IsOrthographicCamera() ?
        OrthographicDepthBufferToLinear(positionSS.z) : positionSS.w;
    // 通过用屏幕UV使用点采样器和SAMPLE_DEPTH_TEXTURE_LOD宏采样深度缓存
    // 这个宏和 SAMPLE_TEXTURE2D_LOD 一样除了只有R通道
    // 通过这步可以获得rawDepth
    f.bufferDepth = SAMPLE_DEPTH_TEXTURE_LOD(
        _CameraDepthTexture, sampler_point_clamp, f.screenUV, 0);
    // 在此处理rawDepth,不管是正交还是透视摄像机
    f.bufferDepth = IsOrthographicCamera() ?
        OrthographicDepthBufferToLinear(f.bufferDepth) :
        LinearEyeDepth(f.bufferDepth, _ZBufferParams);
    
    return f;
}

float4 GetBufferColor(Fragment fragment, float2 uvOffset = float2(0.0, 0.0))
{
    float2 uv = fragment.screenUV + uvOffset;
    return SAMPLE_TEXTURE2D_LOD(_CameraColorTexture, sampler_linear_clamp, uv, 0);
}

#endif