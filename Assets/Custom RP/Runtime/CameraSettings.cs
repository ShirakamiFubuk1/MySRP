using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class CameraSettings
{
    // 在Final Pass中调整相机混合模式仅对上层的叠加相机有意义.
    // 底部摄像机将与摄像机目标的任何初始内容混合,
    // 这些内容要么是随机的,要么是先前帧的累计,除非编辑器提供清除的目标.
    // 因此第一台相机应该使用One Zero模式进行混合.
    // 为了支持替换,叠加和其他分层设置,我们将给摄像机添加可配置的最终混合模式,该模式尽在启用后期特效时使用
    // 这里给上述需求创建一个类似于阴影配置新的可序列化的CameraSettings设置类
    [Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source, destination;
    }

    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };

    // 添加每个摄像机单独的后处理配置
    public bool overridePostFX = false;

    public PostFXSettings postFXSettings = default;

    // 因为相机自己没有LayerMask所以需要自己定义一个
    // 又因为我们的light's Mask 是一个int,这里我们也用int,默认层级设为-1
    // 此处我们引用自己定义的方法GUI
    [RenderingLayerMaskField]
    public int renderingLayerMask = -1;

    // 为每个相机提供单独的开关
    public bool maskLights = false;

    public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

    [Range(0.1f, 2f)] public float renderScale = 1f;
    
    public bool copyColor = true, copyDepth = true;
    
    public enum RenderScaleMode { Inherit, Multiply, Override }

    public float GetRenderScale(float scale)
    {
        return
            renderScaleMode == RenderScaleMode.Inherit ? scale :
            renderScaleMode == RenderScaleMode.Override ? renderScale :
            scale * renderScale;
    }
    
    public bool allowFXAA = false;

    public bool keepAlpha = false;
}