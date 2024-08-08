using Ryujinx.Common;
using Ryujinx.Common.Memory;
using Ryujinx.Graphics.Device;
using Ryujinx.Graphics.Gpu.Engine.Threed;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.Graphics.Texture;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Ryujinx.Graphics.Gpu.Engine.Dma
{
    /// <summary>
    /// Represents a DMA copy engine class.
    /// </summary>
    class DmaClass : IDeviceState
    {
        private readonly GpuContext _context;
        private readonly GpuChannel _channel;
        private readonly ThreedClass _3dEngine;
        private readonly DeviceState<DmaClassState> _state;

        /// <summary>
        /// Copy flags passed on DMA launch.
        /// </summary>
        [Flags]
        private enum CopyFlags
        {
            SrcLinear = 1 << 7,
            DstLinear = 1 << 8,
            MultiLineEnable = 1 << 9,
            RemapEnable = 1 << 10,
        }

        /// <summary>
        /// Texture parameters for copy.
        /// </summary>
        private readonly struct TextureParams
        {
            /// <summary>
            /// Copy region X coordinate.
            /// </summary>
            public readonly int RegionX;

            /// <summary>
            /// Copy region Y coordinate.
            /// </summary>
            public readonly int RegionY;

            /// <summary>
            /// Offset from the base pointer of the data in memory.
            /// </summary>
            public readonly int BaseOffset;

            /// <summary>
            /// Bytes per pixel.
            /// </summary>
            public readonly int Bpp;

            /// <summary>
            /// Whether the texture is linear. If false, the texture is block linear.
            /// </summary>
            public readonly bool Linear;

            /// <summary>
            /// Pixel offset from XYZ coordinates calculator.
            /// </summary>
            public readonly OffsetCalculator Calculator;

            /// <summary>
            /// Creates texture parameters.
            /// </summary>
            /// <param name="regionX">Copy region X coordinate</param>
            /// <param name="regionY">Copy region Y coordinate</param>
            /// <param name="baseOffset">Offset from the base pointer of the data in memory</param>
            /// <param name="bpp">Bytes per pixel</param>
            /// <param name="linear">Whether the texture is linear. If false, the texture is block linear</param>
            /// <param name="calculator">Pixel offset from XYZ coordinates calculator</param>
            public TextureParams(int regionX, int regionY, int baseOffset, int bpp, bool linear, OffsetCalculator calculator)
            {
                RegionX = regionX;
                RegionY = regionY;
                BaseOffset = baseOffset;
                Bpp = bpp;
                Linear = linear;
                Calculator = calculator;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 3, Pack = 1)]
        private struct UInt24
        {
            public byte Byte0;
            public byte Byte1;
            public byte Byte2;
        }

        /// <summary>
        /// Creates a new instance of the DMA copy engine class.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="channel">GPU channel</param>
        /// <param name="threedEngine">3D engine</param>
        public DmaClass(GpuContext context, GpuChannel channel, ThreedClass threedEngine)
        {
            _context = context;
            _channel = channel;
            _3dEngine = threedEngine;
            _state = new DeviceState<DmaClassState>(new Dictionary<string, RwCallback>
            {
                { nameof(DmaClassState.LaunchDma), new RwCallback(LaunchDma, null) },
            });
        }

        /// <summary>
        /// Reads data from the class registers.
        /// </summary>
        /// <param name="offset">Register byte offset</param>
        /// <returns>Data at the specified offset</returns>
        public int Read(int offset) => _state.Read(offset);

        /// <summary>
        /// Writes data to the class registers.
        /// </summary>
        /// <param name="offset">Register byte offset</param>
        /// <param name="data">Data to be written</param>
        public void Write(int offset, int data) => _state.Write(offset, data);

        /// <summary>
        /// Determine if a buffer-to-texture region covers the entirety of a texture.
        /// </summary>
        /// <param name="tex">Texture to compare</param>
        /// <param name="linear">True if the texture is linear, false if block linear</param>
        /// <param name="bpp">Texture bytes per pixel</param>
        /// <param name="stride">Texture stride</param>
        /// <param name="xCount">Number of pixels to be copied</param>
        /// <param name="yCount">Number of lines to be copied</param>
        /// <returns></returns>
        private static bool IsTextureCopyComplete(DmaTexture tex, bool linear, int bpp, int stride, int xCount, int yCount)
        {
            if (linear)
            {
                // If the stride is negative, the texture has to be flipped, so
                // the fast copy is not trivial, use the slow path.
                if (stride <= 0)
                {
                    return false;
                }

                int alignWidth = Constants.StrideAlignment / bpp;
                return stride / bpp == BitUtils.AlignUp(xCount, alignWidth);
            }
            else
            {
                int alignWidth = Constants.GobAlignment / bpp;
                return tex.RegionX == 0 &&
                       tex.RegionY == 0 &&
                       tex.Width == BitUtils.AlignUp(xCount, alignWidth) &&
                       tex.Height == yCount;
            }
        }

        /// <summary>
        /// Releases a semaphore for a given LaunchDma method call.
        /// </summary>
        /// <param name="argument">The LaunchDma call argument</param>
        private void ReleaseSemaphore(int argument)
        {
            LaunchDmaSemaphoreType type = (LaunchDmaSemaphoreType)((argument >> 3) & 0x3);
            if (type != LaunchDmaSemaphoreType.None)
            {
                ulong address = ((ulong)_state.State.SetSemaphoreA << 32) | _state.State.SetSemaphoreB;
                if (type == LaunchDmaSemaphoreType.ReleaseOneWordSemaphore)
                {
                    _channel.MemoryManager.Write(address, _state.State.SetSemaphorePayload);
                }
                else /* if (type == LaunchDmaSemaphoreType.ReleaseFourWordSemaphore) */
                {
                    _channel.MemoryManager.Write(address + 8, _context.GetTimestamp());
                    _channel.MemoryManager.Write(address, (ulong)_state.State.SetSemaphorePayload);
                }
            }
        }

        /// <summary>
        /// Performs a buffer to buffer, or buffer to texture copy.
        /// </summary>
        /// <param name="argument">The LaunchDma call argument</param>
        private void DmaCopy(int argument)
        {
            var memoryManager = _channel.MemoryManager;

            CopyFlags copyFlags = (CopyFlags)argument;

            bool srcLinear = copyFlags.HasFlag(CopyFlags.SrcLinear);
            bool dstLinear = copyFlags.HasFlag(CopyFlags.DstLinear);
            bool copy2D = copyFlags.HasFlag(CopyFlags.MultiLineEnable);
            bool remap = copyFlags.HasFlag(CopyFlags.RemapEnable);

            uint size = _state.State.LineLengthIn;

            if (size == 0)
            {
                return;
            }

            ulong srcGpuVa = ((ulong)_state.State.OffsetInUpperUpper << 32) | _state.State.OffsetInLower;
            ulong dstGpuVa = ((ulong)_state.State.OffsetOutUpperUpper << 32) | _state.State.OffsetOutLower;

            int xCount = (int)_state.State.LineLengthIn;
            int yCount = (int)_state.State.LineCount;

            _channel.TextureManager.RefreshModifiedTextures();
            _3dEngine.CreatePendingSyncs();
            _3dEngine.FlushUboDirty();

            if (copy2D)
            {
                // Buffer to texture copy.
                int componentSize = (int)_state.State.SetRemapComponentsComponentSize + 1;
                int srcComponents = (int)_state.State.SetRemapComponentsNumSrcComponents + 1;
                int dstComponents = (int)_state.State.SetRemapComponentsNumDstComponents + 1;
                int srcBpp = remap ? srcComponents * componentSize : 1;
                int dstBpp = remap ? dstComponents * componentSize : 1;

                var dst = Unsafe.As<uint, DmaTexture>(ref _state.State.SetDstBlockSize);
                var src = Unsafe.As<uint, DmaTexture>(ref _state.State.SetSrcBlockSize);

                int srcRegionX = 0, srcRegionY = 0, dstRegionX = 0, dstRegionY = 0;

                if (!srcLinear)
                {
                    srcRegionX = src.RegionX;
                    srcRegionY = src.RegionY;
                }

                if (!dstLinear)
                {
                    dstRegionX = dst.RegionX;
                    dstRegionY = dst.RegionY;
                }

                int srcStride = (int)_state.State.PitchIn;
                int dstStride = (int)_state.State.PitchOut;

                var srcCalculator = new OffsetCalculator(
                    src.Width,
                    src.Height,
                    srcStride,
                    srcLinear,
                    src.MemoryLayout.UnpackGobBlocksInY(),
                    src.MemoryLayout.UnpackGobBlocksInZ(),
                    srcBpp);

                var dstCalculator = new OffsetCalculator(
                    dst.Width,
                    dst.Height,
                    dstStride,
                    dstLinear,
                    dst.MemoryLayout.UnpackGobBlocksInY(),
                    dst.MemoryLayout.UnpackGobBlocksInZ(),
                    dstBpp);

                (int srcBaseOffset, int srcSize) = srcCalculator.GetRectangleRange(srcRegionX, srcRegionY, xCount, yCount);
                (int dstBaseOffset, int dstSize) = dstCalculator.GetRectangleRange(dstRegionX, dstRegionY, xCount, yCount);

                if (srcLinear && srcStride < 0)
                {
                    srcBaseOffset += srcStride * (yCount - 1);
                }

                if (dstLinear && dstStride < 0)
                {
                    dstBaseOffset += dstStride * (yCount - 1);
                }

                ReadOnlySpan<byte> srcSpan = memoryManager.GetSpan(srcGpuVa + (ulong)srcBaseOffset, srcSize, true);

                // If remapping is disabled, we always copy the components directly, in order.
                // If it's enabled, but the mapping is just XYZW, we also copy them in order.
                bool isIdentityRemap = !remap ||
                    (_state.State.SetRemapComponentsDstX == SetRemapComponentsDst.SrcX &&
                    (dstComponents < 2 || _state.State.SetRemapComponentsDstY == SetRemapComponentsDst.SrcY) &&
                    (dstComponents < 3 || _state.State.SetRemapComponentsDstZ == SetRemapComponentsDst.SrcZ) &&
                    (dstComponents < 4 || _state.State.SetRemapComponentsDstW == SetRemapComponentsDst.SrcW));

                bool completeSource = IsTextureCopyComplete(src, srcLinear, srcBpp, srcStride, xCount, yCount);
                bool completeDest = IsTextureCopyComplete(dst, dstLinear, dstBpp, dstStride, xCount, yCount);

                // Try to set the texture data directly,
                // but only if we are doing a complete copy,
                // and not for block linear to linear copies, since those are typically accessed from the CPU.

                if (completeSource && completeDest && !(dstLinear && !srcLinear) && isIdentityRemap)
                {
                    var target = memoryManager.Physical.TextureCache.FindTexture(
                        memoryManager,
                        dstGpuVa,
                        dstBpp,
                        dstStride,
                        dst.Height,
                        xCount,
                        yCount,
                        dstLinear,
                        dst.MemoryLayout.UnpackGobBlocksInY(),
                        dst.MemoryLayout.UnpackGobBlocksInZ());

                    if (target != null)
                    {
                        MemoryOwner<byte> data;
                        if (srcLinear)
                        {
                            data = LayoutConverter.ConvertLinearStridedToLinear(
                                target.Info.Width,
                                target.Info.Height,
                                1,
                                1,
                                xCount * srcBpp,
                                srcStride,
                                target.Info.FormatInfo.BytesPerPixel,
                                srcSpan);
                        }
                        else
                        {
                            data = LayoutConverter.ConvertBlockLinearToLinear(
                                src.Width,
                                src.Height,
                                src.Depth,
                                1,
                                1,
                                1,
                                1,
                                1,
                                srcBpp,
                                src.MemoryLayout.UnpackGobBlocksInY(),
                                src.MemoryLayout.UnpackGobBlocksInZ(),
                                1,
                                new SizeInfo((int)target.Size),
                                srcSpan);
                        }

                        target.SynchronizeMemory();
                        target.SetData(data);
                        target.SignalModified();
                        return;
                    }
                    else if (srcCalculator.LayoutMatches(dstCalculator))
                    {
                        // No layout conversion has to be performed, just copy the data entirely.
                        memoryManager.Write(dstGpuVa + (ulong)dstBaseOffset, srcSpan);
                        return;
                    }
                }

                // OPT: This allocates a (potentially) huge temporary array and then copies an existing
                // region of memory into it, data that might get overwritten entirely anyways. Ideally this should
                // all be rewritten to use pooled arrays, but that gets complicated with packed data and strides
                Span<byte> dstSpan = memoryManager.GetSpan(dstGpuVa + (ulong)dstBaseOffset, dstSize).ToArray();

                TextureParams srcParams = new(srcRegionX, srcRegionY, srcBaseOffset, srcBpp, srcLinear, srcCalculator);
                TextureParams dstParams = new(dstRegionX, dstRegionY, dstBaseOffset, dstBpp, dstLinear, dstCalculator);

                if (isIdentityRemap)
                {
                    // The order of the components doesn't change, so we can just copy directly
                    // (with layout conversion if necessary).

                    switch (srcBpp)
                    {
                        case 1:
                            Copy<byte>(dstSpan, srcSpan, dstParams, srcParams);
                            break;
                        case 2:
                            Copy<ushort>(dstSpan, srcSpan, dstParams, srcParams);
                            break;
                        case 4:
                            Copy<uint>(dstSpan, srcSpan, dstParams, srcParams);
                            break;
                        case 8:
                            Copy<ulong>(dstSpan, srcSpan, dstParams, srcParams);
                            break;
                        case 12:
                            Copy<Bpp12Pixel>(dstSpan, srcSpan, dstParams, srcParams);
                            break;
                        case 16:
                            Copy<Vector128<byte>>(dstSpan, srcSpan, dstParams, srcParams);
                            break;
                        default:
                            throw new NotSupportedException($"Unable to copy ${srcBpp} bpp pixel format.");
                    }
                }
                else
                {
                    // The order or value of the components might change.

                    switch (componentSize)
                    {
                        case 1:
                            CopyShuffle<byte>(dstSpan, srcSpan, dstParams, srcParams);
                            break;
                        case 2:
                            CopyShuffle<ushort>(dstSpan, srcSpan, dstParams, srcParams);
                            break;
                        case 3:
                            CopyShuffle<UInt24>(dstSpan, srcSpan, dstParams, srcParams);
                            break;
                        case 4:
                            CopyShuffle<uint>(dstSpan, srcSpan, dstParams, srcParams);
                            break;
                        default:
                            throw new NotSupportedException($"Unable to copy ${componentSize} component size.");
                    }
                }

                memoryManager.Write(dstGpuVa + (ulong)dstBaseOffset, dstSpan);
            }
            else
            {
                if (remap &&
                    _state.State.SetRemapComponentsDstX == SetRemapComponentsDst.ConstA &&
                    _state.State.SetRemapComponentsDstY == SetRemapComponentsDst.ConstA &&
                    _state.State.SetRemapComponentsDstZ == SetRemapComponentsDst.ConstA &&
                    _state.State.SetRemapComponentsDstW == SetRemapComponentsDst.ConstA &&
                    _state.State.SetRemapComponentsNumSrcComponents == SetRemapComponentsNumComponents.One &&
                    _state.State.SetRemapComponentsNumDstComponents == SetRemapComponentsNumComponents.One &&
                    _state.State.SetRemapComponentsComponentSize == SetRemapComponentsComponentSize.Four)
                {
                    // Fast path for clears when remap is enabled.
                    memoryManager.Physical.BufferCache.ClearBuffer(memoryManager, dstGpuVa, size * 4, _state.State.SetRemapConstA);
                }
                else
                {
                    // TODO: Implement remap functionality.
                    // Buffer to buffer copy.

                    bool srcIsPitchKind = memoryManager.GetKind(srcGpuVa).IsPitch();
                    bool dstIsPitchKind = memoryManager.GetKind(dstGpuVa).IsPitch();

                    if (!srcIsPitchKind && dstIsPitchKind)
                    {
                        CopyGobBlockLinearToLinear(memoryManager, srcGpuVa, dstGpuVa, size);
                    }
                    else if (srcIsPitchKind && !dstIsPitchKind)
                    {
                        CopyGobLinearToBlockLinear(memoryManager, srcGpuVa, dstGpuVa, size);
                    }
                    else
                    {
                        memoryManager.Physical.BufferCache.CopyBuffer(memoryManager, srcGpuVa, dstGpuVa, size);
                    }
                }
            }
        }

        /// <summary>
        /// Copies data from one texture to another, while performing layout conversion if necessary.
        /// </summary>
        /// <typeparam name="T">Pixel type</typeparam>
        /// <param name="dstSpan">Destination texture memory region</param>
        /// <param name="srcSpan">Source texture memory region</param>
        /// <param name="dst">Destination texture parameters</param>
        /// <param name="src">Source texture parameters</param>
        private unsafe void Copy<T>(Span<byte> dstSpan, ReadOnlySpan<byte> srcSpan, TextureParams dst, TextureParams src) where T : unmanaged
        {
            int xCount = (int)_state.State.LineLengthIn;
            int yCount = (int)_state.State.LineCount;

            if (src.Linear && dst.Linear && src.Bpp == dst.Bpp)
            {
                // Optimized path for purely linear copies - we don't need to calculate every single byte offset,
                // and we can make use of Span.CopyTo which is very very fast (even compared to pointers)
                for (int y = 0; y < yCount; y++)
                {
                    src.Calculator.SetY(src.RegionY + y);
                    dst.Calculator.SetY(dst.RegionY + y);
                    int srcOffset = src.Calculator.GetOffset(src.RegionX);
                    int dstOffset = dst.Calculator.GetOffset(dst.RegionX);
                    srcSpan.Slice(srcOffset - src.BaseOffset, xCount * src.Bpp)
                        .CopyTo(dstSpan.Slice(dstOffset - dst.BaseOffset, xCount * dst.Bpp));
                }
            }
            else
            {
                fixed (byte* dstPtr = dstSpan, srcPtr = srcSpan)
                {
                    byte* dstBase = dstPtr - dst.BaseOffset; // Layout offset is relative to the base, so we need to subtract the span's offset.
                    byte* srcBase = srcPtr - src.BaseOffset;

                    for (int y = 0; y < yCount; y++)
                    {
                        src.Calculator.SetY(src.RegionY + y);
                        dst.Calculator.SetY(dst.RegionY + y);

                        for (int x = 0; x < xCount; x++)
                        {
                            int srcOffset = src.Calculator.GetOffset(src.RegionX + x);
                            int dstOffset = dst.Calculator.GetOffset(dst.RegionX + x);

                            *(T*)(dstBase + dstOffset) = *(T*)(srcBase + srcOffset);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets texture pixel data to a constant value, while performing layout conversion if necessary.
        /// </summary>
        /// <typeparam name="T">Pixel type</typeparam>
        /// <param name="dstSpan">Destination texture memory region</param>
        /// <param name="dst">Destination texture parameters</param>
        /// <param name="fillValue">Constant pixel value to be set</param>
        private unsafe void Fill<T>(Span<byte> dstSpan, TextureParams dst, T fillValue) where T : unmanaged
        {
            int xCount = (int)_state.State.LineLengthIn;
            int yCount = (int)_state.State.LineCount;

            fixed (byte* dstPtr = dstSpan)
            {
                byte* dstBase = dstPtr - dst.BaseOffset; // Layout offset is relative to the base, so we need to subtract the span's offset.

                for (int y = 0; y < yCount; y++)
                {
                    dst.Calculator.SetY(dst.RegionY + y);

                    for (int x = 0; x < xCount; x++)
                    {
                        int dstOffset = dst.Calculator.GetOffset(dst.RegionX + x);

                        *(T*)(dstBase + dstOffset) = fillValue;
                    }
                }
            }
        }

        /// <summary>
        /// Copies data from one texture to another, while performing layout conversion and component shuffling if necessary.
        /// </summary>
        /// <typeparam name="T">Pixel type</typeparam>
        /// <param name="dstSpan">Destination texture memory region</param>
        /// <param name="srcSpan">Source texture memory region</param>
        /// <param name="dst">Destination texture parameters</param>
        /// <param name="src">Source texture parameters</param>
        private void CopyShuffle<T>(Span<byte> dstSpan, ReadOnlySpan<byte> srcSpan, TextureParams dst, TextureParams src) where T : unmanaged
        {
            int dstComponents = (int)_state.State.SetRemapComponentsNumDstComponents + 1;

            for (int i = 0; i < dstComponents; i++)
            {
                SetRemapComponentsDst componentsDst = i switch
                {
                    0 => _state.State.SetRemapComponentsDstX,
                    1 => _state.State.SetRemapComponentsDstY,
                    2 => _state.State.SetRemapComponentsDstZ,
                    _ => _state.State.SetRemapComponentsDstW,
                };

                switch (componentsDst)
                {
                    case SetRemapComponentsDst.SrcX:
                        Copy<T>(dstSpan[(Unsafe.SizeOf<T>() * i)..], srcSpan, dst, src);
                        break;
                    case SetRemapComponentsDst.SrcY:
                        Copy<T>(dstSpan[(Unsafe.SizeOf<T>() * i)..], srcSpan[Unsafe.SizeOf<T>()..], dst, src);
                        break;
                    case SetRemapComponentsDst.SrcZ:
                        Copy<T>(dstSpan[(Unsafe.SizeOf<T>() * i)..], srcSpan[(Unsafe.SizeOf<T>() * 2)..], dst, src);
                        break;
                    case SetRemapComponentsDst.SrcW:
                        Copy<T>(dstSpan[(Unsafe.SizeOf<T>() * i)..], srcSpan[(Unsafe.SizeOf<T>() * 3)..], dst, src);
                        break;
                    case SetRemapComponentsDst.ConstA:
                        Fill<T>(dstSpan[(Unsafe.SizeOf<T>() * i)..], dst, Unsafe.As<uint, T>(ref _state.State.SetRemapConstA));
                        break;
                    case SetRemapComponentsDst.ConstB:
                        Fill<T>(dstSpan[(Unsafe.SizeOf<T>() * i)..], dst, Unsafe.As<uint, T>(ref _state.State.SetRemapConstB));
                        break;
                }
            }
        }

        /// <summary>
        /// Copies block linear data with block linear GOBs to a block linear destination with linear GOBs.
        /// </summary>
        /// <param name="memoryManager">GPU memory manager</param>
        /// <param name="srcGpuVa">Source GPU virtual address</param>
        /// <param name="dstGpuVa">Destination GPU virtual address</param>
        /// <param name="size">Size in bytes of the copy</param>
        private static void CopyGobBlockLinearToLinear(MemoryManager memoryManager, ulong srcGpuVa, ulong dstGpuVa, ulong size)
        {
            if (((srcGpuVa | dstGpuVa | size) & 0xf) == 0)
            {
                for (ulong offset = 0; offset < size; offset += 16)
                {
                    Vector128<byte> data = memoryManager.Read<Vector128<byte>>(ConvertGobLinearToBlockLinearAddress(srcGpuVa + offset), true);
                    memoryManager.Write(dstGpuVa + offset, data);
                }
            }
            else
            {
                for (ulong offset = 0; offset < size; offset++)
                {
                    byte data = memoryManager.Read<byte>(ConvertGobLinearToBlockLinearAddress(srcGpuVa + offset), true);
                    memoryManager.Write(dstGpuVa + offset, data);
                }
            }
        }

        /// <summary>
        /// Copies block linear data with linear GOBs to a block linear destination with block linear GOBs.
        /// </summary>
        /// <param name="memoryManager">GPU memory manager</param>
        /// <param name="srcGpuVa">Source GPU virtual address</param>
        /// <param name="dstGpuVa">Destination GPU virtual address</param>
        /// <param name="size">Size in bytes of the copy</param>
        private static void CopyGobLinearToBlockLinear(MemoryManager memoryManager, ulong srcGpuVa, ulong dstGpuVa, ulong size)
        {
            if (((srcGpuVa | dstGpuVa | size) & 0xf) == 0)
            {
                for (ulong offset = 0; offset < size; offset += 16)
                {
                    Vector128<byte> data = memoryManager.Read<Vector128<byte>>(srcGpuVa + offset, true);
                    memoryManager.Write(ConvertGobLinearToBlockLinearAddress(dstGpuVa + offset), data);
                }
            }
            else
            {
                for (ulong offset = 0; offset < size; offset++)
                {
                    byte data = memoryManager.Read<byte>(srcGpuVa + offset, true);
                    memoryManager.Write(ConvertGobLinearToBlockLinearAddress(dstGpuVa + offset), data);
                }
            }
        }

        /// <summary>
        /// Calculates the GOB block linear address from a linear address.
        /// </summary>
        /// <param name="address">Linear address</param>
        /// <returns>Block linear address</returns>
        private static ulong ConvertGobLinearToBlockLinearAddress(ulong address)
        {
            // y2 y1 y0 x5 x4 x3 x2 x1 x0 -> x5 y2 y1 x4 y0 x3 x2 x1 x0
            return (address & ~0x1f0UL) |
                ((address & 0x40) >> 2) |
                ((address & 0x10) << 1) |
                ((address & 0x180) >> 1) |
                ((address & 0x20) << 3);
        }

        /// <summary>
        /// Performs a buffer to buffer, or buffer to texture copy, then optionally releases a semaphore.
        /// </summary>
        /// <param name="argument">Method call argument</param>
        private void LaunchDma(int argument)
        {
            DmaCopy(argument);
            ReleaseSemaphore(argument);
        }
    }
}
