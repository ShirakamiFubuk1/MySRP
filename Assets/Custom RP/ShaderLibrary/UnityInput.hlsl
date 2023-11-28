#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;

    //光照贴图坐标通常由Unity按照网格自动生成,或者由导入的网格数据生成一部分。
    //它定义了一个纹理的展开，使网格扁平，映射到纹理坐标.
    //展开在光照贴图中按照对象进行缩放和定位，因此每个实例都有自己的空间。
    //光照贴图UV变换作为缓冲区的一部分传递给GPU，需要将他们添加到缓冲区。
    //即使这两条已经被启用，也要需要添加进来，防止打断SRP批处理兼容
    //光照贴图也适用于GPU实例化，所有数据会在需要时实例化
    float4 unity_LightmapST;
    float4 unity_DynamicLightmapST;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_perv_MatrixM;
float4x4 unity_perv_MatirxIM;
float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;

#endif