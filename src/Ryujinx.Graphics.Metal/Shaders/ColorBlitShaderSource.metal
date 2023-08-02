#include <metal_stdlib>

using namespace metal;

struct TexCoordIn {
    float4 tex_coord_in_data;
};

vertex float4 vertexMain(uint vertexID [[vertex_id]],
                          constant TexCoordIn& tex_coord_in [[buffer(1)]]) {
    int low = vertexID & 1;
    int high = vertexID >> 1;
    float2 tex_coord;
    tex_coord.x = tex_coord_in.tex_coord_in_data[low];
    tex_coord.y = tex_coord_in.tex_coord_in_data[2 + high];

    float4 position;
    position.x = (float(low) - 0.5) * 2.0;
    position.y = (float(high) - 0.5) * 2.0;
    position.z = 0.0;
    position.w = 1.0;

    return position;
}

fragment float4 fragmentMain(float2 tex_coord [[stage_in]],
                              texture2d<float> tex [[texture(0)]]) {
    float4 color = tex.sample(metal::address::clamp_to_edge, tex_coord);
    return color;
}
