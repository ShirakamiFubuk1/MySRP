using UnityEngine;

// 因为我们不能直接把相机设置添加到相机组件中,所以创建一个支持的CustomRenderPipelineCamera组件
// 这个类可以被附加给向是相机的组件且只能添加一个
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{
    // 添加CameraSettings设置
    [SerializeField] private CameraSettings settings = default;

    // 因为设置是个类,这些属性需要确保存在,所以没有设置的时候需要创建
    // 这可能出现在部件还没有被编辑器序列化时,或者在运行时被添加到摄像机的情况下
    // ?? 运算符的意思是 settings == null ? setings = new CameraSettings() : settings;
    public CameraSettings Settings => settings ?? (settings = new CameraSettings());
}