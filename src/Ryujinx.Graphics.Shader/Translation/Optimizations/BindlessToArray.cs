using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using System;
using System.Collections.Generic;
using static Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandHelper;

namespace Ryujinx.Graphics.Shader.Translation.Optimizations
{
    static class BindlessToArray
    {
        private const int NvnTextureBufferIndex = 2;
        private const int HardcodedArrayLengthOgl = 4;

        // 1 and 0 elements are not considered arrays anymore.
        private const int MinimumArrayLength = 2;

        public static void RunPassOgl(BasicBlock block, ResourceManager resourceManager)
        {
            // We can turn a bindless texture access into a indexed access,
            // as long the following conditions are true:
            // - The handle is loaded using a LDC instruction.
            // - The handle is loaded from the constant buffer with the handles (CB2 for NVN).
            // - The load has a constant offset.
            // The base offset of the array of handles on the constant buffer is the constant offset.
            for (LinkedListNode<INode> node = block.Operations.First; node != null; node = node.Next)
            {
                if (node.Value is not TextureOperation texOp)
                {
                    continue;
                }

                if ((texOp.Flags & TextureFlags.Bindless) == 0)
                {
                    continue;
                }

                if (texOp.GetSource(0).AsgOp is not Operation handleAsgOp)
                {
                    continue;
                }

                if (handleAsgOp.Inst != Instruction.Load ||
                    handleAsgOp.StorageKind != StorageKind.ConstantBuffer ||
                    handleAsgOp.SourcesCount != 4)
                {
                    continue;
                }

                Operand ldcSrc0 = handleAsgOp.GetSource(0);

                if (ldcSrc0.Type != OperandType.Constant ||
                    !resourceManager.TryGetConstantBufferSlot(ldcSrc0.Value, out int src0CbufSlot) ||
                    src0CbufSlot != NvnTextureBufferIndex)
                {
                    continue;
                }

                Operand ldcSrc1 = handleAsgOp.GetSource(1);

                // We expect field index 0 to be accessed.
                if (ldcSrc1.Type != OperandType.Constant || ldcSrc1.Value != 0)
                {
                    continue;
                }

                Operand ldcSrc2 = handleAsgOp.GetSource(2);

                // FIXME: This is missing some checks, for example, a check to ensure that the shift value is 2.
                // Might be not worth fixing since if that doesn't kick in, the result will be no texture
                // to access anyway which is also wrong.
                // Plus this whole transform is fundamentally flawed as-is since we have no way to know the array size.
                // Eventually, this should be entirely removed in favor of a implementation that supports true bindless
                // texture access.
                if (ldcSrc2.AsgOp is not Operation shrOp || shrOp.Inst != Instruction.ShiftRightU32)
                {
                    continue;
                }

                if (shrOp.GetSource(0).AsgOp is not Operation shrOp2 || shrOp2.Inst != Instruction.ShiftRightU32)
                {
                    continue;
                }

                if (shrOp2.GetSource(0).AsgOp is not Operation addOp || addOp.Inst != Instruction.Add)
                {
                    continue;
                }

                Operand addSrc1 = addOp.GetSource(1);

                if (addSrc1.Type != OperandType.Constant)
                {
                    continue;
                }

                TurnIntoArray(resourceManager, texOp, NvnTextureBufferIndex, addSrc1.Value / 4, HardcodedArrayLengthOgl);

                Operand index = Local();

                Operand source = addOp.GetSource(0);

                Operation shrBy3 = new(Instruction.ShiftRightU32, index, source, Const(3));

                block.Operations.AddBefore(node, shrBy3);

                texOp.SetSource(0, index);
            }
        }

        public static void RunPass(BasicBlock block, ResourceManager resourceManager, IGpuAccessor gpuAccessor)
        {
            // We can turn a bindless texture access into a indexed access,
            // as long the following conditions are true:
            // - The handle is loaded using a LDC instruction.
            // - The handle is loaded from the constant buffer with the handles (CB2 for NVN).
            // - The load has a constant offset.
            // The base offset of the array of handles on the constant buffer is the constant offset.
            for (LinkedListNode<INode> node = block.Operations.First; node != null; node = node.Next)
            {
                if (node.Value is not TextureOperation texOp)
                {
                    continue;
                }

                if ((texOp.Flags & TextureFlags.Bindless) == 0)
                {
                    continue;
                }

                if (texOp.GetSource(0).AsgOp is not Operation handleAsgOp)
                {
                    continue;
                }

                int secondaryCbufSlot = 0;
                int secondaryCbufOffset = 0;
                bool hasSecondaryHandle = false;

                if (handleAsgOp.Inst == Instruction.BitwiseOr)
                {
                    Operand src0 = handleAsgOp.GetSource(0);
                    Operand src1 = handleAsgOp.GetSource(1);

                    if (src0.Type == OperandType.ConstantBuffer && src1.AsgOp is Operation)
                    {
                        handleAsgOp = src1.AsgOp as Operation;
                        secondaryCbufSlot = src0.GetCbufSlot();
                        secondaryCbufOffset = src0.GetCbufOffset();
                        hasSecondaryHandle = true;
                    }
                    else if (src0.AsgOp is Operation && src1.Type == OperandType.ConstantBuffer)
                    {
                        handleAsgOp = src0.AsgOp as Operation;
                        secondaryCbufSlot = src1.GetCbufSlot();
                        secondaryCbufOffset = src1.GetCbufOffset();
                        hasSecondaryHandle = true;
                    }
                }

                if (handleAsgOp.Inst != Instruction.Load ||
                    handleAsgOp.StorageKind != StorageKind.ConstantBuffer ||
                    handleAsgOp.SourcesCount != 4)
                {
                    continue;
                }

                Operand ldcSrc0 = handleAsgOp.GetSource(0);

                if (ldcSrc0.Type != OperandType.Constant ||
                    !resourceManager.TryGetConstantBufferSlot(ldcSrc0.Value, out int src0CbufSlot))
                {
                    continue;
                }

                Operand ldcSrc1 = handleAsgOp.GetSource(1);

                // We expect field index 0 to be accessed.
                if (ldcSrc1.Type != OperandType.Constant || ldcSrc1.Value != 0)
                {
                    continue;
                }

                Operand ldcVecIndex = handleAsgOp.GetSource(2);
                Operand ldcElemIndex = handleAsgOp.GetSource(3);

                if (ldcVecIndex.Type != OperandType.LocalVariable || ldcElemIndex.Type != OperandType.LocalVariable)
                {
                    continue;
                }

                int cbufSlot;
                int handleIndex;

                if (hasSecondaryHandle)
                {
                    cbufSlot = TextureHandle.PackSlots(src0CbufSlot, secondaryCbufSlot);
                    handleIndex = TextureHandle.PackOffsets(0, secondaryCbufOffset, TextureHandleType.SeparateSamplerHandle);
                }
                else
                {
                    cbufSlot = src0CbufSlot;
                    handleIndex = 0;
                }

                int length = Math.Max(MinimumArrayLength, gpuAccessor.QueryTextureArrayLengthFromBuffer(src0CbufSlot));

                TurnIntoArray(resourceManager, texOp, cbufSlot, handleIndex, length);

                Operand vecIndex = Local();
                Operand elemIndex = Local();
                Operand index = Local();
                Operand indexMin = Local();

                block.Operations.AddBefore(node, new Operation(Instruction.ShiftLeft, vecIndex, ldcVecIndex, Const(1)));
                block.Operations.AddBefore(node, new Operation(Instruction.ShiftRightU32, elemIndex, ldcElemIndex, Const(1)));
                block.Operations.AddBefore(node, new Operation(Instruction.Add, index, vecIndex, elemIndex));
                block.Operations.AddBefore(node, new Operation(Instruction.MinimumU32, indexMin, index, Const(length - 1)));

                texOp.SetSource(0, indexMin);
            }
        }

        private static void TurnIntoArray(ResourceManager resourceManager, TextureOperation texOp, int cbufSlot, int handleIndex, int length)
        {
            int binding = resourceManager.GetTextureOrImageBinding(
                texOp.Inst,
                texOp.Type,
                texOp.Format,
                texOp.Flags & ~TextureFlags.Bindless,
                cbufSlot,
                handleIndex,
                length);

            texOp.TurnIntoArray(binding);
        }
    }
}
