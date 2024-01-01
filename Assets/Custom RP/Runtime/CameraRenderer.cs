﻿using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    private ScriptableRenderContext context;

    private Camera camera;

    private const string bufferName = "Render Camera";

    private CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    private CullingResults cullingResults;

    //指出使用哪种Pass
    private static ShaderTagId 
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    private Lighting lighting = new Lighting();
    
    private PostFXStack postFXStack = new PostFXStack();

    // private static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    static int
        colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
        depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
        colorTextureId = Shader.PropertyToID("_CameraColorTexture"),
        depthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
        sourceTextureId = Shader.PropertyToID("_SourceTexture"),
        srcBlendId = Shader.PropertyToID("_CameraSrcBlend"),
        dstBlendId = Shader.PropertyToID("_CameraDstBlend");

    private bool 
        useHDR,
        useScaleRendering,
        colorLUTPointSampler,
        useColorTexture,
        useDepthTexture,
        useIntermediateBuffer;

    private static CameraSettings defaultCameraSettings = new CameraSettings();

    private Material material;

    private Texture2D missingTexture;

    private static bool copyTextureSupported = 
        SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    private static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);
    
    public void Render(ScriptableRenderContext context, Camera camera, 
        CameraBufferSettings bufferSettings, bool colorLUTPointSampler, 
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        ShadowSettings shadowSettings,PostFXSettings postFXSettings,
        int colorLUTResolution)
    {
        this.context = context;
        this.camera = camera;

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = 
            crpCamera ? crpCamera.Settings : defaultCameraSettings;

        // useDepthTexture = true;

        if (camera.cameraType == CameraType.Reflection)
        {
            useColorTexture = bufferSettings.copyColorReflection;
            useDepthTexture = bufferSettings.copyDepthReflection;
        }
        else
        {
            useColorTexture = bufferSettings.copyColor &&
                              cameraSettings.copyColor;
            useDepthTexture = bufferSettings.copyDepth && 
                              cameraSettings.copyDepth;
        }
        if (cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }

        float renderScale = bufferSettings.renderScale;
        useScaleRendering = renderScale < 0.99f || renderScale > 1.01f;
        PrepareBuffer();
        PrepareForSceneWindow();
        //调用Setup之前调用Cull,如果失败则终止
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }
        
        // 当管线和摄像机都启用hdr时才会计算hdr
        useHDR = bufferSettings.allowHDR && camera.allowHDR;
        this.colorLUTPointSampler = colorLUTPointSampler;
        
        //将Shadows渲染在对应相机样本内
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        //使阴影信息在几何前绘制
        lighting.Setup(
            context,cullingResults,shadowSettings,useLightsPerObject,
            cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
        // 在CameraRender中调用FX实例堆栈
        postFXStack.Setup(context,camera,postFXSettings,useHDR,
            colorLUTResolution,colorLUTPointSampler, cameraSettings.finalBlendMode);
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(
            useDynamicBatching,useGPUInstancing,useLightsPerObject,
            cameraSettings.renderingLayerMask);
        DrawUnsupportedShaders();
        DrawGizmosBeforeFX();
        // 如果处于活动状态,在提交之前调用FX堆栈
        if (postFXStack.IsActive)
        {
            postFXStack.Render(colorAttachmentId);
        }
        else if(useIntermediateBuffer)
        {
            DrawFinal(cameraSettings.finalBlendMode);
            ExecuteBuffer();
        }
        // 由于后处理的存在,将Gizmos分为前后两部分分开渲染,省的给Gizmos也加个后处理效果
        DrawGizmosAfterFX();
        // 同一清理所有申请的buffer
        Clearup();
        // //清除ShadowAtlas申请的RT
        // lighting.Cleanup();
        Submit();
    }

    void Setup()
    {
        //用命令缓冲区名称将清除命令圈在一个范围内，省的把别的相机内容清除掉
        context.SetupCameraProperties(camera);
        //1=Skybox,2=Color,3=Depth,4=Nothing
        CameraClearFlags flags = camera.clearFlags;

        useIntermediateBuffer = useScaleRendering ||
            useColorTexture || useDepthTexture || postFXStack.IsActive;
        // 之前的设置都直接渲染到摄像机的缓冲区,要么是用于显示的缓冲区,要么是配置的渲染纹理
        // 我们无法直接控制这些内容, 只能覆盖这些设置
        if (useIntermediateBuffer)
        {
            // 当渲染到中间帧缓冲区时,渲染为填充任意数据的纹理
            // 为了防止出现随机结果,当堆栈处于活动状态时,始终清除深度和颜色
            // 注意,如果不清除,在使用FX堆栈时就可能将一个相机渲染在另一个相机之上
            // 有很多方法可以解决这个问题,此处略
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }
            // 因此,为了给激活的堆栈提供纹理数据,必须使用渲染的图像作为相机的中间帧缓冲区.
            // 获取并将其设置为渲染目标的工作方式与阴影贴图类似,我们只是用这个格式,在清除之前存储纹理
            // HDR渲染只有与后期处理相结合时才有意义,因为没法直接更改最终的帧缓冲区格式
            // 因此我们创建自己的中间缓冲区时,在适当的时候使用默认HDR格式,而不是用默认的LDR格式
            // 如果目标平台支持的话也可以用其他HDR格式,但是为了普适性这里用默认的HDR格式
            // 在HDR模式下单步调试会发现画面很暗,因为线性颜色数据按原样显示了,因此被错误的当成sRGB
            buffer.GetTemporaryRT(colorAttachmentId, camera.pixelWidth, 
                camera.pixelHeight, 32, FilterMode.Bilinear, useHDR ? 
                RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            buffer.GetTemporaryRT(
                    depthAttachmentId, camera.pixelWidth, camera.pixelHeight, 32,
                    FilterMode.Point, RenderTextureFormat.Depth
                );
            buffer.SetRenderTarget(
                colorAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, 
                depthAttachmentId, 
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }
        
        //清除图像缓存,前两个true控制是否应该清除深度和颜色数据，第三个是用于清楚的颜色。
        buffer.ClearRenderTarget
        (
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            //如果flags=2,即清除Color,则需要渲染一个线性空间的背景颜色
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
            );
        //使用命令缓冲区诸如给Profiler
        buffer.BeginSample(SampleName);
        buffer.SetGlobalTexture(colorTextureId, missingTexture);
        buffer.SetGlobalTexture(depthTextureId, missingTexture);
        ExecuteBuffer();
    }

    void Submit()
    {
        //结束采样
        buffer.EndSample(SampleName);
        //执行命令缓冲区
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    
    void DrawVisibleGeometry(
        bool useDynamicBatching,bool useGPUInstancing,bool useLightsPerObject, 
        int renderingLayerMask
        )
    {
        // 默认情况下所有可见光会逐片元计算光照.这对于平行光来说无所谓,但对于超过其他光源范围的片元来说是不必要的
        // 大部分情况下点光源和聚光灯只能影响一小部分片元,导致大部分工作是无用的,却会占据大量的计算资源
        // 为了提升在大量其他光源的情况下的性能,我们需要减少其他光源逐片元评估的次数
        // 有很多方式可以达成这个效果,最简单的是用Unity自带的per-object light indices
        // 实现思路是Unity决定哪个光源会影响每个对象,并将信息发送到GPU.
        // 在渲染对象时值评估相关的灯光,而忽略其他的灯光.这样灯光就是逐对象确定的而不是逐片元决定.
        // 这通常适用于小物体,对于大型物体来说效果不理想,因为光线可能只影响物体的一小部分,但将会被用于评估整个面
        // 同时每个物体可以影响的灯光数量是有限制的,因此大型物体更容易缺失某些光照
        // 由于Unity的per-object light并不理想,所以需要设置为可选
        PerObjectData lightsPerObjectFlags = useLightsPerObject ? 
                PerObjectData.LightData | PerObjectData.LightIndices : 
                PerObjectData.None;
        //用于确定用正交还是透视
        var sortingSettings = new SortingSettings
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(
                unlitShaderTagId,sortingSettings
            )
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            //传递per-object data给drawing settings
            //插值的光照探针数据必须传递给GPU。通过PerObjectData.LightProbe启用
            //通过PerObjectData.LightProbeProxyVolume启用LPPV
            //使用PerObjectData.ShadowMask启用将相关数据发送到GPU中
            //设置OcclusionProbe关键字来启用该功能的数据传输
            //设置ReflectionProbes来启用反射探针
            perObjectData = 
                PerObjectData.ReflectionProbes |
                PerObjectData.Lightmaps | PerObjectData.ShadowMask | 
                PerObjectData.LightProbe | PerObjectData.OcclusionProbe
                | PerObjectData.LightProbeProxyVolume |
                PerObjectData.OcclusionProbeProxyVolume |
                lightsPerObjectFlags
        };
        drawingSettings.SetShaderPassName(1,litShaderTagId);
        //指出哪些Render队列是被允许的
        var filteringSettings = new FilteringSettings(
            RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask);
        
        context.DrawRenderers(
                //告诉Render哪些东西是可以看到的，此外还要提供绘制设置和筛选设置
                cullingResults,ref drawingSettings,ref filteringSettings
            );
        
        context.DrawSkybox(camera);
        if (useColorTexture || useDepthTexture)
        {
            CopyAttachments();            
        }

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        
        context.DrawRenderers(
                cullingResults,ref drawingSettings,ref filteringSettings
            );
    }

    bool Cull(float maxShadowDistance)
    {
        //out的作用
        //当struct参数被定义为输出参数时当作一个对象引用,指向参数所在的内存堆栈上的位置
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            //判断是先到达远平面还是maxShadowDistance
            p.shadowDistance = Mathf.Min(camera.farClipPlane,maxShadowDistance);
            //ref用作优化项,以防止传递ScriptableCullingParameters结构的副本，因为其相当大
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    // 给FX的堆栈添加一个释放纹理的方法,同时也可以把光照的Clear移动到这里
    void Clearup()
    {
        lighting.Cleanup();
        if (useIntermediateBuffer)
        {
            buffer.ReleaseTemporaryRT(colorAttachmentId);
            buffer.ReleaseTemporaryRT(depthAttachmentId);
            if(useColorTexture)
            {
                buffer.ReleaseTemporaryRT(colorTextureId);
            }
            if(useDepthTexture)
            {
                buffer.ReleaseTemporaryRT(depthTextureId);
            }            
        }
    }

    void CopyAttachments()
    {
        if (useColorTexture)
        {
            buffer.GetTemporaryRT(
                colorTextureId, camera.pixelWidth, camera.pixelHeight, 0,
                FilterMode.Bilinear, useHDR ? 
                    RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
            );
            if (copyTextureSupported)
            {
                buffer.CopyTexture(
                    colorAttachmentId, colorTextureId);
            }
            else
            {
                Draw(colorAttachmentId, colorTextureId);
            }
        }
        if (useDepthTexture)
        {
            buffer.GetTemporaryRT(
                    depthTextureId, camera.pixelWidth, camera.pixelHeight, 32, 
                    FilterMode.Point, RenderTextureFormat.Depth
                );
            if (copyTextureSupported)
            {
                buffer.CopyTexture(
                    depthAttachmentId, depthTextureId);                
            }
            else
            {
                Draw(depthAttachmentId, depthTextureId, true);
                // buffer.SetRenderTarget(
                //         colorAttachmentId, 
                //         RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                //         depthAttachmentId, 
                //         RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
                //     );
            }
            // ExecuteBuffer();
        }

        if (!copyTextureSupported)
        {
            buffer.SetRenderTarget(
                colorAttachmentId,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 
                depthAttachmentId, 
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store 
            );
        }
        ExecuteBuffer();
    }

    public CameraRenderer(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        missingTexture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Missing"
        };
        missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        missingTexture.Apply(true, true);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(missingTexture);
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
    {
        buffer.SetGlobalTexture(sourceTextureId, from);
        buffer.SetRenderTarget(
                to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        buffer.DrawProcedural(
                Matrix4x4.identity, material, isDepth ? 1 : 0, 
                MeshTopology.Triangles, 3
            );
    }
    
    void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
    {
        buffer.SetGlobalFloat(srcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendId, (float)finalBlendMode.destination);
        // 通过该命令使Source使from可用,使用to作为渲染目标
        buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
        buffer.SetRenderTarget(
            BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect ? 
                RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);
        // 然后绘制三角形,我们通过调用Matrix4x4.identity,程序化创建的材质,使用的Pass序号,和绘制的图形与顶点数来调用
        buffer.DrawProcedural(Matrix4x4.identity, material, 0,
            MeshTopology.Triangles,3);
        buffer.SetGlobalFloat(srcBlendId, 1f);
        buffer.SetGlobalFloat(dstBlendId, 0f);
    }
}
