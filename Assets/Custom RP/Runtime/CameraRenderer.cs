using UnityEngine;
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
        bufferSizeId = Shader.PropertyToID("_CameraBufferSize"),
        // 当粒子Billboard和物体香蕉时,极具过度在视觉上很突兀,而且会显得很扁平不立体
        // 解决方案时用软性粒子,当他们后面又不透明的几何形状时,软颗粒会淡入淡出.
        // 为了实现这一点,必须将粒子的片段深度与之前绘制到相机缓冲区中相同位置的深度进行比较.
        // 为了实现这个目的需要访问之前存储在摄像机中的缓冲区信息
        // 我们摄像机使用的是单个帧缓冲区,其中包含颜色和深度信息.
        // 这是典型的缓冲区配置,但颜色和深度数据始终存储在单独的缓冲区中,成为帧缓冲区附件.
        // 要想访问深度缓冲区,我们得单独定义这些组件.        
        colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
        depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
        // 我们不能在深度缓冲区用于渲染的同时对其进行采样,必须复制他们
        // 需要引入一个bool值来指示useDepthTexture是否使用深度纹理_CameraDepthTexture.
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

    private Vector2Int bufferSize;

    private const float renderScaleMin = 0.1f, renderScaleMax = 2f;
    
    public void Render(ScriptableRenderContext context, Camera camera, 
        CameraBufferSettings bufferSettings, bool colorLUTPointSampler, 
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        ShadowSettings shadowSettings,PostFXSettings postFXSettings,
        int colorLUTResolution)
    {
        this.context = context;
        this.camera = camera;

        // 获取Camera上挂载的CustomRenderPipelineCamera组件
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
        
        // 如果启用了单独的后处理配置则优先用
        if (cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }

        float renderScale = 
            cameraSettings.GetRenderScale(bufferSettings.renderScale);
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
        if (useScaleRendering)
        {
            renderScale = Mathf.Clamp(renderScale, 0.1f, 2f);
            bufferSize.x = (int)(camera.pixelWidth * renderScale);
            bufferSize.y = (int)(camera.pixelHeight * renderScale);
        }
        else
        {
            bufferSize.x = camera.pixelWidth;
            bufferSize.y = camera.pixelHeight;
        }
        this.colorLUTPointSampler = colorLUTPointSampler;
        
        //将Shadows渲染在对应相机样本内
        buffer.BeginSample(SampleName);
        buffer.SetGlobalVector(bufferSizeId, new Vector4(
                1f / bufferSize.x, 1f / bufferSize.y,
                bufferSize.x, bufferSize.y
            ));
        ExecuteBuffer();
        //使阴影信息在几何前绘制
        lighting.Setup(
            context,cullingResults,shadowSettings,useLightsPerObject,
            cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
        bufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;
        // 在CameraRender中调用FX实例堆栈
        postFXStack.Setup(context,camera,bufferSize,postFXSettings,
            cameraSettings.keepAlpha, useHDR, colorLUTResolution, 
            colorLUTPointSampler, cameraSettings.finalBlendMode,
            bufferSettings.bicubicRescaling, bufferSettings.fxaa);
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
            // 将颜色缓存附件传递给Render
            postFXStack.Render(colorAttachmentId);
        }
        else if(useIntermediateBuffer)
        {
            // 使用附带相机混合模式的设置作为参数输入
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

        // 先判断是否要需要存储缓存中间缓存
        useIntermediateBuffer = useScaleRendering ||
            useColorTexture || useDepthTexture || postFXStack.IsActive;
        // 之前的设置都直接渲染到摄像机的缓冲区,要么是用于显示的缓冲区,要么是配置的渲染纹理
        // 我们无法直接控制这些内容, 只能覆盖这些设置
        // 使用该设置来跟踪中间帧,在获得缓存组件之前调用
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
            buffer.GetTemporaryRT(
                colorAttachmentId, bufferSize.x, bufferSize.y, 
                0, FilterMode.Bilinear, useHDR ? 
                RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            // 在此处将颜色缓存和深度缓存分开存储,而不是一个复合缓冲区
            // 将原来的sourceId分别换成对应的AttachmentId
            // 颜色缓存中没有深度,深度缓存的RenderTextureFormat格式为Depth
            // 深度缓冲区使用FilterMode.Point而不是双线性滤波,因为滤波深度信息没意义
            buffer.GetTemporaryRT(
                    depthAttachmentId, bufferSize.x, bufferSize.y, 
                    32, FilterMode.Point, RenderTextureFormat.Depth
                );
            // 两个缓冲组件可以用一个单独的SetRenderTarget设置.
            // 给每个缓存Id后面跟上对应的操作即可.         
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
        // 先给这两个贴图赋一个默认材质
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
            // 将Layer信息传入过滤设置并把类型转换为uint
            RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask);
        
        context.DrawRenderers(
                //告诉Render哪些东西是可以看到的，此外还要提供绘制设置和筛选设置
                cullingResults,ref drawingSettings,ref filteringSettings
            );
        
        context.DrawSkybox(camera);
        // 把复制材质放在不透明绘制之后,即绘制完成skybox
        // 这也意味着深度材质只有渲染透明材质时有用
        // 但是只有使用后处理时才会有附加缓存组件可以获得,要兼容不启动后处理时的情况
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
            // 因为这两个缓存区都是我们申请的,需要手动清除缓存
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

    // 创建一个复制缓存组件的函数来获得临时复制的深度材质
    void CopyAttachments()
    {
        if (useColorTexture)
        {
            buffer.GetTemporaryRT(
                colorTextureId, bufferSize.x, bufferSize.y, 
                0, FilterMode.Bilinear, useHDR ? 
                    RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
            );
            if (copyTextureSupported)
            {
                // 通过该函数来复制材质,将临时复制的组件复制到我们准备好的缓存空间
                buffer.CopyTexture(
                    colorAttachmentId, colorTextureId);
            }
            else
            {
                // 如果不支持复制贴图,则使用绘制替代,虽然开销更高
                Draw(colorAttachmentId, colorTextureId);
            }
        }
        if (useDepthTexture)
        {
            buffer.GetTemporaryRT(
                    depthTextureId, bufferSize.x, bufferSize.y, 
                    32, FilterMode.Point, RenderTextureFormat.Depth
                );
            if (copyTextureSupported)
            {
                buffer.CopyTexture(
                    depthAttachmentId, depthTextureId);                
            }
            else
            {
                Draw(depthAttachmentId, 
                    depthTextureId, true);
                // buffer.SetRenderTarget(
                //         colorAttachmentId, 
                //         RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                //         depthAttachmentId, 
                //         RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
                //     );
            }
            // ExecuteBuffer();
        }

        // 如果不支持复制材质则在此设置贴图
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

    // 由于深度纹理是可选的,所以有可能不存在.这时着色器对其采样时结果将是随机的
    // 可以是控温里,也可以是之前的缓存,也可以是另一台相机的缓存.
    // 在不透明渲染阶段,着色器可以能过早对深度纹理采样.我们至少要保证无效样本的结果一致.
    // 故我们创建一个不存在纹理时的默认纹理.
    public CameraRenderer(Shader shader)
    {
        // 使用CoreUtils工具来获得临时材质,输入材质为对用的shader
        // 这个方法可以创建一个新的材质且在Editor中隐藏也不会保存为资源
        // 当shader丢失的时候会报错
        material = CoreUtils.CreateEngineMaterial(shader);
        // 因为CoreUtils中没有贴图操作的方法,所以设置为HideAndDontSave
        // 将其命名为Missing,当Debug时可以很明确知道纹理丢失
        // 给他简单设定为1x1材质所有通道设定为0.5.
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
        // 创建一个弃用方法销毁这两种材质
        // 该方法会定期销毁材质,具体取决于Unity是否处于运行模式.
        // 需要这样做的原因是每次修改RP资源时就会创建新的RP实例和渲染器
        // 这可能会导致创建许多材质.
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(missingTexture);
    }

    // 类似于PostFX里面添加一个绘制函数,在不使用PostFX时可以绘制帧缓存
    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
    {
        buffer.SetGlobalTexture(sourceTextureId, from);
        buffer.SetRenderTarget(
                to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        // 在此处根据是否使用深度来判断使用的Pass
        buffer.DrawProcedural(
                Matrix4x4.identity, material, isDepth ? 1 : 0, 
                MeshTopology.Triangles, 3
            );
    }
    
    // 由于剔除,光线处理和阴影渲染是按照摄像机执行的,因此最好每帧尽可能渲染较少的摄像机,最好只渲染一个摄像机.
    // 但有时确实需要同时展示多个视角.如多人分屏游戏,后视镜,自上而下多个叠加层,游戏内摄像头和3D角色肖像.
    // 其中第一人称游戏的手持物体不管周围环境怎么变手上都不变,因为他们是两个不同的相机渲染的.
    // 因为这时最终绘制,所以可以用硬编码值替换除了source之外的所有参数
    // 添加FinalBlendMode对多相机的支持
    // 因为默认情况下仅在使用单个摄像机时有效,但在渲染为没有后期特效的中间纹理时会失败.
    // 原因是因为我们正在对摄像机目标执行常规复制,这会忽略视口和最终混合模式.
    // 所以除了Copy Pass之外我们还需要把混合模式变回one-zero来防止影响复制操作,
    // 输入的贴图是颜色的附件缓存,输入参数是混合模式
    void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
    {
        buffer.SetGlobalFloat(srcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendId, (float)finalBlendMode.destination);
        // 通过该命令使Source使from可用,使用to作为渲染目标
        buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
        buffer.SetRenderTarget(
            BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect ? 
                // 因为需要执行FinalPas的alphaBlend,此处需要总是存储目标缓存
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
