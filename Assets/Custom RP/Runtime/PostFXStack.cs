using UnityEngine;
using UnityEngine.Rendering;

// 像Lighting和Shadows一样,创建一个PostFXStack的类,用于跟踪缓冲区,上下文,相机和FX的设置

public partial class PostFXStack
{
    private const string bufferName = "Post FX";

    private CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    private ScriptableRenderContext context;

    private Camera camera;

    private PostFXSettings settings;

    private int 
        bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        bloomResultId = Shader.PropertyToID("BloomResult"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2");

    private int bloomPyramidId;

    private const int maxBloomPyramidLevels = 16;

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
        ToneMappingACES,
        ToneMappingNeutral,
        ToneMappingReinhard
    }

    private bool useHDR;
    
    public void Setup(ScriptableRenderContext context, Camera camera, 
        PostFXSettings settings ,bool useHDR)
    {
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
        if (DoBloom(sourceId))
        {
            DoToneMapping(bloomResultId);
            buffer.ReleaseTemporaryRT(bloomResultId);
        }
        else
        {
            DoToneMapping(sourceId);
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

    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    bool DoBloom(int sourceId)
    {
        //buffer.BeginSample("Bloom");
        PostFXSettings.BloomSettings bloom = settings.Bloom;
        int width = camera.pixelWidth / 2,
            height = camera.pixelHeight / 2;
        if (bloom.maxIterations == 0 || bloom.intensity <= 0f ||
            height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
        {
            // Draw(sourceId,BuiltinRenderTextureType.CameraTarget,Pass.Copy);
            // buffer.EndSample("Bloom");
            
            return false;
        }
        
        buffer.BeginSample("Bloom");

        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId,threshold);
        
        RenderTextureFormat format = useHDR ? 
            RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT(
                bloomPrefilterId,width,height,0,FilterMode.Bilinear,format
            );
        Draw(sourceId,bloomPrefilterId,
            bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        int fromId = bloomPrefilterId,
            toId = bloomPyramidId + 1;

        int i;
        for (i = 0; i < bloom.maxIterations; i++)
        {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
            {
                break;
            }

            int midId = toId - 1;
            buffer.GetTemporaryRT(midId,width,height,0,FilterMode.Bilinear,format);
            buffer.GetTemporaryRT(toId,width,height,0,FilterMode.Bilinear,format);
            Draw(fromId,midId,Pass.BloomHorizontal);
            Draw(midId,toId,Pass.BloomVertical);
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }
        
        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        buffer.SetGlobalFloat(
                bloomBicubicUpsamplingId,bloom.bicubicUpsampling ? 1f : 0f
            );

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
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloomIntensityId,bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }
        
        if (i > 1)
        {
            //Draw(fromId,BuiltinRenderTextureType.CameraTarget,Pass.Copy);
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
            
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId,toId,finalPass);
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }            
        }
        else
        {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }

        buffer.SetGlobalFloat(bloomIntensityId,finalIntensity);
        buffer.SetGlobalTexture(fxSource2Id,sourceId);
        buffer.GetTemporaryRT(bloomResultId,camera.pixelWidth,camera.pixelHeight,0,
            FilterMode.Bilinear,format);
        Draw(fromId,
            bloomResultId,combinePass);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom");
        return true;
    }

    void DoToneMapping(int sourceId)
    {
        PostFXSettings.ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        Pass pass = mode < 0 ? Pass.Copy : Pass.ToneMappingACES + (int)mode;
        Draw(sourceId,BuiltinRenderTextureType.CameraTarget,pass);
    }
}