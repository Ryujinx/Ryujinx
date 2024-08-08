float swizzleAdd(float x, float y, int mask, uint thread_index_in_simdgroup)
{
    float4 xLut = float4(1.0, -1.0, 1.0, 0.0);
    float4 yLut = float4(1.0, 1.0, -1.0, 1.0);
    int lutIdx = (mask >> (int(thread_index_in_simdgroup & 3u) * 2)) & 3;
    return x * xLut[lutIdx] + y * yLut[lutIdx];
}
