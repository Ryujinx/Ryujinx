using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;

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

            bool colorIsVector = isGather || !isShadow;

            string texCall = "texture.sample(";

            string samplerName = GetSamplerName(context.Properties, texOp);

            texCall += samplerName;

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
