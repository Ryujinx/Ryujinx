using ARMeilleure.Common;
using Ryujinx.Cpu.LightningJit.CodeGen;
using Ryujinx.Cpu.LightningJit.CodeGen.Arm64;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Cpu.LightningJit.Arm64.Target.Arm64
{
    static class InstEmitSystem
    {
        private delegate void SoftwareInterruptHandler(ulong address, int imm);
        private delegate ulong Get64();
        private delegate bool GetBool();

        public static void RewriteInstruction(
            CodeWriter writer,
            RegisterAllocator regAlloc,
            TailMerger tailMerger,
            InstName name,
            ulong pc,
            uint encoding,
            int spillBaseOffset)
        {
            if (name == InstName.Brk)
            {
                Assembler asm = new(writer);

                WriteCall(ref asm, regAlloc, GetBrkHandlerPtr(), spillBaseOffset, null, pc, encoding);
                WriteSyncPoint(writer, ref asm, regAlloc, tailMerger, spillBaseOffset);
            }
            else if (name == InstName.Svc)
            {
                uint svcId = (ushort)(encoding >> 5);

                Assembler asm = new(writer);

                WriteCall(ref asm, regAlloc, GetSvcHandlerPtr(), spillBaseOffset, null, pc, svcId);
                WriteSyncPoint(writer, ref asm, regAlloc, tailMerger, spillBaseOffset);
            }
            else if (name == InstName.UdfPermUndef)
            {
                Assembler asm = new(writer);

                WriteCall(ref asm, regAlloc, GetUdfHandlerPtr(), spillBaseOffset, null, pc, encoding);
                WriteSyncPoint(writer, ref asm, regAlloc, tailMerger, spillBaseOffset);
            }
            else if ((encoding & ~0x1f) == 0xd53bd060) // mrs x0, tpidrro_el0
            {
                uint rd = encoding & 0x1f;

                if (rd != RegisterUtils.ZrIndex)
                {
                    Assembler asm = new(writer);

                    asm.LdrRiUn(Register((int)rd), Register(regAlloc.FixedContextRegister), NativeContextOffsets.TpidrroEl0Offset);
                }
            }
            else if ((encoding & ~0x1f) == 0xd53bd040) // mrs x0, tpidr_el0
            {
                uint rd = encoding & 0x1f;

                if (rd != RegisterUtils.ZrIndex)
                {
                    Assembler asm = new(writer);

                    asm.LdrRiUn(Register((int)rd), Register(regAlloc.FixedContextRegister), NativeContextOffsets.TpidrEl0Offset);
                }
            }
            else if ((encoding & ~0x1f) == 0xd53b0020 && IsCtrEl0AccessForbidden()) // mrs x0, ctr_el0
            {
                uint rd = encoding & 0x1f;

                if (rd != RegisterUtils.ZrIndex)
                {
                    Assembler asm = new(writer);

                    // TODO: Use host value? But that register can't be accessed on macOS...
                    asm.Mov(Register((int)rd, OperandType.I32), 0x8444c004);
                }
            }
            else if ((encoding & ~0x1f) == 0xd53be020) // mrs x0, cntpct_el0
            {
                uint rd = encoding & 0x1f;

                if (rd != RegisterUtils.ZrIndex)
                {
                    Assembler asm = new(writer);

                    WriteCall(ref asm, regAlloc, GetCntpctEl0Ptr(), spillBaseOffset, (int)rd);
                }
            }
            else if ((encoding & ~0x1f) == 0xd51bd040) // msr tpidr_el0, x0
            {
                uint rd = encoding & 0x1f;

                if (rd != RegisterUtils.ZrIndex)
                {
                    Assembler asm = new(writer);

                    asm.StrRiUn(Register((int)rd), Register(regAlloc.FixedContextRegister), NativeContextOffsets.TpidrEl0Offset);
                }
            }
            else
            {
                writer.WriteInstruction(encoding);
            }
        }

        public static bool NeedsCall(uint encoding)
        {
            if ((encoding & ~(0xffffu << 5)) == 0xd4000001u) // svc #0
            {
                return true;
            }
            else if ((encoding & ~0x1f) == 0xd53b0020 && IsCtrEl0AccessForbidden()) // mrs x0, ctr_el0
            {
                return true;
            }
            else if ((encoding & ~0x1f) == 0xd53be020) // mrs x0, cntpct_el0
            {
                return true;
            }

            return false;
        }

        private static bool IsCtrEl0AccessForbidden()
        {
            // Only Linux allows accessing CTR_EL0 from user mode.
            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsIOS();
        }

        public static bool IsCacheInstForbidden(uint encoding)
        {
            // Windows does not allow the cache maintenance instructions to be used from user mode.
            return OperatingSystem.IsWindows() && SysUtils.IsCacheInstUciTrapped(encoding);
        }

        public static bool NeedsContextStoreLoad(InstName name)
        {
            return name == InstName.Svc;
        }

        private static IntPtr GetBrkHandlerPtr()
        {
            return Marshal.GetFunctionPointerForDelegate<SoftwareInterruptHandler>(NativeInterface.Break);
        }

        private static IntPtr GetSvcHandlerPtr()
        {
            return Marshal.GetFunctionPointerForDelegate<SoftwareInterruptHandler>(NativeInterface.SupervisorCall);
        }

        private static IntPtr GetUdfHandlerPtr()
        {
            return Marshal.GetFunctionPointerForDelegate<SoftwareInterruptHandler>(NativeInterface.Undefined);
        }

        private static IntPtr GetCntpctEl0Ptr()
        {
            return Marshal.GetFunctionPointerForDelegate<Get64>(NativeInterface.GetCntpctEl0);
        }

        private static IntPtr CheckSynchronizationPtr()
        {
            return Marshal.GetFunctionPointerForDelegate<GetBool>(NativeInterface.CheckSynchronization);
        }

        public static void WriteSyncPoint(CodeWriter writer, RegisterAllocator regAlloc, TailMerger tailMerger, int spillBaseOffset)
        {
            Assembler asm = new(writer);

            WriteSyncPoint(writer, ref asm, regAlloc, tailMerger, spillBaseOffset);
        }

        private static void WriteSyncPoint(CodeWriter writer, ref Assembler asm, RegisterAllocator regAlloc, TailMerger tailMerger, int spillBaseOffset)
        {
            int tempRegister = regAlloc.AllocateTempGprRegister();

            Operand rt = Register(tempRegister, OperandType.I32);

            asm.LdrRiUn(rt, Register(regAlloc.FixedContextRegister), NativeContextOffsets.CounterOffset);

            int branchIndex = writer.InstructionPointer;
            asm.Cbnz(rt, 0);

            WriteSpill(ref asm, regAlloc, 1u << tempRegister, spillBaseOffset, tempRegister);

            Operand rn = Register(tempRegister == 0 ? 1 : 0);

            asm.Mov(rn, (ulong)CheckSynchronizationPtr());
            asm.Blr(rn);

            tailMerger.AddConditionalZeroReturn(writer, asm, Register(0, OperandType.I32));

            WriteFill(ref asm, regAlloc, 1u << tempRegister, spillBaseOffset, tempRegister);

            asm.LdrRiUn(rt, Register(regAlloc.FixedContextRegister), NativeContextOffsets.CounterOffset);

            uint branchInst = writer.ReadInstructionAt(branchIndex);
            writer.WriteInstructionAt(branchIndex, branchInst | (((uint)(writer.InstructionPointer - branchIndex) & 0x7ffff) << 5));

            asm.Sub(rt, rt, new Operand(OperandKind.Constant, OperandType.I32, 1));
            asm.StrRiUn(rt, Register(regAlloc.FixedContextRegister), NativeContextOffsets.CounterOffset);

            regAlloc.FreeTempGprRegister(tempRegister);
        }

        public static void RewriteCallInstruction(
            CodeWriter writer,
            RegisterAllocator regAlloc,
            TailMerger tailMerger,
            Action writeEpilogue,
            AddressTable<ulong> funcTable,
            IntPtr dispatchStubPtr,
            InstName name,
            ulong pc,
            uint encoding,
            int spillBaseOffset,
            bool isTail = false)
        {
            Assembler asm = new(writer);

            switch (name)
            {
                case InstName.BUncond:
                case InstName.Bl:
                case InstName.Blr:
                case InstName.Br:
                    if (name == InstName.BUncond || name == InstName.Bl)
                    {
                        int imm = ImmUtils.ExtractSImm26Times4(encoding);

                        WriteCallWithGuestAddress(
                            writer,
                            ref asm,
                            regAlloc,
                            tailMerger,
                            writeEpilogue,
                            funcTable,
                            dispatchStubPtr,
                            spillBaseOffset,
                            pc,
                            new(OperandKind.Constant, OperandType.I64, pc + (ulong)imm),
                            isTail);
                    }
                    else
                    {
                        int rnIndex = RegisterUtils.ExtractRn(encoding);
                        if (rnIndex == RegisterUtils.ZrIndex)
                        {
                            WriteCallWithGuestAddress(
                                writer,
                                ref asm,
                                regAlloc,
                                tailMerger,
                                writeEpilogue,
                                funcTable,
                                dispatchStubPtr,
                                spillBaseOffset,
                                pc,
                                new(OperandKind.Constant, OperandType.I64, 0UL),
                                isTail);
                        }
                        else
                        {
                            rnIndex = regAlloc.RemapReservedGprRegister(rnIndex);

                            WriteCallWithGuestAddress(
                                writer,
                                ref asm,
                                regAlloc,
                                tailMerger,
                                writeEpilogue,
                                funcTable,
                                dispatchStubPtr,
                                spillBaseOffset,
                                pc,
                                Register(rnIndex),
                                isTail);
                        }
                    }
                    break;

                default:
                    Debug.Fail($"Unknown branch instruction \"{name}\".");
                    break;
            }
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
            ulong pc,
            Operand guestAddress,
            bool isTail = false)
        {
            int tempRegister;

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
            }

            tempRegister = regAlloc.FixedContextRegister == 1 ? 2 : 1;

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

                ulong nextAddress = pc + 4UL;

                asm.Mov(rn, nextAddress);
                asm.Cmp(Register(0), rn);

                tailMerger.AddConditionalReturn(writer, asm, ArmCondition.Ne);

                WriteFillSkipContext(ref asm, regAlloc, spillBaseOffset);
            }
        }

        private static void WriteCall(
            ref Assembler asm,
            RegisterAllocator regAlloc,
            IntPtr funcPtr,
            int spillBaseOffset,
            int? resultRegister,
            params ulong[] callArgs)
        {
            uint resultMask = 0u;

            if (resultRegister.HasValue)
            {
                resultMask = 1u << resultRegister.Value;
            }

            int tempRegister = callArgs.Length;

            if (resultRegister.HasValue && tempRegister == resultRegister.Value)
            {
                tempRegister++;
            }

            WriteSpill(ref asm, regAlloc, resultMask, spillBaseOffset, tempRegister);

            // We only support up to 7 arguments right now.
            // ABI defines the first 8 integer arguments to be passed on registers X0-X7.
            // We need at least one register to put the function address on, so that reduces the number of
            // registers we can use for that by one.

            Debug.Assert(callArgs.Length < 8);

            for (int index = 0; index < callArgs.Length; index++)
            {
                asm.Mov(Register(index), callArgs[index]);
            }

            Operand rn = Register(tempRegister);

            asm.Mov(rn, (ulong)funcPtr);
            asm.Blr(rn);

            if (resultRegister.HasValue && resultRegister.Value != 0)
            {
                asm.Mov(Register(resultRegister.Value), Register(0));
            }

            WriteFill(ref asm, regAlloc, resultMask, spillBaseOffset, tempRegister);
        }

        private static void WriteSpill(ref Assembler asm, RegisterAllocator regAlloc, uint exceptMask, int spillOffset, int tempRegister)
        {
            WriteSpillOrFill(ref asm, regAlloc, exceptMask, spillOffset, tempRegister, spill: true);
        }

        private static void WriteFill(ref Assembler asm, RegisterAllocator regAlloc, uint exceptMask, int spillOffset, int tempRegister)
        {
            WriteSpillOrFill(ref asm, regAlloc, exceptMask, spillOffset, tempRegister, spill: false);
        }

        private static void WriteSpillOrFill(
            ref Assembler asm,
            RegisterAllocator regAlloc,
            uint exceptMask,
            int spillOffset,
            int tempRegister,
            bool spill)
        {
            uint gprMask = regAlloc.AllGprMask & ~(AbiConstants.GprCalleeSavedRegsMask | exceptMask);

            if (regAlloc.AllPStateMask != 0 && !spill)
            {
                // We must reload the status register before reloading the GPRs,
                // since we might otherwise trash one of them by using it as temp register.

                Operand rt = Register(tempRegister, OperandType.I32);

                asm.LdrRiUn(rt, Register(RegisterUtils.SpIndex), spillOffset + BitOperations.PopCount(gprMask) * 8);
                asm.MsrNzcv(rt);
            }

            while (gprMask != 0)
            {
                int reg = BitOperations.TrailingZeroCount(gprMask);

                if (reg < 31 && (gprMask & (2u << reg)) != 0 && spillOffset < RegisterSaveRestore.Encodable9BitsOffsetLimit)
                {
                    if (spill)
                    {
                        asm.StpRiUn(
                            Register(regAlloc.RemapReservedGprRegister(reg)),
                            Register(regAlloc.RemapReservedGprRegister(reg + 1)),
                            Register(RegisterUtils.SpIndex),
                            spillOffset);
                    }
                    else
                    {
                        asm.LdpRiUn(
                            Register(regAlloc.RemapReservedGprRegister(reg)),
                            Register(regAlloc.RemapReservedGprRegister(reg + 1)),
                            Register(RegisterUtils.SpIndex),
                            spillOffset);
                    }

                    gprMask &= ~(3u << reg);
                    spillOffset += 16;
                }
                else
                {
                    if (spill)
                    {
                        asm.StrRiUn(Register(regAlloc.RemapReservedGprRegister(reg)), Register(RegisterUtils.SpIndex), spillOffset);
                    }
                    else
                    {
                        asm.LdrRiUn(Register(regAlloc.RemapReservedGprRegister(reg)), Register(RegisterUtils.SpIndex), spillOffset);
                    }

                    gprMask &= ~(1u << reg);
                    spillOffset += 8;
                }
            }

            if (regAlloc.AllPStateMask != 0)
            {
                if (spill)
                {
                    Operand rt = Register(tempRegister, OperandType.I32);

                    asm.MrsNzcv(rt);
                    asm.StrRiUn(rt, Register(RegisterUtils.SpIndex), spillOffset);
                }

                spillOffset += 8;
            }

            if ((spillOffset & 8) != 0)
            {
                spillOffset += 8;
            }

            uint fpSimdMask = regAlloc.AllFpSimdMask;

            while (fpSimdMask != 0)
            {
                int reg = BitOperations.TrailingZeroCount(fpSimdMask);

                if (reg < 31 && (fpSimdMask & (2u << reg)) != 0 && spillOffset < RegisterSaveRestore.Encodable9BitsOffsetLimit)
                {
                    if (spill)
                    {
                        asm.StpRiUn(
                            Register(reg, OperandType.V128),
                            Register(reg + 1, OperandType.V128),
                            Register(RegisterUtils.SpIndex),
                            spillOffset);
                    }
                    else
                    {
                        asm.LdpRiUn(
                            Register(reg, OperandType.V128),
                            Register(reg + 1, OperandType.V128),
                            Register(RegisterUtils.SpIndex),
                            spillOffset);
                    }

                    fpSimdMask &= ~(3u << reg);
                    spillOffset += 32;
                }
                else
                {
                    if (spill)
                    {
                        asm.StrRiUn(Register(reg, OperandType.V128), Register(RegisterUtils.SpIndex), spillOffset);
                    }
                    else
                    {
                        asm.LdrRiUn(Register(reg, OperandType.V128), Register(RegisterUtils.SpIndex), spillOffset);
                    }

                    fpSimdMask &= ~(1u << reg);
                    spillOffset += 16;
                }
            }
        }

        private static void WriteSpillSkipContext(ref Assembler asm, RegisterAllocator regAlloc, int spillOffset)
        {
            WriteSpillOrFillSkipContext(ref asm, regAlloc, spillOffset, spill: true);
        }

        private static void WriteFillSkipContext(ref Assembler asm, RegisterAllocator regAlloc, int spillOffset)
        {
            WriteSpillOrFillSkipContext(ref asm, regAlloc, spillOffset, spill: false);
        }

        private static void WriteSpillOrFillSkipContext(ref Assembler asm, RegisterAllocator regAlloc, int spillOffset, bool spill)
        {
            uint gprMask = regAlloc.AllGprMask & ((1u << regAlloc.FixedContextRegister) | (1u << regAlloc.FixedPageTableRegister));
            gprMask &= ~AbiConstants.GprCalleeSavedRegsMask;

            while (gprMask != 0)
            {
                int reg = BitOperations.TrailingZeroCount(gprMask);

                if (reg < 31 && (gprMask & (2u << reg)) != 0 && spillOffset < RegisterSaveRestore.Encodable9BitsOffsetLimit)
                {
                    if (spill)
                    {
                        asm.StpRiUn(
                            Register(regAlloc.RemapReservedGprRegister(reg)),
                            Register(regAlloc.RemapReservedGprRegister(reg + 1)),
                            Register(RegisterUtils.SpIndex),
                            spillOffset);
                    }
                    else
                    {
                        asm.LdpRiUn(
                            Register(regAlloc.RemapReservedGprRegister(reg)),
                            Register(regAlloc.RemapReservedGprRegister(reg + 1)),
                            Register(RegisterUtils.SpIndex),
                            spillOffset);
                    }

                    gprMask &= ~(3u << reg);
                    spillOffset += 16;
                }
                else
                {
                    if (spill)
                    {
                        asm.StrRiUn(Register(regAlloc.RemapReservedGprRegister(reg)), Register(RegisterUtils.SpIndex), spillOffset);
                    }
                    else
                    {
                        asm.LdrRiUn(Register(regAlloc.RemapReservedGprRegister(reg)), Register(RegisterUtils.SpIndex), spillOffset);
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
    }
}
