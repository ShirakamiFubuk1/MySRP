Shader "Custom RP/Unlit"
{
    Properties
    {

    }
    SubShader
    {
        Pass
        {
        HLSLPROGRAM

        #include "UnlitPass.hlsl"
        
        #pragma vertex UnlitPassVertex
        #pragma fragment UnlitPassFragment

        ENDHLSL
        }
    }
}
