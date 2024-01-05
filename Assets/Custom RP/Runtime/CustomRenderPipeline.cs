using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    // 因为我们需要兼容没有FX时的各种缓存,所以必须在构造渲染器时提供着色器.
    // 因此需要在CustomRenderPipeline中提供一个shader.
    private CameraRenderer renderer;// = new CameraRenderer();

    private bool 
        useDynamicBatching, 
        useGPUInstancing, 
        useLightsPerObject,
        // allowHDR,
        colorLUTPointSampler;

    private CameraBufferSettings cameraBufferSettings;

    private ShadowSettings shadowSettings;

    // 在Custom RenderPipeline中追踪FX设置,并在渲染时将他们与其他设置一起传递给相机渲染器
    private PostFXSettings postFXSettings;

    int colorLUTResolution;
    
    protected override void Render(
        ScriptableRenderContext context, Camera[] cameras
    )
    {
        foreach (Camera camera in cameras)
        {
            renderer.Render(
                context, camera, cameraBufferSettings, colorLUTPointSampler, 
                useDynamicBatching,useGPUInstancing, useLightsPerObject,shadowSettings, 
                postFXSettings, colorLUTResolution);
        }
    }

    public CustomRenderPipeline(CameraBufferSettings cameraBufferSettings, 
        bool colorLUTPointSampler, bool useDynamicBatching,
        bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject,
        ShadowSettings shadowSettings, PostFXSettings postFXSettings,
        int colorLUTResolution, Shader cameraRendererShader)

    {
        //追踪阴影设置
        this.colorLUTPointSampler = colorLUTPointSampler;
        this.colorLUTResolution = colorLUTResolution;
        this.shadowSettings = shadowSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        // this.allowHDR = allowHDR;
        this.cameraBufferSettings = cameraBufferSettings;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        //设置全局使用线性颜色
        GraphicsSettings.lightsUseLinearIntensity = true;

        // 给Editor模式下添加我们的自定义设置,如光照代理
        InitializeForEditor();

        // 在此处构建CameraRender
        renderer = new CameraRenderer(cameraRendererShader);
    }
}
