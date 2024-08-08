#include <metal_stdlib>

using namespace metal;

struct CopyVertexOut {
    float4 position [[position]];
    float2 uv;
};

struct Textures
{
    texture2d<float, access::sample> texture;
    sampler sampler;
};

struct FragmentOut {
    float depth [[depth(any)]];
};

fragment FragmentOut fragmentMain(CopyVertexOut in [[stage_in]],
                             constant Textures &textures [[buffer(TEXTURES_INDEX)]]) {
    FragmentOut out;

    out.depth = textures.texture.sample(textures.sampler, in.uv).r;

    return out;
}
