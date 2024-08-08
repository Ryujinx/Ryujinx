#include <metal_stdlib>

using namespace metal;

struct StrideArguments {
    int4 data;
};

struct InData {
    uint8_t data[1];
};

struct OutData {
    uint8_t data[1];
};

struct ConstantBuffers {
    constant StrideArguments* stride_arguments;
};

struct StorageBuffers {
    device InData* in_data;
    device OutData* out_data;
};

kernel void kernelMain(constant ConstantBuffers &constant_buffers [[buffer(CONSTANT_BUFFERS_INDEX)]],
                       device StorageBuffers &storage_buffers [[buffer(STORAGE_BUFFERS_INDEX)]],
                       uint3 thread_position_in_grid [[thread_position_in_grid]],
                       uint3 threads_per_threadgroup [[threads_per_threadgroup]],
                       uint3 threadgroups_per_grid [[threads_per_grid]])
{
    // Determine what slice of the stride copies this invocation will perform.

    int sourceStride = constant_buffers.stride_arguments->data.x;
    int targetStride = constant_buffers.stride_arguments->data.y;
    int bufferSize = constant_buffers.stride_arguments->data.z;
    int sourceOffset = constant_buffers.stride_arguments->data.w;

    int strideRemainder = targetStride - sourceStride;
    int invocations = int(threads_per_threadgroup.x * threadgroups_per_grid.x);

    int copiesRequired = bufferSize / sourceStride;

    // Find the copies that this invocation should perform.

    // - Copies that all invocations perform.
    int allInvocationCopies = copiesRequired / invocations;

    // - Extra remainder copy that this invocation performs.
    int index = int(thread_position_in_grid.x);
    int extra = (index < (copiesRequired % invocations)) ? 1 : 0;

    int copyCount = allInvocationCopies + extra;

    // Finally, get the starting offset. Make sure to count extra copies.

    int startCopy = allInvocationCopies * index + min(copiesRequired % invocations, index);

    int srcOffset = sourceOffset + startCopy * sourceStride;
    int dstOffset = startCopy * targetStride;

    // Perform the copies for this region
    for (int i = 0; i < copyCount; i++) {
        for (int j = 0; j < sourceStride; j++) {
            storage_buffers.out_data->data[dstOffset++] = storage_buffers.in_data->data[srcOffset++];
        }

        for (int j = 0; j < strideRemainder; j++) {
            storage_buffers.out_data->data[dstOffset++] = uint8_t(0);
        }
    }
}
