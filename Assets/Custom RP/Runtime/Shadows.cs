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
        maxShadowedOtherLightCount = 16,
        maxCascades = 4;

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    //追踪shadowed light的信息
    private ShadowedDirectionalLight[] shadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        // 加一个布尔值用于判断是否是点光源
        public bool isPoint;
    }

    private ShadowedOtherLight[] shadowedOtherLights =
        new ShadowedOtherLight[maxShadowedOtherLightCount];
    
    private int 
        shadowedDirectionalLightCount,
        shadowedOtherLightCount;

    private static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        //shadowDistanceId = Shader.PropertyToID("_ShadowDistance"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        //收集着色器中图集atlas大小和texel纹素大小
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
        otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
        otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
        otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles"),
        shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

    private static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades],
        otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

    private static Vector4[]
        cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades],
        otherShadowTiles = new Vector4[maxShadowedOtherLightCount];

    private static string[] 
        directionalFilterKeywords =
        {
            "_DIRECTIONAL_PCF3",
            "_DIRECTIONAL_PCF5",
            "_DIRECTIONAL_PCF7"
        },
        //用于控制shadowMask类型的关键字
        shadowMaskKeywords =
        {
            //shadowMask模式,它的工作方式与shadowMaskDistance完全相同
            //只是Unity将会忽略所有使用shadowMask的灯光的staticShadowCaster
            //因为shadowMask模式随处可用,所以可以全部使用来减少实时阴影,但是效果会差一些
            "_SHADOW_MASK_ALWAYS",
            //距离模式
            "_SHADOW_MASK_DISTANCE"
            //关于Subtractive mixed lighting mode
            //减法模式是另一种将烘焙光照和阴影的方式,只用一个单独的光照贴图
            //方法是完全烘焙光照但同时也是用实时光照,然后计算实时光照的漫反射,采样实时阴影
            //用这些来确定哪些漫反射光照需要被shadowed,是那些你从漫反射GI中减去的
            //虽然最后得到了烘焙照明的静态对象,但是也计算了漫反射实时照明(脱裤子放屁)
        },
        otherFilterKeywords =
        {
            "_OTHER_PCF3",
            "_OTHER_PCF5",
            "_OTHER_PCF7"
        };

    private static string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    private bool useShadowMask;

    private Vector4 atlasSizes;

public void Setup(ScriptableRenderContext context, 
        CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.shadowSettings = shadowSettings;
        //初始化阴影的时候将该值设为0
        shadowedDirectionalLightCount = shadowedOtherLightCount = 0;
        //启用shadowMask
        useShadowMask = false;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    //用于给阴影贴图在阴影图集中预留位置以及存储相关渲染信息
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (shadowedDirectionalLightCount < maxShadowedDirectionalLightCount
            && light.shadows != LightShadows.None && light.shadowStrength > 0f
            //防止跳过没有实时shadowCaster的光源
            // && cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds b)
            )
        {
            //如果不使用shadowMask则返回-1,故初始化为-1
            float maskChannel = -1;
            //由于需要判断是否需要使用shadowMask,必须检测是否有使用它的灯
            LightBakingOutput lightBaking = light.bakingOutput;
            if (
                //光照模式为Mixed同时光照混合模式设为shadowMask时使用阴影蒙版
                lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
            )
            {
                useShadowMask = true;
                //通过occlusionMaskChannel来获得光照mask的通道index
                maskChannel = lightBaking.occlusionMaskChannel;
            }
            //需要先判断光源是否使用阴影蒙版，之后在检查是否有实时阴影投射器
            if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                //当阴影强度大于0时，着色器将对阴影贴图进行采样，即便这是不正确的
                //这种情况我们可以通过将阴影强度取反来避免。
                //如果不使用shadowMask则返回-1
                return new Vector4(-light.shadowStrength, 0f, 0f,maskChannel);
            }
            //从灯光中获取各种属性
            shadowedDirectionalLights[shadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
            //将灯光的默认偏移放入返回数据中
            return new Vector4(light.shadowStrength,  
                shadowSettings.directional.cascadeCount * shadowedDirectionalLightCount++,
                light.shadowNormalBias,maskChannel
                );
        }
        return new Vector4(0f,0f,0f,-1f);
    }

    public void Render()
    {
        if (shadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            //因为WebGL2.0等平台会绑定贴图和采样器，当shader加载时如果没有贴图将会报错
            buffer.GetTemporaryRT(dirShadowAtlasId,1,1,32,
                FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        }

        // 如果没有其他光阴影则像上面一样需要声明一个虚拟贴图,直接使用上面生成好的就行
        if (shadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId,dirShadowAtlasId);
        }
        //给是否使用关键字添加开关，即使不使用任何光照贴图也需要进行此操作，因为shadowMask不是实时的
        buffer.BeginSample(bufferName);
        //在buffer中设置shadowMask关键字来启用
        SetKeywords(shadowMaskKeywords,useShadowMask ? 
            //通过查询QualitySettings中的shadowMaskMode来决定应该启用哪个关键字
            QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 
            : -1);
        // 将所有阴影的衰减放在此处统一处理,防止有些地方没有直接光阴影却有其他光阴影
        // 为了防止因为在Cascade外但有其他光阴影的情况,应当把Cascade从globalStrength中拿出
        // 不让他影响全局阴影强度,但我们仍需要shadowDistanceFade的值,所以挪到这里
        buffer.SetGlobalInt(cascadeCountId, shadowedDirectionalLightCount > 0 ? 
            shadowSettings.directional.cascadeCount : 0);
        float f = 1f - shadowSettings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId,new Vector4(
            1f/shadowSettings.maxDistance,1/shadowSettings.distanceFade,
            1f / (1f - f * f)));
        buffer.SetGlobalVector(shadowAtlasSizeId,atlasSizes);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows()
    {
        int atlasSize = (int)shadowSettings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;
        //声明一个方形RT,1属性id，2宽，3高，4缓存深度，越高越高，5过滤模式，6RT类型
        buffer.GetTemporaryRT(dirShadowAtlasId,atlasSize,atlasSize,
            32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        //因为我们的RT只需要从中获得shadowdata，所以不需要管他的初始状态，因为存完信息就删除了
        //故LoadAction选DontCare，StoreAction选Store
        buffer.SetRenderTarget(dirShadowAtlasId,
            RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
        //1clearDepth,2clearColor
        buffer.ClearRenderTarget(true,false,Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId,1f);
        buffer.BeginSample(bufferName);
        //处理buffer
        ExecuteBuffer();
        //分割Buffer给对应的light,用来支持最多四个直接光
        int tiles = shadowedDirectionalLightCount * shadowSettings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i,split,tileSize);
        }
        
        // //渲染级联后，将级联计数和对应的球体发送给GPU
        // buffer.SetGlobalInt(cascadeCountId,shadowSettings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId,cascadeCullingSpheres);
        //由于阴影粉刺(shadow-acne)的大小取决于世界空间纹素(texel)的大小,
        //因此需要添加一个cascadeData来存储纹素大小等信息并发给GPU以提高通讯效率
        buffer.SetGlobalVectorArray(cascadeDataId,cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId,dirShadowMatrices);
        //buffer.SetGlobalFloat(shadowDistanceId,shadowSettings.maxDistance);

        // //使级联圆边缘平滑
        // float f = 1f - shadowSettings.directional.cascadeFade;
        // //将maxDistance和distanceFade的倒数发给GPU,避免在着色器中使用除法
        // buffer.SetGlobalVector(shadowDistanceFadeId,
        //     new Vector4(1f/shadowSettings.maxDistance,1f/shadowSettings.distanceFade,1f/(1f - f*f))
        //     );
        //设置关键字调用不同的滤波器种类
        SetKeywords(
            directionalFilterKeywords,(int)shadowSettings.directional.filter - 1
            );
        //调整关键字数组和索引，设置级联混合关键字
        SetKeywords(
            cascadeBlendKeywords,(int)shadowSettings.directional.cascadeBlend - 1
            );
        // buffer.SetGlobalVector(
        //     shadowAtlasSizeId,new Vector4(atlasSize,1f / atlasSize)
        //     );
        buffer.EndSample(bufferName);
        //处理Buffer
        ExecuteBuffer();
    }
    
    void RenderOtherShadows()
    {
        int atlasSize = (int)shadowSettings.other.atlasSize;
        // zw存其他光源阴影的atlas
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;
        //声明一个方形RT,1属性id，2宽，3高，4缓存深度，越高越高，5过滤模式，6RT类型
        buffer.GetTemporaryRT(otherShadowAtlasId,atlasSize,atlasSize,
            32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        //因为我们的RT只需要从中获得shadowdata，所以不需要管他的初始状态，因为存完信息就删除了
        //故LoadAction选DontCare，StoreAction选Store
        buffer.SetRenderTarget(otherShadowAtlasId,
            RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
        //1clearDepth,2clearColor
        buffer.ClearRenderTarget(true,false,Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId,0f);
        buffer.BeginSample(bufferName);
        //处理buffer
        ExecuteBuffer();
        //分割Buffer给对应的light,用来支持最多四个直接光
        int tiles = shadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowedOtherLightCount;)
        {
            // 如果是点光源则循环计数加六
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            // 反之如果是聚光灯则计数加一
            else
            {
                // RenderDirectionalShadows(i,split,tileSize);
                RenderSpotShadows(i,split,tileSize);
                i += 1;
            }
        }
        
        // //渲染级联后，将级联计数和对应的球体发送给GPU
        // buffer.SetGlobalInt(cascadeCountId,shadowSettings.directional.cascadeCount);
        //buffer.SetGlobalVectorArray(cascadeCullingSpheresId,cascadeCullingSpheres);
        // //由于阴影粉刺(shadow-acne)的大小取决于世界空间纹素(texel)的大小,
        // //因此需要添加一个cascadeData来存储纹素大小等信息并发给GPU以提高通讯效率
        // buffer.SetGlobalVectorArray(cascadeDataId,cascadeData);
        buffer.SetGlobalMatrixArray(otherShadowMatricesId,otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId,otherShadowTiles);
        //buffer.SetGlobalFloat(shadowDistanceId,shadowSettings.maxDistance);

        // //使级联圆边缘平滑
        // float f = 1f - shadowSettings.directional.cascadeFade;
        // //将maxDistance和distanceFade的倒数发给GPU,避免在着色器中使用除法
        // buffer.SetGlobalVector(shadowDistanceFadeId,
        //     new Vector4(1f/shadowSettings.maxDistance,1f/shadowSettings.distanceFade,1f/(1f - f*f))
        //     );
        //设置关键字调用不同的滤波器种类
        SetKeywords(
            otherFilterKeywords,(int)shadowSettings.other.filter - 1
            );
        // //调整关键字数组和索引，设置级联混合关键字
        // SetKeywords(
        //     cascadeBlendKeywords,(int)shadowSettings.directional.cascadeBlend - 1
        //     );
        // buffer.SetGlobalVector(
        //     shadowAtlasSizeId,new Vector4(atlasSize,1f / atlasSize)
        //     );
        buffer.EndSample(bufferName);
        //处理Buffer
        ExecuteBuffer();
    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        //按照索引获取开启阴影的光照
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        int cascadeCount = shadowSettings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = shadowSettings.directional.CascadeRatios;
        //缩小阴影投射器的采集范围，防止采集用不到的地方造成额外的开销
        float cullingFactor = Mathf.Max(0f, 0.8f - shadowSettings.directional.cascadeFade);
        float tileScale = 1f / split;
        
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
                projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex,split,tileSize),tileScale
                );
            buffer.SetViewProjectionMatrices(viewMatrix,projectionMatrix);
            //bias的用法是在渲染时应用全局深度偏差，在渲染前调用缓冲区设置值，渲染后改为0,读取灯光中的固定偏移值            
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
        if (shadowedOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        }
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
    
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
    {
        //最直观的做法是让0表示零深度,1表示最大深度.由于深度缓冲区精度有限,而且为非线性存储
        //所以我们反转缓冲区来更高效利用高效率部分.
        if (SystemInfo.usesReversedZBuffer) {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        // //Clip Space是一个正方型,从-1到1,中心是0.但是纹理坐标和深度都是从0到1
        // //通过XYZ缩放和偏移一半将其烘焙到矩阵中
        // //由于矩阵乘法运算量过大,且会导致大量无意义的零乘法,此处直接调整矩阵
        // float scale = 1f / split;
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
        //通过球面半径获得直径，除以tileSize获得单个贴片长度
        float texelSize = 2f * cullingSphere.w / tileSize;
        //法线bias需要增加偏置来匹配滤波器尺寸，通过将纹素大小乘以一加滤波模式来进行此项操作
        float filterSize = texelSize * ((float)shadowSettings.directional.filter + 1.0f);
        //因为增加了采样区域，需要在平方运算之前将球体半径减去过滤器尺寸
        cullingSphere.w -= filterSize;
        //通过比较球体中心的距离平方与球的半径平方来判断是否在球内
		cullingSphere.w *= cullingSphere.w;
		cascadeCullingSpheres[index] = cullingSphere;
        //将级联球的半径平方的倒数存在X,因为texel贴片是正方型，最坏的情况下要乘以根号二
        cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
    }

    //设置关键字
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

    // 点光源和聚光灯都可以被烘焙进shadowMask中,只需要把模式设置为Mixed.
    // 每个光照都会获得其中的一个通道和直接光一样,但是因为他们的范围有限有可能多个光共用一个通道
    // 因此ShadowMask可以支持很多个光照,但每个纹素最多只有四个
    // 当阴影超过这个数量,会按照不重要程度吧多余的光照强制转换为Baked直到没有冲突
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);
        }
        
        // 控制使用的Mask通道,默认为-1
        float maskChannel = -1f;
        // if (light.shadows != LightShadows.None && light.shadowStrength > 0f)
        // {
        LightBakingOutput lightBaking = light.bakingOutput;
        if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }
        // }
        bool isPoint = light.type == LightType.Point;
        // 当前的shadowOtherLightCount,如果是点光源则数量加6,如果是聚光灯则加一
        int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
        // 检查其他灯光数量是否达到上限以及此处是否能渲染到该光的阴影
        // 如果是则将该灯光的阴影强度返回值改为负值,这样合适的时候烘焙阴影将被应用
        if (newLightCount > maxShadowedOtherLightCount ||
            !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPoint
        };

        Vector4 data = new Vector4(light.shadowStrength, shadowedOtherLightCount,
            isPoint ? 1f : 0f, maskChannel);
        shadowedOtherLightCount = newLightCount;
        return data;
        // return new Vector4(0f, 0f, 0f, -1f);
    }

    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowDrawingSettings = new ShadowDrawingSettings(
            cullingResults,light.visibleLightIndex);
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex,out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix,out ShadowSplitData splitData
            );
        shadowDrawingSettings.splitData = splitData;
        // projectionMatrix.m00 = 1f / (Mathf.Tan(fov * 0.5)*aspect)
        // projectionMatrix.m11 = 1f / Mathf.Tan(fov * 0.5)
        float texelSize = 2f / (tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float)shadowSettings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        // 从SetTileViewpot获得Atlas对应的Tile的Offset
        Vector2 offset = SetTileViewport(index, split, tileSize);
        // 优化项,因为ConverToAtlasMatrix也要用1f/split,故直接在此计算
        // 因为将Clip的-1到1转换到贴图的NDC空间,故用边长1f/份数就是tile的大小
        float tileScale = 1f / split;
        // 设置偏移数据
        SetOtherTileData(index,offset,tileScale,bias);
        otherShadowMatrices[index] = ConvertToAtlasMatrix(
            projectionMatrix * viewMatrix,offset,tileScale);
        buffer.SetViewProjectionMatrices(viewMatrix,projectionMatrix);
        buffer.SetGlobalDepthBias(0f,light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowDrawingSettings);
        buffer.SetGlobalDepthBias(0f,0f);
    }

    void RenderPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowDrawingSettings = new ShadowDrawingSettings(
            cullingResults,light.visibleLightIndex);
        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)shadowSettings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1f / split;
        float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        // 因为PointLight是需要渲染六面的,所以要循环六次
        for (int i = 0; i < 6; i++)
        {
            // 跟聚光灯的区别是23需要CubemapFace的索引和fovBias
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
              light.visibleLightIndex,(CubemapFace)i,fovBias,
              out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
              out ShadowSplitData splitData
            );
            // 由于Unity渲染点光源阴影的方式是颠倒的,他反转了三角形的绘制顺序
            // 正常情况下是从点光源开始的,但是Unity是先从远处开始
            // 这虽然解决了阴影粉刺问题,但是会导致光线泄露
            // 我们虽然不能停止Unity的反转绘制,我们可以通过取反从上面获得的viewMatrix中的一行
            // 我们将反转第二行,这样就会把所有的东西变回正常值
            // 因为第二行第一个一般是0,我们只需要把剩下三个取反
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;
            shadowDrawingSettings.splitData = splitData;
            int tileIndex = index + i;
            // // projectionMatrix.m00 = 1f / (Mathf.Tan(fov * 0.5)*aspect)
            // // projectionMatrix.m11 = 1f / Mathf.Tan(fov * 0.5)
            // float texelSize = 2f / (tileSize * projectionMatrix.m00);
            // float filterSize = texelSize * ((float)shadowSettings.other.filter + 1f);
            // float bias = light.normalBias * filterSize * 1.4142136f;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            // float tileScale = 1f / split;
            SetOtherTileData(tileIndex,offset,tileScale,bias);
            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
              projectionMatrix * viewMatrix,offset,tileScale);
            buffer.SetViewProjectionMatrices(viewMatrix,projectionMatrix);
            buffer.SetGlobalDepthBias(0f,light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowDrawingSettings);
            buffer.SetGlobalDepthBias(0f,0f);  
        }
    }
    
    void SetOtherTileData(int index,Vector2 offset,float scale,float bias)
    {
        float border = atlasSizes.w * 0.5f;
        Vector4 data;
        // 因为聚光灯的tile是紧密贴合视锥体的,所以normalBias和filterSize将会把
        // 阴影采样器推出atlas对应的tile的边缘范围
        // 解决这个问题的最简单的方法是手动把采样边缘保持在atlas对应贴图内
        // 就好像每个atlas对应的贴图都是自己独立的纹理
        // 这样虽然会拉伸边缘附近的阴影,但不会有无效的阴影
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[index] = data;
    }
}