using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Memory;
using ARMeilleure.Translation;
using ARMeilleure.Translation.PTC;
using System;
using System.Reflection;
using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.IntermediateRepresentation.Operand.Factory;

namespace ARMeilleure.Instructions
{
    static class InstEmitMemoryHelper
    {
        private const int PageBits = 12;
        private const int PageMask = (1 << PageBits) - 1;

        private enum Extension
        {
            Zx,
            Sx32,
            Sx64,
        }

        public static void EmitLoadZx(ArmEmitterContext context, Operand address, int rt, int size)
        {
            EmitLoad(context, address, Extension.Zx, rt, size);
        }

        public static void EmitLoadSx32(ArmEmitterContext context, Operand address, int rt, int size)
        {
            EmitLoad(context, address, Extension.Sx32, rt, size);
        }

        public static void EmitLoadSx64(ArmEmitterContext context, Operand address, int rt, int size)
        {
            EmitLoad(context, address, Extension.Sx64, rt, size);
        }

        private static void EmitLoad(ArmEmitterContext context, Operand address, Extension ext, int rt, int size)
        {
            bool isSimd = IsSimd(context);

            if ((uint)size > (isSimd ? 4 : 3))
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            if (isSimd)
            {
                EmitReadVector(context, address, context.VectorZero(), rt, 0, size);
            }
            else
            {
                EmitReadInt(context, address, rt, size);
            }

            if (!isSimd && !(context.CurrOp is OpCode32 && rt == State.RegisterAlias.Aarch32Pc))
            {
                Operand value = GetInt(context, rt);

                if (ext == Extension.Sx32 || ext == Extension.Sx64)
                {
                    OperandType destType = ext == Extension.Sx64 ? OperandType.I64 : OperandType.I32;

                    switch (size)
                    {
                        case 0:
                            value = context.SignExtend8(destType, value);
                            break;
                        case 1:
                            value = context.SignExtend16(destType, value);
                            break;
                        case 2:
                            value = context.SignExtend32(destType, value);
                            break;
                    }
                }

                SetInt(context, rt, value);
            }
        }

        public static void EmitLoadSimd(
            ArmEmitterContext context,
            Operand address,
            Operand vector,
            int rt,
            int elem,
            int size)
        {
            EmitReadVector(context, address, vector, rt, elem, size);
        }

        public static void EmitStore(ArmEmitterContext context, Operand address, int rt, int size)
        {
            bool isSimd = IsSimd(context);

            if ((uint)size > (isSimd ? 4 : 3))
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            if (isSimd)
            {
                EmitWriteVector(context, address, rt, 0, size);
            }
            else
            {
                EmitWriteInt(context, address, rt, size);
            }
        }

        public static void EmitStoreSimd(
            ArmEmitterContext context,
            Operand address,
            int rt,
            int elem,
            int size)
        {
            EmitWriteVector(context, address, rt, elem, size);
        }

        private static bool IsSimd(ArmEmitterContext context)
        {
            return context.CurrOp is IOpCodeSimd &&
                 !(context.CurrOp is OpCodeSimdMemMs ||
                   context.CurrOp is OpCodeSimdMemSs);
        }

        public static Operand EmitReadInt(ArmEmitterContext context, Operand address, int size)
        {
            Operand temp = context.AllocateLocal(size == 3 ? OperandType.I64 : OperandType.I32);

            Operand lblSlowPath = Label();
            Operand lblEnd = Label();

            Operand physAddr = EmitPtPointerLoad(context, address, lblSlowPath, write: false, size);

            Operand value = default;

            switch (size)
            {
                case 0:
                    value = context.Load8(physAddr);
                    break;
                case 1:
                    value = context.Load16(physAddr);
                    break;
                case 2:
                    value = context.Load(OperandType.I32, physAddr);
                    break;
                case 3:
                    value = context.Load(OperandType.I64, physAddr);
                    break;
            }

            context.Copy(temp, value);

            if (!context.Memory.Type.IsHostMappedOrTracked())
            {
                context.Branch(lblEnd);

                context.MarkLabel(lblSlowPath, BasicBlockFrequency.Cold);

                context.Copy(temp, EmitReadIntFallback(context, address, size));

                context.MarkLabel(lblEnd);
            }

            return temp;
        }

        private static void EmitReadInt(ArmEmitterContext context, Operand address, int rt, int size)
        {
            Operand lblSlowPath = Label();
            Operand lblEnd = Label();

            Operand physAddr = EmitPtPointerLoad(context, address, lblSlowPath, write: false, size);

            Operand value = default;

            switch (size)
            {
                case 0:
                    value = context.Load8(physAddr);
                    break;
                case 1:
                    value = context.Load16(physAddr);
                    break;
                case 2:
                    value = context.Load(OperandType.I32, physAddr);
                    break;
                case 3:
                    value = context.Load(OperandType.I64, physAddr);
                    break;
            }

            SetInt(context, rt, value);

            if (!context.Memory.Type.IsHostMappedOrTracked())
            {
                context.Branch(lblEnd);

                context.MarkLabel(lblSlowPath, BasicBlockFrequency.Cold);

                EmitReadIntFallback(context, address, rt, size);

                context.MarkLabel(lblEnd);
            }
        }

        public static Operand EmitReadIntAligned(ArmEmitterContext context, Operand address, int size)
        {
            if ((uint)size > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            Operand physAddr = EmitPtPointerLoad(context, address, default, write: false, size);

            return size switch
            {
                0 => context.Load8(physAddr),
                1 => context.Load16(physAddr),
                2 => context.Load(OperandType.I32, physAddr),
                3 => context.Load(OperandType.I64, physAddr),
                _ => context.Load(OperandType.V128, physAddr),
            };
        }

        private static void EmitReadVector(
            ArmEmitterContext context,
            Operand address,
            Operand vector,
            int rt,
            int elem,
            int size)
        {
            Operand lblSlowPath = Label();
            Operand lblEnd = Label();

            Operand physAddr = EmitPtPointerLoad(context, address, lblSlowPath, write: false, size);

            Operand value = default;

            switch (size)
            {
                case 0:
                    value = context.VectorInsert8(vector, context.Load8(physAddr), elem);
                    break;
                case 1:
                    value = context.VectorInsert16(vector, context.Load16(physAddr), elem);
                    break;
                case 2:
                    value = context.VectorInsert(vector, context.Load(OperandType.I32, physAddr), elem);
                    break;
                case 3:
                    value = context.VectorInsert(vector, context.Load(OperandType.I64, physAddr), elem);
                    break;
                case 4:
                    value = context.Load(OperandType.V128, physAddr);
                    break;
            }

            context.Copy(GetVec(rt), value);

            if (!context.Memory.Type.IsHostMappedOrTracked())
            {
                context.Branch(lblEnd);

                context.MarkLabel(lblSlowPath, BasicBlockFrequency.Cold);

                EmitReadVectorFallback(context, address, vector, rt, elem, size);

                context.MarkLabel(lblEnd);
            }
        }

        private static Operand VectorCreate(ArmEmitterContext context, Operand value)
        {
            return context.VectorInsert(context.VectorZero(), value, 0);
        }

        private static void EmitWriteInt(ArmEmitterContext context, Operand address, int rt, int size)
        {
            Operand lblSlowPath = Label();
            Operand lblEnd = Label();

            Operand physAddr = EmitPtPointerLoad(context, address, lblSlowPath, write: true, size);

            Operand value = GetInt(context, rt);

            if (size < 3 && value.Type == OperandType.I64)
            {
                value = context.ConvertI64ToI32(value);
            }

            switch (size)
            {
                case 0:
                    context.Store8(physAddr, value);
                    break;
                case 1:
                    context.Store16(physAddr, value);
                    break;
                case 2:
                    context.Store(physAddr, value);
                    break;
                case 3:
                    context.Store(physAddr, value);
                    break;
            }

            if (!context.Memory.Type.IsHostMappedOrTracked())
            {
                context.Branch(lblEnd);

                context.MarkLabel(lblSlowPath, BasicBlockFrequency.Cold);

                EmitWriteIntFallback(context, address, rt, size);

                context.MarkLabel(lblEnd);
            }
        }

        public static void EmitWriteIntAligned(ArmEmitterContext context, Operand address, Operand value, int size)
        {
            if ((uint)size > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            Operand physAddr = EmitPtPointerLoad(context, address, default, write: true, size);

            if (size < 3 && value.Type == OperandType.I64)
            {
                value = context.ConvertI64ToI32(value);
            }

            if (size == 0)
            {
                context.Store8(physAddr, value);
            }
            else if (size == 1)
            {
                context.Store16(physAddr, value);
            }
            else
            {
                context.Store(physAddr, value);
            }
        }

        private static void EmitWriteVector(
            ArmEmitterContext context,
            Operand address,
            int rt,
            int elem,
            int size)
        {
            Operand lblSlowPath = Label();
            Operand lblEnd = Label();

            Operand physAddr = EmitPtPointerLoad(context, address, lblSlowPath, write: true, size);

            Operand value = GetVec(rt);

            switch (size)
            {
                case 0:
                    context.Store8(physAddr, context.VectorExtract8(value, elem));
                    break;
                case 1:
                    context.Store16(physAddr, context.VectorExtract16(value, elem));
                    break;
                case 2:
                    context.Store(physAddr, context.VectorExtract(OperandType.I32, value, elem));
                    break;
                case 3:
                    context.Store(physAddr, context.VectorExtract(OperandType.I64, value, elem));
                    break;
                case 4:
                    context.Store(physAddr, value);
                    break;
            }

            if (!context.Memory.Type.IsHostMappedOrTracked())
            {
                context.Branch(lblEnd);

                context.MarkLabel(lblSlowPath, BasicBlockFrequency.Cold);

                EmitWriteVectorFallback(context, address, rt, elem, size);

                context.MarkLabel(lblEnd);
            }
        }

        public static Operand EmitPtPointerLoad(ArmEmitterContext context, Operand address, Operand lblSlowPath, bool write, int size)
        {
            if (context.Memory.Type.IsHostMapped())
            {
                return EmitHostMappedPointer(context, address);
            }
            else if (context.Memory.Type.IsHostTracked())
            {
                if (address.Type == OperandType.I32)
                {
                    address = context.ZeroExtend32(OperandType.I64, address);
                }

                if (context.Memory.Type == MemoryManagerType.HostTracked)
                {
                    Operand mask = Const(ulong.MaxValue >> (64 - context.Memory.AddressSpaceBits));
                    address = context.BitwiseAnd(address, mask);
                }

                Operand ptBase = !context.HasPtc
                    ? Const(context.Memory.PageTablePointer.ToInt64())
                    : Const(context.Memory.PageTablePointer.ToInt64(), Ptc.PageTableSymbol);

                Operand ptOffset = context.ShiftRightUI(address, Const(PageBits));

                return context.Add(address, context.Load(OperandType.I64, context.Add(ptBase, context.ShiftLeft(ptOffset, Const(3)))));
            }

            int ptLevelBits = context.Memory.AddressSpaceBits - PageBits;
            int ptLevelSize = 1 << ptLevelBits;
            int ptLevelMask = ptLevelSize - 1;

            Operand addrRotated = size != 0 ? context.RotateRight(address, Const(size)) : address;
            Operand addrShifted = context.ShiftRightUI(addrRotated, Const(PageBits - size));

            Operand pte = !context.HasPtc
                ? Const(context.Memory.PageTablePointer.ToInt64())
                : Const(context.Memory.PageTablePointer.ToInt64(), Ptc.PageTableSymbol);

            Operand pteOffset = context.BitwiseAnd(addrShifted, Const(addrShifted.Type, ptLevelMask));

            if (pteOffset.Type == OperandType.I32)
            {
                pteOffset = context.ZeroExtend32(OperandType.I64, pteOffset);
            }

            pte = context.Load(OperandType.I64, context.Add(pte, context.ShiftLeft(pteOffset, Const(3))));

            if (addrShifted.Type == OperandType.I32)
            {
                addrShifted = context.ZeroExtend32(OperandType.I64, addrShifted);
            }

            // If the VA is out of range, or not aligned to the access size, force PTE to 0 by masking it.
            pte = context.BitwiseAnd(pte, context.ShiftRightSI(context.Add(addrShifted, Const(-(long)ptLevelSize)), Const(63)));

            if (lblSlowPath != default)
            {
                if (write)
                {
                    context.BranchIf(lblSlowPath, pte, Const(0L), Comparison.LessOrEqual);
                    pte = context.BitwiseAnd(pte, Const(0xffffffffffffUL)); // Ignore any software protection bits. (they are still used by C# memory access)
                }
                else
                {
                    pte = context.ShiftLeft(pte, Const(1));
                    context.BranchIf(lblSlowPath, pte, Const(0L), Comparison.LessOrEqual);
                    pte = context.ShiftRightUI(pte, Const(1));
                }
            }
            else
            {
                // When no label is provided to jump to a slow path if the address is invalid,
                // we do the validation ourselves, and throw if needed.

                Operand lblNotWatched = Label();

                // Is the page currently being tracked for read/write? If so we need to call SignalMemoryTracking.
                context.BranchIf(lblNotWatched, pte, Const(0L), Comparison.GreaterOrEqual, BasicBlockFrequency.Cold);

                // Signal memory tracking. Size here doesn't matter as address is assumed to be size aligned here.
                context.Call(typeof(NativeInterface).GetMethod(nameof(NativeInterface.SignalMemoryTracking)), address, Const(1UL), Const(write ? 1 : 0));
                context.MarkLabel(lblNotWatched);

                pte = context.BitwiseAnd(pte, Const(0xffffffffffffUL)); // Ignore any software protection bits. (they are still used by C# memory access)

                Operand lblNonNull = Label();

                // Skip exception if the PTE address is non-null (not zero).
                context.BranchIfTrue(lblNonNull, pte, BasicBlockFrequency.Cold);

                // The call is not expected to return (it should throw).
                context.Call(typeof(NativeInterface).GetMethod(nameof(NativeInterface.ThrowInvalidMemoryAccess)), address);
                context.MarkLabel(lblNonNull);
            }

            Operand pageOffset = context.BitwiseAnd(address, Const(address.Type, PageMask));

            if (pageOffset.Type == OperandType.I32)
            {
                pageOffset = context.ZeroExtend32(OperandType.I64, pageOffset);
            }

            return context.Add(pte, pageOffset);
        }

        public static Operand EmitHostMappedPointer(ArmEmitterContext context, Operand address)
        {
            if (address.Type == OperandType.I32)
            {
                address = context.ZeroExtend32(OperandType.I64, address);
            }

            if (context.Memory.Type == MemoryManagerType.HostMapped)
            {
                Operand mask = Const(ulong.MaxValue >> (64 - context.Memory.AddressSpaceBits));
                address = context.BitwiseAnd(address, mask);
            }

            Operand baseAddr = !context.HasPtc
                ? Const(context.Memory.PageTablePointer.ToInt64())
                : Const(context.Memory.PageTablePointer.ToInt64(), Ptc.PageTableSymbol);

            return context.Add(baseAddr, address);
        }

        private static void EmitReadIntFallback(ArmEmitterContext context, Operand address, int rt, int size)
        {
            SetInt(context, rt, EmitReadIntFallback(context, address, size));
        }

        private static Operand EmitReadIntFallback(ArmEmitterContext context, Operand address, int size)
        {
            MethodInfo info = null;

            switch (size)
            {
                case 0:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.ReadByte));
                    break;
                case 1:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.ReadUInt16));
                    break;
                case 2:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.ReadUInt32));
                    break;
                case 3:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.ReadUInt64));
                    break;
            }

            return context.Call(info, address);
        }

        private static void EmitReadVectorFallback(
            ArmEmitterContext context,
            Operand address,
            Operand vector,
            int rt,
            int elem,
            int size)
        {
            MethodInfo info = null;

            switch (size)
            {
                case 0:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.ReadByte));
                    break;
                case 1:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.ReadUInt16));
                    break;
                case 2:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.ReadUInt32));
                    break;
                case 3:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.ReadUInt64));
                    break;
                case 4:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.ReadVector128));
                    break;
            }

            Operand value = context.Call(info, address);

            switch (size)
            {
                case 0:
                    value = context.VectorInsert8(vector, value, elem);
                    break;
                case 1:
                    value = context.VectorInsert16(vector, value, elem);
                    break;
                case 2:
                    value = context.VectorInsert(vector, value, elem);
                    break;
                case 3:
                    value = context.VectorInsert(vector, value, elem);
                    break;
            }

            context.Copy(GetVec(rt), value);
        }

        private static void EmitWriteIntFallback(ArmEmitterContext context, Operand address, int rt, int size)
        {
            MethodInfo info = null;

            switch (size)
            {
                case 0:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteByte));
                    break;
                case 1:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteUInt16));
                    break;
                case 2:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteUInt32));
                    break;
                case 3:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteUInt64));
                    break;
            }

            Operand value = GetInt(context, rt);

            if (size < 3 && value.Type == OperandType.I64)
            {
                value = context.ConvertI64ToI32(value);
            }

            context.Call(info, address, value);
        }

        private static void EmitWriteVectorFallback(
            ArmEmitterContext context,
            Operand address,
            int rt,
            int elem,
            int size)
        {
            MethodInfo info = null;

            switch (size)
            {
                case 0:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteByte));
                    break;
                case 1:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteUInt16));
                    break;
                case 2:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteUInt32));
                    break;
                case 3:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteUInt64));
                    break;
                case 4:
                    info = typeof(NativeInterface).GetMethod(nameof(NativeInterface.WriteVector128));
                    break;
            }

            Operand value = default;

            if (size < 4)
            {
                switch (size)
                {
                    case 0:
                        value = context.VectorExtract8(GetVec(rt), elem);
                        break;
                    case 1:
                        value = context.VectorExtract16(GetVec(rt), elem);
                        break;
                    case 2:
                        value = context.VectorExtract(OperandType.I32, GetVec(rt), elem);
                        break;
                    case 3:
                        value = context.VectorExtract(OperandType.I64, GetVec(rt), elem);
                        break;
                }
            }
            else
            {
                value = GetVec(rt);
            }

            context.Call(info, address, value);
        }

        private static Operand GetInt(ArmEmitterContext context, int rt)
        {
            return context.CurrOp is OpCode32 ? GetIntA32(context, rt) : GetIntOrZR(context, rt);
        }

        private static void SetInt(ArmEmitterContext context, int rt, Operand value)
        {
            if (context.CurrOp is OpCode32)
            {
                SetIntA32(context, rt, value);
            }
            else
            {
                SetIntOrZR(context, rt, value);
            }
        }

        // ARM32 helpers.
        public static Operand GetMemM(ArmEmitterContext context, bool setCarry = true)
        {
            return context.CurrOp switch
            {
                IOpCode32MemRsImm op => GetMShiftedByImmediate(context, op, setCarry),
                IOpCode32MemReg op => GetIntA32(context, op.Rm),
                IOpCode32Mem op => Const(op.Immediate),
                OpCode32SimdMemImm op => Const(op.Immediate),
                _ => throw InvalidOpCodeType(context.CurrOp),
            };
        }

        private static InvalidOperationException InvalidOpCodeType(OpCode opCode)
        {
            return new InvalidOperationException($"Invalid OpCode type \"{opCode?.GetType().Name ?? "null"}\".");
        }

        public static Operand GetMShiftedByImmediate(ArmEmitterContext context, IOpCode32MemRsImm op, bool setCarry)
        {
            Operand m = GetIntA32(context, op.Rm);

            int shift = op.Immediate;

            if (shift == 0)
            {
                switch (op.ShiftType)
                {
                    case ShiftType.Lsr:
                        shift = 32;
                        break;
                    case ShiftType.Asr:
                        shift = 32;
                        break;
                    case ShiftType.Ror:
                        shift = 1;
                        break;
                }
            }

            if (shift != 0)
            {
                setCarry &= false;

                switch (op.ShiftType)
                {
                    case ShiftType.Lsl:
                        m = InstEmitAluHelper.GetLslC(context, m, setCarry, shift);
                        break;
                    case ShiftType.Lsr:
                        m = InstEmitAluHelper.GetLsrC(context, m, setCarry, shift);
                        break;
                    case ShiftType.Asr:
                        m = InstEmitAluHelper.GetAsrC(context, m, setCarry, shift);
                        break;
                    case ShiftType.Ror:
                        if (op.Immediate != 0)
                        {
                            m = InstEmitAluHelper.GetRorC(context, m, setCarry, shift);
                        }
                        else
                        {
                            m = InstEmitAluHelper.GetRrxC(context, m, setCarry);
                        }
                        break;
                }
            }

            return m;
        }
    }
}
