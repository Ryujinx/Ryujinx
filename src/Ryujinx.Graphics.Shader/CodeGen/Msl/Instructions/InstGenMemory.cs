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

        public static string Store(CodeGenContext context, AstOperation operation)
        {
            return GenerateLoadOrStore(context, operation, isStore: true);
        }

        public static string TextureSample(CodeGenContext context, AstOperation operation)
        {
            AstTextureOperation texOp = (AstTextureOperation)operation;

            bool isGather = (texOp.Flags & TextureFlags.Gather) != 0;
            bool isShadow = (texOp.Type & SamplerType.Shadow) != 0;
            bool intCoords = (texOp.Flags & TextureFlags.IntCoords) != 0;

            bool isArray = (texOp.Type & SamplerType.Array) != 0;

            bool colorIsVector = isGather || !isShadow;

            string texCall = "texture.";

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
                texCall += "sample(";

                string samplerName = GetSamplerName(context.Properties, texOp);

                texCall += samplerName;
            }

            int coordsCount = texOp.Type.GetDimensions();

            int pCount = coordsCount;

            int arrayIndexElem = -1;

            if (isArray)
            {
                arrayIndexElem = pCount++;
            }

            if (isShadow && !isGather)
            {
                pCount++;
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
                        else
                        {
                            elems[index] = Src(coordType);
                        }
                    }

                    string prefix = intCoords ? "int" : "float";

                    return prefix + count + "(" + string.Join(", ", elems) + ")";
                }
                else
                {
                    return Src(coordType);
                }
            }

            Append(AssemblePVector(pCount));

            texCall += ")" + (colorIsVector ? GetMaskMultiDest(texOp.Index) : "");

            return texCall;
        }

        private static string GetSamplerName(ShaderProperties resourceDefinitions, AstTextureOperation textOp)
        {
            return resourceDefinitions.Textures[textOp.Binding].Name;
        }

        // TODO: Verify that this is valid in MSL
        private static string GetMask(int index)
        {
            return $".{"rgba".AsSpan(index, 1)}";
        }

        private static string GetMaskMultiDest(int mask)
        {
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

            string textureName = "texture";
            string texCall = textureName + ".";
            texCall += $"get_num_samples()";

            return texCall;
        }

        public static string TextureQuerySize(CodeGenContext context, AstOperation operation)
        {
            AstTextureOperation texOp = (AstTextureOperation)operation;

            string textureName = "texture";
            string texCall = textureName + ".";

            if (texOp.Index == 3)
            {
                texCall += $"get_num_mip_levels()";
            }
            else
            {
                context.Properties.Textures.TryGetValue(texOp.Binding, out TextureDefinition definition);
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

                texCall += $"){GetMask(texOp.Index)}";
            }

            return texCall;
        }
    }
}
