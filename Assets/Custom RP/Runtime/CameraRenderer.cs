using UnityEngine;
using UnityEngine.Rendering;

public class CameraRenderer
{
    private ScriptableRenderContext context;

    private Camera camera;
    
    public void Render(ScriptableRenderContext context,Camera camera)
    {
        this.context = context;
        this.camera = camera;

        Setup();
        DrawVisibleGeometry();
        Submit();
    }

    void Setup()
    {
        context.SetupCameraProperties(camera);
    }

    void Submit()
    {
        context.Submit();
    }
    
    void DrawVisibleGeometry()
    {
        context.DrawSkybox(camera);
    }
}
