using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class Lighting
{
    private const string bufferName = "Lighting";

    private CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    private static int
        //用于索引追踪这些属性
        // dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
        // dirLightDirectionalId = Shader.PropertyToID("_DirectionalLightDirection");
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData"),
        otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
        otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
        otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

    private static Vector4[]
        //由于着色器对struct支持不好，所以尽量少用struct
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount],
        otherLightColors = new Vector4[maxOtherLightCount],
        otherLightPositions = new Vector4[maxOtherLightCount],
        otherLightDirections = new Vector4[maxOtherLightCount],
        otherLightSpotAngles = new Vector4[maxOtherLightCount],
        otherLightShadowData = new Vector4[maxOtherLightCount];

    private CullingResults cullingResults;

    // 其他光源与平行光一样,只能支持有限数量.场景通常可以包含许多其他光源,因他们的有效范围有限.
    // 通常对于给定帧,只有所有可见光的一部分子集可见.我们可以支持的最大数量是用于单帧,而不是整个场景.
    // 如果我们最终得到的可见光超过最大值,一些光将被省略.Unity可以根据重要性对可见光进行排序
    // 因此只要可见光不变,省略的光源都是一致的.但是如果他们确实发生了变化,无论是相机移动还是什么
    // 这可能会倒是明显的曝光.因此我们不想使用太低的最大值.
    private const int 
        maxDirLightCount = 4,
        maxOtherLightCount = 64;

    private Shadows shadows = new Shadows();

    private static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

    public void Setup(ScriptableRenderContext context,CullingResults cullingResults,
        ShadowSettings shadowSettings,bool useLightsPerObject)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        shadows.Setup(context,cullingResults,shadowSettings);
        //支持多光源
        //SetupDirectionalLight();
        SetupLights(useLightsPerObject);
        //渲染阴影
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    // 单光照写法
    // void SetupDirectionalLight()
    // {
    //     //通过RenderSettings.sun来获得在Window/Rendering/Lighting中的默认光照
    //     Light light = RenderSettings.sun;
    //     //通过对buffer进行操作来将光照数据送给GPU
    //     //light.color是配置中的光照强度，真正的光照还受光照强度倍数影响，所以要乘以强度系数
    //     buffer.SetGlobalVector(dirLightColorId,light.color.linear * light.intensity);
    //     buffer.SetGlobalVector(dirLightDirectionalId, -light.transform.forward);
    // }
    
    void SetupDirectionalLight(int index,int visibleIndex,ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        //GetColumn(2)是获得M矩阵的第三行，即旋转，取反表示光照方向
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        //初始化可接受阴影的光
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light,visibleIndex);
    }

    // Unity只是简单的给每个物体创建了所有使用中的光照列表,并按照重要程度进行简单排序.
    // 这个列表并不会管这个物体是否能被光照到而且也包含了直接光.我们需要整理他让他只包含可见的直接光
    void SetupLights(bool useLightsPerObject)
    {
        // 在遍历可见光之前我们需要从cullingResults中取回lightIndexMap
        // 这通过调用GetLightIndexMap(Allocator.temp)完成,返回一个临时的NativeArray<int>
        // 其中包含了light indices用于匹配可见的light indices加上所有其他场景中在使用的光源
        // 加入判断当不适用per-object数据的时候返回NativeArray<int>不会分配任何东西的默认值
        NativeArray<int> indexMap = useLightsPerObject ?
            cullingResults.GetLightIndexMap(Allocator.Temp) : default;
        //通过visible获得所有可见光，通过Unity.Collections.NativeArray和visibleLight泛型。
        //NativeArray是内存的索引数组，可以沟通脚本和引擎。
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0 , otherLightCount = 0;
        int i;
        for( i = 0;i < visibleLights.Length; i++)
        {
            // 因为只需要其他光的light indices,其他的需要跳过
            // 所以把顺序默认设定为-1,遇到满足条件的光源时改成其他数值
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            // //检测是否是平行光
            // if (visibleLight.lightType == LightType.Directional)
            // {
            //     //先执行dirLightCount再++,ref是优化项                
            //     SetupDirectionalLight(dirLightCount++,ref visibleLight);
            //     if (dirLightCount >= maxDirLightCount)
            //     {
            //         break;
            //     }                
            // }
            switch (visibleLight.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                    {
                        SetupDirectionalLight(dirLightCount++,i,ref visibleLight);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        // 将其他光的顺序设定为对应顺序
                        newIndex = otherLightCount;
                        SetupPointLight(otherLightCount++,i,ref visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupSpotLight(otherLightCount++,i,ref visibleLight);
                    }
                    break;
            }
            
            // 同时限制不可见光的indices            
            if (useLightsPerObject)
            {
                indexMap[i] = newIndex;
            }
        }
        
        if (useLightsPerObject)
        {
            // 当调整完indexMap之后需要将其传回Unity中
            for (; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }
            cullingResults.SetLightIndexMap(indexMap);
            // 传回数据后不再需要indexMap,将其弃用
            indexMap.Dispose();
            // 在GPU端启用对应的关键字
            Shader.EnableKeyword(lightsPerObjectKeyword);
        }
        else
        {
            // 关闭对应的关键字
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }
        
        buffer.SetGlobalInt(dirLightCountId,dirLightCount);
        if (dirLightCount > 0)
        {
            //使用索引ID和对应的数组设置Buffer
            buffer.SetGlobalVectorArray(dirLightColorsId,dirLightColors);
            //使用索引获取对应光照的方向
            buffer.SetGlobalVectorArray(dirLightDirectionsId,dirLightDirections);
            //使用索引逐光照存储阴影信息
            buffer.SetGlobalVectorArray(dirLightShadowDataId,dirLightShadowData);
        }
        buffer.SetGlobalInt(otherLightCountId,otherLightCount);
        if (otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorsId,otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId,otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsId,otherLightDirections);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId,otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataId,otherLightShadowData);
        }
    }

    public void Cleanup()
    {
        shadows.Cleanup();
    }

    void SetupPointLight(int index,int visibleIndex, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        // 转换矩阵的第四行代表位置
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 
            1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        // 为了点光源不受角度衰减计算的影响,需要将其值设为0和1
        otherLightSpotAngles[index] = new Vector4(0f, 1f);
        Light light = visibleLight.light;
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
    }
    
    void SetupSpotLight(int index,int visibleIndex, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        // 可以将光的范围存储在位置的第四分量中.
        // 此处存储1/r^2 以优化,同时避免除以零
        position.w = 
            1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        // localToWorldMatrix.GetColumn(2)代表z方向
        // 在Unity中从本地到世界的三维旋转顺序是z→x→y,故在Matrix中中123列分别是y,x,z轴旋转
        // 因为获得z轴坐标是需要计算矩阵MA,其中A为[0,0,1,0]^-1,得到的数据即为z轴,和GetColumn(2)的数据一样
        // 此处取反表示反射光线
        otherLightDirections[index] = 
            -visibleLight.localToWorldMatrix.GetColumn(2);

        Light light = visibleLight.light;
        // 然而inner angle需要通过light.innerSpotAngle来获得
        // 因为可配置内角是Unity新版的功能,VisibleLight中没有他,因为他会更改该字段的长度并需要重构Unity内部代码
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        // outer angle可以从visibleLight.spotAngle中获得
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[index] = 
            new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
    }
}