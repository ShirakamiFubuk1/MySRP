using UnityEngine;
using UnityEngine.Rendering;

public class CameraRenderer
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
    private static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    
    public void Render(ScriptableRenderContext context,Camera camera)
    {
        this.context = context;
        this.camera = camera;

        //调用Setup之前调用Cull,如果失败则终止
        if (!Cull())
        {
            return;
        }

        Setup();
        DrawVisibleGeometry();
        Submit();
    }

    void Setup()
    {
        //用命令缓冲区名称将清除命令圈在一个范围内，省的把别的相机内容清除掉
        context.SetupCameraProperties(camera);
        //使用命令缓冲区诸如给Profiler
        buffer.BeginSample(bufferName);
        //清除图像缓存,前两个true控制是否应该清除深度和颜色数据，第三个是用于清楚的颜色。
        buffer.ClearRenderTarget(true,true,Color.clear);
        ExecuteBuffer();
    }

    void Submit()
    {
        //结束采样
        buffer.EndSample(bufferName);
        //执行命令缓冲区
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    
    void DrawVisibleGeometry()
    {
        //用于确定用正交还是透视
        var sortingSetting = new SortingSettings
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSetting = new DrawingSettings(
                unlitShaderTagId,sortingSetting
            );
        //指出哪些Render队列是被允许的
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        
        context.DrawRenderers(
                //告诉Render哪些东西是可以看到的，此外还要提供绘制设置和筛选设置
                cullingResults,ref drawingSetting,ref filteringSettings
            );
        
        context.DrawSkybox(camera);

        sortingSetting.criteria = SortingCriteria.CommonTransparent;
        drawingSetting.sortingSettings = sortingSetting;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        
        context.DrawRenderers(
                cullingResults,ref drawingSetting,ref filteringSettings
            );
    }

    bool Cull()
    {
        //out的作用
        //当struct参数被定义为输出参数时当作一个对象引用,指向参数所在的内存堆栈上的位置
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            //ref用作优化项,以防止传递ScriptableCullingParameters结构的副本，因为其相当大
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
}
