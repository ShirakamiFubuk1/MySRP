Shader "Hidden/Custom RP/Post FX Stack"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "PostFXStackPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "Copy"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment CopyPassFragment
            
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Horizontal"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomHorizontalPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Bloom Vertical"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomVerticalPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Bloom Add"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomAddPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Bloom Scatter"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomScatterPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Bloom Scatter Final"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomScatterFinalPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Bloom Prefilter"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomPrefilterPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Bloom Prefilter Fireflies"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomPrefilterFirefliesPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Tone Mapping None"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ColorGradingNonePassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Tone Mapping ACES"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ColorGradingACESPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Tone Mapping Neutral"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ColorGradingNeutralPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Tone Mapping Reinhard"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ColorGradingReinhardPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "Apply Color Grading"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ApplyColorGradingPassFragment

            ENDHLSL
        }

        // 清晰的HDR边缘和色彩校正伪影都是因为在色彩校正和色调映射之前插入HDR颜色造成的.
        // 解决方案是调整渲染比例之后再执行这两个操作,然后再进行一次复制,重新缩放LDR颜色.
        Pass
        {
            Name "Final Rescale"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment FinalPassFragmentRescale

            ENDHLSL
        }

        Pass
        {
            Name "FXAA"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment FXAAPassFragment
                #pragma multi_compile _ FXAA_QUALITY_MEDIUM FXAA_QUALITY_LOW
                #include "FXAAPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "Apply Color Grading With Luma"
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ApplyColorGradingWithLumaPassFragment

            ENDHLSL
        }

        Pass
        {
            Name "FXAA With Luma"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM

                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment FXAAPassFragment
                #pragma multi_compile _ FXAA_QUALITY_MEDIUM FXAA_QUALITY_LOW
                #define FXAA_ALPHA_CONTAINS_LUMA
                #include "FXAAPass.hlsl"

            ENDHLSL
        }
    }
}