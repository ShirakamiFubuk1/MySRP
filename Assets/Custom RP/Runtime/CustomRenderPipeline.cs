﻿using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer = new CameraRenderer();

    private bool 
        useDynamicBatching, 
        useGPUInstancing, 
        useLightsPerObject,
        allowHDR;

    private ShadowSettings shadowSettings;

    // 在Custom RenderPipeline中追踪FX设置,并在渲染时将他们与其他设置一起传递给相机渲染器
    private PostFXSettings postFXSettings;

    protected override void Render(
        ScriptableRenderContext context, Camera[] cameras
    )
    {
        foreach (Camera camera in cameras)
        {
            renderer.Render(context,camera,allowHDR, 
                useDynamicBatching,useGPUInstancing,
                useLightsPerObject,shadowSettings,postFXSettings);
        }
    }

    public CustomRenderPipeline(bool allowHDR,bool useDynamicBatching,
        bool useGPUInstancing,bool useSRPBatcher,bool useLightsPerObject,
        ShadowSettings shadowSettings,PostFXSettings postFXSettings)
    {
        //追踪阴影设置
        this.shadowSettings = shadowSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        this.allowHDR = allowHDR;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        //设置全局使用线性颜色
        GraphicsSettings.lightsUseLinearIntensity = true;

        // 给Editor模式下添加我们的自定义设置,如光照代理
        InitializeForEditor();
    }
}
