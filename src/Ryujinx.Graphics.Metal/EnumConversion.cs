using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    static class EnumConversion
    {
        public static MTLSamplerAddressMode Convert(this AddressMode mode)
        {
            return mode switch
            {
                AddressMode.Clamp => MTLSamplerAddressMode.ClampToEdge, // TODO: Should be clamp.
                AddressMode.Repeat => MTLSamplerAddressMode.Repeat,
                AddressMode.MirrorClamp => MTLSamplerAddressMode.MirrorClampToEdge, // TODO: Should be mirror clamp.
                AddressMode.MirroredRepeat => MTLSamplerAddressMode.MirrorRepeat,
                AddressMode.ClampToBorder => MTLSamplerAddressMode.ClampToBorderColor,
                AddressMode.ClampToEdge => MTLSamplerAddressMode.ClampToEdge,
                AddressMode.MirrorClampToEdge => MTLSamplerAddressMode.MirrorClampToEdge,
                AddressMode.MirrorClampToBorder => MTLSamplerAddressMode.ClampToBorderColor, // TODO: Should be mirror clamp to border.
                _ => LogInvalidAndReturn(mode, nameof(AddressMode), MTLSamplerAddressMode.ClampToEdge) // TODO: Should be clamp.
            };
        }

        public static MTLBlendFactor Convert(this BlendFactor factor)
        {
            return factor switch
            {
                BlendFactor.Zero or BlendFactor.ZeroGl => MTLBlendFactor.Zero,
                BlendFactor.One or BlendFactor.OneGl => MTLBlendFactor.One,
                BlendFactor.SrcColor or BlendFactor.SrcColorGl => MTLBlendFactor.SourceColor,
                BlendFactor.OneMinusSrcColor or BlendFactor.OneMinusSrcColorGl => MTLBlendFactor.OneMinusSourceColor,
                BlendFactor.SrcAlpha or BlendFactor.SrcAlphaGl => MTLBlendFactor.SourceAlpha,
                BlendFactor.OneMinusSrcAlpha or BlendFactor.OneMinusSrcAlphaGl => MTLBlendFactor.OneMinusSourceAlpha,
                BlendFactor.DstAlpha or BlendFactor.DstAlphaGl => MTLBlendFactor.DestinationAlpha,
                BlendFactor.OneMinusDstAlpha or BlendFactor.OneMinusDstAlphaGl => MTLBlendFactor.OneMinusDestinationAlpha,
                BlendFactor.DstColor or BlendFactor.DstColorGl => MTLBlendFactor.DestinationColor,
                BlendFactor.OneMinusDstColor or BlendFactor.OneMinusDstColorGl => MTLBlendFactor.OneMinusDestinationColor,
                BlendFactor.SrcAlphaSaturate or BlendFactor.SrcAlphaSaturateGl => MTLBlendFactor.SourceAlphaSaturated,
                BlendFactor.Src1Color or BlendFactor.Src1ColorGl => MTLBlendFactor.Source1Color,
                BlendFactor.OneMinusSrc1Color or BlendFactor.OneMinusSrc1ColorGl => MTLBlendFactor.OneMinusSource1Color,
                BlendFactor.Src1Alpha or BlendFactor.Src1AlphaGl => MTLBlendFactor.Source1Alpha,
                BlendFactor.OneMinusSrc1Alpha or BlendFactor.OneMinusSrc1AlphaGl => MTLBlendFactor.OneMinusSource1Alpha,
                BlendFactor.ConstantColor => MTLBlendFactor.BlendColor,
                BlendFactor.OneMinusConstantColor => MTLBlendFactor.OneMinusBlendColor,
                BlendFactor.ConstantAlpha => MTLBlendFactor.BlendAlpha,
                BlendFactor.OneMinusConstantAlpha => MTLBlendFactor.OneMinusBlendAlpha,
                _ => LogInvalidAndReturn(factor, nameof(BlendFactor), MTLBlendFactor.Zero)
            };
        }

        public static MTLBlendOperation Convert(this BlendOp op)
        {
            return op switch
            {
                BlendOp.Add or BlendOp.AddGl => MTLBlendOperation.Add,
                BlendOp.Subtract or BlendOp.SubtractGl => MTLBlendOperation.Subtract,
                BlendOp.ReverseSubtract or BlendOp.ReverseSubtractGl => MTLBlendOperation.ReverseSubtract,
                BlendOp.Minimum => MTLBlendOperation.Min,
                BlendOp.Maximum => MTLBlendOperation.Max,
                _ => LogInvalidAndReturn(op, nameof(BlendOp), MTLBlendOperation.Add)
            };
        }

        public static MTLCompareFunction Convert(this CompareOp op)
        {
            return op switch
            {
                CompareOp.Never or CompareOp.NeverGl => MTLCompareFunction.Never,
                CompareOp.Less or CompareOp.LessGl => MTLCompareFunction.Less,
                CompareOp.Equal or CompareOp.EqualGl => MTLCompareFunction.Equal,
                CompareOp.LessOrEqual or CompareOp.LessOrEqualGl => MTLCompareFunction.LessEqual,
                CompareOp.Greater or CompareOp.GreaterGl => MTLCompareFunction.Greater,
                CompareOp.NotEqual or CompareOp.NotEqualGl => MTLCompareFunction.NotEqual,
                CompareOp.GreaterOrEqual or CompareOp.GreaterOrEqualGl => MTLCompareFunction.GreaterEqual,
                CompareOp.Always or CompareOp.AlwaysGl => MTLCompareFunction.Always,
                _ => LogInvalidAndReturn(op, nameof(CompareOp), MTLCompareFunction.Never)
            };
        }

        public static MTLCullMode Convert(this Face face)
        {
            return face switch
            {
                Face.Back => MTLCullMode.Back,
                Face.Front => MTLCullMode.Front,
                Face.FrontAndBack => MTLCullMode.None,
                _ => LogInvalidAndReturn(face, nameof(Face), MTLCullMode.Back)
            };
        }

        public static MTLWinding Convert(this FrontFace frontFace)
        {
            // The viewport is flipped vertically, therefore we need to switch the winding order as well
            return frontFace switch
            {
                FrontFace.Clockwise => MTLWinding.CounterClockwise,
                FrontFace.CounterClockwise => MTLWinding.Clockwise,
                _ => LogInvalidAndReturn(frontFace, nameof(FrontFace), MTLWinding.Clockwise)
            };
        }

        public static MTLIndexType Convert(this IndexType type)
        {
            return type switch
            {
                IndexType.UShort => MTLIndexType.UInt16,
                IndexType.UInt => MTLIndexType.UInt32,
                _ => LogInvalidAndReturn(type, nameof(IndexType), MTLIndexType.UInt16)
            };
        }

        public static MTLSamplerMinMagFilter Convert(this MagFilter filter)
        {
            return filter switch
            {
                MagFilter.Nearest => MTLSamplerMinMagFilter.Nearest,
                MagFilter.Linear => MTLSamplerMinMagFilter.Linear,
                _ => LogInvalidAndReturn(filter, nameof(MagFilter), MTLSamplerMinMagFilter.Nearest)
            };
        }

        public static (MTLSamplerMinMagFilter, MTLSamplerMipFilter) Convert(this MinFilter filter)
        {
            return filter switch
            {
                MinFilter.Nearest => (MTLSamplerMinMagFilter.Nearest, MTLSamplerMipFilter.Nearest),
                MinFilter.Linear => (MTLSamplerMinMagFilter.Linear, MTLSamplerMipFilter.Linear),
                MinFilter.NearestMipmapNearest => (MTLSamplerMinMagFilter.Nearest, MTLSamplerMipFilter.Nearest),
                MinFilter.LinearMipmapNearest => (MTLSamplerMinMagFilter.Linear, MTLSamplerMipFilter.Nearest),
                MinFilter.NearestMipmapLinear => (MTLSamplerMinMagFilter.Nearest, MTLSamplerMipFilter.Linear),
                MinFilter.LinearMipmapLinear => (MTLSamplerMinMagFilter.Linear, MTLSamplerMipFilter.Linear),
                _ => LogInvalidAndReturn(filter, nameof(MinFilter), (MTLSamplerMinMagFilter.Nearest, MTLSamplerMipFilter.Nearest))

            };
        }

        // TODO: Metal does not have native support for Triangle Fans but it is possible to emulate with TriangleStrip and moving around the indices
        public static MTLPrimitiveType Convert(this PrimitiveTopology topology)
        {
            return topology switch
            {
                PrimitiveTopology.Points => MTLPrimitiveType.Point,
                PrimitiveTopology.Lines => MTLPrimitiveType.Line,
                PrimitiveTopology.LineStrip => MTLPrimitiveType.LineStrip,
                PrimitiveTopology.Triangles => MTLPrimitiveType.Triangle,
                PrimitiveTopology.TriangleStrip => MTLPrimitiveType.TriangleStrip,
                _ => LogInvalidAndReturn(topology, nameof(PrimitiveTopology), MTLPrimitiveType.Triangle)
            };
        }

        public static MTLStencilOperation Convert(this StencilOp op)
        {
            return op switch
            {
                StencilOp.Keep or StencilOp.KeepGl => MTLStencilOperation.Keep,
                StencilOp.Zero or StencilOp.ZeroGl => MTLStencilOperation.Zero,
                StencilOp.Replace or StencilOp.ReplaceGl => MTLStencilOperation.Replace,
                StencilOp.IncrementAndClamp or StencilOp.IncrementAndClampGl => MTLStencilOperation.IncrementClamp,
                StencilOp.DecrementAndClamp or StencilOp.DecrementAndClampGl => MTLStencilOperation.DecrementClamp,
                StencilOp.Invert or StencilOp.InvertGl => MTLStencilOperation.Invert,
                StencilOp.IncrementAndWrap or StencilOp.IncrementAndWrapGl => MTLStencilOperation.IncrementWrap,
                StencilOp.DecrementAndWrap or StencilOp.DecrementAndWrapGl => MTLStencilOperation.DecrementWrap,
                _ => LogInvalidAndReturn(op, nameof(StencilOp), MTLStencilOperation.Keep)
            };
        }

        public static MTLTextureType Convert(this Target target)
        {
            return target switch
            {
                Target.TextureBuffer => MTLTextureType.TextureBuffer,
                Target.Texture1D => MTLTextureType.Type1D,
                Target.Texture1DArray => MTLTextureType.Type1DArray,
                Target.Texture2D => MTLTextureType.Type2D,
                Target.Texture2DArray => MTLTextureType.Type2DArray,
                Target.Texture2DMultisample => MTLTextureType.Type2DMultisample,
                Target.Texture2DMultisampleArray => MTLTextureType.Type2DMultisampleArray,
                Target.Texture3D => MTLTextureType.Type3D,
                Target.Cubemap => MTLTextureType.Cube,
                Target.CubemapArray => MTLTextureType.CubeArray,
                _ => LogInvalidAndReturn(target, nameof(Target), MTLTextureType.Type2D)
            };
        }

        public static MTLTextureSwizzle Convert(this SwizzleComponent swizzleComponent)
        {
            return swizzleComponent switch
            {
                SwizzleComponent.Zero => MTLTextureSwizzle.Zero,
                SwizzleComponent.One => MTLTextureSwizzle.One,
                SwizzleComponent.Red => MTLTextureSwizzle.Red,
                SwizzleComponent.Green => MTLTextureSwizzle.Green,
                SwizzleComponent.Blue => MTLTextureSwizzle.Blue,
                SwizzleComponent.Alpha => MTLTextureSwizzle.Alpha,
                _ => LogInvalidAndReturn(swizzleComponent, nameof(SwizzleComponent), MTLTextureSwizzle.Zero)
            };
        }

        public static MTLVertexFormat Convert(this Format format)
        {
            return format switch
            {
                Format.R16Float => MTLVertexFormat.Half,
                Format.R16G16Float => MTLVertexFormat.Half2,
                Format.R16G16B16Float => MTLVertexFormat.Half3,
                Format.R16G16B16A16Float => MTLVertexFormat.Half4,
                Format.R32Float => MTLVertexFormat.Float,
                Format.R32G32Float => MTLVertexFormat.Float2,
                Format.R32G32B32Float => MTLVertexFormat.Float3,
                Format.R11G11B10Float => MTLVertexFormat.FloatRG11B10,
                Format.R32G32B32A32Float => MTLVertexFormat.Float4,
                Format.R8Uint => MTLVertexFormat.UChar,
                Format.R8G8Uint => MTLVertexFormat.UChar2,
                Format.R8G8B8Uint => MTLVertexFormat.UChar3,
                Format.R8G8B8A8Uint => MTLVertexFormat.UChar4,
                Format.R16Uint => MTLVertexFormat.UShort,
                Format.R16G16Uint => MTLVertexFormat.UShort2,
                Format.R16G16B16Uint => MTLVertexFormat.UShort3,
                Format.R16G16B16A16Uint => MTLVertexFormat.UShort4,
                Format.R32Uint => MTLVertexFormat.UInt,
                Format.R32G32Uint => MTLVertexFormat.UInt2,
                Format.R32G32B32Uint => MTLVertexFormat.UInt3,
                Format.R32G32B32A32Uint => MTLVertexFormat.UInt4,
                Format.R8Sint => MTLVertexFormat.Char,
                Format.R8G8Sint => MTLVertexFormat.Char2,
                Format.R8G8B8Sint => MTLVertexFormat.Char3,
                Format.R8G8B8A8Sint => MTLVertexFormat.Char4,
                Format.R16Sint => MTLVertexFormat.Short,
                Format.R16G16Sint => MTLVertexFormat.Short2,
                Format.R16G16B16Sint => MTLVertexFormat.Short3,
                Format.R16G16B16A16Sint => MTLVertexFormat.Short4,
                Format.R32Sint => MTLVertexFormat.Int,
                Format.R32G32Sint => MTLVertexFormat.Int2,
                Format.R32G32B32Sint => MTLVertexFormat.Int3,
                Format.R32G32B32A32Sint => MTLVertexFormat.Int4,
                Format.R8Unorm => MTLVertexFormat.UCharNormalized,
                Format.R8G8Unorm => MTLVertexFormat.UChar2Normalized,
                Format.R8G8B8Unorm => MTLVertexFormat.UChar3Normalized,
                Format.R8G8B8A8Unorm => MTLVertexFormat.UChar4Normalized,
                Format.R16Unorm => MTLVertexFormat.UShortNormalized,
                Format.R16G16Unorm => MTLVertexFormat.UShort2Normalized,
                Format.R16G16B16Unorm => MTLVertexFormat.UShort3Normalized,
                Format.R16G16B16A16Unorm => MTLVertexFormat.UShort4Normalized,
                Format.R10G10B10A2Unorm => MTLVertexFormat.UInt1010102Normalized,
                Format.R8Snorm => MTLVertexFormat.CharNormalized,
                Format.R8G8Snorm => MTLVertexFormat.Char2Normalized,
                Format.R8G8B8Snorm => MTLVertexFormat.Char3Normalized,
                Format.R8G8B8A8Snorm => MTLVertexFormat.Char4Normalized,
                Format.R16Snorm => MTLVertexFormat.ShortNormalized,
                Format.R16G16Snorm => MTLVertexFormat.Short2Normalized,
                Format.R16G16B16Snorm => MTLVertexFormat.Short3Normalized,
                Format.R16G16B16A16Snorm => MTLVertexFormat.Short4Normalized,
                Format.R10G10B10A2Snorm => MTLVertexFormat.Int1010102Normalized,

                _ => LogInvalidAndReturn(format, nameof(Format), MTLVertexFormat.Float4)
            };
        }

        private static T2 LogInvalidAndReturn<T1, T2>(T1 value, string name, T2 defaultValue = default)
        {
            Logger.Debug?.Print(LogClass.Gpu, $"Invalid {name} enum value: {value}.");

            return defaultValue;
        }
    }
}
