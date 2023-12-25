﻿using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private bool 
        useDynamicBatching = true, 
        useGPUInstancing = true, 
        useSRPBatcher = true,
        useLightsPerObject = true;

    // 在这里使用单个堆栈,通过为其添加配置项,将其传递给RP的构造函数,从而提供给RP
    [SerializeField] private PostFXSettings postFXSettings = default;

    [SerializeField] private bool allowHDR = true;
    
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(allowHDR,useDynamicBatching, useGPUInstancing,
            useSRPBatcher,useLightsPerObject,shadows,postFXSettings);
    }

    [SerializeField] private ShadowSettings shadows = default;
}
