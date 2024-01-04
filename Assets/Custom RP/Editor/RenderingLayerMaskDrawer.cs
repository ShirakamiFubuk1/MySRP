using UnityEditor;
using UnityEngine;
using UnityEngine;
using UnityEngine.Rendering;

// 创建一个继承PropertyDrawer的类
[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingLayerMaskDrawer : PropertyDrawer
{
    public static void Draw(
        Rect position, SerializedProperty property, GUIContent label
    )
    {
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int mask = property.intValue;
        // 当数值为-1时对uint使用特殊处理
        bool isUint = property.type == "uint";
        if (isUint && mask == int.MaxValue)
        {
            mask = -1;
        }

        // 通过传入属性来获得Editor.GUI,而不是EditorGUILayout.MaskField
        mask = EditorGUI.MaskField(
                position, label, mask,
                GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
            );
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = isUint && mask == -1 ? int.MaxValue : mask;
        }

        EditorGUI.showMixedValue = false;
    }

    // 为了让Draw更简便使用,添加一个没有Rect参数的变体
    // 使用EditorGUILayout.GetControlRect来作为默认值调用原来的Draw
    public static void Draw(SerializedProperty property, GUIContent label)
    {
        Draw(EditorGUILayout.GetControlRect(), property, label);
    }
    
    // 重构OnGUI方法, 直接在里面调用Draw
    public override void OnGUI(
        Rect position, SerializedProperty property, GUIContent label
    )
    {
        Draw(position, property, label);
    }
}