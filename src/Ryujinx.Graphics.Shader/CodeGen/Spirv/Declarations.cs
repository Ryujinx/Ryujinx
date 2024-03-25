using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using Spv.Generator;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static Spv.Specification;
using SpvInstruction = Spv.Generator.Instruction;

namespace Ryujinx.Graphics.Shader.CodeGen.Spirv
{
    static class Declarations
    {
        public static void DeclareParameters(CodeGenContext context, StructuredFunction function)
        {
            DeclareParameters(context, function.InArguments, 0);
            DeclareParameters(context, function.OutArguments, function.InArguments.Length);
        }

        private static void DeclareParameters(CodeGenContext context, IEnumerable<AggregateType> argTypes, int argIndex)
        {
            foreach (var argType in argTypes)
            {
                var argPointerType = context.TypePointer(StorageClass.Function, context.GetType(argType));
                var spvArg = context.FunctionParameter(argPointerType);

                context.DeclareArgument(argIndex++, spvArg);
            }
        }

        public static void DeclareLocals(CodeGenContext context, StructuredFunction function)
        {
            foreach (AstOperand local in function.Locals)
            {
                var localPointerType = context.TypePointer(StorageClass.Function, context.GetType(local.VarType));
                var spvLocal = context.Variable(localPointerType, StorageClass.Function);

                context.AddLocalVariable(spvLocal);
                context.DeclareLocal(local, spvLocal);
            }
        }

        public static void DeclareAll(CodeGenContext context, StructuredProgramInfo info)
        {
            DeclareConstantBuffers(context, context.Properties.ConstantBuffers.Values);
            DeclareStorageBuffers(context, context.Properties.StorageBuffers.Values);
            DeclareMemories(context, context.Properties.LocalMemories, context.LocalMemories, StorageClass.Private);
            DeclareMemories(context, context.Properties.SharedMemories, context.SharedMemories, StorageClass.Workgroup);
            DeclareSamplers(context, context.Properties.Textures.Values);
            DeclareImages(context, context.Properties.Images.Values);
            DeclareInputsAndOutputs(context, info);
        }

        private static void DeclareMemories(
            CodeGenContext context,
            IReadOnlyDictionary<int, MemoryDefinition> memories,
            Dictionary<int, SpvInstruction> dict,
            StorageClass storage)
        {
            foreach ((int id, MemoryDefinition memory) in memories)
            {
                var pointerType = context.TypePointer(storage, context.GetType(memory.Type, memory.ArrayLength));
                var variable = context.Variable(pointerType, storage);

                context.AddGlobalVariable(variable);

                dict.Add(id, variable);
            }
        }

        private static void DeclareConstantBuffers(CodeGenContext context, IEnumerable<BufferDefinition> buffers)
        {
            DeclareBuffers(context, buffers, isBuffer: false);
        }

        private static void DeclareStorageBuffers(CodeGenContext context, IEnumerable<BufferDefinition> buffers)
        {
            DeclareBuffers(context, buffers, isBuffer: true);
        }

        private static void DeclareBuffers(CodeGenContext context, IEnumerable<BufferDefinition> buffers, bool isBuffer)
        {
            HashSet<SpvInstruction> decoratedTypes = new();

            foreach (BufferDefinition buffer in buffers)
            {
                int setIndex = context.TargetApi == TargetApi.Vulkan ? buffer.Set : 0;
                int alignment = buffer.Layout == BufferLayout.Std140 ? 16 : 4;
                int alignmentMask = alignment - 1;
                int offset = 0;

                SpvInstruction[] structFieldTypes = new SpvInstruction[buffer.Type.Fields.Length];
                int[] structFieldOffsets = new int[buffer.Type.Fields.Length];

                for (int fieldIndex = 0; fieldIndex < buffer.Type.Fields.Length; fieldIndex++)
                {
                    StructureField field = buffer.Type.Fields[fieldIndex];
                    int fieldSize = (field.Type.GetSizeInBytes() + alignmentMask) & ~alignmentMask;

                    structFieldTypes[fieldIndex] = context.GetType(field.Type, field.ArrayLength);
                    structFieldOffsets[fieldIndex] = offset;

                    if (field.Type.HasFlag(AggregateType.Array))
                    {
                        // We can't decorate the type more than once.
                        if (decoratedTypes.Add(structFieldTypes[fieldIndex]))
                        {
                            context.Decorate(structFieldTypes[fieldIndex], Decoration.ArrayStride, (LiteralInteger)fieldSize);
                        }

                        // Zero lengths are assumed to be a "runtime array" (which does not have a explicit length
                        // specified on the shader, and instead assumes the bound buffer length).
                        // It is only valid as the last struct element.

                        Debug.Assert(field.ArrayLength > 0 || fieldIndex == buffer.Type.Fields.Length - 1);

                        offset += fieldSize * field.ArrayLength;
                    }
                    else
                    {
                        offset += fieldSize;
                    }
                }

                var structType = context.TypeStruct(false, structFieldTypes);

                if (decoratedTypes.Add(structType))
                {
                    context.Decorate(structType, isBuffer ? Decoration.BufferBlock : Decoration.Block);

                    for (int fieldIndex = 0; fieldIndex < structFieldOffsets.Length; fieldIndex++)
                    {
                        context.MemberDecorate(structType, fieldIndex, Decoration.Offset, (LiteralInteger)structFieldOffsets[fieldIndex]);
                    }
                }

                var pointerType = context.TypePointer(StorageClass.Uniform, structType);
                var variable = context.Variable(pointerType, StorageClass.Uniform);

                context.Name(variable, buffer.Name);
                context.Decorate(variable, Decoration.DescriptorSet, (LiteralInteger)setIndex);
                context.Decorate(variable, Decoration.Binding, (LiteralInteger)buffer.Binding);
                context.AddGlobalVariable(variable);

                if (isBuffer)
                {
                    context.StorageBuffers.Add(buffer.Binding, variable);
                }
                else
                {
                    context.ConstantBuffers.Add(buffer.Binding, variable);
                }
            }
        }

        private static void DeclareSamplers(CodeGenContext context, IEnumerable<TextureDefinition> samplers)
        {
            foreach (var sampler in samplers)
            {
                int setIndex = context.TargetApi == TargetApi.Vulkan ? sampler.Set : 0;

                var dim = (sampler.Type & SamplerType.Mask) switch
                {
                    SamplerType.Texture1D => Dim.Dim1D,
                    SamplerType.Texture2D => Dim.Dim2D,
                    SamplerType.Texture3D => Dim.Dim3D,
                    SamplerType.TextureCube => Dim.Cube,
                    SamplerType.TextureBuffer => Dim.Buffer,
                    _ => throw new InvalidOperationException($"Invalid sampler type \"{sampler.Type & SamplerType.Mask}\"."),
                };

                var imageType = context.TypeImage(
                    context.TypeFP32(),
                    dim,
                    sampler.Type.HasFlag(SamplerType.Shadow),
                    sampler.Type.HasFlag(SamplerType.Array),
                    sampler.Type.HasFlag(SamplerType.Multisample),
                    1,
                    ImageFormat.Unknown);

                var sampledImageType = context.TypeSampledImage(imageType);
                var sampledImagePointerType = context.TypePointer(StorageClass.UniformConstant, sampledImageType);
                var sampledImageVariable = context.Variable(sampledImagePointerType, StorageClass.UniformConstant);

                context.Samplers.Add(sampler.Binding, (imageType, sampledImageType, sampledImageVariable));
                context.SamplersTypes.Add(sampler.Binding, sampler.Type);

                context.Name(sampledImageVariable, sampler.Name);
                context.Decorate(sampledImageVariable, Decoration.DescriptorSet, (LiteralInteger)setIndex);
                context.Decorate(sampledImageVariable, Decoration.Binding, (LiteralInteger)sampler.Binding);
                context.AddGlobalVariable(sampledImageVariable);
            }
        }

        private static void DeclareImages(CodeGenContext context, IEnumerable<TextureDefinition> images)
        {
            foreach (var image in images)
            {
                int setIndex = context.TargetApi == TargetApi.Vulkan ? image.Set : 0;

                var dim = GetDim(image.Type);

                var imageType = context.TypeImage(
                    context.GetType(image.Format.GetComponentType()),
                    dim,
                    image.Type.HasFlag(SamplerType.Shadow),
                    image.Type.HasFlag(SamplerType.Array),
                    image.Type.HasFlag(SamplerType.Multisample),
                    AccessQualifier.ReadWrite,
                    GetImageFormat(image.Format));

                var imagePointerType = context.TypePointer(StorageClass.UniformConstant, imageType);
                var imageVariable = context.Variable(imagePointerType, StorageClass.UniformConstant);

                context.Images.Add(image.Binding, (imageType, imageVariable));

                context.Name(imageVariable, image.Name);
                context.Decorate(imageVariable, Decoration.DescriptorSet, (LiteralInteger)setIndex);
                context.Decorate(imageVariable, Decoration.Binding, (LiteralInteger)image.Binding);

                if (image.Flags.HasFlag(TextureUsageFlags.ImageCoherent))
                {
                    context.Decorate(imageVariable, Decoration.Coherent);
                }

                context.AddGlobalVariable(imageVariable);
            }
        }

        private static Dim GetDim(SamplerType type)
        {
            return (type & SamplerType.Mask) switch
            {
                SamplerType.Texture1D => Dim.Dim1D,
                SamplerType.Texture2D => Dim.Dim2D,
                SamplerType.Texture3D => Dim.Dim3D,
                SamplerType.TextureCube => Dim.Cube,
                SamplerType.TextureBuffer => Dim.Buffer,
                _ => throw new ArgumentException($"Invalid sampler type \"{type & SamplerType.Mask}\"."),
            };
        }

        private static ImageFormat GetImageFormat(TextureFormat format)
        {
            return format switch
            {
                TextureFormat.Unknown => ImageFormat.Unknown,
                TextureFormat.R8Unorm => ImageFormat.R8,
                TextureFormat.R8Snorm => ImageFormat.R8Snorm,
                TextureFormat.R8Uint => ImageFormat.R8ui,
                TextureFormat.R8Sint => ImageFormat.R8i,
                TextureFormat.R16Float => ImageFormat.R16f,
                TextureFormat.R16Unorm => ImageFormat.R16,
                TextureFormat.R16Snorm => ImageFormat.R16Snorm,
                TextureFormat.R16Uint => ImageFormat.R16ui,
                TextureFormat.R16Sint => ImageFormat.R16i,
                TextureFormat.R32Float => ImageFormat.R32f,
                TextureFormat.R32Uint => ImageFormat.R32ui,
                TextureFormat.R32Sint => ImageFormat.R32i,
                TextureFormat.R8G8Unorm => ImageFormat.Rg8,
                TextureFormat.R8G8Snorm => ImageFormat.Rg8Snorm,
                TextureFormat.R8G8Uint => ImageFormat.Rg8ui,
                TextureFormat.R8G8Sint => ImageFormat.Rg8i,
                TextureFormat.R16G16Float => ImageFormat.Rg16f,
                TextureFormat.R16G16Unorm => ImageFormat.Rg16,
                TextureFormat.R16G16Snorm => ImageFormat.Rg16Snorm,
                TextureFormat.R16G16Uint => ImageFormat.Rg16ui,
                TextureFormat.R16G16Sint => ImageFormat.Rg16i,
                TextureFormat.R32G32Float => ImageFormat.Rg32f,
                TextureFormat.R32G32Uint => ImageFormat.Rg32ui,
                TextureFormat.R32G32Sint => ImageFormat.Rg32i,
                TextureFormat.R8G8B8A8Unorm => ImageFormat.Rgba8,
                TextureFormat.R8G8B8A8Snorm => ImageFormat.Rgba8Snorm,
                TextureFormat.R8G8B8A8Uint => ImageFormat.Rgba8ui,
                TextureFormat.R8G8B8A8Sint => ImageFormat.Rgba8i,
                TextureFormat.R16G16B16A16Float => ImageFormat.Rgba16f,
                TextureFormat.R16G16B16A16Unorm => ImageFormat.Rgba16,
                TextureFormat.R16G16B16A16Snorm => ImageFormat.Rgba16Snorm,
                TextureFormat.R16G16B16A16Uint => ImageFormat.Rgba16ui,
                TextureFormat.R16G16B16A16Sint => ImageFormat.Rgba16i,
                TextureFormat.R32G32B32A32Float => ImageFormat.Rgba32f,
                TextureFormat.R32G32B32A32Uint => ImageFormat.Rgba32ui,
                TextureFormat.R32G32B32A32Sint => ImageFormat.Rgba32i,
                TextureFormat.R10G10B10A2Unorm => ImageFormat.Rgb10A2,
                TextureFormat.R10G10B10A2Uint => ImageFormat.Rgb10a2ui,
                TextureFormat.R11G11B10Float => ImageFormat.R11fG11fB10f,
                _ => throw new ArgumentException($"Invalid texture format \"{format}\"."),
            };
        }

        private static void DeclareInputsAndOutputs(CodeGenContext context, StructuredProgramInfo info)
        {
            int firstLocation = int.MaxValue;

            if (context.Definitions.Stage == ShaderStage.Fragment && context.Definitions.DualSourceBlend)
            {
                foreach (var ioDefinition in info.IoDefinitions)
                {
                    if (ioDefinition.IoVariable == IoVariable.FragmentOutputColor && ioDefinition.Location < firstLocation)
                    {
                        firstLocation = ioDefinition.Location;
                    }
                }
            }

            foreach (var ioDefinition in info.IoDefinitions)
            {
                PixelImap iq = PixelImap.Unused;

                if (context.Definitions.Stage == ShaderStage.Fragment)
                {
                    var ioVariable = ioDefinition.IoVariable;
                    if (ioVariable == IoVariable.UserDefined)
                    {
                        iq = context.Definitions.ImapTypes[ioDefinition.Location].GetFirstUsedType();
                    }
                    else
                    {
                        (_, AggregateType varType) = IoMap.GetSpirvBuiltIn(ioVariable);
                        AggregateType elemType = varType & AggregateType.ElementTypeMask;

                        if (elemType is AggregateType.S32 or AggregateType.U32)
                        {
                            iq = PixelImap.Constant;
                        }
                    }
                }
                else if (IoMap.IsPerVertexBuiltIn(ioDefinition.IoVariable))
                {
                    continue;
                }

                bool isOutput = ioDefinition.StorageKind.IsOutput();
                bool isPerPatch = ioDefinition.StorageKind.IsPerPatch();

                DeclareInputOrOutput(context, ioDefinition, isOutput, isPerPatch, iq, firstLocation);
            }

            DeclarePerVertexBlock(context);
        }

        private static void DeclarePerVertexBlock(CodeGenContext context)
        {
            if (context.Definitions.Stage.IsVtg())
            {
                if (context.Definitions.Stage != ShaderStage.Vertex)
                {
                    var perVertexInputStructType = CreatePerVertexStructType(context);
                    int arraySize = context.Definitions.Stage == ShaderStage.Geometry ? context.Definitions.InputTopology.ToInputVertices() : 32;
                    var perVertexInputArrayType = context.TypeArray(perVertexInputStructType, context.Constant(context.TypeU32(), arraySize));
                    var perVertexInputPointerType = context.TypePointer(StorageClass.Input, perVertexInputArrayType);
                    var perVertexInputVariable = context.Variable(perVertexInputPointerType, StorageClass.Input);

                    context.Name(perVertexInputVariable, "gl_in");

                    context.AddGlobalVariable(perVertexInputVariable);
                    context.Inputs.Add(new IoDefinition(StorageKind.Input, IoVariable.Position), perVertexInputVariable);

                    if (context.Definitions.Stage == ShaderStage.Geometry &&
                        context.Definitions.GpPassthrough &&
                        context.HostCapabilities.SupportsGeometryShaderPassthrough)
                    {
                        context.MemberDecorate(perVertexInputStructType, 0, Decoration.PassthroughNV);
                        context.MemberDecorate(perVertexInputStructType, 1, Decoration.PassthroughNV);
                        context.MemberDecorate(perVertexInputStructType, 2, Decoration.PassthroughNV);
                        context.MemberDecorate(perVertexInputStructType, 3, Decoration.PassthroughNV);
                    }
                }

                var perVertexOutputStructType = CreatePerVertexStructType(context);

                void DecorateTfo(IoVariable ioVariable, int fieldIndex)
                {
                    if (context.Definitions.TryGetTransformFeedbackOutput(ioVariable, 0, 0, out var transformFeedbackOutput))
                    {
                        context.MemberDecorate(perVertexOutputStructType, fieldIndex, Decoration.XfbBuffer, (LiteralInteger)transformFeedbackOutput.Buffer);
                        context.MemberDecorate(perVertexOutputStructType, fieldIndex, Decoration.XfbStride, (LiteralInteger)transformFeedbackOutput.Stride);
                        context.MemberDecorate(perVertexOutputStructType, fieldIndex, Decoration.Offset, (LiteralInteger)transformFeedbackOutput.Offset);
                    }
                }

                DecorateTfo(IoVariable.Position, 0);
                DecorateTfo(IoVariable.PointSize, 1);
                DecorateTfo(IoVariable.ClipDistance, 2);

                SpvInstruction perVertexOutputArrayType;

                if (context.Definitions.Stage == ShaderStage.TessellationControl)
                {
                    int arraySize = context.Definitions.ThreadsPerInputPrimitive;
                    perVertexOutputArrayType = context.TypeArray(perVertexOutputStructType, context.Constant(context.TypeU32(), arraySize));
                }
                else
                {
                    perVertexOutputArrayType = perVertexOutputStructType;
                }

                var perVertexOutputPointerType = context.TypePointer(StorageClass.Output, perVertexOutputArrayType);
                var perVertexOutputVariable = context.Variable(perVertexOutputPointerType, StorageClass.Output);

                context.AddGlobalVariable(perVertexOutputVariable);
                context.Outputs.Add(new IoDefinition(StorageKind.Output, IoVariable.Position), perVertexOutputVariable);
            }
        }

        private static SpvInstruction CreatePerVertexStructType(CodeGenContext context)
        {
            var vec4FloatType = context.TypeVector(context.TypeFP32(), 4);
            var floatType = context.TypeFP32();
            var array8FloatType = context.TypeArray(context.TypeFP32(), context.Constant(context.TypeU32(), 8));
            var array1FloatType = context.TypeArray(context.TypeFP32(), context.Constant(context.TypeU32(), 1));

            var perVertexStructType = context.TypeStruct(true, vec4FloatType, floatType, array8FloatType, array1FloatType);

            context.Name(perVertexStructType, "gl_PerVertex");

            context.MemberName(perVertexStructType, 0, "gl_Position");
            context.MemberName(perVertexStructType, 1, "gl_PointSize");
            context.MemberName(perVertexStructType, 2, "gl_ClipDistance");
            context.MemberName(perVertexStructType, 3, "gl_CullDistance");

            context.Decorate(perVertexStructType, Decoration.Block);

            if (context.HostCapabilities.ReducedPrecision)
            {
                context.MemberDecorate(perVertexStructType, 0, Decoration.Invariant);
            }

            context.MemberDecorate(perVertexStructType, 0, Decoration.BuiltIn, (LiteralInteger)BuiltIn.Position);
            context.MemberDecorate(perVertexStructType, 1, Decoration.BuiltIn, (LiteralInteger)BuiltIn.PointSize);
            context.MemberDecorate(perVertexStructType, 2, Decoration.BuiltIn, (LiteralInteger)BuiltIn.ClipDistance);
            context.MemberDecorate(perVertexStructType, 3, Decoration.BuiltIn, (LiteralInteger)BuiltIn.CullDistance);

            return perVertexStructType;
        }

        private static void DeclareInputOrOutput(
            CodeGenContext context,
            IoDefinition ioDefinition,
            bool isOutput,
            bool isPerPatch,
            PixelImap iq = PixelImap.Unused,
            int firstLocation = 0)
        {
            IoVariable ioVariable = ioDefinition.IoVariable;
            var storageClass = isOutput ? StorageClass.Output : StorageClass.Input;

            bool isBuiltIn;
            BuiltIn builtIn = default;
            AggregateType varType;

            if (ioVariable == IoVariable.UserDefined)
            {
                varType = context.Definitions.GetUserDefinedType(ioDefinition.Location, isOutput);
                isBuiltIn = false;
            }
            else if (ioVariable == IoVariable.FragmentOutputColor)
            {
                varType = context.Definitions.GetFragmentOutputColorType(ioDefinition.Location);
                isBuiltIn = false;
            }
            else
            {
                (builtIn, varType) = IoMap.GetSpirvBuiltIn(ioVariable);
                isBuiltIn = true;

                if (varType == AggregateType.Invalid)
                {
                    throw new InvalidOperationException($"Unknown variable {ioVariable}.");
                }
            }

            bool hasComponent = context.Definitions.HasPerLocationInputOrOutputComponent(ioVariable, ioDefinition.Location, ioDefinition.Component, isOutput);

            if (hasComponent)
            {
                varType &= AggregateType.ElementTypeMask;
            }
            else if (ioVariable == IoVariable.UserDefined && context.Definitions.HasTransformFeedbackOutputs(isOutput))
            {
                varType &= AggregateType.ElementTypeMask;
                varType |= context.Definitions.GetTransformFeedbackOutputComponents(ioDefinition.Location, ioDefinition.Component) switch
                {
                    2 => AggregateType.Vector2,
                    3 => AggregateType.Vector3,
                    4 => AggregateType.Vector4,
                    _ => AggregateType.Invalid,
                };
            }

            var spvType = context.GetType(varType, IoMap.GetSpirvBuiltInArrayLength(ioVariable));
            bool builtInPassthrough = false;

            if (!isPerPatch && IoMap.IsPerVertex(ioVariable, context.Definitions.Stage, isOutput))
            {
                int arraySize = context.Definitions.Stage == ShaderStage.Geometry ? context.Definitions.InputTopology.ToInputVertices() : 32;
                spvType = context.TypeArray(spvType, context.Constant(context.TypeU32(), arraySize));

                if (context.Definitions.GpPassthrough && context.HostCapabilities.SupportsGeometryShaderPassthrough)
                {
                    builtInPassthrough = true;
                }
            }

            if (context.Definitions.Stage == ShaderStage.TessellationControl && isOutput && !isPerPatch)
            {
                spvType = context.TypeArray(spvType, context.Constant(context.TypeU32(), context.Definitions.ThreadsPerInputPrimitive));
            }

            var spvPointerType = context.TypePointer(storageClass, spvType);
            var spvVar = context.Variable(spvPointerType, storageClass);

            if (builtInPassthrough)
            {
                context.Decorate(spvVar, Decoration.PassthroughNV);
            }

            if (isBuiltIn)
            {
                if (isPerPatch)
                {
                    context.Decorate(spvVar, Decoration.Patch);
                }

                if (context.HostCapabilities.ReducedPrecision && ioVariable == IoVariable.Position)
                {
                    context.Decorate(spvVar, Decoration.Invariant);
                }

                context.Decorate(spvVar, Decoration.BuiltIn, (LiteralInteger)builtIn);
            }
            else if (isPerPatch)
            {
                context.Decorate(spvVar, Decoration.Patch);

                if (ioVariable == IoVariable.UserDefined)
                {
                    int location = context.AttributeUsage.GetPerPatchAttributeLocation(ioDefinition.Location);

                    context.Decorate(spvVar, Decoration.Location, (LiteralInteger)location);
                }
            }
            else if (ioVariable == IoVariable.UserDefined)
            {
                context.Decorate(spvVar, Decoration.Location, (LiteralInteger)ioDefinition.Location);

                if (hasComponent)
                {
                    context.Decorate(spvVar, Decoration.Component, (LiteralInteger)ioDefinition.Component);
                }

                if (!isOutput &&
                    !isPerPatch &&
                    (context.AttributeUsage.PassthroughAttributes & (1 << ioDefinition.Location)) != 0 &&
                    context.HostCapabilities.SupportsGeometryShaderPassthrough)
                {
                    context.Decorate(spvVar, Decoration.PassthroughNV);
                }
            }
            else if (ioVariable == IoVariable.FragmentOutputColor)
            {
                int location = ioDefinition.Location;

                if (context.Definitions.Stage == ShaderStage.Fragment && context.Definitions.DualSourceBlend)
                {
                    int index = location - firstLocation;

                    if ((uint)index < 2)
                    {
                        context.Decorate(spvVar, Decoration.Location, (LiteralInteger)firstLocation);
                        context.Decorate(spvVar, Decoration.Index, (LiteralInteger)index);
                    }
                    else
                    {
                        context.Decorate(spvVar, Decoration.Location, (LiteralInteger)location);
                    }
                }
                else
                {
                    context.Decorate(spvVar, Decoration.Location, (LiteralInteger)location);
                }
            }

            if (!isOutput)
            {
                switch (iq)
                {
                    case PixelImap.Constant:
                        context.Decorate(spvVar, Decoration.Flat);
                        break;
                    case PixelImap.ScreenLinear:
                        context.Decorate(spvVar, Decoration.NoPerspective);
                        break;
                }
            }
            else if (context.Definitions.TryGetTransformFeedbackOutput(
                ioVariable,
                ioDefinition.Location,
                ioDefinition.Component,
                out var transformFeedbackOutput))
            {
                context.Decorate(spvVar, Decoration.XfbBuffer, (LiteralInteger)transformFeedbackOutput.Buffer);
                context.Decorate(spvVar, Decoration.XfbStride, (LiteralInteger)transformFeedbackOutput.Stride);
                context.Decorate(spvVar, Decoration.Offset, (LiteralInteger)transformFeedbackOutput.Offset);
            }

            context.AddGlobalVariable(spvVar);

            var dict = isPerPatch
                ? (isOutput ? context.OutputsPerPatch : context.InputsPerPatch)
                : (isOutput ? context.Outputs : context.Inputs);
            dict.Add(ioDefinition, spvVar);
        }
    }
}
