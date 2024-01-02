using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private Shader cameraRendererShader = default;
    
    [SerializeField] private bool 
        useDynamicBatching = true, 
        useGPUInstancing = true, 
        useSRPBatcher = true,
        useLightsPerObject = true;

    // 在这里使用单个堆栈,通过为其添加配置项,将其传递给RP的构造函数,从而提供给RP
    [SerializeField] private PostFXSettings postFXSettings = default;

    // [SerializeField] private bool allowHDR = true;

    [SerializeField] private CameraBufferSettings cameraBuffer = new CameraBufferSettings
    {
        allowHDR = true,
        renderScale = 1f,
        fxaa = new CameraBufferSettings.FXAA
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f
        }
    };

    [SerializeField] private bool colorLUTPointSampler = true;
    
    public enum ColorLUTResolution
    {
        _16 = 16, _32 = 32, _64 = 64
    }

    [SerializeField] private ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;
    
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(cameraBuffer, colorLUTPointSampler,
            useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightsPerObject, 
            shadows, postFXSettings, (int)colorLUTResolution, cameraRendererShader);
    } 

    [SerializeField] private ShadowSettings shadows = default;
}
