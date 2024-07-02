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
    constant TexCoords* texCoord;
};

struct Textures
{
    texture2d<float, access::sample> texture;
    sampler sampler;
};

vertex CopyVertexOut vertexMain(uint vid [[vertex_id]],
                                constant ConstantBuffers &constant_buffers [[buffer(20)]]) {
    CopyVertexOut out;

    int low = vid & 1;
    int high = vid >> 1;
    out.uv.x = constant_buffers.texCoord->data[low];
    out.uv.y = constant_buffers.texCoord->data[2 + high];
    out.position.x = (float(low) - 0.5f) * 2.0f;
    out.position.y = (float(high) - 0.5f) * 2.0f;
    out.position.z = 0.0f;
    out.position.w = 1.0f;

    return out;
}

fragment float4 fragmentMain(CopyVertexOut in [[stage_in]],
                             constant Textures &textures [[buffer(22)]]) {
    return textures.texture.sample(textures.sampler, in.uv);
}
