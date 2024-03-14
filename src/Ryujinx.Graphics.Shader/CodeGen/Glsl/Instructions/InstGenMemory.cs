using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Text;
using static Ryujinx.Graphics.Shader.CodeGen.Glsl.Instructions.InstGenHelper;
using static Ryujinx.Graphics.Shader.StructuredIr.InstructionInfo;

namespace Ryujinx.Graphics.Shader.CodeGen.Glsl.Instructions
{
    static class InstGenMemory
    {
        public static string ImageLoadOrStore(CodeGenContext context, AstOperation operation)
        {
            AstTextureOperation texOp = (AstTextureOperation)operation;

            bool isArray = (texOp.Type & SamplerType.Array) != 0;

            var texCallBuilder = new StringBuilder();

            if (texOp.Inst == Instruction.ImageAtomic)
            {
                texCallBuilder.Append((texOp.Flags & TextureFlags.AtomicMask) switch
                {
#pragma warning disable IDE0055 // Disable formatting
                    TextureFlags.Add        => "imageAtomicAdd",
                    TextureFlags.Minimum    => "imageAtomicMin",
                    TextureFlags.Maximum    => "imageAtomicMax",
                    TextureFlags.Increment  => "imageAtomicAdd", // TODO: Clamp value.
                    TextureFlags.Decrement  => "imageAtomicAdd", // TODO: Clamp value.
                    TextureFlags.BitwiseAnd => "imageAtomicAnd",
                    TextureFlags.BitwiseOr  => "imageAtomicOr",
                    TextureFlags.BitwiseXor => "imageAtomicXor",
                    TextureFlags.Swap       => "imageAtomicExchange",
                    TextureFlags.CAS        => "imageAtomicCompSwap",
                    _                       => "imageAtomicAdd",
#pragma warning restore IDE0055
                });
            }
            else
            {
                texCallBuilder.Append(texOp.Inst == Instruction.ImageLoad ? "imageLoad" : "imageStore");
            }

            int srcIndex = 0;

            string Src(AggregateType type)
            {
                return GetSoureExpr(context, texOp.GetSource(srcIndex++), type);
            }

            string imageName = GetImageName(context, texOp, ref srcIndex);

            texCallBuilder.Append('(');
            texCallBuilder.Append(imageName);

            int coordsCount = texOp.Type.GetDimensions();

            int pCount = coordsCount + (isArray ? 1 : 0);

            void Append(string str)
            {
                texCallBuilder.Append(", ");
                texCallBuilder.Append(str);
            }

            if (pCount > 1)
            {
                string[] elems = new string[pCount];

                for (int index = 0; index < pCount; index++)
                {
                    elems[index] = Src(AggregateType.S32);
                }

                Append($"ivec{pCount}({string.Join(", ", elems)})");
            }
            else
            {
                Append(Src(AggregateType.S32));
            }

            if (texOp.Inst == Instruction.ImageStore)
            {
                AggregateType type = texOp.Format.GetComponentType();

                string[] cElems = new string[4];

                for (int index = 0; index < 4; index++)
                {
                    if (srcIndex < texOp.SourcesCount)
                    {
                        cElems[index] = Src(type);
                    }
                    else
                    {
                        cElems[index] = type switch
                        {
                            AggregateType.S32 => NumberFormatter.FormatInt(0),
                            AggregateType.U32 => NumberFormatter.FormatUint(0),
                            _ => NumberFormatter.FormatFloat(0),
                        };
                    }
                }

                string prefix = type switch
                {
                    AggregateType.S32 => "i",
                    AggregateType.U32 => "u",
                    _ => string.Empty,
                };

                Append($"{prefix}vec4({string.Join(", ", cElems)})");
            }

            if (texOp.Inst == Instruction.ImageAtomic)
            {
                AggregateType type = texOp.Format.GetComponentType();

                if ((texOp.Flags & TextureFlags.AtomicMask) == TextureFlags.CAS)
                {
                    Append(Src(type)); // Compare value.
                }

                string value = (texOp.Flags & TextureFlags.AtomicMask) switch
                {
                    TextureFlags.Increment => NumberFormatter.FormatInt(1, type), // TODO: Clamp value
                    TextureFlags.Decrement => NumberFormatter.FormatInt(-1, type), // TODO: Clamp value
                    _ => Src(type),
                };

                Append(value);

                texCallBuilder.Append(')');

                if (type != AggregateType.S32)
                {
                    texCallBuilder
                        .Insert(0, "int(")
                        .Append(')');
                }
            }
            else
            {
                texCallBuilder.Append(')');

                if (texOp.Inst == Instruction.ImageLoad)
                {
                    texCallBuilder.Append(GetMaskMultiDest(texOp.Index));
                }
            }

            return texCallBuilder.ToString();
        }

        public static string Load(CodeGenContext context, AstOperation operation)
        {
            return GenerateLoadOrStore(context, operation, isStore: false);
        }

        public static string Lod(CodeGenContext context, AstOperation operation)
        {
            AstTextureOperation texOp = (AstTextureOperation)operation;

            int coordsCount = texOp.Type.GetDimensions();
            int coordsIndex = 0;

            string samplerName = GetSamplerName(context, texOp, ref coordsIndex);

            string coordsExpr;

            if (coordsCount > 1)
            {
                string[] elems = new string[coordsCount];

                for (int index = 0; index < coordsCount; index++)
                {
                    elems[index] = GetSoureExpr(context, texOp.GetSource(coordsIndex + index), AggregateType.FP32);
                }

                coordsExpr = "vec" + coordsCount + "(" + string.Join(", ", elems) + ")";
            }
            else
            {
                coordsExpr = GetSoureExpr(context, texOp.GetSource(coordsIndex), AggregateType.FP32);
            }

            return $"textureQueryLod({samplerName}, {coordsExpr}){GetMask(texOp.Index)}";
        }

        public static string Store(CodeGenContext context, AstOperation operation)
        {
            return GenerateLoadOrStore(context, operation, isStore: true);
        }

        public static string TextureSample(CodeGenContext context, AstOperation operation)
        {
            AstTextureOperation texOp = (AstTextureOperation)operation;

            bool isGather = (texOp.Flags & TextureFlags.Gather) != 0;
            bool hasDerivatives = (texOp.Flags & TextureFlags.Derivatives) != 0;
            bool intCoords = (texOp.Flags & TextureFlags.IntCoords) != 0;
            bool hasLodBias = (texOp.Flags & TextureFlags.LodBias) != 0;
            bool hasLodLevel = (texOp.Flags & TextureFlags.LodLevel) != 0;
            bool hasOffset = (texOp.Flags & TextureFlags.Offset) != 0;
            bool hasOffsets = (texOp.Flags & TextureFlags.Offsets) != 0;

            bool isArray = (texOp.Type & SamplerType.Array) != 0;
            bool isMultisample = (texOp.Type & SamplerType.Multisample) != 0;
            bool isShadow = (texOp.Type & SamplerType.Shadow) != 0;

            SamplerType type = texOp.Type & SamplerType.Mask;

            bool is2D = type == SamplerType.Texture2D;
            bool isCube = type == SamplerType.TextureCube;

            // 2D Array and Cube shadow samplers with LOD level or bias requires an extension.
            // If the extension is not supported, just remove the LOD parameter.
            if (isArray && isShadow && (is2D || isCube) && !context.HostCapabilities.SupportsTextureShadowLod)
            {
                hasLodBias = false;
                hasLodLevel = false;
            }

            // Cube shadow samplers with LOD level requires an extension.
            // If the extension is not supported, just remove the LOD level parameter.
            if (isShadow && isCube && !context.HostCapabilities.SupportsTextureShadowLod)
            {
                hasLodLevel = false;
            }

            string texCall = intCoords ? "texelFetch" : "texture";

            if (isGather)
            {
                texCall += "Gather";
            }
            else if (hasDerivatives)
            {
                texCall += "Grad";
            }
            else if (hasLodLevel && !intCoords)
            {
                texCall += "Lod";
            }

            if (hasOffset)
            {
                texCall += "Offset";
            }
            else if (hasOffsets)
            {
                texCall += "Offsets";
            }

            int srcIndex = 0;

            string Src(AggregateType type)
            {
                return GetSoureExpr(context, texOp.GetSource(srcIndex++), type);
            }

            string samplerName = GetSamplerName(context, texOp, ref srcIndex);

            texCall += "(" + samplerName;

            int coordsCount = texOp.Type.GetDimensions();

            int pCount = coordsCount;

            int arrayIndexElem = -1;

            if (isArray)
            {
                arrayIndexElem = pCount++;
            }

            // The sampler 1D shadow overload expects a
            // dummy value on the middle of the vector, who knows why...
            bool hasDummy1DShadowElem = texOp.Type == (SamplerType.Texture1D | SamplerType.Shadow);

            if (hasDummy1DShadowElem)
            {
                pCount++;
            }

            if (isShadow && !isGather)
            {
                pCount++;
            }

            // On textureGather*, the comparison value is
            // always specified as an extra argument.
            bool hasExtraCompareArg = isShadow && isGather;

            if (pCount == 5)
            {
                pCount = 4;

                hasExtraCompareArg = true;
            }

            void Append(string str)
            {
                texCall += ", " + str;
            }

            AggregateType coordType = intCoords ? AggregateType.S32 : AggregateType.FP32;

            string AssemblePVector(int count)
            {
                if (count > 1)
                {
                    string[] elems = new string[count];

                    for (int index = 0; index < count; index++)
                    {
                        if (arrayIndexElem == index)
                        {
                            elems[index] = Src(AggregateType.S32);

                            if (!intCoords)
                            {
                                elems[index] = "float(" + elems[index] + ")";
                            }
                        }
                        else if (index == 1 && hasDummy1DShadowElem)
                        {
                            elems[index] = NumberFormatter.FormatFloat(0);
                        }
                        else
                        {
                            elems[index] = Src(coordType);
                        }
                    }

                    string prefix = intCoords ? "i" : string.Empty;

                    return prefix + "vec" + count + "(" + string.Join(", ", elems) + ")";
                }
                else
                {
                    return Src(coordType);
                }
            }

            Append(AssemblePVector(pCount));

            string AssembleDerivativesVector(int count)
            {
                if (count > 1)
                {
                    string[] elems = new string[count];

                    for (int index = 0; index < count; index++)
                    {
                        elems[index] = Src(AggregateType.FP32);
                    }

                    return "vec" + count + "(" + string.Join(", ", elems) + ")";
                }
                else
                {
                    return Src(AggregateType.FP32);
                }
            }

            if (hasExtraCompareArg)
            {
                Append(Src(AggregateType.FP32));
            }

            if (hasDerivatives)
            {
                Append(AssembleDerivativesVector(coordsCount)); // dPdx
                Append(AssembleDerivativesVector(coordsCount)); // dPdy
            }

            if (isMultisample)
            {
                Append(Src(AggregateType.S32));
            }
            else if (hasLodLevel)
            {
                Append(Src(coordType));
            }

            string AssembleOffsetVector(int count)
            {
                if (count > 1)
                {
                    string[] elems = new string[count];

                    for (int index = 0; index < count; index++)
                    {
                        elems[index] = Src(AggregateType.S32);
                    }

                    return "ivec" + count + "(" + string.Join(", ", elems) + ")";
                }
                else
                {
                    return Src(AggregateType.S32);
                }
            }

            if (hasOffset)
            {
                Append(AssembleOffsetVector(coordsCount));
            }
            else if (hasOffsets)
            {
                texCall += $", ivec{coordsCount}[4](";

                texCall += AssembleOffsetVector(coordsCount) + ", ";
                texCall += AssembleOffsetVector(coordsCount) + ", ";
                texCall += AssembleOffsetVector(coordsCount) + ", ";
                texCall += AssembleOffsetVector(coordsCount) + ")";
            }

            if (hasLodBias)
            {
                Append(Src(AggregateType.FP32));
            }

            // textureGather* optional extra component index,
            // not needed for shadow samplers.
            if (isGather && !isShadow)
            {
                Append(Src(AggregateType.S32));
            }

            bool colorIsVector = isGather || !isShadow;

            texCall += ")" + (colorIsVector ? GetMaskMultiDest(texOp.Index) : "");

            return texCall;
        }

        public static string TextureQuerySamples(CodeGenContext context, AstOperation operation)
        {
            AstTextureOperation texOp = (AstTextureOperation)operation;

            int srcIndex = 0;

            string samplerName = GetSamplerName(context, texOp, ref srcIndex);

            return $"textureSamples({samplerName})";
        }

        public static string TextureQuerySize(CodeGenContext context, AstOperation operation)
        {
            AstTextureOperation texOp = (AstTextureOperation)operation;

            int srcIndex = 0;

            string samplerName = GetSamplerName(context, texOp, ref srcIndex);

            if (texOp.Index == 3)
            {
                return $"textureQueryLevels({samplerName})";
            }
            else
            {
                context.Properties.Textures.TryGetValue(texOp.Binding, out TextureDefinition definition);
                bool hasLod = !definition.Type.HasFlag(SamplerType.Multisample) && (definition.Type & SamplerType.Mask) != SamplerType.TextureBuffer;
                string texCall;

                if (hasLod)
                {
                    IAstNode lod = operation.GetSource(srcIndex);
                    string lodExpr = GetSoureExpr(context, lod, GetSrcVarType(operation.Inst, srcIndex));

                    texCall = $"textureSize({samplerName}, {lodExpr}){GetMask(texOp.Index)}";
                }
                else
                {
                    texCall = $"textureSize({samplerName}){GetMask(texOp.Index)}";
                }

                return texCall;
            }
        }

        public static string GenerateLoadOrStore(CodeGenContext context, AstOperation operation, bool isStore)
        {
            StorageKind storageKind = operation.StorageKind;

            string varName;
            AggregateType varType;
            int srcIndex = 0;
            bool isStoreOrAtomic = operation.Inst == Instruction.Store || operation.Inst.IsAtomic();
            int inputsCount = isStoreOrAtomic ? operation.SourcesCount - 1 : operation.SourcesCount;

            if (operation.Inst == Instruction.AtomicCompareAndSwap)
            {
                inputsCount--;
            }

            switch (storageKind)
            {
                case StorageKind.ConstantBuffer:
                case StorageKind.StorageBuffer:
                    if (operation.GetSource(srcIndex++) is not AstOperand bindingIndex || bindingIndex.Type != OperandType.Constant)
                    {
                        throw new InvalidOperationException($"First input of {operation.Inst} with {storageKind} storage must be a constant operand.");
                    }

                    int binding = bindingIndex.Value;
                    BufferDefinition buffer = storageKind == StorageKind.ConstantBuffer
                        ? context.Properties.ConstantBuffers[binding]
                        : context.Properties.StorageBuffers[binding];

                    if (operation.GetSource(srcIndex++) is not AstOperand fieldIndex || fieldIndex.Type != OperandType.Constant)
                    {
                        throw new InvalidOperationException($"Second input of {operation.Inst} with {storageKind} storage must be a constant operand.");
                    }

                    StructureField field = buffer.Type.Fields[fieldIndex.Value];
                    varName = $"{buffer.Name}.{field.Name}";
                    varType = field.Type;
                    break;

                case StorageKind.LocalMemory:
                case StorageKind.SharedMemory:
                    if (operation.GetSource(srcIndex++) is not AstOperand { Type: OperandType.Constant } bindingId)
                    {
                        throw new InvalidOperationException($"First input of {operation.Inst} with {storageKind} storage must be a constant operand.");
                    }

                    MemoryDefinition memory = storageKind == StorageKind.LocalMemory
                        ? context.Properties.LocalMemories[bindingId.Value]
                        : context.Properties.SharedMemories[bindingId.Value];

                    varName = memory.Name;
                    varType = memory.Type;
                    break;

                case StorageKind.Input:
                case StorageKind.InputPerPatch:
                case StorageKind.Output:
                case StorageKind.OutputPerPatch:
                    if (operation.GetSource(srcIndex++) is not AstOperand varId || varId.Type != OperandType.Constant)
                    {
                        throw new InvalidOperationException($"First input of {operation.Inst} with {storageKind} storage must be a constant operand.");
                    }

                    IoVariable ioVariable = (IoVariable)varId.Value;
                    bool isOutput = storageKind.IsOutput();
                    bool isPerPatch = storageKind.IsPerPatch();
                    int location = -1;
                    int component = 0;

                    if (context.Definitions.HasPerLocationInputOrOutput(ioVariable, isOutput))
                    {
                        if (operation.GetSource(srcIndex++) is not AstOperand vecIndex || vecIndex.Type != OperandType.Constant)
                        {
                            throw new InvalidOperationException($"Second input of {operation.Inst} with {storageKind} storage must be a constant operand.");
                        }

                        location = vecIndex.Value;

                        if (operation.SourcesCount > srcIndex &&
                            operation.GetSource(srcIndex) is AstOperand elemIndex &&
                            elemIndex.Type == OperandType.Constant &&
                            context.Definitions.HasPerLocationInputOrOutputComponent(ioVariable, location, elemIndex.Value, isOutput))
                        {
                            component = elemIndex.Value;
                            srcIndex++;
                        }
                    }

                    (varName, varType) = IoMap.GetGlslVariable(
                        context.Definitions,
                        context.HostCapabilities,
                        ioVariable,
                        location,
                        component,
                        isOutput,
                        isPerPatch);

                    if (IoMap.IsPerVertexBuiltIn(context.Definitions.Stage, ioVariable, isOutput))
                    {
                        // Since those exist both as input and output on geometry and tessellation shaders,
                        // we need the gl_in and gl_out prefixes to disambiguate.

                        if (storageKind == StorageKind.Input)
                        {
                            string expr = GetSoureExpr(context, operation.GetSource(srcIndex++), AggregateType.S32);
                            varName = $"gl_in[{expr}].{varName}";
                        }
                        else if (storageKind == StorageKind.Output)
                        {
                            string expr = GetSoureExpr(context, operation.GetSource(srcIndex++), AggregateType.S32);
                            varName = $"gl_out[{expr}].{varName}";
                        }
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Invalid storage kind {storageKind}.");
            }

            int firstSrcIndex = srcIndex;

            for (; srcIndex < inputsCount; srcIndex++)
            {
                IAstNode src = operation.GetSource(srcIndex);

                if ((varType & AggregateType.ElementCountMask) != 0 &&
                    srcIndex == inputsCount - 1 &&
                    src is AstOperand elementIndex &&
                    elementIndex.Type == OperandType.Constant)
                {
                    varName += "." + "xyzw"[elementIndex.Value & 3];
                }
                else if (srcIndex == firstSrcIndex && context.Definitions.Stage == ShaderStage.TessellationControl && storageKind == StorageKind.Output)
                {
                    // GLSL requires that for tessellation control shader outputs,
                    // that the index expression must be *exactly* "gl_InvocationID",
                    // otherwise the compilation fails.
                    // TODO: Get rid of this and use expression propagation to make sure we generate the correct code from IR.
                    varName += "[gl_InvocationID]";
                }
                else
                {
                    varName += $"[{GetSoureExpr(context, src, AggregateType.S32)}]";
                }
            }

            if (isStore)
            {
                varType &= AggregateType.ElementTypeMask;
                varName = $"{varName} = {GetSoureExpr(context, operation.GetSource(srcIndex), varType)}";
            }

            return varName;
        }

        private static string GetSamplerName(CodeGenContext context, AstTextureOperation texOp, ref int srcIndex)
        {
            TextureDefinition definition = context.Properties.Textures[texOp.Binding];
            string name = definition.Name;

            if (definition.ArrayLength != 1)
            {
                name = $"{name}[{GetSoureExpr(context, texOp.GetSource(srcIndex++), AggregateType.S32)}]";
            }

            return name;
        }

        private static string GetImageName(CodeGenContext context, AstTextureOperation texOp, ref int srcIndex)
        {
            TextureDefinition definition = context.Properties.Images[texOp.Binding];
            string name = definition.Name;

            if (definition.ArrayLength != 1)
            {
                name = $"{name}[{GetSoureExpr(context, texOp.GetSource(srcIndex++), AggregateType.S32)}]";
            }

            return name;
        }

        private static string GetMask(int index)
        {
            return $".{"rgba".AsSpan(index, 1)}";
        }

        private static string GetMaskMultiDest(int mask)
        {
            StringBuilder swizzleBuilder = new();
            swizzleBuilder.Append('.');

            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    swizzleBuilder.Append("xyzw"[i]);
                }
            }

            return swizzleBuilder.ToString();
        }
    }
}
