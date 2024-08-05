using SharpMetal.Metal;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Metal
{
    public struct RenderEncoderResources
    {
        public List<Resource> Resources = new();
        public List<BufferResource> VertexBuffers = new();
        public List<BufferResource> FragmentBuffers = new();

        public RenderEncoderResources() { }

        public void Clear()
        {
            Resources.Clear();
            VertexBuffers.Clear();
            FragmentBuffers.Clear();
        }
    }

    public struct ComputeEncoderResources
    {
        public List<Resource> Resources = new();
        public List<BufferResource> Buffers = new();

        public ComputeEncoderResources() { }

        public void Clear()
        {
            Resources.Clear();
            Buffers.Clear();
        }
    }

    public struct BufferResource
    {
        public MTLBuffer Buffer;
        public ulong Offset;
        public ulong Binding;

        public BufferResource(MTLBuffer buffer, ulong offset, ulong binding)
        {
            Buffer = buffer;
            Offset = offset;
            Binding = binding;
        }
    }

    public struct Resource
    {
        public MTLResource MtlResource;
        public MTLResourceUsage ResourceUsage;
        public MTLRenderStages Stages;

        public Resource(MTLResource resource, MTLResourceUsage resourceUsage, MTLRenderStages stages)
        {
            MtlResource = resource;
            ResourceUsage = resourceUsage;
            Stages = stages;
        }
    }
}
