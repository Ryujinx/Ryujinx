#include <metal_stdlib>

using namespace metal;

struct VertexOut {
    float4 position [[position]];
};

struct FragmentOut {
    float depth [[depth(any)]];
};

struct ClearDepth {
    float data;
};

struct ConstantBuffers {
    constant ClearDepth* clear_depth;
};

vertex VertexOut vertexMain(ushort vid [[vertex_id]]) {
    int low = vid & 1;
    int high = vid >> 1;

    VertexOut out;

    out.position.x = (float(low) - 0.5f) * 2.0f;
    out.position.y = (float(high) - 0.5f) * 2.0f;
    out.position.z = 0.0f;
    out.position.w = 1.0f;

    return out;
}

fragment FragmentOut fragmentMain(VertexOut in [[stage_in]],
                                  constant ConstantBuffers &constant_buffers [[buffer(CONSTANT_BUFFERS_INDEX)]]) {
    FragmentOut out;

    out.depth = constant_buffers.clear_depth->data;

    return out;
}
