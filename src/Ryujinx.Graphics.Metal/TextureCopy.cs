using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    static class TextureCopy
    {
        public static ulong CopyFromOrToBuffer(
            CommandBufferScoped cbs,
            MTLBuffer buffer,
            MTLTexture image,
            TextureCreateInfo info,
            bool to,
            int dstLayer,
            int dstLevel,
            int x,
            int y,
            int width,
            int height,
            ulong offset = 0)
        {
            MTLBlitCommandEncoder blitCommandEncoder = cbs.Encoders.EnsureBlitEncoder();

            bool is3D = info.Target == Target.Texture3D;

            int blockWidth = BitUtils.DivRoundUp(width, info.BlockWidth);
            int blockHeight = BitUtils.DivRoundUp(height, info.BlockHeight);
            ulong bytesPerRow = (ulong)BitUtils.AlignUp(blockWidth * info.BytesPerPixel, 4);
            ulong bytesPerImage = bytesPerRow * (ulong)blockHeight;

            MTLOrigin origin = new MTLOrigin { x = (ulong)x, y = (ulong)y, z = is3D ? (ulong)dstLayer : 0 };
            MTLSize region = new MTLSize { width = (ulong)width, height = (ulong)height, depth = 1 };

            uint layer = is3D ? 0 : (uint)dstLayer;

            if (to)
            {
                blitCommandEncoder.CopyFromTexture(
                    image,
                    layer,
                    (ulong)dstLevel,
                    origin,
                    region,
                    buffer,
                    offset,
                    bytesPerRow,
                    bytesPerImage);
            }
            else
            {
                blitCommandEncoder.CopyFromBuffer(buffer, offset, bytesPerRow, bytesPerImage, region, image, layer, (ulong)dstLevel, origin);
            }

            return offset + bytesPerImage;
        }

        public static void Copy(
            CommandBufferScoped cbs,
            MTLTexture srcImage,
            MTLTexture dstImage,
            TextureCreateInfo srcInfo,
            TextureCreateInfo dstInfo,
            int srcLayer,
            int dstLayer,
            int srcLevel,
            int dstLevel)
        {
            int srcDepth = srcInfo.GetDepthOrLayers();
            int srcLevels = srcInfo.Levels;

            int dstDepth = dstInfo.GetDepthOrLayers();
            int dstLevels = dstInfo.Levels;

            if (dstInfo.Target == Target.Texture3D)
            {
                dstDepth = Math.Max(1, dstDepth >> dstLevel);
            }

            int depth = Math.Min(srcDepth, dstDepth);
            int levels = Math.Min(srcLevels, dstLevels);

            Copy(
                cbs,
                srcImage,
                dstImage,
                srcInfo,
                dstInfo,
                srcLayer,
                dstLayer,
                srcLevel,
                dstLevel,
                depth,
                levels);
        }

        public static void Copy(
            CommandBufferScoped cbs,
            MTLTexture srcImage,
            MTLTexture dstImage,
            TextureCreateInfo srcInfo,
            TextureCreateInfo dstInfo,
            int srcDepthOrLayer,
            int dstDepthOrLayer,
            int srcLevel,
            int dstLevel,
            int depthOrLayers,
            int levels)
        {
            MTLBlitCommandEncoder blitCommandEncoder = cbs.Encoders.EnsureBlitEncoder();

            int srcZ;
            int srcLayer;
            int srcDepth;
            int srcLayers;

            if (srcInfo.Target == Target.Texture3D)
            {
                srcZ = srcDepthOrLayer;
                srcLayer = 0;
                srcDepth = depthOrLayers;
                srcLayers = 1;
            }
            else
            {
                srcZ = 0;
                srcLayer = srcDepthOrLayer;
                srcDepth = 1;
                srcLayers = depthOrLayers;
            }

            int dstZ;
            int dstLayer;
            int dstLayers;

            if (dstInfo.Target == Target.Texture3D)
            {
                dstZ = dstDepthOrLayer;
                dstLayer = 0;
                dstLayers = 1;
            }
            else
            {
                dstZ = 0;
                dstLayer = dstDepthOrLayer;
                dstLayers = depthOrLayers;
            }

            int srcWidth = srcInfo.Width;
            int srcHeight = srcInfo.Height;

            int dstWidth = dstInfo.Width;
            int dstHeight = dstInfo.Height;

            srcWidth = Math.Max(1, srcWidth >> srcLevel);
            srcHeight = Math.Max(1, srcHeight >> srcLevel);

            dstWidth = Math.Max(1, dstWidth >> dstLevel);
            dstHeight = Math.Max(1, dstHeight >> dstLevel);

            int blockWidth = 1;
            int blockHeight = 1;
            bool sizeInBlocks = false;

            MTLBuffer tempBuffer = default;

            if (srcInfo.Format != dstInfo.Format && (srcInfo.IsCompressed || dstInfo.IsCompressed))
            {
                // Compressed alias copies need to happen through a temporary buffer.
                // The data is copied from the source to the buffer, then the buffer to the destination.
                // The length of the buffer should be the maximum slice size for the destination.

                tempBuffer = blitCommandEncoder.Device.NewBuffer((ulong)dstInfo.GetMipSize2D(0), MTLResourceOptions.ResourceStorageModePrivate);
            }

            // When copying from a compressed to a non-compressed format,
            // the non-compressed texture will have the size of the texture
            // in blocks (not in texels), so we must adjust that size to
            // match the size in texels of the compressed texture.
            if (!srcInfo.IsCompressed && dstInfo.IsCompressed)
            {
                srcWidth *= dstInfo.BlockWidth;
                srcHeight *= dstInfo.BlockHeight;
                blockWidth = dstInfo.BlockWidth;
                blockHeight = dstInfo.BlockHeight;

                sizeInBlocks = true;
            }
            else if (srcInfo.IsCompressed && !dstInfo.IsCompressed)
            {
                dstWidth *= srcInfo.BlockWidth;
                dstHeight *= srcInfo.BlockHeight;
                blockWidth = srcInfo.BlockWidth;
                blockHeight = srcInfo.BlockHeight;
            }

            int width = Math.Min(srcWidth, dstWidth);
            int height = Math.Min(srcHeight, dstHeight);

            for (int level = 0; level < levels; level++)
            {
                // Stop copy if we are already out of the levels range.
                if (level >= srcInfo.Levels || dstLevel + level >= dstInfo.Levels)
                {
                    break;
                }

                int copyWidth = sizeInBlocks ? BitUtils.DivRoundUp(width, blockWidth) : width;
                int copyHeight = sizeInBlocks ? BitUtils.DivRoundUp(height, blockHeight) : height;

                int layers = Math.Max(dstLayers - dstLayer, srcLayers);

                for (int layer = 0; layer < layers; layer++)
                {
                    if (tempBuffer.NativePtr != 0)
                    {
                        // Copy through the temp buffer
                        CopyFromOrToBuffer(cbs, tempBuffer, srcImage, srcInfo, true, srcLayer + layer, srcLevel + level, 0, 0, copyWidth, copyHeight);

                        int dstBufferWidth = sizeInBlocks ? copyWidth * blockWidth : BitUtils.DivRoundUp(copyWidth, blockWidth);
                        int dstBufferHeight = sizeInBlocks ? copyHeight * blockHeight : BitUtils.DivRoundUp(copyHeight, blockHeight);

                        CopyFromOrToBuffer(cbs, tempBuffer, dstImage, dstInfo, false, dstLayer + layer, dstLevel + level, 0, 0, dstBufferWidth, dstBufferHeight);
                    }
                    else if (srcInfo.Samples > 1 && srcInfo.Samples != dstInfo.Samples)
                    {
                        // TODO

                        Logger.Warning?.PrintMsg(LogClass.Gpu, "Unsupported mismatching sample count copy");
                    }
                    else
                    {
                        blitCommandEncoder.CopyFromTexture(
                            srcImage,
                            (ulong)(srcLayer + layer),
                            (ulong)(srcLevel + level),
                            new MTLOrigin { z = (ulong)srcZ },
                            new MTLSize { width = (ulong)copyWidth, height = (ulong)copyHeight, depth = (ulong)srcDepth },
                            dstImage,
                            (ulong)(dstLayer + layer),
                            (ulong)(dstLevel + level),
                            new MTLOrigin { z = (ulong)dstZ });
                    }
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);

                if (srcInfo.Target == Target.Texture3D)
                {
                    srcDepth = Math.Max(1, srcDepth >> 1);
                }
            }

            if (tempBuffer.NativePtr != 0)
            {
                tempBuffer.Dispose();
            }
        }
    }
}
