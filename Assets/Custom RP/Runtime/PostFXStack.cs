using UnityEngine;
using UnityEngine.Rendering;
// 这里内联关联设置,将设置关联到该类,这样可以使代码更短
// using static 的作用类似于namespace,但是给类型用的
// 它能将一个类的所有成员或结构可以直接调用而不是用xx.xxxx
using static PostFXSettings;

// 像Lighting和Shadows一样,创建一个PostFXStack的类,用于跟踪缓冲区,上下文,相机和FX的设置

public partial class PostFXStack
{
    private const string 
        bufferName = "Post FX",
        fxaaQualityLowKeyword = "FXAA_QUALITY_LOW",
        fxaaQualityMediumKeyword = "FXAA_QUALITY_MEDIUM";

    private CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    private ScriptableRenderContext context;

    private Camera camera;

    private PostFXSettings settings;

    private int 
        bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        // 提前为半分辨率的图像声明一个纹理,将其作为新的起点.
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        // 我们需要调整Bloom的效果,因此获取一个新的全分辨率临时纹理,并将其作为DoBloom的渲染目标
        // 除此之外让该纹理返回是否绘制的内容,而不是在跳过Bloom时直接绘制相机目标
        bloomResultId = Shader.PropertyToID("BloomResult"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
        colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments"),
        colorFilterId = Shader.PropertyToID("_ColorFilter"),
        whiteBalanceId = Shader.PropertyToID("_WhiteBalance"),
        splitToningShadowId = Shader.PropertyToID("_SplitToningShadows"),
        splitToningHighlightId = Shader.PropertyToID("_SplitToningHighlights"),
        smhShadowsId = Shader.PropertyToID("_SMHShadows"),
        smhMidtonesId = Shader.PropertyToID("_SMHMidtones"),
        smhHighlightsId = Shader.PropertyToID("_SMHHighlights"),
        smhRangeId = Shader.PropertyToID("_SMHRange"),
        channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed"),
        channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen"),
        channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue"),
        colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT"),
        colorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters"),
        colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLogC"),
        colorGradingResultId = Shader.PropertyToID("_ColorGradingResult"),
        usePointSamplerId = Shader.PropertyToID("_UsePointSampler"),
        finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend"),
        finalDstBlendId = Shader.PropertyToID("_FinalDstBlend"),
        finalResultId = Shader.PropertyToID("_FinalResult"),
        copyBicubicId = Shader.PropertyToID("_CopyBicubic"),
        fxaaConfigId = Shader.PropertyToID("_FXAAConfig");

    private int 
        bloomPyramidId,
        colorLUTResolution;

    // 至多使用65536 * 65536 大小的Texture降低至1像素,所以最大Levels使用16
    private const int maxBloomPyramidLevels = 16;

    private CameraSettings.FinalBlendMode finalBlendMode;

    // 添加一个公共属性用来指示堆栈是否属于活动状态,仅当堆栈设置存在时才会显示活动
    // 如果未提供任何设置,则应跳过后处理
    public bool IsActive => settings != null;

    enum Pass
    {
        Copy,
        BloomHorizontal,
        BloomVertical,
        BloomAdd,
        BloomScatter,
        BloomScatterFinal,
        BloomPrefilter,
        BloomPrefilterFireflies,
        ColorGradingNone,
        ColorGradingACES,
        ColorGradingNeutral,
        ColorGradingReinhard,
        ApplyColorGrading,
        FinalRescale,
        FXAA,
        ApplyColorGradingWithLuma,
        FXAAWithLuma
    }

    private bool
        useHDR,
        keepAlpha,
        colorLUTPointSampler;
       
    CameraBufferSettings.BicubicRescalingMode bicubicRescaling;

    private static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);

    // 因为我们现在用的是自定义的相机缓存尺寸,需要单独创建一个组件来追踪该属性
    private Vector2Int bufferSize;

    private CameraBufferSettings.FXAA fxaa;
    
    public void Setup(ScriptableRenderContext context, Camera camera, 
        Vector2Int bufferSize, PostFXSettings settings, bool keepAlpha,
        bool useHDR, int colorLUTResolution, bool colorLUTPointSampler, 
        CameraSettings.FinalBlendMode finalBlendMode, 
        CameraBufferSettings.BicubicRescalingMode bicubicRescaling, 
        CameraBufferSettings.FXAA fxaa)
    {
        this.fxaa = fxaa;
        this.bicubicRescaling = bicubicRescaling;
        // 在此处追踪正确的bufferSize
        this.bufferSize = bufferSize;
        // 可以获得从CameraRenderer中传来的设置了
        this.finalBlendMode = finalBlendMode;
        this.colorLUTPointSampler = colorLUTPointSampler;
        this.colorLUTResolution = colorLUTResolution;
        this.keepAlpha = keepAlpha;
        this.useHDR = useHDR;
        this.context = context;
        this.camera = camera;
        // 设置FX只应用于适当的相机,通过检测是否有游戏或场景相机来强制执行这一点,如果没有则设为null
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        // 执行检测SceneView设置的函数
        ApplySceneViewState();
    }

    public void Render(int sourceId)
    {
        // 我们还需要一个渲染堆栈的公共方法
        // 只需要使用适当的着色器绘制一个覆盖整个图像的矩形就可以把效果应用于整个图像
        // 由于目前还没有着色器,先将渲染得到的屏幕内容复制到相机的缓冲区中
        // 这可以通过调用CommandBuffer,想起传递Source和目标的Id来完成
        // 这些标识符用多种格式提供,我们将使用整数作为源,为其添加参数目标,最后清除缓冲区
        //buffer.Blit(sourceId,BuiltinRenderTextureType.CameraTarget);
        //Draw(sourceId,BuiltinRenderTextureType.CameraTarget,Pass.Copy);
        // 调整DoBloom,当Bloom使用时对结果进行色调映射,然后释放缓存
        // 否则直接映射原图
        if (DoBloom(sourceId))
        {
            DoFinal(bloomResultId);
            buffer.ReleaseTemporaryRT(bloomResultId);
        }
        else
        {
            DoFinal(sourceId);
        }
        context.ExecuteCommandBuffer(buffer);
        // 在这种情况下,我们不需要手动开始和结束缓冲区样本,因为我们不需要调用ClearRenderTarget
        // 这是因为我们完全替换了目标位置的内容
        buffer.Clear();
    }

    // 定义我们自己的Draw方法,使用一个from一个to和一个Pass来绘制
    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        // 通过该命令使Source使from可用,使用to作为渲染目标
        buffer.SetGlobalTexture(fxSourceId,from);
        buffer.SetRenderTarget(to,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
        // 然后绘制三角形,我们通过调用Matrix4x4.identity,程序化创建的材质,使用的Pass序号,和绘制的图形与顶点数来调用
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material,(int)pass,
            MeshTopology.Triangles,3);
    }
    
    // 由于现在有两种最终Pass,所以提供一个Pass选项来选择
    void DrawFinal(RenderTargetIdentifier from, Pass pass)
    {
        // 将混合配置传递给shader
        buffer.SetGlobalFloat(finalSrcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(finalDstBlendId, (float)finalBlendMode.destination);
        // 通过该命令使Source使from可用,使用to作为渲染目标
        buffer.SetGlobalTexture(fxSourceId,from);
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
            // 当如果使用一个tile-based的GPU,则会在渲染窗口的边缘获得伪影超出边界
            // 发生这种情况是因为备品彼得切片区域包含部分垃圾数据
            // 通过在不适用完整视口时加载目标来解决这个问题.
            // 因为Apple Silicon Mac具有tile-base的GPU并不支持DontCare选项
            // 当destination的blend模式不是Zero时,需要加载targetBuffer
            finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect ? 
                RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);
        // 然后绘制三角形,我们通过调用Matrix4x4.identity,程序化创建的材质,使用的Pass序号,和绘制的图形与顶点数来调用
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material,(int)pass,
            MeshTopology.Triangles,3);
    }

    public PostFXStack()
    {
        // 使用_BloomPyramidX来作为材质的标识符
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        // 需要在每一步之间增加一个纵向模糊,需要把步进数乘二
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            // 使用循环生成各级标志,而不是单独去声明他们
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    // Bloom效果一般用于把物体变亮,代表颜色的散射.这可以通过模糊图像来实现
    // 亮度高的像素将会流进相对较暗的像素里,这样表现就是发光效果
    // 最快且最简单的方法来模糊一个材质时通过复制他到另外一个只有它一半长和高的材质
    // 任意一个像素通过邻近的四个像素升采样到一个像素,同时用双线性滤波平均2x2的像素
    // 每循环一次都只有一点点效果,因此多循环几次,直到降采样到一个需要的等级
    // 创建DoBloom方法,通过给定一个sourceId来实现Bloom效果
    bool DoBloom(int sourceId)
    {
        // 初始化
        //buffer.BeginSample("Bloom");
        // 使用BloomSettings中的配置文件
        PostFXSettings.BloomSettings bloom = settings.Bloom;
        // 生成第一级金字塔的参数
        int width, height;
        if (bloom.ignoreRenderScale)
        {
            width = camera.pixelWidth / 2;
            height = camera.pixelHeight / 2;
        }
        else
        {
            // 当不使用renderScale时使用我们修改过的尺寸
            width = bufferSize.x / 2;
            height = bufferSize.y / 2;
        }
        // 如果迭代数小于0,或强度小于等于0,或默认的宽高小于限制数的二倍,则直接跳过Bloom
        if (bloom.maxIterations == 0 || bloom.intensity <= 0f ||
            // 因为直接使用半分辨率跳过了第一次迭代,所以本应该用在第一次迭代的像素限制应该乘二来适配一半分辨率
            height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
        {
            // Draw(sourceId,BuiltinRenderTextureType.CameraTarget,Pass.Copy);
            // buffer.EndSample("Bloom");
            
            // 返回Bloom是否执行而不是直接结束绘制
            return false;
        }
        
        buffer.BeginSample("Bloom");

        // 通过引入阈值来限制部分区域过亮
        Vector4 threshold;
        // 我们的输入值时Gamma的,因为这更符合直觉
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId,threshold);
        
        RenderTextureFormat format = useHDR ? 
            RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        // 获取半分辨率的预处理结果,并把它当作Pyramid0,同时再将高度减半
        buffer.GetTemporaryRT(
                bloomPrefilterId,width,height,0,FilterMode.Bilinear,format
            );
        // HDR的一个缺点是它可以产生比周围环境亮的多的小区域.
        // 当这些区域的大小为像素或者更小时,他们会急剧改变相对大小,并在移动过程中突然出现和消失,从而导致闪烁
        // 这些区域成为萤火虫.当对他们施加Bloom时,会变得闪来闪去
        // 完全消除这个问题需要接近无限的分辨率,这明显不可能.
        // 可以通过在预处理过程中模糊图像来淡化fireflies的影响
        Draw(sourceId,bloomPrefilterId,
            bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        int fromId = bloomPrefilterId,
            // 用于从末端清理多余的pyramid以及记录Dst
            toId = bloomPyramidId + 1;

        int i;
        // Downsample阶段
        for (i = 0; i < bloom.maxIterations; i++)
        {
            // 每一次迭代之前先判断宽高是否小于限制
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
            {
                break;
            }

            // mid初始化为dst的上一个
            int midId = toId - 1;
            // 获得to和mid的对应Texture
            buffer.GetTemporaryRT(midId,width,height,0,FilterMode.Bilinear,format);
            buffer.GetTemporaryRT(toId,width,height,0,FilterMode.Bilinear,format);
            // 把他们作为图像源输入Draw中根据选定pass绘制
            Draw(fromId,midId,Pass.BloomHorizontal);
            Draw(midId,toId,Pass.BloomVertical);
            // id重新排序
            fromId = toId;
            // 因为一次执行两个操作,所以现在循环步进数为2
            toId += 2;
            width /= 2;
            height /= 2;
        }
        
        //中间阶段
        // 进入pyramid之后就不需要半分辨率图了,把他释放掉
        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        buffer.SetGlobalFloat(
                bloomBicubicUpsamplingId,bloom.bicubicUpsampling ? 1f : 0f
            );

        // 我们将同时支持传统的叠加Bloom和开销更少的散射Bloom
        // 在散射的情况下,我们将使用散射量而不是1来表示强度
        Pass combinePass,finalPass;
        float finalIntensity;
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = finalPass = Pass.BloomAdd;
            buffer.SetGlobalFloat(bloomIntensityId,1f);
            finalIntensity = bloom.intensity;
        }
        else
        {
            combinePass = Pass.BloomScatter;
            // 和叠加Bloom不同,散射Bloom在低强度的时候也会有效,而真实情况是只有在强度较高时才会有效果
            // 虽然不太现实,但仍可以应用阈值来消除较暗的散射.
            // 这可以在使用更强的光晕是保持图像清晰,也会消除光线使图像变暗
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloomIntensityId,bloom.scatter);
            // 因为大于一的强度不适合散射Bloom,这会增加光
            // 所以需要将散射的强度最大之限制为0.95.否则原图将对结果没有贡献
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }
        
        // Upsamping阶段
        
        // 这种实现方式只有在迭代数至少有两个时才会生效.
        if (i > 1)
        {
            //Draw(fromId,BuiltinRenderTextureType.CameraTarget,Pass.Copy);
            // 释放上一张横向采样的缓存,即释放2号缓存
            buffer.ReleaseTemporaryRT(fromId - 1);
            // 将Dst设置为更上一个用于水平采样的id,以此来反转队列
            toId -= 5;
            
            for (i -= 1; i > 0; i--)
            {
                // 把源图画给dst+1,最后一级的水平,我们需要在第一步之前停止,故i-=1
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                // 将上一级垂直结果和这一级垂直结果混合,每次混合都包含之前的数据
                Draw(fromId,toId,finalPass);
                // 释放上一级水平和垂直的缓存
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                // fromId = 0,toId = 
                // 索引值向前跳
                fromId = toId;
                toId -= 2;
            }            
        }
        else
        {
            // 当迭代数仅仅为1时无法完成升采样,需要把申请的RT删除并从别处混合
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }
        
        // 最后一部分混合处理过的图像和第一级的垂直结果,同时包括只迭代一次的结果
        buffer.SetGlobalFloat(bloomIntensityId,finalIntensity);
        buffer.SetGlobalTexture(fxSource2Id,sourceId);
        // 直接将全分辨率的截图带到最后阶段
        // 将最终的全分辨率也应用到renderScale
        buffer.GetTemporaryRT(bloomResultId,
            bufferSize.x,bufferSize.y,0,
            FilterMode.Bilinear,format);
        Draw(fromId,
            bloomResultId,combinePass);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom");
        return true;
    }

    // 添加一个配置文件
    void ConfigureColorAdjustments()
    {
        ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
        // 设置一个V4来调整颜色,各分量表示曝光,对比度,色相转换,饱和度
        // 曝光度使用光圈大小来度量的, 意味着我们必须用2的幂次方来作为配置
        // 对比度和饱和度范围为0-2,色相转换范围为-1,1,而滤色一定要处于线性色彩空间
        buffer.SetGlobalVector(colorAdjustmentsId,new Vector4(
                Mathf.Pow(2f,colorAdjustments.postExposure),
                colorAdjustments.contrast * 0.01f + 1f,
                colorAdjustments.hueShift * (1f / 360f),
                colorAdjustments.saturation * 0.01f + 1f 
            ));
        buffer.SetGlobalColor(colorFilterId,colorAdjustments.colorFilter.linear);
    }

    void ConfigureWhiteBalance()
    {
        WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
        // 使用CoreLibrary中的ColorUtils.ColorBalanceToLMSCoeffs函数来计算色温
        // 输入参数为色温和tint
        buffer.SetGlobalVector(whiteBalanceId,ColorUtils.ColorBalanceToLMSCoeffs(
                whiteBalance.temperature,whiteBalance.tint
            ));
    }

    void ConfigureSplitToning()
    {
        SplitToningSettings splitToning = settings.SplitToning;
        // 将平衡值缩放到-之后存储在阴影颜色的a中
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(splitToningShadowId,splitColor);
        buffer.SetGlobalColor(splitToningHighlightId,splitToning.hightlights);
    }

    void ConfigureChannelMixer()
    {
        ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedId,channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenId,channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueId,channelMixer.blue);
    }

    void ConfigureShadowsMidtonesHighlights()
    {
        ShaodwsMidtonesHighlightsSettings smh = settings.ShaodwsMidtonesHighlights;
        buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
        buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
        buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
        buffer.SetGlobalVector(smhRangeId, new Vector4(
                smh.shadowStart,smh.shadowEnd,smh.highlightsStart,smh.highLightsEnd
            ));
    }

    void ConfigureFXAA()
    {
        if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Low)
        {
            buffer.EnableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        else if(fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium)
        {
            buffer.EnableShaderKeyword(fxaaQualityMediumKeyword);
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
        }
        else
        {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        buffer.SetGlobalVector(fxaaConfigId, 
            new Vector4(fxaa.fixedThreshold, 
                fxaa.relativeThreshold, fxaa.subpixelBlending
                ));
    }
    
    // 虽然我们可以在HDR中渲染,但对于普通设备来说最终的帧缓冲区始终是LDR的.
    // 因此颜色通道在1处被截断.实际上最终的白点位置位于1.
    // 那些及其鲜艳的颜色最终看起来和完全饱和的颜色没有什么不同.
    // 如果不使用任何后期特效就无法分辨出那些物体和灯光是非常明亮的.
    // 为此,我们需要调整图像的亮度,增加白点,以便最亮的颜色不在超过1
    // 我们可以通过均匀地变暗整个图像来做到这点,但这会使大部分图像变得很黑,以至于无法正常观测.
    // 理想情况下我们应该大量调整非常明亮的颜色,而只调整一点深色.
    // 因此我们需要一个不均匀的颜色调整.这种颜色调整并不代表光本身的物理变化,而是我们如何观测他.
    // 例如我们眼睛对较深的色调比对较浅的色调更敏感
    // 从HDR到LDR称为色调映射,没有单一正确的方法来执行色调映射,可以使用不同的方法得到不同的结果.
    void DoFinal(int sourceId)
    {
        // 引用颜色调整的配置文件
        ConfigureColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMixer();
        ConfigureShadowsMidtonesHighlights();

        // 由于LUT是3D的,但常规着色器无法渲染3D纹理.
        // 因此将使用等宽的2D纹理来模拟3D纹理.
        // 因此LUT纹理的高毒等于配置的分辨率,宽度为配置的平方.
        // 默认使用的HDR格式获取具有改大小的临时渲染纹理
        int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        buffer.GetTemporaryRT(
                colorGradingLUTId, lutWidth, lutHeight, 0,
                FilterMode.Bilinear, RenderTextureFormat.DefaultHDR
            );
        // URP不是单独进行色调映射吗?
        // URP将颜色分级和色调映射烘焙到LUT中来进行HDR渲染, 但LDR渲染单独执行色调映射.
        // 但是色调映射对于LDR渲染没有多大意义故此处不做特殊处理
        // 除此之外URP对LDR LUT使用LDR RGBA格式,但我们这里使用默认HDR格式来保持简洁
        // 而且Unity的曝光并未包含在色调映射之内,但是这里的包含在里面
        buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
                lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)
            ));
        
        ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        // 根据配置选择tonemapping方案,以及跳过tonemapping
        Pass pass = Pass.ColorGradingNone + (int)mode;
        // 当使用HDR时就会使用LogC空间
        buffer.SetGlobalFloat(
                colorGradingLUTInLogId, useHDR && pass != Pass.ColorGradingNone ? 1f : 0f
            );
        buffer.SetGlobalFloat(
                usePointSamplerId, colorLUTPointSampler ? 1f : 0f
            );
        
        // 绘制LUT
        // 是否要每帧更新LUT
        // 仅对LUT纹理进行颜色分级和色调映射比单独对图像的所有像素进行调色和色阶映射要少得多
        // 进一步优化是缓存LUT.但是,确定是否需要LUT刷新可能会变得很复杂
        // 尤其是每个摄像机支持的不同的设置或混合设置时.
        // 所以此处我们简单处理,即在每次渲染摄像机时重新创建LUT
        // URP/HDRP也是这样么做的
        Draw(
            sourceId, 
            colorGradingLUTId, pass);
        
        // 因为之前已经计算完毕所需要的图像,此处在传递同一个参数就会覆盖之前的参数
        buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
                1f / lutWidth, 1f / lutHeight, lutHeight - 1f
            ));

        buffer.SetGlobalFloat(finalSrcBlendId, 1f);
        buffer.SetGlobalFloat(finalDstBlendId, 0f);
        if (fxaa.enabled)
        {
            ConfigureFXAA();
            buffer.GetTemporaryRT(
                    colorGradingResultId, bufferSize.x, bufferSize.y, 0,
                    FilterMode.Bilinear, RenderTextureFormat.Default
                );
            Draw(sourceId, 
                colorGradingResultId, 
                keepAlpha ? Pass.ApplyColorGrading : Pass.ApplyColorGradingWithLuma);
        }
        // 通过该条件判断是否使用了renderScale,只需判断长或者宽
        if (bufferSize.x == camera.pixelWidth)
        {
            if (fxaa.enabled)
            {
                DrawFinal(colorGradingResultId, 
                    keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                buffer.ReleaseTemporaryRT(colorGradingResultId);
            }
            else
            {
                DrawFinal(sourceId, Pass.ApplyColorGrading);                  
            }
        }
        else
        // 为了使Rescale 操作再LDR下执行
        // 如果使用了renderScale则需要绘制两次.
        // 首先获取与当前缓冲区大小匹配的临时缓冲区纹理.
        // 当LDR颜色存入其中时,我们使用默认的渲染纹理格式即可,
        // 然后用常规方式绘制finalPass,同时将混合模式设置为OneZero
        // 最后使用DrawFinal来绘制finalRescalePass,最后释放缓存
        {
            // buffer.SetGlobalFloat(finalSrcBlendId, 1f);
            // buffer.SetGlobalFloat(finalDstBlendId, 0f);
            buffer.GetTemporaryRT(
                    finalResultId, bufferSize.x, bufferSize.y, 0,
                    FilterMode.Bilinear, RenderTextureFormat.Default
                );
            if (fxaa.enabled)
            {
                Draw(colorGradingResultId, 
                    finalResultId,
                    keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                buffer.ReleaseTemporaryRT(colorGradingResultId);
            }
            else
            {
                Draw(sourceId, finalResultId, 
                    Pass.ApplyColorGrading);                
            }
            // 由于bicubic采样只有在升采样时作用较大,降采样没啥效果
            // 且当renderScale为2时没啥用处,因为和双线性滤波效果相同
            // 所以将bicubic的选项改为Off,UpOnly和UpAndDown
            bool bicubicSampling =
                bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                bufferSize.x < camera.pixelWidth;
            // 当renderScale过小时可能会导致大果粒,所以支持bicubic采样来淡化这点
            buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1f : 0f);
            DrawFinal(finalResultId, Pass.FinalRescale);
            buffer.ReleaseTemporaryRT(finalResultId);
        }

        // 释放LUT
        buffer.ReleaseTemporaryRT(colorGradingLUTId);
    }
}