using Ryujinx.Common.Memory;
using Silk.NET.Vulkan;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Ryujinx.Graphics.Vulkan
{
    struct PipelineUid : IRefEquatable<PipelineUid>
    {
        public ulong Id0;
        public ulong Id1;

        public ulong Id2;
        public ulong Id3;

        private readonly uint VertexAttributeDescriptionsCount => (byte)((Id0 >> 38) & 0xFF);
        private readonly uint VertexBindingDescriptionsCount => (byte)((Id0 >> 46) & 0xFF);
        private readonly uint ColorBlendAttachmentStateCount => (byte)((Id1 >> 8) & 0xFF);
        private readonly bool HasDepthStencil => ((Id1 >> 63) & 0x1) != 0UL;

        public Array32<VertexInputAttributeDescription> VertexAttributeDescriptions;
        public Array33<VertexInputBindingDescription> VertexBindingDescriptions;
        public Array8<PipelineColorBlendAttachmentState> ColorBlendAttachmentState;
        public Array9<Format> AttachmentFormats;
        public uint AttachmentIntegerFormatMask;
        public bool LogicOpsAllowed;

        public readonly override bool Equals(object obj)
        {
            return obj is PipelineUid other && Equals(other);
        }

        public bool Equals(ref PipelineUid other)
        {
            if (!Unsafe.As<ulong, Vector128<byte>>(ref Id2).Equals(Unsafe.As<ulong, Vector128<byte>>(ref other.Id2)))
            {
                return false;
            }

            if (!SequenceEqual<VertexInputAttributeDescription>(VertexAttributeDescriptions.AsSpan(), other.VertexAttributeDescriptions.AsSpan(), VertexAttributeDescriptionsCount))
            {
                return false;
            }

            if (!SequenceEqual<VertexInputBindingDescription>(VertexBindingDescriptions.AsSpan(), other.VertexBindingDescriptions.AsSpan(), VertexBindingDescriptionsCount))
            {
                return false;
            }

            if (!SequenceEqual<PipelineColorBlendAttachmentState>(ColorBlendAttachmentState.AsSpan(), other.ColorBlendAttachmentState.AsSpan(), ColorBlendAttachmentStateCount))
            {
                return false;
            }

            if (!SequenceEqual<Format>(AttachmentFormats.AsSpan(), other.AttachmentFormats.AsSpan(), ColorBlendAttachmentStateCount + (HasDepthStencil ? 1u : 0u)))
            {
                return false;
            }

            return true;
        }

        private static bool SequenceEqual<T>(ReadOnlySpan<T> x, ReadOnlySpan<T> y, uint count) where T : unmanaged
        {
            return MemoryMarshal.Cast<T, byte>(x[..(int)count]).SequenceEqual(MemoryMarshal.Cast<T, byte>(y[..(int)count]));
        }

        public override int GetHashCode()
        {
            ulong hash64 = Id0 * 23 ^
                           Id1 * 23 ^
                           Id2 * 23 ^
                           Id3 * 23;

            for (int i = 0; i < (int)VertexAttributeDescriptionsCount; i++)
            {
                hash64 ^= VertexAttributeDescriptions[i].Binding * 23;
                hash64 ^= (uint)VertexAttributeDescriptions[i].Format * 23;
                hash64 ^= VertexAttributeDescriptions[i].Location * 23;
                hash64 ^= VertexAttributeDescriptions[i].Offset * 23;
            }

            for (int i = 0; i < (int)VertexBindingDescriptionsCount; i++)
            {
                hash64 ^= VertexBindingDescriptions[i].Binding * 23;
                hash64 ^= (uint)VertexBindingDescriptions[i].InputRate * 23;
                hash64 ^= VertexBindingDescriptions[i].Stride * 23;
            }

            for (int i = 0; i < (int)ColorBlendAttachmentStateCount; i++)
            {
                hash64 ^= ColorBlendAttachmentState[i].BlendEnable * 23;
                hash64 ^= (uint)ColorBlendAttachmentState[i].SrcColorBlendFactor * 23;
                hash64 ^= (uint)ColorBlendAttachmentState[i].DstColorBlendFactor * 23;
                hash64 ^= (uint)ColorBlendAttachmentState[i].ColorBlendOp * 23;
                hash64 ^= (uint)ColorBlendAttachmentState[i].SrcAlphaBlendFactor * 23;
                hash64 ^= (uint)ColorBlendAttachmentState[i].DstAlphaBlendFactor * 23;
                hash64 ^= (uint)ColorBlendAttachmentState[i].AlphaBlendOp * 23;
                hash64 ^= (uint)ColorBlendAttachmentState[i].ColorWriteMask * 23;
            }

            for (int i = 0; i < (int)ColorBlendAttachmentStateCount; i++)
            {
                hash64 ^= (uint)AttachmentFormats[i] * 23;
            }

            return (int)hash64 ^ ((int)(hash64 >> 32) * 17);
        }
    }
}
