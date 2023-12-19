using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
// 因为使用UnityEngine.Experimental.GlobalIllumination会导致报错
// 所以定义一下LightType的类型
using LightType = UnityEngine.LightType;

// 因为Unity默认会使用 lagacy RP的衰减方式
// 因此我们需要使Unity使用我们的光照代理里的衰减方式
public partial class CustomRenderPipeline
{
    
    partial void InitializeForEditor();
    
#if UNITY_EDITOR

    // 让Unity在引擎模式下使用我们的设置
    partial void InitializeForEditor()
    {
        Lightmapping.SetDelegate(lightsDelegate);
    }

    // 当我们的管线被处理过之后需要清除并重置代理数据
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Lightmapping.ResetDelegate();
    }

    // 需要覆盖lightmapper初始化光照的方式,使用的代理函数名为Lightmapping.RequestLightsDelegate
    // 使用lambda表达式来定义这个代理方法,防止到处定义
    private static Lightmapping.RequestLightsDelegate lightsDelegate =
        (Light[] lights, NativeArray<LightDataGI> output) =>
        {
            // 使用LightDataGI()来承载光照数据
            var lightData = new LightDataGI();
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                // 因为需要对每种光照类型都需要单独的代码,此处使用switch
                switch (light.type)
                {
                    case LightType.Directional:
                        // 构建一个代理的light结构
                        var directionalLight = new DirectionalLight();
                        // 使用light和结构体的引用作为参数调用LightmapperUtils,从Unity内部获得光照
                        LightmapperUtils.Extract(light, ref directionalLight);
                        // 通过引用光照结构初始化光照数据,根据给定光照类型判断
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        // // 在Unity 2022中还可以设置聚光灯的内角和衰减
                        // spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        // spotLight.angularFalloff =
                        //     AngularFalloffType.AnalyticAndInnerAngle;
                        lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        var rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectangleLight);
                        // 由于区域光不支持实时光照,故强制使用烘焙光照
                        rectangleLight.mode = LightMode.Baked;
                        lightData.Init(ref rectangleLight);
                        break;
                    // 默认设置,Unity不会烘焙这个光照
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }

                // 给所有光照的衰减类型都设置为FalloffType.InverseSquared
                lightData.falloff = FalloffType.InverseSquared;
                output[i] = lightData;
            }
        };

#endif
}