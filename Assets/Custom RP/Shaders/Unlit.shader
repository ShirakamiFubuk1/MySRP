Shader "Custom RP/Unlit"
{
    Properties
    {
        _BaseColor("Color",Color) = (1.0,1.0,1.0,1.0)
    }
    SubShader
    {
        Pass
        {
        HLSLPROGRAM

        #include "UnlitPass.hlsl"

        //使用这条语句会产生两个变体,一个有instacing一个没有
        #pragma multi_compile_instancing
        #pragma vertex UnlitPassVertex
        #pragma fragment UnlitPassFragment

        ENDHLSL
        }
    }
}
