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
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    private static Vector4[]
        //由于着色器对struct支持不好，所以尽量少用struct
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];

    private CullingResults cullingResults;

    private const int maxDirLightCount = 4;

    private Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context,CullingResults cullingResults,ShadowSettings shadowSettings)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        shadows.Setup(context,cullingResults,shadowSettings);
        //支持多光源
        //SetupDirectionalLight();
        SetupLights();
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
    
    void SetupDirectionalLight(int index,ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        //GetColumn(2)是获得M矩阵的第三行，即旋转，取反表示光照方向
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        //初始化可接受阴影的光
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light,index);
    }

    void SetupLights()
    {
        //通过visible获得所有可见光，通过Unity.Collections.NativeArray和visibleLight泛型。
        //NativeArray是内存的索引数组，可以沟通脚本和引擎。
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0;
        
        for(int i = 0;i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            //检测是否是平行光
            if (visibleLight.lightType == LightType.Directional)
            {
                //先执行dirLightCount再++,ref是优化项                
                SetupDirectionalLight(dirLightCount++,ref visibleLight);
                if (dirLightCount >= maxDirLightCount)
                {
                    break;
                }                
            }
        }
        
        buffer.SetGlobalInt(dirLightCountId,dirLightCount);
        //使用索引ID和对应的数组设置Buffer
        buffer.SetGlobalVectorArray(dirLightColorsId,dirLightColors);
        //使用索引获取对应光照的方向
        buffer.SetGlobalVectorArray(dirLightDirectionsId,dirLightDirections);
        //使用索引逐光照存储阴影信息
        buffer.SetGlobalVectorArray(dirLightShadowDataId,dirLightShadowData);
    }

    public void Cleanup()
    {
        shadows.Cleanup();
    }
}