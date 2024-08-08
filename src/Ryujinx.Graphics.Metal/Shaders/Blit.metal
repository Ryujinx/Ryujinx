#include <metal_stdlib>

using namespace metal;

struct CopyVertexOut {
    float4 position [[position]];
    float2 uv;
};

struct TexCoords {
    float data[4];
};

struct ConstantBuffers {
    constant TexCoords* tex_coord;
};

struct Textures
{
    texture2d<FORMAT, access::sample> texture;
    sampler sampler;
};

vertex CopyVertexOut vertexMain(uint vid [[vertex_id]],
                                constant ConstantBuffers &constant_buffers [[buffer(CONSTANT_BUFFERS_INDEX)]]) {
    CopyVertexOut out;

    int low = vid & 1;
    int high = vid >> 1;
    out.uv.x = constant_buffers.tex_coord->data[low];
    out.uv.y = constant_buffers.tex_coord->data[2 + high];
    out.position.x = (float(low) - 0.5f) * 2.0f;
    out.position.y = (float(high) - 0.5f) * 2.0f;
    out.position.z = 0.0f;
    out.position.w = 1.0f;

    return out;
}

fragment FORMAT4 fragmentMain(CopyVertexOut in [[stage_in]],
                             constant Textures &textures [[buffer(TEXTURES_INDEX)]]) {
    return textures.texture.sample(textures.sampler, in.uv);
}
