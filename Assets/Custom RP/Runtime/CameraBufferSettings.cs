using System;
using UnityEngine;

[Serializable]
public struct CameraBufferSettings
{
    // 因为复制深度需要额外的操作,特别是不使用后处理时还需要中间帧和额外的复制
    // 所以我们吧是否开启深度作为一个摄像机配置.同时也把是否使用allowHDR加进来
    // 此外还引入了一个单独的切换开关,用于控制在渲染反射时是否复制深度.
    // 这很有用因为反射是在没有后处理的情况下渲染的,
    // 同时粒子也不会出现在反射里,因为复制反射的深度成本很高,且用处不大.
    public bool allowHDR;
    
    public bool copyColor, copyColorReflection, copyDepth, copyDepthReflection;

    // 由于低于0.1会基本看不出结果,高于2.0在双线性滤波之后不会有太大变化
    // 甚至会跳过很多像素导致画质下降,故我们把范围定位(0.1, 2.0);
    [Range(0.1f, 2f)] public float renderScale;
    
    public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }

    public BicubicRescalingMode bicubicRescaling;
    
    [Serializable]
    public struct FXAA 
    {
        public bool enabled;
        
        // Trims the algorithm from processing darks.
        //   0.0833 - upper limit (default, the start of visible unfiltered edges)
        //   0.0625 - high quality (faster)
        //   0.0312 - visible limit (slower)
        [Range(0.0312f, 0.0833f)] public float fixedThreshold;

        // The minimum amount of local contrast required to apply algorithm.
        //   0.333 - too little (faster)
        //   0.250 - low quality
        //   0.166 - default
        //   0.125 - high quality 
        //   0.063 - overkill (slower)
        [Range(0.063f, 0.333f)] public float relativeThreshold;

        // Choose the amount of sub-pixel aliasing removal.
        // This can effect sharpness.
        //   1.00 - upper limit (softer)
        //   0.75 - default amount of filtering
        //   0.50 - lower limit (sharper, less sub-pixel aliasing removal)
        //   0.25 - almost off
        //   0.00 - completely off
        [Range(0f, 1f)] public float subpixelBlending;
        
        public enum Quality { Low, Medium, High}

        public Quality quality;
    }

    public FXAA fxaa;
}