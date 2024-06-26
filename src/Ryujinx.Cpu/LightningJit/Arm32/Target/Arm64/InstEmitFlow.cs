using ARMeilleure.Common;
using Ryujinx.Cpu.LightningJit.CodeGen;
using Ryujinx.Cpu.LightningJit.CodeGen.Arm64;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Ryujinx.Cpu.LightningJit.Arm32.Target.Arm64
{
    static class InstEmitFlow
    {
        private const int SpIndex = 31;

        public static void B(CodeGenContext context, int imm, ArmCondition condition)
        {
            context.AddPendingBranch(InstName.B, imm);

            if (condition == ArmCondition.Al)
            {
                context.Arm64Assembler.B(0);
            }
            else
            {
                context.Arm64Assembler.B(condition, 0);
            }
        }

        public static void Bl(CodeGenContext context, int imm, bool sourceIsThumb, bool targetIsThumb)
        {
            uint nextAddress = sourceIsThumb ? context.Pc | 1u : context.Pc - 4;
            uint targetAddress = targetIsThumb ? context.Pc + (uint)imm : (context.Pc & ~3u) + (uint)imm;

            if (sourceIsThumb != targetIsThumb)
            {
                if (targetIsThumb)
                {
                    InstEmitCommon.SetThumbFlag(context);
                }
                else
                {
                    InstEmitCommon.ClearThumbFlag(context);
                }
            }

            context.AddPendingCall(targetAddress, nextAddress);

            context.Arm64Assembler.B(0);
        }

        public static void Blx(CodeGenContext context, uint rm, bool sourceIsThumb)
        {
            Operand rmOperand = InstEmitCommon.GetInputGpr(context, rm);

            InstEmitCommon.SetThumbFlag(context, rmOperand);

            uint nextAddress = sourceIsThumb ? (context.Pc - 2) | 1u : context.Pc - 4;

            context.AddPendingIndirectCall(rm, nextAddress);

            context.Arm64Assembler.B(0);
        }

        public static void Bx(CodeGenContext context, uint rm)
        {
            Operand rmOperand = InstEmitCommon.GetInputGpr(context, rm);

            InstEmitCommon.SetThumbFlag(context, rmOperand);

            context.AddPendingIndirectBranch(InstName.Bx, rm);

            context.Arm64Assembler.B(0);
        }

        public static void Cbnz(CodeGenContext context, uint rn, int imm, bool op)
        {
            Operand rnOperand = InstEmitCommon.GetInputGpr(context, rn);

            context.AddPendingBranch(InstName.Cbnz, imm);

            if (op)
            {
                context.Arm64Assembler.Cbnz(rnOperand, 0);
            }
            else
            {
                context.Arm64Assembler.Cbz(rnOperand, 0);
            }
        }

        public static void It(CodeGenContext context, uint firstCond, uint mask)
        {
            Debug.Assert(mask != 0);

            int instCount = 4 - BitOperations.TrailingZeroCount(mask);

            Span<ArmCondition> conditions = stackalloc ArmCondition[instCount];

            int i = 0;

            for (int index = 5 - instCount; index < 4; index++)
            {
                bool invert = (mask & (1u << index)) != 0;

                if (invert)
                {
                    conditions[i++] = ((ArmCondition)firstCond).Invert();
                }
                else
                {
                    conditions[i++] = (ArmCondition)firstCond;
                }
            }

            conditions[i] = (ArmCondition)firstCond;

            context.SetItBlockStart(conditions);
        }

        public static void Tbb(CodeGenContext context, uint rn, uint rm, bool h)
        {
            context.Arm64Assembler.Mov(context.RegisterAllocator.RemapGprRegister(RegisterUtils.PcRegister), context.Pc);

            context.AddPendingTableBranch(rn, rm, h);

            context.Arm64Assembler.B(0);
        }

        public unsafe static void WriteCallWithGuestAddress(
            CodeWriter writer,
            ref Assembler asm,
            RegisterAllocator regAlloc,
            TailMerger tailMerger,
            Action writeEpilogue,
            AddressTable<ulong> funcTable,
            IntPtr funcPtr,
            int spillBaseOffset,
            uint nextAddress,
            Operand guestAddress,
            bool isTail = false)
        {
            int tempRegister;
            int tempGuestAddress = -1;

            bool inlineLookup = guestAddress.Kind != OperandKind.Constant && funcTable != null && funcTable.Sparse && funcTable.Levels.Length == 2;

            if (guestAddress.Kind == OperandKind.Constant)
            {
                tempRegister = regAlloc.AllocateTempGprRegister();

                asm.Mov(Register(tempRegister), guestAddress.Value);
                asm.StrRiUn(Register(tempRegister), Register(regAlloc.FixedContextRegister), NativeContextOffsets.DispatchAddressOffset);

                regAlloc.FreeTempGprRegister(tempRegister);
            }
            else
            {
                asm.StrRiUn(guestAddress, Register(regAlloc.FixedContextRegister), NativeContextOffsets.DispatchAddressOffset);

                if (inlineLookup && guestAddress.Value == 0)
                {
                    // X0 will be overwritten. Move the address to a temp register.
                    tempGuestAddress = regAlloc.AllocateTempGprRegister();
                    asm.Mov(Register(tempGuestAddress), guestAddress);
                }
            }

            tempRegister = NextFreeRegister(1, tempGuestAddress);

            if (!isTail)
            {
                WriteSpillSkipContext(ref asm, regAlloc, spillBaseOffset);
            }

            Operand rn = Register(tempRegister);

            if (regAlloc.FixedContextRegister != 0)
            {
                asm.Mov(Register(0), Register(regAlloc.FixedContextRegister));
            }

            if (guestAddress.Kind == OperandKind.Constant && funcTable != null)
            {
                ulong funcPtrLoc = (ulong)Unsafe.AsPointer(ref funcTable.GetValue(guestAddress.Value));

                asm.Mov(rn, funcPtrLoc & ~0xfffUL);
                asm.LdrRiUn(rn, rn, (int)(funcPtrLoc & 0xfffUL));
            }
            else if (inlineLookup)
            {
                // Inline table lookup. Only enabled when the sparse function table is enabled with 2 levels.

                Operand indexReg = Register(NextFreeRegister(tempRegister + 1, tempGuestAddress));

                if (tempGuestAddress != -1)
                {
                    guestAddress = Register(tempGuestAddress);
                }

                var level0 = funcTable.Levels[0];
                asm.Ubfx(indexReg, guestAddress, level0.Index, level0.Length);
                asm.Lsl(indexReg, indexReg, Const(3));

                ulong tableBase = (ulong)funcTable.Base;

                // Index into the table.
                asm.Mov(rn, tableBase);
                asm.Add(rn, rn, indexReg);

                // Load the page address.
                asm.LdrRiUn(rn, rn, 0);

                var level1 = funcTable.Levels[1];
                asm.Ubfx(indexReg, guestAddress, level1.Index, level1.Length);
                asm.Lsl(indexReg, indexReg, Const(3));

                // Index into the page.
                asm.Add(rn, rn, indexReg);

                // Load the final branch address
                asm.LdrRiUn(rn, rn, 0);

                if (tempGuestAddress != -1)
                {
                    regAlloc.FreeTempGprRegister(tempGuestAddress);
                }
            }
            else
            {
                asm.Mov(rn, (ulong)funcPtr);
            }

            if (isTail)
            {
                writeEpilogue();
                asm.Br(rn);
            }
            else
            {
                asm.Blr(rn);

                asm.Mov(rn, nextAddress);
                asm.Cmp(Register(0), rn);

                tailMerger.AddConditionalReturn(writer, asm, ArmCondition.Ne);

                WriteFillSkipContext(ref asm, regAlloc, spillBaseOffset);
            }
        }

        public static void WriteSpillSkipContext(ref Assembler asm, RegisterAllocator regAlloc, int spillOffset)
        {
            WriteSpillOrFillSkipContext(ref asm, regAlloc, spillOffset, spill: true);
        }

        public static void WriteFillSkipContext(ref Assembler asm, RegisterAllocator regAlloc, int spillOffset)
        {
            WriteSpillOrFillSkipContext(ref asm, regAlloc, spillOffset, spill: false);
        }

        private static void WriteSpillOrFillSkipContext(ref Assembler asm, RegisterAllocator regAlloc, int spillOffset, bool spill)
        {
            uint gprMask = regAlloc.UsedGprsMask & ((1u << regAlloc.FixedContextRegister) | (1u << regAlloc.FixedPageTableRegister));

            while (gprMask != 0)
            {
                int reg = BitOperations.TrailingZeroCount(gprMask);

                if (reg < 31 && (gprMask & (2u << reg)) != 0 && spillOffset < RegisterSaveRestore.Encodable9BitsOffsetLimit)
                {
                    if (spill)
                    {
                        asm.StpRiUn(Register(reg), Register(reg + 1), Register(SpIndex), spillOffset);
                    }
                    else
                    {
                        asm.LdpRiUn(Register(reg), Register(reg + 1), Register(SpIndex), spillOffset);
                    }

                    gprMask &= ~(3u << reg);
                    spillOffset += 16;
                }
                else
                {
                    if (spill)
                    {
                        asm.StrRiUn(Register(reg), Register(SpIndex), spillOffset);
                    }
                    else
                    {
                        asm.LdrRiUn(Register(reg), Register(SpIndex), spillOffset);
                    }

                    gprMask &= ~(1u << reg);
                    spillOffset += 8;
                }
            }
        }

        private static Operand Register(int register, OperandType type = OperandType.I64)
        {
            return new Operand(register, RegisterType.Integer, type);
        }

        private static Operand Const(long value, OperandType type = OperandType.I64)
        {
            return new Operand(type, (ulong)value);
        }

        private static int NextFreeRegister(int start, int avoid)
        {
            if (start == avoid)
            {
                start++;
            }

            return start;
        }
    }
}
