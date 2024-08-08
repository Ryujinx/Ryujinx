using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    static class FormatTable
    {
        private static readonly MTLPixelFormat[] _table;

        static FormatTable()
        {
            _table = new MTLPixelFormat[Enum.GetNames(typeof(Format)).Length];

            Add(Format.R8Unorm, MTLPixelFormat.R8Unorm);
            Add(Format.R8Snorm, MTLPixelFormat.R8Snorm);
            Add(Format.R8Uint, MTLPixelFormat.R8Uint);
            Add(Format.R8Sint, MTLPixelFormat.R8Sint);
            Add(Format.R16Float, MTLPixelFormat.R16Float);
            Add(Format.R16Unorm, MTLPixelFormat.R16Unorm);
            Add(Format.R16Snorm, MTLPixelFormat.R16Snorm);
            Add(Format.R16Uint, MTLPixelFormat.R16Uint);
            Add(Format.R16Sint, MTLPixelFormat.R16Sint);
            Add(Format.R32Float, MTLPixelFormat.R32Float);
            Add(Format.R32Uint, MTLPixelFormat.R32Uint);
            Add(Format.R32Sint, MTLPixelFormat.R32Sint);
            Add(Format.R8G8Unorm, MTLPixelFormat.RG8Unorm);
            Add(Format.R8G8Snorm, MTLPixelFormat.RG8Snorm);
            Add(Format.R8G8Uint, MTLPixelFormat.RG8Uint);
            Add(Format.R8G8Sint, MTLPixelFormat.RG8Sint);
            Add(Format.R16G16Float, MTLPixelFormat.RG16Float);
            Add(Format.R16G16Unorm, MTLPixelFormat.RG16Unorm);
            Add(Format.R16G16Snorm, MTLPixelFormat.RG16Snorm);
            Add(Format.R16G16Uint, MTLPixelFormat.RG16Uint);
            Add(Format.R16G16Sint, MTLPixelFormat.RG16Sint);
            Add(Format.R32G32Float, MTLPixelFormat.RG32Float);
            Add(Format.R32G32Uint, MTLPixelFormat.RG32Uint);
            Add(Format.R32G32Sint, MTLPixelFormat.RG32Sint);
            // Add(Format.R8G8B8Unorm,         MTLPixelFormat.R8G8B8Unorm);
            // Add(Format.R8G8B8Snorm,         MTLPixelFormat.R8G8B8Snorm);
            // Add(Format.R8G8B8Uint,          MTLPixelFormat.R8G8B8Uint);
            // Add(Format.R8G8B8Sint,          MTLPixelFormat.R8G8B8Sint);
            // Add(Format.R16G16B16Float,      MTLPixelFormat.R16G16B16Float);
            // Add(Format.R16G16B16Unorm,      MTLPixelFormat.R16G16B16Unorm);
            // Add(Format.R16G16B16Snorm,      MTLPixelFormat.R16G16B16SNorm);
            // Add(Format.R16G16B16Uint,       MTLPixelFormat.R16G16B16Uint);
            // Add(Format.R16G16B16Sint,       MTLPixelFormat.R16G16B16Sint);
            // Add(Format.R32G32B32Float,      MTLPixelFormat.R32G32B32Sfloat);
            // Add(Format.R32G32B32Uint,       MTLPixelFormat.R32G32B32Uint);
            // Add(Format.R32G32B32Sint,       MTLPixelFormat.R32G32B32Sint);
            Add(Format.R8G8B8A8Unorm, MTLPixelFormat.RGBA8Unorm);
            Add(Format.R8G8B8A8Snorm, MTLPixelFormat.RGBA8Snorm);
            Add(Format.R8G8B8A8Uint, MTLPixelFormat.RGBA8Uint);
            Add(Format.R8G8B8A8Sint, MTLPixelFormat.RGBA8Sint);
            Add(Format.R16G16B16A16Float, MTLPixelFormat.RGBA16Float);
            Add(Format.R16G16B16A16Unorm, MTLPixelFormat.RGBA16Unorm);
            Add(Format.R16G16B16A16Snorm, MTLPixelFormat.RGBA16Snorm);
            Add(Format.R16G16B16A16Uint, MTLPixelFormat.RGBA16Uint);
            Add(Format.R16G16B16A16Sint, MTLPixelFormat.RGBA16Sint);
            Add(Format.R32G32B32A32Float, MTLPixelFormat.RGBA32Float);
            Add(Format.R32G32B32A32Uint, MTLPixelFormat.RGBA32Uint);
            Add(Format.R32G32B32A32Sint, MTLPixelFormat.RGBA32Sint);
            Add(Format.S8Uint, MTLPixelFormat.Stencil8);
            Add(Format.D16Unorm, MTLPixelFormat.Depth16Unorm);
            Add(Format.S8UintD24Unorm, MTLPixelFormat.Depth24UnormStencil8);
            Add(Format.X8UintD24Unorm, MTLPixelFormat.Depth24UnormStencil8);
            Add(Format.D32Float, MTLPixelFormat.Depth32Float);
            Add(Format.D24UnormS8Uint, MTLPixelFormat.Depth24UnormStencil8);
            Add(Format.D32FloatS8Uint, MTLPixelFormat.Depth32FloatStencil8);
            Add(Format.R8G8B8A8Srgb, MTLPixelFormat.RGBA8UnormsRGB);
            // Add(Format.R4G4Unorm,           MTLPixelFormat.R4G4Unorm);
            Add(Format.R4G4B4A4Unorm, MTLPixelFormat.RGBA8Unorm);
            // Add(Format.R5G5B5X1Unorm,       MTLPixelFormat.R5G5B5X1Unorm);
            Add(Format.R5G5B5A1Unorm, MTLPixelFormat.BGR5A1Unorm);
            Add(Format.R5G6B5Unorm, MTLPixelFormat.B5G6R5Unorm);
            Add(Format.R10G10B10A2Unorm, MTLPixelFormat.RGB10A2Unorm);
            Add(Format.R10G10B10A2Uint, MTLPixelFormat.RGB10A2Uint);
            Add(Format.R11G11B10Float, MTLPixelFormat.RG11B10Float);
            Add(Format.R9G9B9E5Float, MTLPixelFormat.RGB9E5Float);
            Add(Format.Bc1RgbaUnorm, MTLPixelFormat.BC1RGBA);
            Add(Format.Bc2Unorm, MTLPixelFormat.BC2RGBA);
            Add(Format.Bc3Unorm, MTLPixelFormat.BC3RGBA);
            Add(Format.Bc1RgbaSrgb, MTLPixelFormat.BC1RGBAsRGB);
            Add(Format.Bc2Srgb, MTLPixelFormat.BC2RGBAsRGB);
            Add(Format.Bc3Srgb, MTLPixelFormat.BC3RGBAsRGB);
            Add(Format.Bc4Unorm, MTLPixelFormat.BC4RUnorm);
            Add(Format.Bc4Snorm, MTLPixelFormat.BC4RSnorm);
            Add(Format.Bc5Unorm, MTLPixelFormat.BC5RGUnorm);
            Add(Format.Bc5Snorm, MTLPixelFormat.BC5RGSnorm);
            Add(Format.Bc7Unorm, MTLPixelFormat.BC7RGBAUnorm);
            Add(Format.Bc7Srgb, MTLPixelFormat.BC7RGBAUnormsRGB);
            Add(Format.Bc6HSfloat, MTLPixelFormat.BC6HRGBFloat);
            Add(Format.Bc6HUfloat, MTLPixelFormat.BC6HRGBUfloat);
            Add(Format.Etc2RgbUnorm, MTLPixelFormat.ETC2RGB8);
            // Add(Format.Etc2RgbaUnorm, MTLPixelFormat.ETC2RGBA8);
            Add(Format.Etc2RgbPtaUnorm, MTLPixelFormat.ETC2RGB8A1);
            Add(Format.Etc2RgbSrgb, MTLPixelFormat.ETC2RGB8sRGB);
            // Add(Format.Etc2RgbaSrgb, MTLPixelFormat.ETC2RGBA8sRGB);
            Add(Format.Etc2RgbPtaSrgb, MTLPixelFormat.ETC2RGB8A1sRGB);
            // Add(Format.R8Uscaled,           MTLPixelFormat.R8Uscaled);
            // Add(Format.R8Sscaled,           MTLPixelFormat.R8Sscaled);
            // Add(Format.R16Uscaled,          MTLPixelFormat.R16Uscaled);
            // Add(Format.R16Sscaled,          MTLPixelFormat.R16Sscaled);
            // Add(Format.R32Uscaled,          MTLPixelFormat.R32Uscaled);
            // Add(Format.R32Sscaled,          MTLPixelFormat.R32Sscaled);
            // Add(Format.R8G8Uscaled,         MTLPixelFormat.R8G8Uscaled);
            // Add(Format.R8G8Sscaled,         MTLPixelFormat.R8G8Sscaled);
            // Add(Format.R16G16Uscaled,       MTLPixelFormat.R16G16Uscaled);
            // Add(Format.R16G16Sscaled,       MTLPixelFormat.R16G16Sscaled);
            // Add(Format.R32G32Uscaled,       MTLPixelFormat.R32G32Uscaled);
            // Add(Format.R32G32Sscaled,       MTLPixelFormat.R32G32Sscaled);
            // Add(Format.R8G8B8Uscaled,       MTLPixelFormat.R8G8B8Uscaled);
            // Add(Format.R8G8B8Sscaled,       MTLPixelFormat.R8G8B8Sscaled);
            // Add(Format.R16G16B16Uscaled,    MTLPixelFormat.R16G16B16Uscaled);
            // Add(Format.R16G16B16Sscaled,    MTLPixelFormat.R16G16B16Sscaled);
            // Add(Format.R32G32B32Uscaled,    MTLPixelFormat.R32G32B32Uscaled);
            // Add(Format.R32G32B32Sscaled,    MTLPixelFormat.R32G32B32Sscaled);
            // Add(Format.R8G8B8A8Uscaled,     MTLPixelFormat.R8G8B8A8Uscaled);
            // Add(Format.R8G8B8A8Sscaled,     MTLPixelFormat.R8G8B8A8Sscaled);
            // Add(Format.R16G16B16A16Uscaled, MTLPixelFormat.R16G16B16A16Uscaled);
            // Add(Format.R16G16B16A16Sscaled, MTLPixelFormat.R16G16B16A16Sscaled);
            // Add(Format.R32G32B32A32Uscaled, MTLPixelFormat.R32G32B32A32Uscaled);
            // Add(Format.R32G32B32A32Sscaled, MTLPixelFormat.R32G32B32A32Sscaled);
            // Add(Format.R10G10B10A2Snorm,    MTLPixelFormat.A2B10G10R10SNormPack32);
            // Add(Format.R10G10B10A2Sint,     MTLPixelFormat.A2B10G10R10SintPack32);
            // Add(Format.R10G10B10A2Uscaled,  MTLPixelFormat.A2B10G10R10UscaledPack32);
            // Add(Format.R10G10B10A2Sscaled,  MTLPixelFormat.A2B10G10R10SscaledPack32);
            Add(Format.Astc4x4Unorm, MTLPixelFormat.ASTC4x4LDR);
            Add(Format.Astc5x4Unorm, MTLPixelFormat.ASTC5x4LDR);
            Add(Format.Astc5x5Unorm, MTLPixelFormat.ASTC5x5LDR);
            Add(Format.Astc6x5Unorm, MTLPixelFormat.ASTC6x5LDR);
            Add(Format.Astc6x6Unorm, MTLPixelFormat.ASTC6x6LDR);
            Add(Format.Astc8x5Unorm, MTLPixelFormat.ASTC8x5LDR);
            Add(Format.Astc8x6Unorm, MTLPixelFormat.ASTC8x6LDR);
            Add(Format.Astc8x8Unorm, MTLPixelFormat.ASTC8x8LDR);
            Add(Format.Astc10x5Unorm, MTLPixelFormat.ASTC10x5LDR);
            Add(Format.Astc10x6Unorm, MTLPixelFormat.ASTC10x6LDR);
            Add(Format.Astc10x8Unorm, MTLPixelFormat.ASTC10x8LDR);
            Add(Format.Astc10x10Unorm, MTLPixelFormat.ASTC10x10LDR);
            Add(Format.Astc12x10Unorm, MTLPixelFormat.ASTC12x10LDR);
            Add(Format.Astc12x12Unorm, MTLPixelFormat.ASTC12x12LDR);
            Add(Format.Astc4x4Srgb, MTLPixelFormat.ASTC4x4sRGB);
            Add(Format.Astc5x4Srgb, MTLPixelFormat.ASTC5x4sRGB);
            Add(Format.Astc5x5Srgb, MTLPixelFormat.ASTC5x5sRGB);
            Add(Format.Astc6x5Srgb, MTLPixelFormat.ASTC6x5sRGB);
            Add(Format.Astc6x6Srgb, MTLPixelFormat.ASTC6x6sRGB);
            Add(Format.Astc8x5Srgb, MTLPixelFormat.ASTC8x5sRGB);
            Add(Format.Astc8x6Srgb, MTLPixelFormat.ASTC8x6sRGB);
            Add(Format.Astc8x8Srgb, MTLPixelFormat.ASTC8x8sRGB);
            Add(Format.Astc10x5Srgb, MTLPixelFormat.ASTC10x5sRGB);
            Add(Format.Astc10x6Srgb, MTLPixelFormat.ASTC10x6sRGB);
            Add(Format.Astc10x8Srgb, MTLPixelFormat.ASTC10x8sRGB);
            Add(Format.Astc10x10Srgb, MTLPixelFormat.ASTC10x10sRGB);
            Add(Format.Astc12x10Srgb, MTLPixelFormat.ASTC12x10sRGB);
            Add(Format.Astc12x12Srgb, MTLPixelFormat.ASTC12x12sRGB);
            Add(Format.B5G6R5Unorm, MTLPixelFormat.B5G6R5Unorm);
            Add(Format.B5G5R5A1Unorm, MTLPixelFormat.BGR5A1Unorm);
            Add(Format.A1B5G5R5Unorm, MTLPixelFormat.A1BGR5Unorm);
            Add(Format.B8G8R8A8Unorm, MTLPixelFormat.BGRA8Unorm);
            Add(Format.B8G8R8A8Srgb, MTLPixelFormat.BGRA8UnormsRGB);
        }

        private static void Add(Format format, MTLPixelFormat mtlFormat)
        {
            _table[(int)format] = mtlFormat;
        }

        public static MTLPixelFormat GetFormat(Format format)
        {
            var mtlFormat = _table[(int)format];

            if (mtlFormat == MTLPixelFormat.Depth24UnormStencil8)
            {
                if (!MTLDevice.CreateSystemDefaultDevice().Depth24Stencil8PixelFormatSupported)
                {
                    Logger.Error?.PrintMsg(LogClass.Gpu, "Application requested Depth24Stencil8, which is unsupported on this device!");
                    mtlFormat = MTLPixelFormat.Depth32FloatStencil8;
                }
            }

            if (mtlFormat == MTLPixelFormat.Invalid)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Application requested {format}, no direct equivalent was found!");
            }

            return mtlFormat;
        }
    }
}
