using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.State;
using ARMeilleure.Translation;
using System;
using System.Diagnostics;

using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.IntermediateRepresentation.Operand.Factory;

namespace ARMeilleure.Instructions
{
    static class InstEmitAluHelper
    {
        public static bool ShouldSetFlags(ArmEmitterContext context)
        {
            IOpCode32HasSetFlags op = (IOpCode32HasSetFlags)context.CurrOp;

            if (op.SetFlags == null)
            {
                return !context.IsInIfThenBlock;
            }

            return op.SetFlags.Value;
        }

        public static void EmitNZFlagsCheck(ArmEmitterContext context, Operand d)
        {
            SetFlag(context, PState.NFlag, context.ICompareLess(d, Const(d.Type, 0)));
            SetFlag(context, PState.ZFlag, context.ICompareEqual(d, Const(d.Type, 0)));
        }

        public static void EmitAdcsCCheck(ArmEmitterContext context, Operand n, Operand d)
        {
            // C = (Rd == Rn && CIn) || Rd < Rn
            Operand cIn = GetFlag(PState.CFlag);

            Operand cOut = context.BitwiseAnd(context.ICompareEqual(d, n), cIn);

            cOut = context.BitwiseOr(cOut, context.ICompareLessUI(d, n));

            SetFlag(context, PState.CFlag, cOut);
        }

        public static void EmitAddsCCheck(ArmEmitterContext context, Operand n, Operand d)
        {
            // C = Rd < Rn
            SetFlag(context, PState.CFlag, context.ICompareLessUI(d, n));
        }

        public static void EmitAddsVCheck(ArmEmitterContext context, Operand n, Operand m, Operand d)
        {
            // V = (Rd ^ Rn) & ~(Rn ^ Rm) < 0
            Operand vOut = context.BitwiseExclusiveOr(d, n);

            vOut = context.BitwiseAnd(vOut, context.BitwiseNot(context.BitwiseExclusiveOr(n, m)));

            vOut = context.ICompareLess(vOut, Const(vOut.Type, 0));

            SetFlag(context, PState.VFlag, vOut);
        }

        public static void EmitSbcsCCheck(ArmEmitterContext context, Operand n, Operand m)
        {
            // C = (Rn == Rm && CIn) || Rn > Rm
            Operand cIn = GetFlag(PState.CFlag);

            Operand cOut = context.BitwiseAnd(context.ICompareEqual(n, m), cIn);

            cOut = context.BitwiseOr(cOut, context.ICompareGreaterUI(n, m));

            SetFlag(context, PState.CFlag, cOut);
        }

        public static void EmitSubsCCheck(ArmEmitterContext context, Operand n, Operand m)
        {
            // C = Rn >= Rm
            SetFlag(context, PState.CFlag, context.ICompareGreaterOrEqualUI(n, m));
        }

        public static void EmitSubsVCheck(ArmEmitterContext context, Operand n, Operand m, Operand d)
        {
            // V = (Rd ^ Rn) & (Rn ^ Rm) < 0
            Operand vOut = context.BitwiseExclusiveOr(d, n);

            vOut = context.BitwiseAnd(vOut, context.BitwiseExclusiveOr(n, m));

            vOut = context.ICompareLess(vOut, Const(vOut.Type, 0));

            SetFlag(context, PState.VFlag, vOut);
        }

        public static Operand EmitReverseBits32Op(ArmEmitterContext context, Operand op)
        {
            Debug.Assert(op.Type == OperandType.I32);

            Operand val = context.BitwiseOr(context.ShiftRightUI(context.BitwiseAnd(op, Const(0xaaaaaaaau)), Const(1)),
                                            context.ShiftLeft(context.BitwiseAnd(op, Const(0x55555555u)), Const(1)));

            val = context.BitwiseOr(context.ShiftRightUI(context.BitwiseAnd(val, Const(0xccccccccu)), Const(2)),
                                    context.ShiftLeft(context.BitwiseAnd(val, Const(0x33333333u)), Const(2)));
            val = context.BitwiseOr(context.ShiftRightUI(context.BitwiseAnd(val, Const(0xf0f0f0f0u)), Const(4)),
                                    context.ShiftLeft(context.BitwiseAnd(val, Const(0x0f0f0f0fu)), Const(4)));
            val = context.BitwiseOr(context.ShiftRightUI(context.BitwiseAnd(val, Const(0xff00ff00u)), Const(8)),
                                    context.ShiftLeft(context.BitwiseAnd(val, Const(0x00ff00ffu)), Const(8)));

            return context.BitwiseOr(context.ShiftRightUI(val, Const(16)), context.ShiftLeft(val, Const(16)));
        }

        public static Operand EmitReverseBytes16_64Op(ArmEmitterContext context, Operand op)
        {
            Debug.Assert(op.Type == OperandType.I64);

            return context.BitwiseOr(context.ShiftRightUI(context.BitwiseAnd(op, Const(0xff00ff00ff00ff00ul)), Const(8)),
                                     context.ShiftLeft(context.BitwiseAnd(op, Const(0x00ff00ff00ff00fful)), Const(8)));
        }

        public static Operand EmitReverseBytes16_32Op(ArmEmitterContext context, Operand op)
        {
            Debug.Assert(op.Type == OperandType.I32);

            Operand val = EmitReverseBytes16_64Op(context, context.ZeroExtend32(OperandType.I64, op));

            return context.ConvertI64ToI32(val);
        }

        private static void EmitAluWritePc(ArmEmitterContext context, Operand value)
        {
            Debug.Assert(value.Type == OperandType.I32);

            if (((OpCode32)context.CurrOp).IsThumb)
            {
                bool isReturn = IsA32Return(context);
                if (!isReturn)
                {
                    context.StoreToContext();
                }

                InstEmitFlowHelper.EmitVirtualJump(context, value, isReturn);
            }
            else
            {
                EmitBxWritePc(context, value);
            }
        }

        public static void EmitGenericAluStoreA32(ArmEmitterContext context, int rd, bool setFlags, Operand value)
        {
            Debug.Assert(value.Type == OperandType.I32);

            if (rd == RegisterAlias.Aarch32Pc && setFlags)
            {
                if (setFlags)
                {
                    // TODO: Load SPSR etc.

                    EmitBxWritePc(context, value);
                }
                else
                {
                    EmitAluWritePc(context, value);
                }
            }
            else
            {
                SetIntA32(context, rd, value);
            }
        }

        public static Operand GetAluN(ArmEmitterContext context)
        {
            if (context.CurrOp is IOpCodeAlu op)
            {
                if (op.DataOp == DataOp.Logical || op is IOpCodeAluRs)
                {
                    return GetIntOrZR(context, op.Rn);
                }
                else
                {
                    return GetIntOrSP(context, op.Rn);
                }
            }
            else if (context.CurrOp is IOpCode32Alu op32)
            {
                return GetIntA32(context, op32.Rn);
            }
            else
            {
                throw InvalidOpCodeType(context.CurrOp);
            }
        }

        public static Operand GetAluM(ArmEmitterContext context, bool setCarry = true)
        {
            switch (context.CurrOp)
            {
                // ARM32.
                case IOpCode32AluImm op:
                    {
                        if (ShouldSetFlags(context) && op.IsRotated && setCarry)
                        {
                            SetFlag(context, PState.CFlag, Const((uint)op.Immediate >> 31));
                        }

                        return Const(op.Immediate);
                    }

                case IOpCode32AluImm16 op:
                    return Const(op.Immediate);

                case IOpCode32AluRsImm op:
                    return GetMShiftedByImmediate(context, op, setCarry);
                case IOpCode32AluRsReg op:
                    return GetMShiftedByReg(context, op, setCarry);

                case IOpCode32AluReg op:
                    return GetIntA32(context, op.Rm);

                // ARM64.
                case IOpCodeAluImm op:
                    {
                        if (op.GetOperandType() == OperandType.I32)
                        {
                            return Const((int)op.Immediate);
                        }
                        else
                        {
                            return Const(op.Immediate);
                        }
                    }

                case IOpCodeAluRs op:
                    {
                        Operand value = GetIntOrZR(context, op.Rm);

                        switch (op.ShiftType)
                        {
                            case ShiftType.Lsl:
                                value = context.ShiftLeft(value, Const(op.Shift));
                                break;
                            case ShiftType.Lsr:
                                value = context.ShiftRightUI(value, Const(op.Shift));
                                break;
                            case ShiftType.Asr:
                                value = context.ShiftRightSI(value, Const(op.Shift));
                                break;
                            case ShiftType.Ror:
                                value = context.RotateRight(value, Const(op.Shift));
                                break;
                        }

                        return value;
                    }

                case IOpCodeAluRx op:
                    {
                        Operand value = GetExtendedM(context, op.Rm, op.IntType);

                        value = context.ShiftLeft(value, Const(op.Shift));

                        return value;
                    }

                default:
                    throw InvalidOpCodeType(context.CurrOp);
            }
        }

        private static InvalidOperationException InvalidOpCodeType(OpCode opCode)
        {
            return new InvalidOperationException($"Invalid OpCode type \"{opCode?.GetType().Name ?? "null"}\".");
        }

        // ARM32 helpers.
        public static Operand GetMShiftedByImmediate(ArmEmitterContext context, IOpCode32AluRsImm op, bool setCarry)
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
                setCarry &= ShouldSetFlags(context);

                switch (op.ShiftType)
                {
                    case ShiftType.Lsl:
                        m = GetLslC(context, m, setCarry, shift);
                        break;
                    case ShiftType.Lsr:
                        m = GetLsrC(context, m, setCarry, shift);
                        break;
                    case ShiftType.Asr:
                        m = GetAsrC(context, m, setCarry, shift);
                        break;
                    case ShiftType.Ror:
                        if (op.Immediate != 0)
                        {
                            m = GetRorC(context, m, setCarry, shift);
                        }
                        else
                        {
                            m = GetRrxC(context, m, setCarry);
                        }
                        break;
                }
            }

            return m;
        }

        public static int DecodeImmShift(ShiftType shiftType, int shift)
        {
            if (shift == 0)
            {
                switch (shiftType)
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

            return shift;
        }

        public static Operand GetMShiftedByReg(ArmEmitterContext context, IOpCode32AluRsReg op, bool setCarry)
        {
            Operand m = GetIntA32(context, op.Rm);
            Operand s = context.ZeroExtend8(OperandType.I32, GetIntA32(context, op.Rs));
            Operand shiftIsZero = context.ICompareEqual(s, Const(0));

            Operand zeroResult = m;
            Operand shiftResult = m;

            setCarry &= ShouldSetFlags(context);

            switch (op.ShiftType)
            {
                case ShiftType.Lsl:
                    shiftResult = EmitLslC(context, m, setCarry, s, shiftIsZero);
                    break;
                case ShiftType.Lsr:
                    shiftResult = EmitLsrC(context, m, setCarry, s, shiftIsZero);
                    break;
                case ShiftType.Asr:
                    shiftResult = EmitAsrC(context, m, setCarry, s, shiftIsZero);
                    break;
                case ShiftType.Ror:
                    shiftResult = EmitRorC(context, m, setCarry, s, shiftIsZero);
                    break;
            }

            return context.ConditionalSelect(shiftIsZero, zeroResult, shiftResult);
        }

        public static void EmitIfHelper(ArmEmitterContext context, Operand boolValue, Action action, bool expected = true)
        {
            Debug.Assert(boolValue.Type == OperandType.I32);

            Operand endLabel = Label();

            if (expected)
            {
                context.BranchIfFalse(endLabel, boolValue);
            }
            else
            {
                context.BranchIfTrue(endLabel, boolValue);
            }

            action();

            context.MarkLabel(endLabel);
        }

        public static Operand EmitLslC(ArmEmitterContext context, Operand m, bool setCarry, Operand shift, Operand shiftIsZero)
        {
            Debug.Assert(m.Type == OperandType.I32 && shift.Type == OperandType.I32 && shiftIsZero.Type == OperandType.I32);

            Operand shiftLarge = context.ICompareGreaterOrEqual(shift, Const(32));
            Operand result = context.ShiftLeft(m, shift);
            if (setCarry)
            {
                EmitIfHelper(context, shiftIsZero, () =>
                {
                    Operand cOut = context.ShiftRightUI(m, context.Subtract(Const(32), shift));

                    cOut = context.BitwiseAnd(cOut, Const(1));
                    cOut = context.ConditionalSelect(context.ICompareGreater(shift, Const(32)), Const(0), cOut);

                    SetFlag(context, PState.CFlag, cOut);
                }, false);
            }

            return context.ConditionalSelect(shiftLarge, Const(0), result);
        }

        public static Operand GetLslC(ArmEmitterContext context, Operand m, bool setCarry, int shift)
        {
            Debug.Assert(m.Type == OperandType.I32);

            if ((uint)shift > 32)
            {
                return GetShiftByMoreThan32(context, setCarry);
            }
            else if (shift == 32)
            {
                if (setCarry)
                {
                    SetCarryMLsb(context, m);
                }

                return Const(0);
            }
            else
            {
                if (setCarry)
                {
                    Operand cOut = context.ShiftRightUI(m, Const(32 - shift));

                    cOut = context.BitwiseAnd(cOut, Const(1));

                    SetFlag(context, PState.CFlag, cOut);
                }

                return context.ShiftLeft(m, Const(shift));
            }
        }

        public static Operand EmitLsrC(ArmEmitterContext context, Operand m, bool setCarry, Operand shift, Operand shiftIsZero)
        {
            Debug.Assert(m.Type == OperandType.I32 && shift.Type == OperandType.I32 && shiftIsZero.Type == OperandType.I32);

            Operand shiftLarge = context.ICompareGreaterOrEqual(shift, Const(32));
            Operand result = context.ShiftRightUI(m, shift);
            if (setCarry)
            {
                EmitIfHelper(context, shiftIsZero, () =>
                {
                    Operand cOut = context.ShiftRightUI(m, context.Subtract(shift, Const(1)));

                    cOut = context.BitwiseAnd(cOut, Const(1));
                    cOut = context.ConditionalSelect(context.ICompareGreater(shift, Const(32)), Const(0), cOut);

                    SetFlag(context, PState.CFlag, cOut);
                }, false);
            }

            return context.ConditionalSelect(shiftLarge, Const(0), result);
        }

        public static Operand GetLsrC(ArmEmitterContext context, Operand m, bool setCarry, int shift)
        {
            Debug.Assert(m.Type == OperandType.I32);

            if ((uint)shift > 32)
            {
                return GetShiftByMoreThan32(context, setCarry);
            }
            else if (shift == 32)
            {
                if (setCarry)
                {
                    SetCarryMMsb(context, m);
                }

                return Const(0);
            }
            else
            {
                if (setCarry)
                {
                    SetCarryMShrOut(context, m, shift);
                }

                return context.ShiftRightUI(m, Const(shift));
            }
        }

        private static Operand GetShiftByMoreThan32(ArmEmitterContext context, bool setCarry)
        {
            if (setCarry)
            {
                SetFlag(context, PState.CFlag, Const(0));
            }

            return Const(0);
        }

        public static Operand EmitAsrC(ArmEmitterContext context, Operand m, bool setCarry, Operand shift, Operand shiftIsZero)
        {
            Debug.Assert(m.Type == OperandType.I32 && shift.Type == OperandType.I32 && shiftIsZero.Type == OperandType.I32);

            Operand l32Result;
            Operand ge32Result;

            Operand less32 = context.ICompareLess(shift, Const(32));

            ge32Result = context.ShiftRightSI(m, Const(31));

            if (setCarry)
            {
                EmitIfHelper(context, context.BitwiseOr(less32, shiftIsZero), () =>
                {
                    SetCarryMLsb(context, ge32Result);
                }, false);
            }

            l32Result = context.ShiftRightSI(m, shift);
            if (setCarry)
            {
                EmitIfHelper(context, context.BitwiseAnd(less32, context.BitwiseNot(shiftIsZero)), () =>
                {
                    Operand cOut = context.ShiftRightUI(m, context.Subtract(shift, Const(1)));

                    cOut = context.BitwiseAnd(cOut, Const(1));

                    SetFlag(context, PState.CFlag, cOut);
                });
            }

            return context.ConditionalSelect(less32, l32Result, ge32Result);
        }

        public static Operand GetAsrC(ArmEmitterContext context, Operand m, bool setCarry, int shift)
        {
            Debug.Assert(m.Type == OperandType.I32);

            if ((uint)shift >= 32)
            {
                m = context.ShiftRightSI(m, Const(31));

                if (setCarry)
                {
                    SetCarryMLsb(context, m);
                }

                return m;
            }
            else
            {
                if (setCarry)
                {
                    SetCarryMShrOut(context, m, shift);
                }

                return context.ShiftRightSI(m, Const(shift));
            }
        }

        public static Operand EmitRorC(ArmEmitterContext context, Operand m, bool setCarry, Operand shift, Operand shiftIsZero)
        {
            Debug.Assert(m.Type == OperandType.I32 && shift.Type == OperandType.I32 && shiftIsZero.Type == OperandType.I32);

            shift = context.BitwiseAnd(shift, Const(0x1f));
            m = context.RotateRight(m, shift);

            if (setCarry)
            {
                EmitIfHelper(context, shiftIsZero, () =>
                {
                    SetCarryMMsb(context, m);
                }, false);
            }

            return m;
        }

        public static Operand GetRorC(ArmEmitterContext context, Operand m, bool setCarry, int shift)
        {
            Debug.Assert(m.Type == OperandType.I32);

            shift &= 0x1f;

            m = context.RotateRight(m, Const(shift));

            if (setCarry)
            {
                SetCarryMMsb(context, m);
            }

            return m;
        }

        public static Operand GetRrxC(ArmEmitterContext context, Operand m, bool setCarry)
        {
            Debug.Assert(m.Type == OperandType.I32);

            // Rotate right by 1 with carry.
            Operand cIn = context.Copy(GetFlag(PState.CFlag));

            if (setCarry)
            {
                SetCarryMLsb(context, m);
            }

            m = context.ShiftRightUI(m, Const(1));

            m = context.BitwiseOr(m, context.ShiftLeft(cIn, Const(31)));

            return m;
        }

        private static void SetCarryMLsb(ArmEmitterContext context, Operand m)
        {
            Debug.Assert(m.Type == OperandType.I32);

            SetFlag(context, PState.CFlag, context.BitwiseAnd(m, Const(1)));
        }

        private static void SetCarryMMsb(ArmEmitterContext context, Operand m)
        {
            Debug.Assert(m.Type == OperandType.I32);

            SetFlag(context, PState.CFlag, context.ShiftRightUI(m, Const(31)));
        }

        private static void SetCarryMShrOut(ArmEmitterContext context, Operand m, int shift)
        {
            Debug.Assert(m.Type == OperandType.I32);

            Operand cOut = context.ShiftRightUI(m, Const(shift - 1));

            cOut = context.BitwiseAnd(cOut, Const(1));

            SetFlag(context, PState.CFlag, cOut);
        }
    }
}
