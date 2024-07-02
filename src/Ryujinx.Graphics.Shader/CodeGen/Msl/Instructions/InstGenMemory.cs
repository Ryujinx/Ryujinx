using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenHelper;
using static Ryujinx.Graphics.Shader.StructuredIr.InstructionInfo;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGenMemory
    {
        public static string GenerateLoadOrStore(CodeGenContext context, AstOperation operation, bool isStore)
        {
            StorageKind storageKind = operation.StorageKind;

            string varName;
            AggregateType varType;
            int srcIndex = 0;
            bool isStoreOrAtomic = operation.Inst == Instruction.Store || operation.Inst.IsAtomic();
            int inputsCount = isStoreOrAtomic ? operation.SourcesCount - 1 : operation.SourcesCount;
            bool fieldHasPadding = false;

            if (operation.Inst == Instruction.AtomicCompareAndSwap)
            {
                inputsCount--;
            }

            string fieldName = "";
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

                    fieldHasPadding = buffer.Layout == BufferLayout.Std140
                                      && ((field.Type & AggregateType.Vector4) == 0)
                                      && ((field.Type & AggregateType.Array) != 0);

                    varName = storageKind == StorageKind.ConstantBuffer
                        ? "constant_buffers"
                        : "storage_buffers";
                    varName += "." + buffer.Name;
                    varName += "->" + field.Name;
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
                            context.Definitions.HasPerLocationInputOrOutputComponent(ioVariable, vecIndex.Value, elemIndex.Value, isOutput))
                        {
                            component = elemIndex.Value;
                            srcIndex++;
                        }
                    }

                    (varName, varType) = IoMap.GetMslBuiltIn(
                        context.Definitions,
                        ioVariable,
                        location,
                        component,
                        isOutput,
                        isPerPatch);
                    break;

                default:
                    throw new InvalidOperationException($"Invalid storage kind {storageKind}.");
            }

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
                else
                {
                    varName += $"[{GetSourceExpr(context, src, AggregateType.S32)}]";
                }
            }
            varName += fieldName;
            varName += fieldHasPadding ? ".x" : "";

            if (isStore)
            {
                varType &= AggregateType.ElementTypeMask;
                varName = $"{varName} = {GetSourceExpr(context, operation.GetSource(srcIndex), varType)}";
            }

            return varName;
        }

        public static string Load(CodeGenContext context, AstOperation operation)
        {
            return GenerateLoadOrStore(context, operation, isStore: false);
        }

        // TODO: check this
        public static string Lod(CodeGenContext context, AstOperation operation)
        {
            AstTextureOperation texOp = (AstTextureOperation)operation;

            int coordsCount = texOp.Type.GetDimensions();
            int coordsIndex = 0;

            string samplerName = GetSamplerName(context.Properties, texOp);

            string coordsExpr;

            if (coordsCount > 1)
            {
                string[] elems = new string[coordsCount];

                for (int index = 0; index < coordsCount; index++)
                {
                    elems[index] = GetSourceExpr(context, texOp.GetSource(coordsIndex + index), AggregateType.FP32);
                }

                coordsExpr = "float" + coordsCount + "(" + string.Join(", ", elems) + ")";
            }
            else
            {
                coordsExpr = GetSourceExpr(context, texOp.GetSource(coordsIndex), AggregateType.FP32);
            }

            return $"textures.tex_{samplerName}.calculate_unclamped_lod(textures.samp_{samplerName}, {coordsExpr}){GetMaskMultiDest(texOp.Index)}";
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
            bool isShadow = (texOp.Type & SamplerType.Shadow) != 0;

            bool colorIsVector = isGather || !isShadow;

            string samplerName = GetSamplerName(context.Properties, texOp);
            string texCall = $"textures.tex_{samplerName}";
            texCall += ".";

            int srcIndex = 0;

            string Src(AggregateType type)
            {
                return GetSourceExpr(context, texOp.GetSource(srcIndex++), type);
            }

            if (intCoords)
            {
                texCall += "read(";
            }
            else
            {
                if (isGather)
                {
                    texCall += "gather";
                }
                else
                {
                    texCall += "sample";
                }

                if (isShadow)
                {
                    texCall += "_compare";
                }

                texCall += $"(textures.samp_{samplerName}, ";
            }

            int coordsCount = texOp.Type.GetDimensions();

            int pCount = coordsCount;

            bool appended = false;
            void Append(string str)
            {
                if (appended)
                {
                    texCall += ", ";
                }
                else
                {
                    appended = true;
                }
                texCall += str;
            }

            AggregateType coordType = intCoords ? AggregateType.S32 : AggregateType.FP32;

            string AssemblePVector(int count)
            {
                string coords;
                if (count > 1)
                {
                    string[] elems = new string[count];

                    for (int index = 0; index < count; index++)
                    {
                        elems[index] = Src(coordType);
                    }

                    coords = string.Join(", ", elems);
                }
                else
                {
                    coords = Src(coordType);
                }

                string prefix = intCoords ? "uint" : "float";

                return prefix + (count > 1 ? count : "") + "(" + coords + ")";
            }

            Append(AssemblePVector(pCount));

            if (isArray)
            {
                Append(Src(AggregateType.S32));
            }

            if (isShadow)
            {
                Append(Src(AggregateType.FP32));
            }

            if (hasDerivatives)
            {
                Logger.Warning?.PrintMsg(LogClass.Gpu, "Unused sampler derivatives!");
            }

            if (hasLodBias)
            {
                Logger.Warning?.PrintMsg(LogClass.Gpu, "Unused sample LOD bias!");
            }

            if (hasLodLevel)
            {
                if (intCoords)
                {
                    Append(Src(coordType));
                }
                else
                {
                    Append($"level({Src(coordType)})");
                }
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

                    return "int" + count + "(" + string.Join(", ", elems) + ")";
                }
                else
                {
                    return Src(AggregateType.S32);
                }
            }

            // TODO: Support reads with offsets
            if (!intCoords)
            {
                if (hasOffset)
                {
                    Append(AssembleOffsetVector(coordsCount));
                }
                else if (hasOffsets)
                {
                    Logger.Warning?.PrintMsg(LogClass.Gpu, "Multiple offsets on gathers are not yet supported!");
                }
            }

            texCall += ")" + (colorIsVector ? GetMaskMultiDest(texOp.Index) : "");

            return texCall;
        }

        private static string GetSamplerName(ShaderProperties resourceDefinitions, AstTextureOperation textOp)
        {
            return resourceDefinitions.Textures[textOp.GetTextureSetAndBinding()].Name;
        }

        private static string GetMaskMultiDest(int mask)
        {
            if (mask == 0x0)
            {
                return "";
            }

            string swizzle = ".";

            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    swizzle += "xyzw"[i];
                }
            }

            return swizzle;
        }

        public static string TextureQuerySamples(CodeGenContext context, AstOperation operation)
        {
            AstTextureOperation texOp = (AstTextureOperation)operation;

            bool isBindless = (texOp.Flags & TextureFlags.Bindless) != 0;

            // TODO: Bindless texture support. For now we just return 0.
            if (isBindless)
            {
                return NumberFormatter.FormatInt(0);
            }

            string samplerName = GetSamplerName(context.Properties, texOp);
            string textureName = $"textures.tex_{samplerName}";
            string texCall = textureName + ".";
            texCall += "get_num_samples()";

            return texCall;
        }

        public static string TextureQuerySize(CodeGenContext context, AstOperation operation)
        {
            AstTextureOperation texOp = (AstTextureOperation)operation;

            string samplerName = GetSamplerName(context.Properties, texOp);
            string textureName = $"textures.tex_{samplerName}";
            string texCall = textureName + ".";

            if (texOp.Index == 3)
            {
                texCall += "get_num_mip_levels()";
            }
            else
            {
                context.Properties.Textures.TryGetValue(texOp.GetTextureSetAndBinding(), out TextureDefinition definition);
                bool hasLod = !definition.Type.HasFlag(SamplerType.Multisample) && (definition.Type & SamplerType.Mask) != SamplerType.TextureBuffer;
                texCall += "get_";

                if (texOp.Index == 0)
                {
                    texCall += "width";
                }
                else if (texOp.Index == 1)
                {
                    texCall += "height";
                }
                else
                {
                    texCall += "depth";
                }

                texCall += "(";

                if (hasLod)
                {
                    IAstNode lod = operation.GetSource(0);
                    string lodExpr = GetSourceExpr(context, lod, GetSrcVarType(operation.Inst, 0));

                    texCall += $"{lodExpr}";
                }

                texCall += ")";
            }

            return texCall;
        }

        public static string PackHalf2x16(CodeGenContext context, AstOperation operation)
        {
            IAstNode src0 = operation.GetSource(0);
            IAstNode src1 = operation.GetSource(1);

            string src0Expr = GetSourceExpr(context, src0, GetSrcVarType(operation.Inst, 0));
            string src1Expr = GetSourceExpr(context, src1, GetSrcVarType(operation.Inst, 1));

            return $"as_type<uint>(half2({src0Expr}, {src1Expr}))";
        }

        public static string UnpackHalf2x16(CodeGenContext context, AstOperation operation)
        {
            IAstNode src = operation.GetSource(0);

            string srcExpr = GetSourceExpr(context, src, GetSrcVarType(operation.Inst, 0));

            return $"float2(as_type<half2>({srcExpr})){GetMask(operation.Index)}";
        }

        private static string GetMask(int index)
        {
            return $".{"xy".AsSpan(index, 1)}";
        }
    }
}
