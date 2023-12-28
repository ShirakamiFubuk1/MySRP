using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]

// 聚光灯自带可配置的outer angle,但在新版本URP之前没有内角的单独配置.因此灯光的窗口不会显示内角的选项.
// SRP可以进一步修改灯光,因此可以需要覆盖默认的灯光Inspector.这需要创建一个编辑器脚本并为其提供属性来完成
// 提供的属性第一个参数必须是类型,第二个参数必须是需要覆盖的Inspector的RP的资产类型.

public class CustomLightEditor : LightEditor
{
    public override void OnInspectorGUI()
    {
        // 为了提高效率需要重写默认的Inspector
        base.OnInspectorGUI();
        DrawRenderingLayerMask();
        // 检查是否选择了聚光灯以及是否有多个不同数值
        if (!settings.lightType.hasMultipleDifferentValues &&
            (LightType)settings.lightType.enumValueIndex == LightType.Spot)
        {
            // 添加控制内外角的滑块.
            settings.DrawInnerAndOuterSpotAngle();
            // // 应用更改的属性.
            // settings.ApplyModifiedProperties();
        }
        
        settings.ApplyModifiedProperties();

        var light = target as Light;
        if (light.cullingMask != -1)
        {
            EditorGUILayout.HelpBox(
                    light.type == LightType.Directional ?
                    "Culling Mask only affects shadows" :
                    "Culling Mask only affects shadow unless Use Lights Per Objects is on",
                    MessageType.Warning
                );
        }
    }

    private static GUIContent renderingLayerMaskLabel = 
        new GUIContent("Rendering Layer Mask", 
            "Functional version of above property");

    void DrawRenderingLayerMask()
    {
        SerializedProperty property = settings.renderingLayerMask;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int mask = property.intValue;
        if (mask == int.MaxValue)
        {
            mask = -1;
        }
        mask = EditorGUILayout.MaskField(
                renderingLayerMaskLabel, mask,
                GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
            );
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = mask == -1 ? Int32.MaxValue : mask;
        }

        EditorGUI.showMixedValue = false;
    }
}