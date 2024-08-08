using SharpMetal.Metal;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Ryujinx.Graphics.Metal.State
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StencilUid
    {
        public uint ReadMask;
        public uint WriteMask;
        public ushort Operations;

        public MTLStencilOperation StencilFailureOperation
        {
            readonly get => (MTLStencilOperation)((Operations >> 0) & 0xF);
            set => Operations = (ushort)((Operations & 0xFFF0) | ((int)value << 0));
        }

        public MTLStencilOperation DepthFailureOperation
        {
            readonly get => (MTLStencilOperation)((Operations >> 4) & 0xF);
            set => Operations = (ushort)((Operations & 0xFF0F) | ((int)value << 4));
        }

        public MTLStencilOperation DepthStencilPassOperation
        {
            readonly get => (MTLStencilOperation)((Operations >> 8) & 0xF);
            set => Operations = (ushort)((Operations & 0xF0FF) | ((int)value << 8));
        }

        public MTLCompareFunction StencilCompareFunction
        {
            readonly get => (MTLCompareFunction)((Operations >> 12) & 0xF);
            set => Operations = (ushort)((Operations & 0x0FFF) | ((int)value << 12));
        }
    }


    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct DepthStencilUid : IEquatable<DepthStencilUid>
    {
        [FieldOffset(0)]
        public StencilUid FrontFace;

        [FieldOffset(10)]
        public ushort DepthState;

        [FieldOffset(12)]
        public StencilUid BackFace;

        [FieldOffset(22)]
        private readonly ushort _padding;

        // Quick access aliases
#pragma warning disable IDE0044 // Add readonly modifier
        [FieldOffset(0)]
        private ulong _id0;
        [FieldOffset(8)]
        private ulong _id1;
        [FieldOffset(0)]
        private Vector128<byte> _id01;
        [FieldOffset(16)]
        private ulong _id2;
#pragma warning restore IDE0044 // Add readonly modifier

        public MTLCompareFunction DepthCompareFunction
        {
            readonly get => (MTLCompareFunction)((DepthState >> 0) & 0xF);
            set => DepthState = (ushort)((DepthState & 0xFFF0) | ((int)value << 0));
        }

        public bool StencilTestEnabled
        {
            readonly get => ((DepthState >> 4) & 0x1) != 0;
            set => DepthState = (ushort)((DepthState & 0xFFEF) | ((value ? 1 : 0) << 4));
        }

        public bool DepthWriteEnabled
        {
            readonly get => ((DepthState >> 15) & 0x1) != 0;
            set => DepthState = (ushort)((DepthState & 0x7FFF) | ((value ? 1 : 0) << 15));
        }

        public readonly override bool Equals(object obj)
        {
            return obj is DepthStencilUid other && EqualsRef(ref other);
        }

        public readonly bool EqualsRef(ref DepthStencilUid other)
        {
            return _id01.Equals(other._id01) && _id2 == other._id2;
        }

        public readonly bool Equals(DepthStencilUid other)
        {
            return EqualsRef(ref other);
        }

        public readonly override int GetHashCode()
        {
            ulong hash64 = _id0 * 23 ^
                           _id1 * 23 ^
                           _id2 * 23;

            return (int)hash64 ^ ((int)(hash64 >> 32) * 17);
        }
    }
}
