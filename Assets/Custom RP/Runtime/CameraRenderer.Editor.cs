using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Profiling;
using UnityEngine.Profiling;

partial class CameraRenderer
{
    partial void DrawGizmosBeforeFX();

    partial void DrawGizmosAfterFX();
    
    partial void DrawUnsupportedShaders();

    partial void PrepareForSceneWindow();

    partial void PrepareBuffer();
    
#if UNITY_EDITOR
    
    private string SampleName { get; set; }
    
    private static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
    
    private static Material errorMaterial;

    partial void DrawGizmosBeforeFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            if (useIntermediateBuffer)
            {
                // 现在我们在不适用FX时也能获取深度,可以结合后期的深度
                // 再次使我们的Gizmos获得深度感知能力
                Draw(depthAttachmentId, 
                    BuiltinRenderTextureType.CameraTarget, true);
                ExecuteBuffer();
            }
            context.DrawGizmos(camera,GizmoSubset.PreImageEffects);
            // context.DrawGizmos(camera,GizmoSubset.PostImageEffects);
        }
    }

    partial void DrawGizmosAfterFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            if (postFXStack.IsActive)
            {
                Draw(depthAttachmentId, 
                    BuiltinRenderTextureType.CameraTarget, true);
                ExecuteBuffer();
            }
            context.DrawGizmos(camera,GizmoSubset.PostImageEffects);
        }
    }
    
    partial void DrawUnsupportedShaders()
    {
        if (errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(
                legacyShaderTagIds[0],new SortingSettings(camera)
            )
        {
            overrideMaterial = errorMaterial
        };
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i,legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(
                cullingResults,ref drawingSettings,ref filteringSettings
            );
    }

    partial void PrepareForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            // 因为不想让RenderScale影响Scene,需要在这里设置false来关闭scaledRendering
            useScaleRendering = false;
        }
    }

    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        buffer.name = SampleName = camera.name;
        Profiler.EndSample();
    }
    
#else

    const string SampleName = bufferName;
    
#endif
}
