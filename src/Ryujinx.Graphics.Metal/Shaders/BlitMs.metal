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
    texture2d_ms<FORMAT, access::read> texture;
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
                             constant Textures &textures [[buffer(TEXTURES_INDEX)]],
                             uint sample_id [[sample_id]]) {
    uint2 tex_size = uint2(textures.texture.get_width(), textures.texture.get_height());
    uint2 tex_coord = uint2(in.uv * float2(tex_size));
    return textures.texture.read(tex_coord, sample_id);
}
