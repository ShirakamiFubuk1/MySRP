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

    [NonSerialized] private Material material;

    [Serializable]
    public struct BloomSettings
    {
        // 因为Bloom是一个和分辨率有关的效果,因此改变分辨率也会改变bloom的实际效果
        // 这个可以通过降低迭代数来看到.减小渲染比例将增强效果,增大渲染比例将减轻效果
        public bool ignoreRenderScale;
        
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
    
    // 添加URP和HDRP的颜色调整后处理工具的功能
    // 第一步是添加一个配置结构,因为我们需要多个属性
    [Serializable] public struct ColorAdjustmentsSettings
    {
        // URP和HDRP的颜色调整功能是相同的, 这里按照相同的顺序添加配置
        // 首先是曝光,然后是对比度,滤色,色相转换,和饱和度
        public float postExposure;

        [Range(-100f, 100f)] public float contrast;

        [ColorUsage(false, true)] public Color colorFilter;

        [Range(-180f, 180f)] public float hueShift;

        [Range(-100f, 100f)] public float saturation;
    }

    [SerializeField] private ColorAdjustmentsSettings colorAdjustments = new ColorAdjustmentsSettings
    {
        // 其他值默认可以为0,但滤色默认值为白色防止更改图像
        colorFilter = Color.white
    };

    public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;
   
    [System.Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            None,
            ACES,
            Neutral,
            Reinhard
        }

        public Mode mode;
    }

    [SerializeField] private ToneMappingSettings toneMapping = default;

    public ToneMappingSettings ToneMapping => toneMapping; 
    
    // 添加白平衡配置
    [Serializable] public struct WhiteBalanceSettings
    {
        [Range(-100f, 100f)] public float temperature, tint;
    }
    
    [SerializeField] private WhiteBalanceSettings whiteBalance = default;
    
    public WhiteBalanceSettings WhiteBalance => whiteBalance;
    
    // 拆分色调工具用于分别对阴影和高光颜色进行着色.
    // 一个典型的案例是将阴影推向冷蓝色,高光推向暖橙色
    [Serializable] public struct SplitToningSettings
    {
        // 配置值包括两种不带Alpha的LDR颜色,用于阴影和高光,默认值为灰色.
        // 还包括一个平衡为-100-100 的滑块,默认值为0
        [ColorUsage(false)] public Color shadows, hightlights;

        [Range(-100f, 100f)] public float balance;
    }

    [SerializeField] private SplitToningSettings splitToning = new SplitToningSettings
    {
        shadows = Color.gray,
        hightlights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;
    
    [Serializable] public struct ChannelMixerSettings
    {
        public Vector3 red, green, blue;
    }

    [SerializeField] private ChannelMixerSettings channelMixer = new ChannelMixerSettings
    {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    // 创建通道混合器的配置文件,允许输入新的RGB值来进行调整,比如交换RG,从G中减去B等
    // 混合器本质是一个3x3转换矩阵,默认为单位矩阵.我们可以使用三个值,分别用于红绿蓝三色配置.
    // Unity的组件为每种颜色显示一个单独的选项卡,每个通道有一个-100-100的滑块
    // 但我们只需要显示矢量,每行代表一个颜色,XYZ用于代表RGB输入
    public ChannelMixerSettings ChannelMixer => channelMixer;
    
    [Serializable] public struct ShaodwsMidtonesHighlightsSettings
    {
        [ColorUsage(false, true)] public Color shadows, midtones, highlights;

        [Range(0f, 2f)] public float shadowStart, shadowEnd, highlightsStart, highLightsEnd;
    }

    // 添加对3Cut分别叠加颜色的支持
    // 它的工作原理类似于拆分色调,只是它还允许调整中间色并解耦合阴影区域和高光区域
    // 阴影强度从头到尾减少,而高光强度从开始到结束增加。这里使用0-2的范围来适当兼容HDR
    // 默认情况下颜色为白色, 我们将使用和Unity相同的区域默认值,阴影为0-0.3,高光为0.55-1
    [SerializeField] private ShaodwsMidtonesHighlightsSettings
        shadowsMidtonesHighlights = new ShaodwsMidtonesHighlightsSettings
        {
            shadows = Color.white,
            midtones = Color.white,
            highlights = Color.white,
            shadowEnd = 0.3f,
            highlightsStart = 0.55f,
            highLightsEnd = 1f
        };

    public ShaodwsMidtonesHighlightsSettings 
        ShaodwsMidtonesHighlights => shadowsMidtonesHighlights;
}