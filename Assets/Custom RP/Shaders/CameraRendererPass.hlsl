#ifndef CUSTOM_CAMERA_RENDERER_PASS_INCLUDED
#define CUSTOM_CAMERA_RENDERER_PASS_INCLUDED

TEXTURE2D(_SourceTexture);

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex(uint vertexID : SV_VertexID)
{
    Varyings output;
    // 需要注意这个三角形就三个顶点
    // 顶点ID小于等于1可以把2号顶点放在x=3.0,ID等于1可以把1放在y=3.0,ID为零的在原点
    // 覆盖x:-1到1,y:-1到1
    output.positionCS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0 , 1.0
    );
    // U使用0,0,2,V使用0,2,0,覆盖U:0-1,V:0-1
    output.screenUV = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0
    );
    // 如果该值小于零说明V轴向下,需要反转
    if(_ProjectionParams.x < 0.0)
    {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }
    return output;
}

float4 CopyPassFragment(Varyings input) : SV_TARGET
{
    return SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_linear_clamp, input.screenUV, 0);
}

#endif