#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

#define MIN_REFLECTIVITY 0.04

struct BRDF
{
    float roughness;
    float3 diffuse;
    float3 specular;
    // 粗糙度散射不止会降低高光强度,也会改变他们
    float perceptualRoughness;
    float fresnel;
};

float OneMinusReflectivity(float metallic)
{
    float range = 1.0 - MIN_REFLECTIVITY;
    //
    return range - metallic * range;
}

BRDF GetBRDF(Surface surface,bool applyAlphaToDiffuse = false)
{
    BRDF brdf;
    
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    brdf.perceptualRoughness =
        PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    
    brdf.diffuse = surface.color * oneMinusReflectivity;
    if(applyAlphaToDiffuse)
    {
        //预乘alpha
        brdf.diffuse *= surface.alpha;        
    }
    brdf.specular = lerp(MIN_REFLECTIVITY,surface.color,surface.metallic);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
    // 通过表面光滑都和反射率相加(最多为1)来得出最终颜色
    brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
    return brdf;
}

float SpecularStrength (Surface surface,BRDF brdf,Light light)
{
    float3 h = SafeNormalize(light.direction + surface.viewDirection);
    float nh2 = Square(saturate(dot(surface.normal,h)));
    float lh2 = Square(saturate(dot(light.direction,h)));
    float r2 = Square(brdf.roughness);
    float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    float normalization = brdf.roughness * 4.0 + 2.0;

    return r2 / (d2 * max(0.1,lh2) * normalization);
}

float3 DirectBRDF(Surface surface,BRDF brdf,Light light)
{
    return SpecularStrength(surface,brdf,light) * brdf.specular + brdf.diffuse;
}

// 添加一个IndirectBRDF函数,包含表面,BRDF参数,从全局照明中获得的漫反射和镜面反射
float3 IndirectBRDF(Surface surface,BRDF brdf,float3 diffuse,float3 specular)
{
    // 通过一减点乘法线和视角方向的四次方来得到菲涅尔效果的强度
    float fresnelStrength = surface.fresnelStrength *
        Pow4(1.0 - saturate(dot(surface.normal,surface.viewDirection)));
    // 添加反射项,简单的把GI高光乘以BRDF的高光颜色
    // 根据强度在BRDF镜面反射和菲涅尔颜色之间进行插值
    float3 reflection = specular * lerp(brdf.specular,brdf.fresnel,fresnelStrength);
    // 因为roughness也会对反射产生影响,用反射值除以roughness的平方加一
    // 这样可以让roughness数值较低的适合不怎么影响反射,当数值较高的时候反射值就只有一半
    reflection /= brdf.roughness * brdf.roughness + 1.0;
    
    return (diffuse * brdf.diffuse + reflection) * surface.occlusion;
}

#endif