#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED

float4 _FXAAConfig;

float GetLuma (float2 uv, float uOffset = 0.0, float vOffset = 0.0)
{
    uv += float2(uOffset, vOffset) * GetSourceTexelSize().xy;
    #if defined(FXAA_ALPHA_CONTAINS_LUMA)
        return GetSource(uv).a;
    #else
        return GetSource(uv).g;
    #endif
}

struct LumaNeighborhood
{
    float m, n, e, s, w, ne, se, sw, nw;
    float highest, lowest, range;
};


LumaNeighborhood GetLumaNeighborhood(float2 uv)
{
    LumaNeighborhood luma;
    luma.m = GetLuma(uv);
    luma.n = GetLuma(uv, 0.0, 1.0);
    luma.e = GetLuma(uv, 1.0, 0.0);
    luma.s = GetLuma(uv, 0.0, -1.0);
    luma.w = GetLuma(uv, -1.0, 0.0);
    luma.ne = GetLuma(uv, 1.0, 1.0);
    luma.se = GetLuma(uv, 1.0, -1.0);
    luma.sw = GetLuma(uv, -1.0, -1.0);
    luma.nw = GetLuma(uv, -1.0, 1.0);
    luma.highest = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.lowest = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.range = luma.highest - luma.lowest;

    return luma;
}

bool CanSkipFXAA (LumaNeighborhood luma)
{
    return luma.range < max(_FXAAConfig.x, _FXAAConfig.y * luma.highest);
}

float GetSubpixelBlendFactor(LumaNeighborhood luma)
{
    float filter = 2.0 * (luma.n + luma.e + luma.s + luma.w);
    filter += luma.ne + luma.nw + luma.se + luma.sw;
    filter *= 1.0 / 12.0;
    filter = abs(filter - luma.m);
    filter = saturate(filter / luma.range);
    filter = smoothstep(0, 1, filter);
    
    return filter * filter;
}

bool IsHorizontalEdge(LumaNeighborhood luma)
{
    float horizontal =
        2.0 * abs(luma.n + luma.s - 2.0 * luma.m) +
            abs(luma.ne + luma.se - 2.0 * luma.e) +
            abs(luma.nw + luma.ne - 2.0 * luma.s);
    float vertical =
        2.0 * abs(luma.e + luma.w - 2.0 * luma.m) +
            abs(luma.ne + luma.nw - 2.0 * luma.n) +
            abs(luma.se + luma.sw - 2.0 * luma.s);

    return horizontal >= vertical;
}

struct FXAAEdge
{
    bool isHorizontal;
    float pixelStep;
};

FXAAEdge GetFXAAEdge(LumaNeighborhood luma)
{
    FXAAEdge edge;
    edge.isHorizontal = IsHorizontalEdge(luma);
    float lumaP, lumaN;
    if(edge.isHorizontal)
    {
        edge.pixelStep = GetSourceTexelSize().y;
        lumaP = luma.n;
        lumaN = luma.s;
    }
    else
    {
        edge.pixelStep = GetSourceTexelSize().x;
        lumaP = luma.e;
        lumaN = luma.w;
    }
    float gradientP = abs(lumaP - luma.m);
    float gradientN = abs(lumaN - luma.m);

    if(gradientP < gradientN)
    {
        edge.pixelStep = -edge.pixelStep;
    }

    return edge;
}

float4 FXAAPassFragment(Varyings input) : SV_TARGET
{
    LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);
    if(CanSkipFXAA(luma))
    {
        return  GetSource(input.screenUV);
    }

    FXAAEdge edge = GetFXAAEdge(luma);

    float blendFactor = GetSubpixelBlendFactor(luma);
    float2 blendUV = input.screenUV;
    if(edge.isHorizontal)
    {
        blendUV.y += blendFactor * edge.pixelStep;
    }
    else
    {
        blendUV.x += blendFactor * edge.pixelStep;
    }
    
    return GetSource(blendUV);
}

#endif