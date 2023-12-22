using UnityEngine;
using UnityEngine.Rendering;

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
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2");

    private int bloomPyramidId;

    private const int maxBloomPyramidLevels = 16;

    public bool IsActive => settings != null;

    enum Pass
    {
        Copy,
        BloomHorizontal,
        BloomVertical,
        BloomCombine
    }

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings)
    {
        this.context = context;
        this.camera = camera;
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        ApplySceneViewState();
    }

    public void Render(int sourceId)
    {
        //buffer.Blit(sourceId,BuiltinRenderTextureType.CameraTarget);
        //Draw(sourceId,BuiltinRenderTextureType.CameraTarget,Pass.Copy);
        DoBloom(sourceId);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceId,from);
        buffer.SetRenderTarget(to,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
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

    void DoBloom(int sourceId)
    {
        buffer.BeginSample("Bloom");
        PostFXSettings.BloomSettings bloom = settings.Bloom;
        int width = camera.pixelWidth / 2,
            height = camera.pixelHeight / 2;
        if (bloom.maxIterations == 0 ||
            height < bloom.downscaleLimit || width < bloom.downscaleLimit)
        {
            Draw(sourceId,BuiltinRenderTextureType.CameraTarget,Pass.Copy);
            buffer.EndSample("Bloom");
            
            return;
        }
        
        RenderTextureFormat format = RenderTextureFormat.Default;
        int fromId = sourceId,
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

        if (i > 1)
        {
            //Draw(fromId,BuiltinRenderTextureType.CameraTarget,Pass.Copy);
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
            
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId,toId,Pass.BloomCombine);
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

        
        buffer.SetGlobalTexture(fxSource2Id,sourceId);
        Draw(fromId,
            BuiltinRenderTextureType.CameraTarget,Pass.BloomCombine);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom");
    }
}