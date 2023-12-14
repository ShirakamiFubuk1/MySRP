﻿Shader "Custom RP/Lit"
{
    Properties
    {
        _BaseMap("Texture",2D) = "white"{}
        _BaseColor("Color",Color) = (0.5,0.5,0.5,1.0)
        [Toggle(_MASK_MAP)] _MaskMapToggle("Mask Map",Float) = 0
        [NoScaleOffset] _MaskMap("Mask (MODS)",2D) = "white"{}
        _Metallic("Metallic",Range(0,1)) = 0
        _Occlusion("Occlusion",Range(0,1)) = 1
        _Smoothness("Smoothness",Range(0,1)) = 0.5
        _Fresnel("Fresnel",Range(0,1)) = 1
        [Toggle(_NORMAL_MAP)] _NormalMapToggle("Normal Map",Float) = 0
        [NoScaleOffset] _NormalMap("Normals",2D) = "bump"{}
        _NormalScale("Normal Scale",Range(0,1)) = 1
        [NoScaleOffset] _EmissionMap("Emission",2D) = "white"{}
        [HDR] _EmissionColor("Emission",Color) = (0.0,0.0,0.0,0.0)
        [Toggle(_DETAIL_MAP)] _DetailMapToggle("Detail Maps",Float) = 0
        _DetailMap("Details",2D) = "linearGrey"{}
        [NoScaleOffset] _DetailNormalMap("Detail Normals",2D) = "bump"{}
        _DetailAlbedo("Detail Albedo",Range(0,1)) = 1
        _DetailSmoothness("Detail Smoothness",Range(0,1)) = 1
        _DetailNormalScale("Detail Normal Scale",Range(0.01,1)) = 1        
        _CutOff("Alpha Cut Off",Range(0.0,1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping",Float) = 0
        [Toggle(_PREMUTIPLY_ALPHA)] _PremultiplyAlpha("Premuliply Alpha",Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend",Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend",Float) = 0
        [Enum(Off,0,On,1)] _ZWrite("Z Write",Float) = 1
        [KeywordEnum(On,Clip,Dither,Off)] _Shadows("Shadows",Float) = 0
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows",Float) = 1
        
        //由于Unity的光照贴图编辑器控制透明度的方法是写死的，通过材质的队列来控制透明,裁剪,还是不透明
        //通过_MainTex和_Color的透明度相乘获得真实透明度,通过_CutOff来确定Alpha裁剪
        //补全缺少的_MainTex和_Color,为shader提供对应的属性,同时使用隐藏标签以便不显示在检查器中
        [HideInInspector] _MainTex("Texture for Lightmap",2D) = "white"{}
        [HideInInspector] _Color("Color for Lightmap",Color) = (0.5,0.5,0.5,1.0)
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "LitInput.hlsl"
        ENDHLSL
        
        Pass
        {
            Tags
            {
                "LightMode" = "CustomLit"
            }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            
            HLSLPROGRAM

            //由于WebGL1.0和OpenGL2.0不支持linear lighting
            //设置target 3.5来防止生成WebGL1.0和OpenGL2.0的变体
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            //#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            #pragma shader_feature _RECEIVE_SHADOWS

            #pragma shader_feature _NORMAL_MAP
            #pragma shader_feature _MASK_MAP
            #pragma shader_feature _DETAIL_MAP

            #pragma multi_compile _ _LIGHTS_PER_OBJECT
            //为三个关键字的传递添加指令，并为2x2配备无关键字添加加号和下划线
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
            //添加一个多编译开关
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            //启用shader关键字，将相应的多编译指令添加到着色器的传递中
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            //使用LIGHTMAP_ON的编译宏来创建对应的分支
            #pragma multi_compile _ LIGHTMAP_ON
            //使用这条语句会产生两个变体,一个有instacing一个没有
            #pragma multi_compile_instancing
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "LitPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
            
            ColorMask 0
            
            HLSLPROGRAM
            
            #pragma target 3.5
            //#pragma shader_feature _CLIPPING
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            
            #include "ShadowCasterPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "Meta"
            }
            
            Cull Off
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "MetaPass.hlsl"

            ENDHLSL
        }
    }
    CustomEditor "CustomShaderGUI"
}
