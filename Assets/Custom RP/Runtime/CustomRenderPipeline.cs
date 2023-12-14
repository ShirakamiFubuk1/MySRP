using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer = new CameraRenderer();

    private bool useDynamicBatching, useGPUInstancing, useLightsPerObject;

    private ShadowSettings shadowSettings;

    protected override void Render(
        ScriptableRenderContext context, Camera[] cameras
    )
    {
        foreach (Camera camera in cameras)
        {
            renderer.Render(context,camera,
                useDynamicBatching,useGPUInstancing,useLightsPerObject,shadowSettings);
        }
    }

    public CustomRenderPipeline(bool useDynamicBatching,
        bool useGPUInstancing,bool useSRPBatcher,bool useLightsPerObject,
        ShadowSettings shadowSettings)
    {
        //追踪阴影设置
        this.shadowSettings = shadowSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        //设置全局使用线性颜色
        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();
    }
}
