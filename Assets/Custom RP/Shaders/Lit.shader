Shader "Custom RP/Lit"
{
    Properties
    {
        _BaseMap("Texture",2D) = "white"{}
        _BaseColor("Color",Color) = (0.5,0.5,0.5,1.0)
        _CutOff("Alpha Cut Off",Range(0.0,1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping",Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend",Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend",Float) = 0
        [Enum(Off,0,On,1)] _ZWrite("Z Write",Float) = 1
    }
    SubShader
    {
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
            //使用这条语句会产生两个变体,一个有instacing一个没有
            #pragma multi_compile_instancing
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            ENDHLSL
        }
    }
}
