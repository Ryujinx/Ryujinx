#include <metal_stdlib>

using namespace metal;

// ------------------
// Simple Blit Shader
// ------------------

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

vertex CopyVertexOut vertexBlit(unsigned short vid [[vertex_id]]) {
    float2 position = quadVertices[vid];

    CopyVertexOut out;

    out.position = float4(position, 0, 1);
    out.position.y = -out.position.y;
    out.uv = position * 0.5f + 0.5f;

    return out;
}

fragment float4 fragmentBlit(CopyVertexOut in [[stage_in]],
                             texture2d<float, access::sample> texture [[texture(0)]],
                             sampler sampler [[sampler(0)]]) {
    return texture.sample(sampler, in.uv);
}
