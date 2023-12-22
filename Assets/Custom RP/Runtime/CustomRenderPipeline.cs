using UnityEngine;
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
