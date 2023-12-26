using System;
using UnityEngine;

// 大多数情况下,渲染的图像并不会直接显示.图像经过后处理之后将会获得各种效果(简称FX).常见的FX包括Bloom,
// Color Gradient, DepthOfField, MotionBlur, ToneMapping等.这些FX以堆栈的形式调用,一个叠在一个上面

// 一个项目可能需要多个Post-FX堆栈配置,因此需要创建一个资产类型来存储堆栈的设置
[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    // 通过此设置链接PostFXshader
    [SerializeField] private Shader shader = default;

    [System.NonSerialized] private Material material;

    [System.Serializable]
    public struct BloomSettings
    {
        [Range(0f, 16f)] public int maxIterations;

        [Min(1f)] public int downscaleLimit;
        
        public bool bicubicUpsampling;

        [Min(0f)] public float threshold;

        [Range(0f, 1f)] public float thresholdKnee;

        [Min(0f)] public float intensity;

        public bool fadeFireflies;
        
        public enum Mode { Additive, Scattering}

        public Mode mode;

        // 由于0和1的散射值会消除除了pyramid0之外的所有采样,因此是没有意义的
        // 所以我们需要把滑块范围限制到0.05 - 0.95
        // 这样会使默认值0无效,所以需要在下面显式初始化, 默认使用0.7,与URP和HDRP一样
        [Range(0.05f, 0.95f)] public float scatter;
    }

    [SerializeField] private BloomSettings bloom = new BloomSettings
    {
        scatter = 0.7f
    };

    public BloomSettings Bloom => bloom;
    
    // 因为我们需要一个Material所以在此创建,并设置隐藏且不保存,因为不需要
    public Material Material
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }

            return material;
        }
    }

    [System.Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            None = -1,
            ACES,
            Neutral,
            Reinhard
        }

        public Mode mode;
    }

    [SerializeField] private ToneMappingSettings toneMapping = default;

    public ToneMappingSettings ToneMapping => toneMapping;
}