using UnityEngine;

// 需要将相机的自定义LayerMask改成下拉列表,需要一个定制GUI
// 但我们又不想给整个CameraSettings类定制,这里只给LayerMask定制
// 创建一个RenderingLayerMaskFieldAttribute类继承PropertyAttribute
// 此操作只是标记了属性并不需要做其他的事情.
public class RenderingLayerMaskFieldAttribute : PropertyAttribute {}