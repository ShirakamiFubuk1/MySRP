Shader "Hidden/Custom RP/Camera Renderer"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE

        #include "../ShaderLibrary/Common.hlsl"
        #include "CameraRendererPass.hlsl"
        
        ENDHLSL

        // 当不使用后处理时,会因为没办法正确渲染中间帧而失败
        // 我们需要定义一个复制方法给这种情况
        // 不幸的是CopyTexture只能用来复制给renderTexture,而不是给最终frameBuffer
        // 把后处理中的复制Pass复制过来来实现上述内容.
        Pass
        {
            Name "Copy"
            
            // 为了兼容不使用后处理时需要把相机上单独设置混合模式设置回去的问题
            // 单独用相机上带的混合模式对pass进行赋值
            Blend [_CameraSrcBlend] [_CameraDstBlend]
            
            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyPassFragment
            
            ENDHLSL
        }

        Pass
        {
            Name "Copy Depth"

            
            ColorMask 0
            ZWrite On
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment CopyPassFragment

            ENDHLSL
        }
    }
}