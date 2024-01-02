using System;
using UnityEngine;

[Serializable]
public struct CameraBufferSettings
{
    public bool allowHDR;
    
    public bool copyColor, copyColorReflection, copyDepth, copyDepthReflection;

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
    }

    public FXAA fxaa;
}