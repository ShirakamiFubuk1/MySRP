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
        // 将默认的RenderScale设定为1
        renderScale = 1f,
        fxaa = new CameraBufferSettings.FXAA
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f
        }
    };

    [SerializeField] private bool colorLUTPointSampler = true;
    
    // 执行每个像素的所有颜色分级是一个步骤繁重的工作.
    // 我们可以制作可能的变体,之应用更改某些内容的步骤,但这需要大量的关键字和传递
    // 相反,可以将颜色分级烘焙到LUT中,对其采样以转换颜色.
    // LUT是3D纹理,通常为32x32x32.使用该纹理并对其采样比直接对整个图像进行颜色调整省的多
    // URP和HDRP使用相同的方法
    // 通常32的彩色LUT分辨率就足够了,但这里我们提供一个可配置选项.
    // 这是一个质量设置,将设置添加到管线资源中,然后用于所有的颜色分级.
    // 虽然URP/HDRP的分辨率支持到65,但是不用POT(Power Of Two)可能会出错
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

    // 从此处获得Shader属性然后把它们传递给管线构造器,这样我们就可以连接上我们的shader
    [SerializeField] private ShadowSettings shadows = default;
}
