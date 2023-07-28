using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal;

namespace Ryujinx.Graphics.Metal
{
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
            return frontFace switch
            {
                FrontFace.Clockwise => MTLWinding.Clockwise,
                FrontFace.CounterClockwise => MTLWinding.CounterClockwise,
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
                Target.TextureBuffer => MTLTextureType.TypeTextureBuffer,
                Target.Texture1D => MTLTextureType.Type1D,
                Target.Texture1DArray => MTLTextureType.Type1DArray,
                Target.Texture2D => MTLTextureType.Type2D,
                Target.Texture2DArray => MTLTextureType.Type2DArray,
                Target.Texture2DMultisample => MTLTextureType.Type2DMultisample,
                Target.Texture2DMultisampleArray => MTLTextureType.Type2DMultisampleArray,
                Target.Texture3D => MTLTextureType.Type3D,
                Target.Cubemap => MTLTextureType.TypeCube,
                Target.CubemapArray => MTLTextureType.TypeCubeArray,
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
                _ => LogInvalidAndReturn(swizzleComponent, nameof(SwizzleComponent), MTLTextureSwizzle.Zero),
            };
        }

        private static T2 LogInvalidAndReturn<T1, T2>(T1 value, string name, T2 defaultValue = default)
        {
            Logger.Debug?.Print(LogClass.Gpu, $"Invalid {name} enum value: {value}.");

            return defaultValue;
        }
    }
}