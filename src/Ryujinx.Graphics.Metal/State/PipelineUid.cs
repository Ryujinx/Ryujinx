using Ryujinx.Common.Memory;
using SharpMetal.Metal;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    struct VertexInputAttributeUid
    {
        public ulong Id0;

        public ulong Offset
        {
            readonly get => (uint)((Id0 >> 0) & 0xFFFFFFFF);
            set => Id0 = (Id0 & 0xFFFFFFFF00000000) | ((ulong)value << 0);
        }

        public MTLVertexFormat Format
        {
            readonly get => (MTLVertexFormat)((Id0 >> 32) & 0xFFFF);
            set => Id0 = (Id0 & 0xFFFF0000FFFFFFFF) | ((ulong)value << 32);
        }

        public ulong BufferIndex
        {
            readonly get => ((Id0 >> 48) & 0xFFFF);
            set => Id0 = (Id0 & 0x0000FFFFFFFFFFFF) | ((ulong)value << 48);
        }
    }

    struct VertexInputLayoutUid
    {
        public ulong Id0;

        public uint Stride
        {
            readonly get => (uint)((Id0 >> 0) & 0xFFFFFFFF);
            set => Id0 = (Id0 & 0xFFFFFFFF00000000) | ((ulong)value << 0);
        }

        public uint StepRate
        {
            readonly get => (uint)((Id0 >> 32) & 0x1FFFFFFF);
            set => Id0 = (Id0 & 0xE0000000FFFFFFFF) | ((ulong)value << 32);
        }

        public MTLVertexStepFunction StepFunction
        {
            readonly get => (MTLVertexStepFunction)((Id0 >> 61) & 0x7);
            set => Id0 = (Id0 & 0x1FFFFFFFFFFFFFFF) | ((ulong)value << 61);
        }
    }

    struct ColorBlendStateUid
    {
        public ulong Id0;

        public MTLPixelFormat PixelFormat
        {
            readonly get => (MTLPixelFormat)((Id0 >> 0) & 0xFFFF);
            set => Id0 = (Id0 & 0xFFFFFFFFFFFF0000) | ((ulong)value << 0);
        }

        public MTLBlendFactor SourceRGBBlendFactor
        {
            readonly get => (MTLBlendFactor)((Id0 >> 16) & 0xFF);
            set => Id0 = (Id0 & 0xFFFFFFFFFF00FFFF) | ((ulong)value << 16);
        }

        public MTLBlendFactor DestinationRGBBlendFactor
        {
            readonly get => (MTLBlendFactor)((Id0 >> 24) & 0xFF);
            set => Id0 = (Id0 & 0xFFFFFFFF00FFFFFF) | ((ulong)value << 24);
        }

        public MTLBlendOperation RgbBlendOperation
        {
            readonly get => (MTLBlendOperation)((Id0 >> 32) & 0xF);
            set => Id0 = (Id0 & 0xFFFFFFF0FFFFFFFF) | ((ulong)value << 32);
        }

        public MTLBlendOperation AlphaBlendOperation
        {
            readonly get => (MTLBlendOperation)((Id0 >> 36) & 0xF);
            set => Id0 = (Id0 & 0xFFFFFF0FFFFFFFFF) | ((ulong)value << 36);
        }

        public MTLBlendFactor SourceAlphaBlendFactor
        {
            readonly get => (MTLBlendFactor)((Id0 >> 40) & 0xFF);
            set => Id0 = (Id0 & 0xFFFF00FFFFFFFFFF) | ((ulong)value << 40);
        }

        public MTLBlendFactor DestinationAlphaBlendFactor
        {
            readonly get => (MTLBlendFactor)((Id0 >> 48) & 0xFF);
            set => Id0 = (Id0 & 0xFF00FFFFFFFFFFFF) | ((ulong)value << 48);
        }

        public MTLColorWriteMask WriteMask
        {
            readonly get => (MTLColorWriteMask)((Id0 >> 56) & 0xF);
            set => Id0 = (Id0 & 0xF0FFFFFFFFFFFFFF) | ((ulong)value << 56);
        }

        public bool Enable
        {
            readonly get => ((Id0 >> 63) & 0x1) != 0UL;
            set => Id0 = (Id0 & 0x7FFFFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 63);
        }

        public void Swap(ColorBlendStateUid uid)
        {
            var format = PixelFormat;

            this = uid;
            PixelFormat = format;
        }
    }

    [SupportedOSPlatform("macos")]
    struct PipelineUid : IRefEquatable<PipelineUid>
    {
        public ulong Id0;
        public ulong Id1;

        private readonly uint VertexAttributeDescriptionsCount => (byte)((Id0 >> 8) & 0xFF);
        private readonly uint VertexBindingDescriptionsCount => (byte)((Id0 >> 16) & 0xFF);
        private readonly uint ColorBlendAttachmentStateCount => (byte)((Id0 >> 24) & 0xFF);

        public Array32<VertexInputAttributeUid> VertexAttributes;
        public Array33<VertexInputLayoutUid> VertexBindings;
        public Array8<ColorBlendStateUid> ColorBlendState;
        public uint AttachmentIntegerFormatMask;
        public bool LogicOpsAllowed;

        public void ResetColorState()
        {
            ColorBlendState = new();

            for (int i = 0; i < ColorBlendState.Length; i++)
            {
                ColorBlendState[i].WriteMask = MTLColorWriteMask.All;
            }
        }

        public readonly override bool Equals(object obj)
        {
            return obj is PipelineUid other && Equals(other);
        }

        public bool Equals(ref PipelineUid other)
        {
            if (!Unsafe.As<ulong, Vector128<byte>>(ref Id0).Equals(Unsafe.As<ulong, Vector128<byte>>(ref other.Id0)))
            {
                return false;
            }

            if (!SequenceEqual<VertexInputAttributeUid>(VertexAttributes.AsSpan(), other.VertexAttributes.AsSpan(), VertexAttributeDescriptionsCount))
            {
                return false;
            }

            if (!SequenceEqual<VertexInputLayoutUid>(VertexBindings.AsSpan(), other.VertexBindings.AsSpan(), VertexBindingDescriptionsCount))
            {
                return false;
            }

            if (!SequenceEqual<ColorBlendStateUid>(ColorBlendState.AsSpan(), other.ColorBlendState.AsSpan(), ColorBlendAttachmentStateCount))
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
                           Id1 * 23;

            for (int i = 0; i < (int)VertexAttributeDescriptionsCount; i++)
            {
                hash64 ^= VertexAttributes[i].Id0 * 23;
            }

            for (int i = 0; i < (int)VertexBindingDescriptionsCount; i++)
            {
                hash64 ^= VertexBindings[i].Id0 * 23;
            }

            for (int i = 0; i < (int)ColorBlendAttachmentStateCount; i++)
            {
                hash64 ^= ColorBlendState[i].Id0 * 23;
            }

            return (int)hash64 ^ ((int)(hash64 >> 32) * 17);
        }
    }
}
