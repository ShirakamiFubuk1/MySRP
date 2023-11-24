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

    private const int
        maxShadowedDirectionalLightCount = 4,
        maxCascades = 4;

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    //追踪shadowed light的信息
    private ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    private int ShadowedDirectionalLightCount;

    private static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        //shadowDistanceId = Shader.PropertyToID("_ShadowDistance"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    private static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];

    private static Vector4[]
        cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades];

    private static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7"
    };

    private static string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

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
    public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount
            && light.shadows != LightShadows.None && light.shadowStrength > 0f
            && cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds b))
        {
            ShadowedDirectionalLights[ShadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
            return new Vector3(light.shadowStrength,  
                shadowSettings.directional.cascadeCount * ShadowedDirectionalLightCount++,
                light.shadowNormalBias
                );
        }
        return Vector3.zero;
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
        //分割Buffer给对应的light,用来支持最多四个直接光
        int tiles = ShadowedDirectionalLightCount * shadowSettings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i,split,tileSize);
        }
        
        buffer.SetGlobalInt(cascadeCountId,shadowSettings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId,cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId,cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId,dirShadowMatrices);
        //buffer.SetGlobalFloat(shadowDistanceId,shadowSettings.maxDistance);

        float f = 1f - shadowSettings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId,
            new Vector4(1f/shadowSettings.maxDistance,1f/shadowSettings.distanceFade,1f/(1f - f*f))
            );
        SetKeywords(
            directionalFilterKeywords,(int)shadowSettings.directional.filter - 1
            );
        SetKeywords(
            cascadeBlendKeywords,(int)shadowSettings.directional.cascadeBlend - 1
            );
        buffer.SetGlobalVector(
            shadowAtlasSizeId,new Vector4(atlasSize,1f / atlasSize)
            );
        buffer.EndSample(bufferName);
        //处理Buffer
        ExecuteBuffer();
    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        //按照索引获取开启阴影的光照
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        int cascadeCount = shadowSettings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = shadowSettings.directional.CascadeRatios;
        float cullingFactor = Mathf.Max(0f, 0.8f - shadowSettings.directional.cascadeFade);

        for (int i = 0; i < cascadeCount; i++)
        {
            //234用于控制cascade,5贴图尺寸，6阴影近平面
            //7 Shadow Projection Matrix,8 Shadow View Matrix,9 splitdata
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex,i,cascadeCount,ratios,tileSize,
                light.nearPlaneOffset, out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            //splitData包含cull信息，需要赋给splitData
            shadowDrawingSettings.splitData = splitData;
            if (index == 0)
            {
                SetCascadeData(i,splitData.cullingSphere,tileSize);
            }
            int tileIndex = tileOffset + i;
            //SetTileViewport(index,split,tileSize);
            //通过将光的Shadow Projection Matrix和View Matrix相乘来获得世界空间转换到光空间的矩阵
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,SetTileViewport(tileIndex,split,tileSize),split
                );
            buffer.SetViewProjectionMatrices(viewMatrix,projectionMatrix);
            buffer.SetGlobalDepthBias(0f,light.slopeScaleBias);
            ExecuteBuffer();
            //命令相机绘制阴影,且只会识别ShadowCasterPass
            context.DrawShadows(ref shadowDrawingSettings);
            buffer.SetGlobalDepthBias(0f,0f);
        }
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }

    Vector2 SetTileViewport(int index, int split,float tileSize)
    {
        //转换成方形
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(
            offset.x * tileSize,offset.y * tileSize,tileSize,tileSize
            ));
        return offset;
    }
    
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        //最直观的做法是让0表示零深度,1表示最大深度.由于深度缓冲区精度有限,而且为非线性存储
        //所以我们反转缓冲区来更高效利用高效率部分.
        if (SystemInfo.usesReversedZBuffer) {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        //Clip Space是一个正方型,从-1到1,中心是0.但是纹理坐标和深度都是从0到1
        //通过XYZ缩放和偏移一半将其烘焙到矩阵中
        //由于矩阵乘法运算量过大,且会导致大量无意义的零乘法,此处直接调整矩阵
        float scale = 1f / split;
        //利用预先计算好的Offset和scale可以节约大量计算量
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        //cascadeData[index].x = 1f / cullingSphere.w;
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)shadowSettings.directional.filter + 1.0f);
        cullingSphere.w -= filterSize;
		cullingSphere.w *= cullingSphere.w;
		cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
    }

    void SetKeywords(string[] keywords,int enabledIndex)
    {
        //int enabledIndex = (int)shadowSettings.directional.filter - 1;
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }
}