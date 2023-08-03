#include <metal_stdlib>

using namespace metal;

constant float2 quadVertices[] = {
    float2(-1, -1),
    float2(-1,  1),
    float2( 1,  1),
    float2(-1, -1),
    float2( 1,  1),
    float2( 1, -1)
};

struct CopyVertexOut {
    float4 position [[position]];
    float2 uv;
};

vertex CopyVertexOut vertexMain(unsigned short vid [[vertex_id]]) {
    float2 position = quadVertices[vid];

    CopyVertexOut out;

    out.position = float4(position, 0, 1);
    out.position.y = -out.position.y;
    out.uv = position * 0.5f + 0.5f;

    return out;
}

fragment float4 fragmentMain(CopyVertexOut in [[stage_in]],
                              texture2d<float> tex) {
    constexpr sampler sam(min_filter::nearest, mag_filter::nearest, mip_filter::none);

    return tex.sample(sam, in.uv).xyzw;
}
