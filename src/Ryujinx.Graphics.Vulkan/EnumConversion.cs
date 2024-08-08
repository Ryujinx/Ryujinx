using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Silk.NET.Vulkan;
using System;
using BlendFactor = Silk.NET.Vulkan.BlendFactor;
using BlendOp = Silk.NET.Vulkan.BlendOp;
using CompareOp = Silk.NET.Vulkan.CompareOp;
using Format = Ryujinx.Graphics.GAL.Format;
using FrontFace = Silk.NET.Vulkan.FrontFace;
using IndexType = Silk.NET.Vulkan.IndexType;
using PrimitiveTopology = Silk.NET.Vulkan.PrimitiveTopology;
using StencilOp = Silk.NET.Vulkan.StencilOp;

namespace Ryujinx.Graphics.Vulkan
{
    static class EnumConversion
    {
        public static ShaderStageFlags Convert(this ShaderStage stage)
        {
            return stage switch
            {
                ShaderStage.Vertex => ShaderStageFlags.VertexBit,
                ShaderStage.Geometry => ShaderStageFlags.GeometryBit,
                ShaderStage.TessellationControl => ShaderStageFlags.TessellationControlBit,
                ShaderStage.TessellationEvaluation => ShaderStageFlags.TessellationEvaluationBit,
                ShaderStage.Fragment => ShaderStageFlags.FragmentBit,
                ShaderStage.Compute => ShaderStageFlags.ComputeBit,
                _ => LogInvalidAndReturn(stage, nameof(ShaderStage), (ShaderStageFlags)0),
            };
        }

        public static PipelineStageFlags ConvertToPipelineStageFlags(this ShaderStage stage)
        {
            return stage switch
            {
                ShaderStage.Vertex => PipelineStageFlags.VertexShaderBit,
                ShaderStage.Geometry => PipelineStageFlags.GeometryShaderBit,
                ShaderStage.TessellationControl => PipelineStageFlags.TessellationControlShaderBit,
                ShaderStage.TessellationEvaluation => PipelineStageFlags.TessellationEvaluationShaderBit,
                ShaderStage.Fragment => PipelineStageFlags.FragmentShaderBit,
                ShaderStage.Compute => PipelineStageFlags.ComputeShaderBit,
                _ => LogInvalidAndReturn(stage, nameof(ShaderStage), (PipelineStageFlags)0),
            };
        }

        public static ShaderStageFlags Convert(this ResourceStages stages)
        {
            ShaderStageFlags stageFlags = stages.HasFlag(ResourceStages.Compute)
                ? ShaderStageFlags.ComputeBit
                : ShaderStageFlags.None;

            if (stages.HasFlag(ResourceStages.Vertex))
            {
                stageFlags |= ShaderStageFlags.VertexBit;
            }

            if (stages.HasFlag(ResourceStages.TessellationControl))
            {
                stageFlags |= ShaderStageFlags.TessellationControlBit;
            }

            if (stages.HasFlag(ResourceStages.TessellationEvaluation))
            {
                stageFlags |= ShaderStageFlags.TessellationEvaluationBit;
            }

            if (stages.HasFlag(ResourceStages.Geometry))
            {
                stageFlags |= ShaderStageFlags.GeometryBit;
            }

            if (stages.HasFlag(ResourceStages.Fragment))
            {
                stageFlags |= ShaderStageFlags.FragmentBit;
            }

            return stageFlags;
        }

        public static DescriptorType Convert(this ResourceType type)
        {
            return type switch
            {
                ResourceType.UniformBuffer => DescriptorType.UniformBuffer,
                ResourceType.StorageBuffer => DescriptorType.StorageBuffer,
                ResourceType.Texture => DescriptorType.SampledImage,
                ResourceType.Sampler => DescriptorType.Sampler,
                ResourceType.TextureAndSampler => DescriptorType.CombinedImageSampler,
                ResourceType.Image => DescriptorType.StorageImage,
                ResourceType.BufferTexture => DescriptorType.UniformTexelBuffer,
                ResourceType.BufferImage => DescriptorType.StorageTexelBuffer,
                _ => throw new ArgumentException($"Invalid resource type \"{type}\"."),
            };
        }

        public static SamplerAddressMode Convert(this AddressMode mode)
        {
            return mode switch
            {
                AddressMode.Clamp => SamplerAddressMode.ClampToEdge, // TODO: Should be clamp.
                AddressMode.Repeat => SamplerAddressMode.Repeat,
                AddressMode.MirrorClamp => SamplerAddressMode.ClampToEdge, // TODO: Should be mirror clamp.
                AddressMode.MirrorClampToEdge => SamplerAddressMode.MirrorClampToEdgeKhr,
                AddressMode.MirrorClampToBorder => SamplerAddressMode.ClampToBorder, // TODO: Should be mirror clamp to border.
                AddressMode.ClampToBorder => SamplerAddressMode.ClampToBorder,
                AddressMode.MirroredRepeat => SamplerAddressMode.MirroredRepeat,
                AddressMode.ClampToEdge => SamplerAddressMode.ClampToEdge,
                _ => LogInvalidAndReturn(mode, nameof(AddressMode), SamplerAddressMode.ClampToEdge), // TODO: Should be clamp.
            };
        }

        public static BlendFactor Convert(this GAL.BlendFactor factor)
        {
            return factor switch
            {
                GAL.BlendFactor.Zero or GAL.BlendFactor.ZeroGl => BlendFactor.Zero,
                GAL.BlendFactor.One or GAL.BlendFactor.OneGl => BlendFactor.One,
                GAL.BlendFactor.SrcColor or GAL.BlendFactor.SrcColorGl => BlendFactor.SrcColor,
                GAL.BlendFactor.OneMinusSrcColor or GAL.BlendFactor.OneMinusSrcColorGl => BlendFactor.OneMinusSrcColor,
                GAL.BlendFactor.SrcAlpha or GAL.BlendFactor.SrcAlphaGl => BlendFactor.SrcAlpha,
                GAL.BlendFactor.OneMinusSrcAlpha or GAL.BlendFactor.OneMinusSrcAlphaGl => BlendFactor.OneMinusSrcAlpha,
                GAL.BlendFactor.DstAlpha or GAL.BlendFactor.DstAlphaGl => BlendFactor.DstAlpha,
                GAL.BlendFactor.OneMinusDstAlpha or GAL.BlendFactor.OneMinusDstAlphaGl => BlendFactor.OneMinusDstAlpha,
                GAL.BlendFactor.DstColor or GAL.BlendFactor.DstColorGl => BlendFactor.DstColor,
                GAL.BlendFactor.OneMinusDstColor or GAL.BlendFactor.OneMinusDstColorGl => BlendFactor.OneMinusDstColor,
                GAL.BlendFactor.SrcAlphaSaturate or GAL.BlendFactor.SrcAlphaSaturateGl => BlendFactor.SrcAlphaSaturate,
                GAL.BlendFactor.Src1Color or GAL.BlendFactor.Src1ColorGl => BlendFactor.Src1Color,
                GAL.BlendFactor.OneMinusSrc1Color or GAL.BlendFactor.OneMinusSrc1ColorGl => BlendFactor.OneMinusSrc1Color,
                GAL.BlendFactor.Src1Alpha or GAL.BlendFactor.Src1AlphaGl => BlendFactor.Src1Alpha,
                GAL.BlendFactor.OneMinusSrc1Alpha or GAL.BlendFactor.OneMinusSrc1AlphaGl => BlendFactor.OneMinusSrc1Alpha,
                GAL.BlendFactor.ConstantColor => BlendFactor.ConstantColor,
                GAL.BlendFactor.OneMinusConstantColor => BlendFactor.OneMinusConstantColor,
                GAL.BlendFactor.ConstantAlpha => BlendFactor.ConstantAlpha,
                GAL.BlendFactor.OneMinusConstantAlpha => BlendFactor.OneMinusConstantAlpha,
                _ => LogInvalidAndReturn(factor, nameof(GAL.BlendFactor), BlendFactor.Zero),
            };
        }

        public static BlendOp Convert(this AdvancedBlendOp op)
        {
            return op switch
            {
                AdvancedBlendOp.Zero => BlendOp.ZeroExt,
                AdvancedBlendOp.Src => BlendOp.SrcExt,
                AdvancedBlendOp.Dst => BlendOp.DstExt,
                AdvancedBlendOp.SrcOver => BlendOp.SrcOverExt,
                AdvancedBlendOp.DstOver => BlendOp.DstOverExt,
                AdvancedBlendOp.SrcIn => BlendOp.SrcInExt,
                AdvancedBlendOp.DstIn => BlendOp.DstInExt,
                AdvancedBlendOp.SrcOut => BlendOp.SrcOutExt,
                AdvancedBlendOp.DstOut => BlendOp.DstOutExt,
                AdvancedBlendOp.SrcAtop => BlendOp.SrcAtopExt,
                AdvancedBlendOp.DstAtop => BlendOp.DstAtopExt,
                AdvancedBlendOp.Xor => BlendOp.XorExt,
                AdvancedBlendOp.Plus => BlendOp.PlusExt,
                AdvancedBlendOp.PlusClamped => BlendOp.PlusClampedExt,
                AdvancedBlendOp.PlusClampedAlpha => BlendOp.PlusClampedAlphaExt,
                AdvancedBlendOp.PlusDarker => BlendOp.PlusDarkerExt,
                AdvancedBlendOp.Multiply => BlendOp.MultiplyExt,
                AdvancedBlendOp.Screen => BlendOp.ScreenExt,
                AdvancedBlendOp.Overlay => BlendOp.OverlayExt,
                AdvancedBlendOp.Darken => BlendOp.DarkenExt,
                AdvancedBlendOp.Lighten => BlendOp.LightenExt,
                AdvancedBlendOp.ColorDodge => BlendOp.ColordodgeExt,
                AdvancedBlendOp.ColorBurn => BlendOp.ColorburnExt,
                AdvancedBlendOp.HardLight => BlendOp.HardlightExt,
                AdvancedBlendOp.SoftLight => BlendOp.SoftlightExt,
                AdvancedBlendOp.Difference => BlendOp.DifferenceExt,
                AdvancedBlendOp.Minus => BlendOp.MinusExt,
                AdvancedBlendOp.MinusClamped => BlendOp.MinusClampedExt,
                AdvancedBlendOp.Exclusion => BlendOp.ExclusionExt,
                AdvancedBlendOp.Contrast => BlendOp.ContrastExt,
                AdvancedBlendOp.Invert => BlendOp.InvertExt,
                AdvancedBlendOp.InvertRGB => BlendOp.InvertRgbExt,
                AdvancedBlendOp.InvertOvg => BlendOp.InvertOvgExt,
                AdvancedBlendOp.LinearDodge => BlendOp.LineardodgeExt,
                AdvancedBlendOp.LinearBurn => BlendOp.LinearburnExt,
                AdvancedBlendOp.VividLight => BlendOp.VividlightExt,
                AdvancedBlendOp.LinearLight => BlendOp.LinearlightExt,
                AdvancedBlendOp.PinLight => BlendOp.PinlightExt,
                AdvancedBlendOp.HardMix => BlendOp.HardmixExt,
                AdvancedBlendOp.Red => BlendOp.RedExt,
                AdvancedBlendOp.Green => BlendOp.GreenExt,
                AdvancedBlendOp.Blue => BlendOp.BlueExt,
                AdvancedBlendOp.HslHue => BlendOp.HslHueExt,
                AdvancedBlendOp.HslSaturation => BlendOp.HslSaturationExt,
                AdvancedBlendOp.HslColor => BlendOp.HslColorExt,
                AdvancedBlendOp.HslLuminosity => BlendOp.HslLuminosityExt,
                _ => LogInvalidAndReturn(op, nameof(AdvancedBlendOp), BlendOp.Add),
            };
        }

        public static BlendOp Convert(this GAL.BlendOp op)
        {
            return op switch
            {
                GAL.BlendOp.Add or GAL.BlendOp.AddGl => BlendOp.Add,
                GAL.BlendOp.Subtract or GAL.BlendOp.SubtractGl => BlendOp.Subtract,
                GAL.BlendOp.ReverseSubtract or GAL.BlendOp.ReverseSubtractGl => BlendOp.ReverseSubtract,
                GAL.BlendOp.Minimum or GAL.BlendOp.MinimumGl => BlendOp.Min,
                GAL.BlendOp.Maximum or GAL.BlendOp.MaximumGl => BlendOp.Max,
                _ => LogInvalidAndReturn(op, nameof(GAL.BlendOp), BlendOp.Add),
            };
        }

        public static BlendOverlapEXT Convert(this AdvancedBlendOverlap overlap)
        {
            return overlap switch
            {
                AdvancedBlendOverlap.Uncorrelated => BlendOverlapEXT.UncorrelatedExt,
                AdvancedBlendOverlap.Disjoint => BlendOverlapEXT.DisjointExt,
                AdvancedBlendOverlap.Conjoint => BlendOverlapEXT.ConjointExt,
                _ => LogInvalidAndReturn(overlap, nameof(AdvancedBlendOverlap), BlendOverlapEXT.UncorrelatedExt),
            };
        }

        public static CompareOp Convert(this GAL.CompareOp op)
        {
            return op switch
            {
                GAL.CompareOp.Never or GAL.CompareOp.NeverGl => CompareOp.Never,
                GAL.CompareOp.Less or GAL.CompareOp.LessGl => CompareOp.Less,
                GAL.CompareOp.Equal or GAL.CompareOp.EqualGl => CompareOp.Equal,
                GAL.CompareOp.LessOrEqual or GAL.CompareOp.LessOrEqualGl => CompareOp.LessOrEqual,
                GAL.CompareOp.Greater or GAL.CompareOp.GreaterGl => CompareOp.Greater,
                GAL.CompareOp.NotEqual or GAL.CompareOp.NotEqualGl => CompareOp.NotEqual,
                GAL.CompareOp.GreaterOrEqual or GAL.CompareOp.GreaterOrEqualGl => CompareOp.GreaterOrEqual,
                GAL.CompareOp.Always or GAL.CompareOp.AlwaysGl => CompareOp.Always,
                _ => LogInvalidAndReturn(op, nameof(GAL.CompareOp), CompareOp.Never),
            };
        }

        public static CullModeFlags Convert(this Face face)
        {
            return face switch
            {
                Face.Back => CullModeFlags.BackBit,
                Face.Front => CullModeFlags.FrontBit,
                Face.FrontAndBack => CullModeFlags.FrontAndBack,
                _ => LogInvalidAndReturn(face, nameof(Face), CullModeFlags.BackBit),
            };
        }

        public static FrontFace Convert(this GAL.FrontFace frontFace)
        {
            // Flipped to account for origin differences.
            return frontFace switch
            {
                GAL.FrontFace.Clockwise => FrontFace.CounterClockwise,
                GAL.FrontFace.CounterClockwise => FrontFace.Clockwise,
                _ => LogInvalidAndReturn(frontFace, nameof(GAL.FrontFace), FrontFace.Clockwise),
            };
        }

        public static IndexType Convert(this GAL.IndexType type)
        {
            return type switch
            {
                GAL.IndexType.UByte => IndexType.Uint8Ext,
                GAL.IndexType.UShort => IndexType.Uint16,
                GAL.IndexType.UInt => IndexType.Uint32,
                _ => LogInvalidAndReturn(type, nameof(GAL.IndexType), IndexType.Uint16),
            };
        }

        public static Filter Convert(this MagFilter filter)
        {
            return filter switch
            {
                MagFilter.Nearest => Filter.Nearest,
                MagFilter.Linear => Filter.Linear,
                _ => LogInvalidAndReturn(filter, nameof(MagFilter), Filter.Nearest),
            };
        }

        public static (Filter, SamplerMipmapMode) Convert(this MinFilter filter)
        {
            return filter switch
            {
                MinFilter.Nearest => (Filter.Nearest, SamplerMipmapMode.Nearest),
                MinFilter.Linear => (Filter.Linear, SamplerMipmapMode.Nearest),
                MinFilter.NearestMipmapNearest => (Filter.Nearest, SamplerMipmapMode.Nearest),
                MinFilter.LinearMipmapNearest => (Filter.Linear, SamplerMipmapMode.Nearest),
                MinFilter.NearestMipmapLinear => (Filter.Nearest, SamplerMipmapMode.Linear),
                MinFilter.LinearMipmapLinear => (Filter.Linear, SamplerMipmapMode.Linear),
                _ => LogInvalidAndReturn(filter, nameof(MinFilter), (Filter.Nearest, SamplerMipmapMode.Nearest)),
            };
        }

        public static PrimitiveTopology Convert(this GAL.PrimitiveTopology topology)
        {
            return topology switch
            {
                GAL.PrimitiveTopology.Points => PrimitiveTopology.PointList,
                GAL.PrimitiveTopology.Lines => PrimitiveTopology.LineList,
                GAL.PrimitiveTopology.LineStrip => PrimitiveTopology.LineStrip,
                GAL.PrimitiveTopology.Triangles => PrimitiveTopology.TriangleList,
                GAL.PrimitiveTopology.TriangleStrip => PrimitiveTopology.TriangleStrip,
                GAL.PrimitiveTopology.TriangleFan => PrimitiveTopology.TriangleFan,
                GAL.PrimitiveTopology.LinesAdjacency => PrimitiveTopology.LineListWithAdjacency,
                GAL.PrimitiveTopology.LineStripAdjacency => PrimitiveTopology.LineStripWithAdjacency,
                GAL.PrimitiveTopology.TrianglesAdjacency => PrimitiveTopology.TriangleListWithAdjacency,
                GAL.PrimitiveTopology.TriangleStripAdjacency => PrimitiveTopology.TriangleStripWithAdjacency,
                GAL.PrimitiveTopology.Patches => PrimitiveTopology.PatchList,
                GAL.PrimitiveTopology.Polygon => PrimitiveTopology.TriangleFan,
                GAL.PrimitiveTopology.Quads => throw new NotSupportedException("Quad topology is not available in Vulkan."),
                GAL.PrimitiveTopology.QuadStrip => throw new NotSupportedException("QuadStrip topology is not available in Vulkan."),
                _ => LogInvalidAndReturn(topology, nameof(GAL.PrimitiveTopology), PrimitiveTopology.TriangleList),
            };
        }

        public static PrimitiveTopology ConvertToClass(this PrimitiveTopology topology)
        {
            return topology switch
            {
                PrimitiveTopology.PointList => PrimitiveTopology.PointList,
                PrimitiveTopology.LineList or
                    PrimitiveTopology.LineStrip or
                    PrimitiveTopology.LineListWithAdjacency or
                    PrimitiveTopology.LineStripWithAdjacency => PrimitiveTopology.LineList,
                PrimitiveTopology.TriangleList or
                    PrimitiveTopology.TriangleStrip or
                    PrimitiveTopology.TriangleFan or
                    PrimitiveTopology.TriangleListWithAdjacency or
                    PrimitiveTopology.TriangleStripWithAdjacency => PrimitiveTopology.TriangleList,
                PrimitiveTopology.PatchList => PrimitiveTopology.PatchList,
                _ => LogInvalidAndReturn(topology, nameof(PrimitiveTopology), PrimitiveTopology.TriangleList),
            };
        }

        public static StencilOp Convert(this GAL.StencilOp op)
        {
            return op switch
            {
                GAL.StencilOp.Keep or GAL.StencilOp.KeepGl => StencilOp.Keep,
                GAL.StencilOp.Zero or GAL.StencilOp.ZeroGl => StencilOp.Zero,
                GAL.StencilOp.Replace or GAL.StencilOp.ReplaceGl => StencilOp.Replace,
                GAL.StencilOp.IncrementAndClamp or GAL.StencilOp.IncrementAndClampGl => StencilOp.IncrementAndClamp,
                GAL.StencilOp.DecrementAndClamp or GAL.StencilOp.DecrementAndClampGl => StencilOp.DecrementAndClamp,
                GAL.StencilOp.Invert or GAL.StencilOp.InvertGl => StencilOp.Invert,
                GAL.StencilOp.IncrementAndWrap or GAL.StencilOp.IncrementAndWrapGl => StencilOp.IncrementAndWrap,
                GAL.StencilOp.DecrementAndWrap or GAL.StencilOp.DecrementAndWrapGl => StencilOp.DecrementAndWrap,
                _ => LogInvalidAndReturn(op, nameof(GAL.StencilOp), StencilOp.Keep),
            };
        }

        public static ComponentSwizzle Convert(this SwizzleComponent swizzleComponent)
        {
            return swizzleComponent switch
            {
                SwizzleComponent.Zero => ComponentSwizzle.Zero,
                SwizzleComponent.One => ComponentSwizzle.One,
                SwizzleComponent.Red => ComponentSwizzle.R,
                SwizzleComponent.Green => ComponentSwizzle.G,
                SwizzleComponent.Blue => ComponentSwizzle.B,
                SwizzleComponent.Alpha => ComponentSwizzle.A,
                _ => LogInvalidAndReturn(swizzleComponent, nameof(SwizzleComponent), ComponentSwizzle.Zero),
            };
        }

        public static ImageType Convert(this Target target)
        {
            return target switch
            {
                Target.Texture1D or
                Target.Texture1DArray or
                Target.TextureBuffer => ImageType.Type1D,
                Target.Texture2D or
                Target.Texture2DArray or
                Target.Texture2DMultisample or
                Target.Cubemap or
                Target.CubemapArray => ImageType.Type2D,
                Target.Texture3D => ImageType.Type3D,
                _ => LogInvalidAndReturn(target, nameof(Target), ImageType.Type2D),
            };
        }

        public static ImageViewType ConvertView(this Target target)
        {
            return target switch
            {
                Target.Texture1D => ImageViewType.Type1D,
                Target.Texture2D or Target.Texture2DMultisample => ImageViewType.Type2D,
                Target.Texture3D => ImageViewType.Type3D,
                Target.Texture1DArray => ImageViewType.Type1DArray,
                Target.Texture2DArray => ImageViewType.Type2DArray,
                Target.Cubemap => ImageViewType.TypeCube,
                Target.CubemapArray => ImageViewType.TypeCubeArray,
                _ => LogInvalidAndReturn(target, nameof(Target), ImageViewType.Type2D),
            };
        }

        public static ImageAspectFlags ConvertAspectFlags(this Format format)
        {
            return format switch
            {
                Format.D16Unorm or Format.D32Float or Format.X8UintD24Unorm => ImageAspectFlags.DepthBit,
                Format.S8Uint => ImageAspectFlags.StencilBit,
                Format.D24UnormS8Uint or
                Format.D32FloatS8Uint or
                Format.S8UintD24Unorm => ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit,
                _ => ImageAspectFlags.ColorBit,
            };
        }

        public static ImageAspectFlags ConvertAspectFlags(this Format format, DepthStencilMode depthStencilMode)
        {
            return format switch
            {
                Format.D16Unorm or Format.D32Float or Format.X8UintD24Unorm => ImageAspectFlags.DepthBit,
                Format.S8Uint => ImageAspectFlags.StencilBit,
                Format.D24UnormS8Uint or
                Format.D32FloatS8Uint or
                Format.S8UintD24Unorm => depthStencilMode == DepthStencilMode.Stencil ? ImageAspectFlags.StencilBit : ImageAspectFlags.DepthBit,
                _ => ImageAspectFlags.ColorBit,
            };
        }

        public static LogicOp Convert(this LogicalOp op)
        {
            return op switch
            {
                LogicalOp.Clear => LogicOp.Clear,
                LogicalOp.And => LogicOp.And,
                LogicalOp.AndReverse => LogicOp.AndReverse,
                LogicalOp.Copy => LogicOp.Copy,
                LogicalOp.AndInverted => LogicOp.AndInverted,
                LogicalOp.Noop => LogicOp.NoOp,
                LogicalOp.Xor => LogicOp.Xor,
                LogicalOp.Or => LogicOp.Or,
                LogicalOp.Nor => LogicOp.Nor,
                LogicalOp.Equiv => LogicOp.Equivalent,
                LogicalOp.Invert => LogicOp.Invert,
                LogicalOp.OrReverse => LogicOp.OrReverse,
                LogicalOp.CopyInverted => LogicOp.CopyInverted,
                LogicalOp.OrInverted => LogicOp.OrInverted,
                LogicalOp.Nand => LogicOp.Nand,
                LogicalOp.Set => LogicOp.Set,
                _ => LogInvalidAndReturn(op, nameof(LogicalOp), LogicOp.Copy),
            };
        }

        public static BufferAllocationType Convert(this BufferAccess access)
        {
            BufferAccess memType = access & BufferAccess.MemoryTypeMask;

            if (memType == BufferAccess.HostMemory || access.HasFlag(BufferAccess.Stream))
            {
                return BufferAllocationType.HostMapped;
            }
            else if (memType == BufferAccess.DeviceMemory)
            {
                return BufferAllocationType.DeviceLocal;
            }
            else if (memType == BufferAccess.DeviceMemoryMapped)
            {
                return BufferAllocationType.DeviceLocalMapped;
            }

            return BufferAllocationType.Auto;
        }

        private static T2 LogInvalidAndReturn<T1, T2>(T1 value, string name, T2 defaultValue = default)
        {
            Logger.Debug?.Print(LogClass.Gpu, $"Invalid {name} enum value: {value}.");

            return defaultValue;
        }
    }
}
