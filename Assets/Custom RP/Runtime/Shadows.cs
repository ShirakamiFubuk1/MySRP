using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    private const string bufferName = "Shadows";

    private CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    private ScriptableRenderContext context;

    private CullingResults cullingResults;

    private ShadowSettings shadowSettings;

    private const int maxShadowedDirectionalLightCount = 1;

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
    }

    //追踪shadowed light的信息
    private ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    private int ShadowedDirectionalLightCount;

    private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");

    public void Setup(ScriptableRenderContext context, 
        CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.shadowSettings = shadowSettings;
        //初始化阴影的时候将该值设为0
        ShadowedDirectionalLightCount = 0;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    //用于给阴影贴图在阴影图集中预留位置以及存储相关渲染信息
    public void ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount
            && light.shadows != LightShadows.None && light.shadowStrength > 0f
            && cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds b))
        {
            ShadowedDirectionalLights[ShadowedDirectionalLightCount++] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex
                };
        }
    }

    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            //因为WebGL2.0等平台会绑定贴图和采样器，当shader加载时如果没有贴图将会报错
            buffer.GetTemporaryRT(dirShadowAtlasId,1,1,32,
                FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        }
    }

    void RenderDirectionalShadows()
    {
        int atlasSize = (int)shadowSettings.directional.atlasSize;
        //声明一个方形RT,1属性id，2宽，3高，4缓存深度，越高越高，5过滤模式，6RT类型
        buffer.GetTemporaryRT(dirShadowAtlasId,atlasSize,atlasSize,
            32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        //因为我们的RT只需要从中获得shadowdata，所以不需要管他的初始状态，因为存完信息就删除了
        //故LoadAction选DontCare，StoreAction选Store
        buffer.SetRenderTarget(dirShadowAtlasId,
            RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
        //1clearDepth,2clearColor
        buffer.ClearRenderTarget(true,false,Color.clear);
        buffer.BeginSample(bufferName);
        //处理buffer
        ExecuteBuffer();

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i,atlasSize);
        }
        
        buffer.EndSample(bufferName);
    }

    void RenderDirectionalShadows(int index, int tileSize)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        //234用于控制cascade,5贴图尺寸，6阴影近平面，78矩阵，9splitdata
        cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex,0,1,Vector3.zero,tileSize,0f,
            out Matrix4x4 viewMatrix,out Matrix4x4 projectionMatrix,
            out ShadowSplitData splitData);
        //splitData包含cull信息，需要赋给splitData
        shadowSettings.splitData = splitData;
        buffer.SetViewProjectionMatrices(viewMatrix,projectionMatrix);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
}