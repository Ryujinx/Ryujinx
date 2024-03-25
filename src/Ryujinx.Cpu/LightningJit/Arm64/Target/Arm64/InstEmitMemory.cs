using ARMeilleure.Memory;
using Ryujinx.Cpu.LightningJit.CodeGen;
using Ryujinx.Cpu.LightningJit.CodeGen.Arm64;
using System;
using System.Diagnostics;

namespace Ryujinx.Cpu.LightningJit.Arm64.Target.Arm64
{
    static class InstEmitMemory
    {
        private const uint XMask = 0x3f808000u;
        private const uint XValue = 0x8000000u;

        public static void RewriteSysInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, uint encoding)
        {
            // TODO: Handle IC instruction, it should invalidate the JIT cache.

            if (InstEmitSystem.IsCacheInstForbidden(encoding))
            {
                // Current OS does not allow cache maintenance instructions from user mode, just do nothing.
                return;
            }

            int rtIndex = RegisterUtils.ExtractRt(encoding);
            if (rtIndex == RegisterUtils.ZrIndex)
            {
                writer.WriteInstruction(encoding);

                return;
            }

            int tempRegister = regAlloc.AllocateTempGprRegister();
            Operand rt = new(tempRegister, RegisterType.Integer, OperandType.I64);
            Operand guestAddress = new(rtIndex, RegisterType.Integer, OperandType.I64);

            Assembler asm = new(writer);

            WriteAddressTranslation(asBits, mmType, regAlloc, ref asm, rt, guestAddress);

            encoding = RegisterUtils.ReplaceRt(encoding, tempRegister);

            writer.WriteInstruction(encoding);

            regAlloc.FreeTempGprRegister(tempRegister);
        }

        public static void RewriteInstruction(
            int asBits,
            MemoryManagerType mmType,
            CodeWriter writer,
            RegisterAllocator regAlloc,
            InstName name,
            InstFlags flags,
            AddressForm addressForm,
            ulong pc,
            uint encoding)
        {
            switch (addressForm)
            {
                case AddressForm.OffsetReg:
                    RewriteOffsetRegMemoryInstruction(asBits, mmType, writer, regAlloc, flags, encoding);
                    break;
                case AddressForm.PostIndexed:
                    RewritePostIndexedMemoryInstruction(asBits, mmType, writer, regAlloc, flags, encoding);
                    break;
                case AddressForm.PreIndexed:
                    RewritePreIndexedMemoryInstruction(asBits, mmType, writer, regAlloc, flags, encoding);
                    break;
                case AddressForm.SignedScaled:
                    RewriteSignedScaledMemoryInstruction(asBits, mmType, writer, regAlloc, flags, encoding);
                    break;
                case AddressForm.UnsignedScaled:
                    RewriteUnsignedScaledMemoryInstruction(asBits, mmType, writer, regAlloc, flags, encoding);
                    break;
                case AddressForm.BaseRegister:
                    // Some applications uses unordered memory instructions in places where
                    // it does need proper ordering, and only work on some CPUs.
                    // To work around this, make all exclusive access operations ordered.

                    if ((encoding & XMask) == XValue)
                    {
                        // Set ordered flag.
                        encoding |= 1u << 15;
                    }

                    RewriteBaseRegisterMemoryInstruction(asBits, mmType, writer, regAlloc, encoding);
                    break;
                case AddressForm.StructNoOffset:
                    RewriteBaseRegisterMemoryInstruction(asBits, mmType, writer, regAlloc, encoding);
                    break;
                case AddressForm.BasePlusOffset:
                    RewriteBasePlusOffsetMemoryInstruction(asBits, mmType, writer, regAlloc, encoding);
                    break;
                case AddressForm.Literal:
                    RewriteLiteralMemoryInstruction(asBits, mmType, writer, regAlloc, name, pc, encoding);
                    break;
                case AddressForm.StructPostIndexedReg:
                    RewriteStructPostIndexedRegMemoryInstruction(asBits, mmType, writer, regAlloc, encoding);
                    break;
                default:
                    writer.WriteInstruction(encoding);
                    break;
            }
        }

        private static void RewriteOffsetRegMemoryInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, InstFlags flags, uint encoding)
        {
            // TODO: Some unallocated encoding cases.

            ArmExtensionType extensionType = (ArmExtensionType)((encoding >> 13) & 7);

            uint size = encoding >> 30;

            if (flags.HasFlag(InstFlags.FpSimd))
            {
                size |= (encoding >> 21) & 4u;
            }

            int shift = (encoding & (1u << 12)) != 0 ? (int)size : 0;

            int tempRegister = regAlloc.AllocateTempGprRegister();
            Operand rn = new(tempRegister, RegisterType.Integer, OperandType.I64);
            Operand guestAddress = new(RegisterUtils.ExtractRn(encoding), RegisterType.Integer, OperandType.I64);
            Operand guestOffset = new(RegisterUtils.ExtractRm(encoding), RegisterType.Integer, OperandType.I64);

            Assembler asm = new(writer);

            asm.Add(rn, guestAddress, guestOffset, extensionType, shift);

            WriteAddressTranslation(asBits, mmType, regAlloc, ref asm, rn, rn);

            encoding = RegisterUtils.ReplaceRn(encoding, tempRegister);
            encoding = (encoding & ~(0xfffu << 10)) | (1u << 24); // Register -> Unsigned offset

            writer.WriteInstruction(encoding);

            regAlloc.FreeTempGprRegister(tempRegister);
        }

        private static void RewritePostIndexedMemoryInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, InstFlags flags, uint encoding)
        {
            bool isPair = flags.HasFlag(InstFlags.Rt2);
            int imm = isPair ? ExtractSImm7Scaled(flags, encoding) : ExtractSImm9(encoding);

            int tempRegister = regAlloc.AllocateTempGprRegister();
            Operand rn = new(tempRegister, RegisterType.Integer, OperandType.I64);
            Operand guestAddress = new(RegisterUtils.ExtractRn(encoding), RegisterType.Integer, OperandType.I64);

            Assembler asm = new(writer);

            WriteAddressTranslation(asBits, mmType, regAlloc, ref asm, rn, guestAddress);

            encoding = RegisterUtils.ReplaceRn(encoding, tempRegister);

            if (isPair)
            {
                // Post-index -> Signed offset
                encoding &= ~(0x7fu << 15);
                encoding ^= 3u << 23;
            }
            else
            {
                // Post-index -> Unsigned offset
                encoding = (encoding & ~(0xfffu << 10)) | (1u << 24);
            }

            writer.WriteInstruction(encoding);

            WriteAddConstant(ref asm, guestAddress, guestAddress, imm);

            regAlloc.FreeTempGprRegister(tempRegister);
        }

        private static void RewritePreIndexedMemoryInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, InstFlags flags, uint encoding)
        {
            bool isPair = flags.HasFlag(InstFlags.Rt2);
            int imm = isPair ? ExtractSImm7Scaled(flags, encoding) : ExtractSImm9(encoding);

            int tempRegister = regAlloc.AllocateTempGprRegister();
            Operand rn = new(tempRegister, RegisterType.Integer, OperandType.I64);
            Operand guestAddress = new(RegisterUtils.ExtractRn(encoding), RegisterType.Integer, OperandType.I64);

            Assembler asm = new(writer);

            WriteAddConstant(ref asm, guestAddress, guestAddress, imm);
            WriteAddressTranslation(asBits, mmType, regAlloc, ref asm, rn, guestAddress);

            encoding = RegisterUtils.ReplaceRn(encoding, tempRegister);

            if (isPair)
            {
                // Pre-index -> Signed offset
                encoding &= ~(0x7fu << 15);
                encoding &= ~(1u << 23);
            }
            else
            {
                // Pre-index -> Unsigned offset
                encoding = (encoding & ~(0xfffu << 10)) | (1u << 24);
            }

            writer.WriteInstruction(encoding);

            regAlloc.FreeTempGprRegister(tempRegister);
        }

        private static void RewriteSignedScaledMemoryInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, InstFlags flags, uint encoding)
        {
            RewriteMemoryInstruction(asBits, mmType, writer, regAlloc, encoding, ExtractSImm7Scaled(flags, encoding), 0x7fu << 15);
        }

        private static void RewriteUnsignedScaledMemoryInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, InstFlags flags, uint encoding)
        {
            RewriteMemoryInstruction(asBits, mmType, writer, regAlloc, encoding, ExtractUImm12Scaled(flags, encoding), 0xfffu << 10);
        }

        private static void RewriteBaseRegisterMemoryInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, uint encoding)
        {
            RewriteMemoryInstruction(asBits, mmType, writer, regAlloc, encoding, 0, 0u);
        }

        private static void RewriteBasePlusOffsetMemoryInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, uint encoding)
        {
            RewriteMemoryInstruction(asBits, mmType, writer, regAlloc, encoding, ExtractSImm9(encoding), 0x1ffu << 12);
        }

        private static void RewriteMemoryInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, uint encoding, int imm, uint immMask)
        {
            int tempRegister = regAlloc.AllocateTempGprRegister();
            Operand rn = new(tempRegister, RegisterType.Integer, OperandType.I64);
            Operand guestAddress = new(RegisterUtils.ExtractRn(encoding), RegisterType.Integer, OperandType.I64);

            Assembler asm = new(writer);

            bool canFoldOffset = CanFoldOffset(mmType, imm);
            if (canFoldOffset)
            {
                imm = 0;
            }

            WriteAddressTranslation(asBits, mmType, regAlloc, ref asm, rn, guestAddress, imm);

            encoding = RegisterUtils.ReplaceRn(encoding, tempRegister);

            if (!canFoldOffset)
            {
                encoding &= ~immMask; // Clear offset
            }

            writer.WriteInstruction(encoding);

            regAlloc.FreeTempGprRegister(tempRegister);
        }

        private static void RewriteLiteralMemoryInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, InstName name, ulong pc, uint encoding)
        {
            Assembler asm = new(writer);

            ulong targetAddress;
            long imm;
            int rtIndex = (int)(encoding & 0x1f);

            if (rtIndex == RegisterUtils.ZrIndex && name != InstName.PrfmLit)
            {
                return;
            }

            Operand rt;

            if (name == InstName.LdrLitFpsimd)
            {
                uint opc = encoding >> 30;

                // TODO: Undefined if opc is invalid?

                rt = new(rtIndex, RegisterType.Vector, opc switch
                {
                    0 => OperandType.FP32,
                    1 => OperandType.FP64,
                    _ => OperandType.V128,
                });
            }
            else
            {
                rt = new(rtIndex, RegisterType.Integer, OperandType.I64);
            }

            switch (name)
            {
                case InstName.Adr:
                case InstName.Adrp:
                    imm = ((long)(encoding >> 29) & 3) | ((long)(encoding >> 3) & 0x1ffffc);
                    imm <<= 43;

                    if (name == InstName.Adrp)
                    {
                        imm >>= 31;
                        targetAddress = (pc & ~0xfffUL) + (ulong)imm;
                    }
                    else
                    {
                        imm >>= 43;
                        targetAddress = pc + (ulong)imm;
                    }

                    asm.Mov(rt, targetAddress);
                    break;
                case InstName.LdrLitGen:
                case InstName.LdrswLit:
                case InstName.LdrLitFpsimd:
                case InstName.PrfmLit:
                    imm = encoding & ~0x1fu;
                    imm <<= 40;
                    imm >>= 43;
                    targetAddress = pc + (ulong)imm;

                    int tempRegister = regAlloc.AllocateTempGprRegister();
                    Operand rn = new(tempRegister, RegisterType.Integer, OperandType.I64);

                    WriteAddressTranslation(asBits, mmType, regAlloc, ref asm, rn, targetAddress);

                    switch (name)
                    {
                        case InstName.LdrLitGen:
                        case InstName.LdrLitFpsimd:
                            asm.LdrRiUn(rt, rn, 0);
                            break;
                        case InstName.LdrswLit:
                            asm.LdrswRiUn(rt, rn, 0);
                            break;
                        case InstName.PrfmLit:
                            asm.PrfmR(rt, rn);
                            break;
                    }

                    regAlloc.FreeTempGprRegister(tempRegister);
                    break;
                default:
                    Debug.Fail($"Invalid literal memory instruction '{name}'.");
                    break;
            }
        }

        private static void RewriteStructPostIndexedRegMemoryInstruction(int asBits, MemoryManagerType mmType, CodeWriter writer, RegisterAllocator regAlloc, uint encoding)
        {
            // TODO: Some unallocated encoding cases.

            int tempRegister = regAlloc.AllocateTempGprRegister();
            Operand rn = new(tempRegister, RegisterType.Integer, OperandType.I64);
            Operand guestAddress = new(RegisterUtils.ExtractRn(encoding), RegisterType.Integer, OperandType.I64);

            int rmIndex = RegisterUtils.ExtractRm(encoding);

            Assembler asm = new(writer);

            WriteAddressTranslation(asBits, mmType, regAlloc, ref asm, rn, guestAddress);

            encoding = RegisterUtils.ReplaceRn(encoding, tempRegister);
            encoding &= ~((0x1fu << 16) | (1u << 23)); // Post-index -> No offset

            writer.WriteInstruction(encoding);

            if (rmIndex == RegisterUtils.ZrIndex)
            {
                bool isSingleStruct = (encoding & (1u << 24)) != 0;
                int offset;

                if (isSingleStruct)
                {
                    int sElems = (int)(((encoding >> 12) & 2u) | ((encoding >> 21) & 1u)) + 1;

                    int size = (int)(encoding >> 10) & 3;
                    int s = (int)(encoding >> 12) & 1;
                    int scale = (int)(encoding >> 14) & 3;
                    int l = (int)(encoding >> 22) & 1;

                    switch (scale)
                    {
                        case 1:
                            if ((size & 1) != 0)
                            {
                                // Undef.
                            }

                            break;

                        case 2:
                            if ((size & 2) != 0 ||
                            ((size & 1) != 0 && s != 0))
                            {
                                // Undef.
                            }

                            if ((size & 1) != 0)
                            {
                                scale = 3;
                            }

                            break;

                        case 3:
                            if (l == 0 || s != 0)
                            {
                                // Undef.
                            }

                            scale = size;

                            break;
                    }

                    int eBytes = 1 << scale;

                    offset = eBytes * sElems;
                }
                else
                {
                    int reps;
                    int sElems;

                    switch ((encoding >> 12) & 0xf)
                    {
                        case 0b0000:
                            reps = 1;
                            sElems = 4;
                            break;
                        case 0b0010:
                            reps = 4;
                            sElems = 1;
                            break;
                        case 0b0100:
                            reps = 1;
                            sElems = 3;
                            break;
                        case 0b0110:
                            reps = 3;
                            sElems = 1;
                            break;
                        case 0b0111:
                            reps = 1;
                            sElems = 1;
                            break;
                        case 0b1000:
                            reps = 1;
                            sElems = 2;
                            break;
                        case 0b1010:
                            reps = 2;
                            sElems = 1;
                            break;

                        default:
                            // Undef.
                            reps = 0;
                            sElems = 0;
                            break;
                    }

                    int size = (int)(encoding >> 10) & 3;
                    bool q = (encoding & (1u << 30)) != 0;

                    if (!q && size == 3 && sElems != 1)
                    {
                        // Undef.
                    }

                    offset = reps * (q ? 16 : 8) * sElems;
                }

                asm.Add(guestAddress, guestAddress, new Operand(OperandKind.Constant, OperandType.I32, (ulong)offset));
            }
            else
            {
                Operand guestOffset = new(rmIndex, RegisterType.Integer, OperandType.I64);

                asm.Add(guestAddress, guestAddress, guestOffset);
            }

            regAlloc.FreeTempGprRegister(tempRegister);
        }

        private static void WriteAddressTranslation(
            int asBits,
            MemoryManagerType mmType,
            RegisterAllocator regAlloc,
            ref Assembler asm,
            Operand destination,
            Operand guestAddress,
            int offset)
        {
            if (offset != 0)
            {
                // They are assumed to be on different registers, otherwise this operation will thrash the address.
                Debug.Assert(destination.Value != guestAddress.Value);

                if (Math.Abs(offset) >= 0x1000)
                {
                    // Too high to encode as 12-bit immediate, do a separate move.
                    asm.Mov(destination, (ulong)offset);
                    asm.Add(destination, destination, guestAddress);
                }
                else
                {
                    // Encode as 12-bit immediate.
                    WriteAddConstant(ref asm, destination, guestAddress, offset);
                }

                guestAddress = destination;
            }

            WriteAddressTranslation(asBits, mmType, regAlloc, ref asm, destination, guestAddress);
        }

        private static void WriteAddressTranslation(int asBits, MemoryManagerType mmType, RegisterAllocator regAlloc, ref Assembler asm, Operand destination, ulong guestAddress)
        {
            asm.Mov(destination, guestAddress);

            WriteAddressTranslation(asBits, mmType, regAlloc, ref asm, destination, destination);
        }

        private static void WriteAddressTranslation(int asBits, MemoryManagerType mmType, RegisterAllocator regAlloc, ref Assembler asm, Operand destination, Operand guestAddress)
        {
            Operand basePointer = new(regAlloc.FixedPageTableRegister, RegisterType.Integer, OperandType.I64);

            if (mmType == MemoryManagerType.HostMapped || mmType == MemoryManagerType.HostMappedUnsafe)
            {
                if (mmType == MemoryManagerType.HostMapped)
                {
                    asm.And(destination, guestAddress, new Operand(OperandKind.Constant, OperandType.I64, ulong.MaxValue >> (64 - asBits)));
                    guestAddress = destination;
                }

                asm.Add(destination, basePointer, guestAddress);
            }
            else
            {
                throw new NotImplementedException(mmType.ToString());
            }
        }

        private static void WriteAddConstant(ref Assembler asm, Operand rd, Operand rn, int value)
        {
            if (value < 0)
            {
                asm.Sub(rd, rn, new Operand(OperandKind.Constant, OperandType.I32, (ulong)-value));
            }
            else
            {
                asm.Add(rd, rn, new Operand(OperandKind.Constant, OperandType.I32, (ulong)value));
            }
        }

        private static bool CanFoldOffset(MemoryManagerType mmType, int offset)
        {
            return mmType == MemoryManagerType.HostMappedUnsafe;
        }

        private static int ExtractSImm7Scaled(InstFlags flags, uint encoding)
        {
            uint opc = flags.HasFlag(InstFlags.FpSimd) ? encoding >> 30 : encoding >> 31;
            return ExtractSImm7(encoding) << (int)(2 + opc);
        }

        private static int ExtractSImm7(uint encoding)
        {
            int imm = (int)(encoding >> 15);

            imm <<= 25;
            imm >>= 25;

            return imm;
        }

        private static int ExtractSImm9(uint encoding)
        {
            int imm = (int)(encoding >> 12);

            imm <<= 23;
            imm >>= 23;

            return imm;
        }

        private static int ExtractUImm12Scaled(InstFlags flags, uint encoding)
        {
            uint size = encoding >> 30;

            if (flags.HasFlag(InstFlags.FpSimd))
            {
                size |= (encoding >> 21) & 4u;
            }

            return ExtractUImm12(encoding) << (int)size;
        }

        private static int ExtractUImm12(uint encoding)
        {
            return (int)(encoding >> 10) & 0xfff;
        }
    }
}
