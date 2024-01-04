Shader "Custom RP/Unlit"
{
    Properties
    {
        _BaseMap("Texture",2D) = "white"{}
        [HDR] _BaseColor("Color",Color) = (1.0,1.0,1.0,1.0)
        _CutOff("Alpha Cut Off",Range(0.0,1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping",Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend",Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend",Float) = 0
        [Enum(Off,0,On,1)] _ZWrite("Z Write",Float) = 1
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows",Float) = 1
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "UnlitInput.hlsl"
        ENDHLSL
        
        Pass
        {
//            Tags
//            {
//                "RenderType" = "Transparent"
//                "Queue" = "Transparent"
//            }
            // 要想使透明度和后期FX一起使用,可以更改Final Pass使用alphaBlend而不是默认OneZero
            // 不启用Bloom后处理的话是正常的,但因为Bloom没使用透明度所以效果会有问题
            // 我们使用的分层方法只有在我们的着色器生成与相机图层混合配合使用合理alpha值才会生效.
            // 因为我们之前并不关心写入的alpha值,因为并未使用它执行任何操作
            // 但是现在如果两个alpha为0.5的对象最终渲染为同一个纹素时,该纹素最终的alpha应该是0.25.
            // 我们需要的情况应该是任一alpha值为1时,结果始终为1,当第二个alpha为零时,应保留原始alpha
            // 这些情况可以用One OneMinusSrcAlpha来混和,
            // 我们可以将alpha通道的着色器混合模式和前面的颜色分开配置,方法是在其后面逗号跟Alpha的模式.
            // 只要使用适当的alpha值就会起作用,这意味着写入深度的对象也要是中生成为1的alpha.
            // 对于不透明材质这很简单,但如果他们始终使用也包含不同alpha的底图则会出错.
            // 对于剪辑材质也可能出错,因为他们依赖于alpha阈值来丢弃片段.
            // 如果片段被裁剪则运行正常,但如果没有被裁减则alpha应该变为1
            // 能确保着色器alpha行为正确的最简便方法是直接在面板上添加一个配置
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            
            HLSLPROGRAM

            #include "UnlitPass.hlsl"

            #pragma shader_feature _CLIPPING
            //使用这条语句会产生两个变体,一个有instacing一个没有
            #pragma multi_compile_instancing
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment

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
            #pragma shader_feature _CLIPPING
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
