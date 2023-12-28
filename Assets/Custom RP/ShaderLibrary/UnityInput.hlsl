#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;

    float4 unity_RenderingLayer;

    // Y里面包含了光照数量
    real4 unity_LightData;
    // 是一个两格长的数组包含两个vector4,所以每个物体最多支持八个逐物体光照
    real4 unity_LightIndices[2];

    //由于动态对象没有阴影蒙版数据.他们使用光照探针而不是使用光照贴图.
    //但是Unity还会将阴影遮罩数据烘焙到光照贴图探针中，将其称作遮挡探针
    //可以通过unity_ProbesOcclusion从Unity缓冲区中访问此数据
    float4 unity_ProbesOcclusion;

    //解码环境立方体的反射数据
    float4 unity_SpecCube0_HDR;

    //光照贴图坐标通常由Unity按照网格自动生成,或者由导入的网格数据生成一部分。
    //它定义了一个纹理的展开，使网格扁平，映射到纹理坐标.
    //展开在光照贴图中按照对象进行缩放和定位，因此每个实例都有自己的空间。
    //光照贴图UV变换作为缓冲区的一部分传递给GPU，需要将他们添加到缓冲区。
    //即使这两条已经被启用，也要需要添加进来，防止打断SRP批处理兼容
    //光照贴图也适用于GPU实例化，所有数据会在需要时实例化
    float4 unity_LightmapST;
    float4 unity_DynamicLightmapST;

    //球谐函数,需要七个float4向量分别代表多项式中的红绿蓝
    //分别被命名为unity_SH*,*代表A,B或C
    //前两个参数有三种版本,用r,g和b后缀代表
    float4 unity_SHAr;
    float4 unity_SHAg;
    float4 unity_SHAb;
    float4 unity_SHBr;
    float4 unity_SHBg;
    float4 unity_SHBb;
    float4 unity_SHC;

    //LPPV相关的输入
    float4 unity_ProbeVolumeParams;
    float4 unity_ProbeVolumeSizeInv;
    float4 unity_ProbeVolumeMin;
    float4x4 unity_ProbeVolumeWorldToObject;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_perv_MatrixM;
float4x4 unity_perv_MatirxIM;
float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;

// 因为API的V轴方向不一样,导致有时候屏幕会上下颠倒
// 大部分情况下Unity会解决这个问题,但在使用RenderTexture时会失效
// Unity决定是否手动翻转需要通过_ProjectionParams.x来决定,将其放入UnityInput
float4 _ProjectionParams;

#endif