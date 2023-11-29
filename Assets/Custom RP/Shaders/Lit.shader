﻿Shader "Custom RP/Lit"
{
    Properties
    {
        _BaseMap("Texture",2D) = "white"{}
        _BaseColor("Color",Color) = (0.5,0.5,0.5,1.0)
        _Metallic("Metallic",Range(0,1)) = 0
        _Smoothness("Smoothness",Range(0,1)) = 0.5
        [NoScaleOffset] _EmissionMap("Emission",2D) = "white"{}
        [HDR] _EmissionColor("Emission",Color) = (0.0,0.0,0.0,0.0)        
        _CutOff("Alpha Cut Off",Range(0.0,1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping",Float) = 0
        [Toggle(_PREMUTIPLY_ALPHA)] _PremultiplyAlpha("Premuliply Alpha",Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend",Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend",Float) = 0
        [Enum(Off,0,On,1)] _ZWrite("Z Write",Float) = 1
        [KeywordEnum(On,Clip,Dither,Off)] _Shadows("Shadows",Float) = 0
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows",Float) = 1
        
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

            #include "LitPass.hlsl"

            //由于WebGL1.0和OpenGL2.0不支持linear lighting
            //设置target 3.5来防止生成WebGL1.0和OpenGL2.0的变体
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            //#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            #pragma shader_feature _RECEIVE_SHADOWS

            //为三个关键字的传递添加指令，并为2x2配备无关键字添加加号和下划线
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ LIGHTMAP_ON
            //使用这条语句会产生两个变体,一个有instacing一个没有
            #pragma multi_compile_instancing
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

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
