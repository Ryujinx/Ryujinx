using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenHelper;

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
                    return "Constant or Storage buffer load or store";
                case StorageKind.LocalMemory:
                case StorageKind.SharedMemory:
                    return "Local or Shader memory load or store";
                case StorageKind.Input:
                case StorageKind.InputPerPatch:
                case StorageKind.Output:
                case StorageKind.OutputPerPatch:
                    return "I/O load or store";
                default:
                    throw new InvalidOperationException($"Invalid storage kind {storageKind}.");
            }
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
    }
}
