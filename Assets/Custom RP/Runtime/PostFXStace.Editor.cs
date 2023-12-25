using UnityEditor;
using UnityEngine;

partial class PostFXStack
{
    // 通过构建一个什么都不做的函数来实现除了SceneView和game中都不显示后处理效果
    partial void ApplySceneViewState();
    
#if UNITY_EDITOR

    // 如果开启后处理但是现在的sceneView关闭了PostProcessing
    partial void ApplySceneViewState() {
        if (
            camera.cameraType == CameraType.SceneView &&
            !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects
        ) {
            settings = null;
        }
    }
    
#endif
}