using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    private const string bufferName = "Lighting";

    private CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    private static int
        //用于索引追踪这些属性
        dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
        dirLightDirectionalId = Shader.PropertyToID("_DirectionalLightDirection");

    public void Setup(ScriptableRenderContext context)
    {
        buffer.BeginSample(bufferName);
        SetupDirectionalLight();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupDirectionalLight()
    {
        //通过RenderSettings.sun来获得在Window/Rendering/Lighting中的默认光照
        Light light = RenderSettings.sun;
        //通过对buffer进行操作来将光照数据送给GPU
        //light.color是配置中的光照强度，真正的光照还受光照强度倍数影响，所以要乘以强度系数
        buffer.SetGlobalVector(dirLightColorId,light.color.linear * light.intensity);
        buffer.SetGlobalVector(dirLightDirectionalId, -light.transform.forward);
    }
}