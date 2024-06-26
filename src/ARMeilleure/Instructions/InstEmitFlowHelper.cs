using ARMeilleure.CodeGen.Linking;
using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.State;
using ARMeilleure.Translation;
using ARMeilleure.Translation.PTC;

using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.IntermediateRepresentation.Operand.Factory;

namespace ARMeilleure.Instructions
{
    static class InstEmitFlowHelper
    {
        public static void EmitCondBranch(ArmEmitterContext context, Operand target, Condition cond)
        {
            if (cond != Condition.Al)
            {
                context.BranchIfTrue(target, GetCondTrue(context, cond));
            }
            else
            {
                context.Branch(target);
            }
        }

        public static Operand GetCondTrue(ArmEmitterContext context, Condition condition)
        {
            Operand cmpResult = context.TryGetComparisonResult(condition);

            if (cmpResult != default)
            {
                return cmpResult;
            }

            Operand value = Const(1);

            Operand Inverse(Operand val)
            {
                return context.BitwiseExclusiveOr(val, Const(1));
            }

            switch (condition)
            {
                case Condition.Eq:
                    value = GetFlag(PState.ZFlag);
                    break;

                case Condition.Ne:
                    value = Inverse(GetFlag(PState.ZFlag));
                    break;

                case Condition.GeUn:
                    value = GetFlag(PState.CFlag);
                    break;

                case Condition.LtUn:
                    value = Inverse(GetFlag(PState.CFlag));
                    break;

                case Condition.Mi:
                    value = GetFlag(PState.NFlag);
                    break;

                case Condition.Pl:
                    value = Inverse(GetFlag(PState.NFlag));
                    break;

                case Condition.Vs:
                    value = GetFlag(PState.VFlag);
                    break;

                case Condition.Vc:
                    value = Inverse(GetFlag(PState.VFlag));
                    break;

                case Condition.GtUn:
                    {
                        Operand c = GetFlag(PState.CFlag);
                        Operand z = GetFlag(PState.ZFlag);

                        value = context.BitwiseAnd(c, Inverse(z));

                        break;
                    }

                case Condition.LeUn:
                    {
                        Operand c = GetFlag(PState.CFlag);
                        Operand z = GetFlag(PState.ZFlag);

                        value = context.BitwiseOr(Inverse(c), z);

                        break;
                    }

                case Condition.Ge:
                    {
                        Operand n = GetFlag(PState.NFlag);
                        Operand v = GetFlag(PState.VFlag);

                        value = context.ICompareEqual(n, v);

                        break;
                    }

                case Condition.Lt:
                    {
                        Operand n = GetFlag(PState.NFlag);
                        Operand v = GetFlag(PState.VFlag);

                        value = context.ICompareNotEqual(n, v);

                        break;
                    }

                case Condition.Gt:
                    {
                        Operand n = GetFlag(PState.NFlag);
                        Operand z = GetFlag(PState.ZFlag);
                        Operand v = GetFlag(PState.VFlag);

                        value = context.BitwiseAnd(Inverse(z), context.ICompareEqual(n, v));

                        break;
                    }

                case Condition.Le:
                    {
                        Operand n = GetFlag(PState.NFlag);
                        Operand z = GetFlag(PState.ZFlag);
                        Operand v = GetFlag(PState.VFlag);

                        value = context.BitwiseOr(z, context.ICompareNotEqual(n, v));

                        break;
                    }
            }

            return value;
        }

        public static void EmitCall(ArmEmitterContext context, ulong immediate)
        {
            bool isRecursive = immediate == context.EntryAddress;

            if (isRecursive)
            {
                context.Branch(context.GetLabel(immediate));
            }
            else
            {
                EmitTableBranch(context, Const(immediate), isJump: false);
            }
        }

        public static void EmitVirtualCall(ArmEmitterContext context, Operand target)
        {
            EmitTableBranch(context, target, isJump: false);
        }

        public static void EmitVirtualJump(ArmEmitterContext context, Operand target, bool isReturn)
        {
            if (isReturn)
            {
                if (target.Type == OperandType.I32)
                {
                    target = context.ZeroExtend32(OperandType.I64, target);
                }

                context.Return(target);
            }
            else
            {
                EmitTableBranch(context, target, isJump: true);
            }
        }

        private static void EmitTableBranch(ArmEmitterContext context, Operand guestAddress, bool isJump)
        {
            context.StoreToContext();

            if (guestAddress.Type == OperandType.I32)
            {
                guestAddress = context.ZeroExtend32(OperandType.I64, guestAddress);
            }

            // Store the target guest address into the native context. The stubs uses this address to dispatch into the
            // next translation.
            Operand nativeContext = context.LoadArgument(OperandType.I64, 0);
            Operand dispAddressAddr = context.Add(nativeContext, Const((ulong)NativeContext.GetDispatchAddressOffset()));
            context.Store(dispAddressAddr, guestAddress);

            Operand hostAddress;

            var table = context.FunctionTable;

            // If address is mapped onto the function table, we can skip the table walk. Otherwise we fallback
            // onto the dispatch stub.
            if (guestAddress.Kind == OperandKind.Constant && context.FunctionTable.IsValid(guestAddress.Value))
            {
                Operand hostAddressAddr = !context.HasPtc ?
                    Const(ref context.FunctionTable.GetValue(guestAddress.Value)) :
                    Const(ref context.FunctionTable.GetValue(guestAddress.Value), new Symbol(SymbolType.FunctionTable, guestAddress.Value));

                hostAddress = context.Load(OperandType.I64, hostAddressAddr);
            }
            else if (table.Sparse && table.Levels.Length == 2)
            {
                // Inline table lookup. Only enabled when the sparse function table is enabled with 2 levels.
                // Deliberately attempts to avoid branches.

                var level0 = table.Levels[0];
                int clearBits0 = 64 - (level0.Index + level0.Length);

                Operand index = context.ShiftLeft(
                    context.ShiftRightUI(context.ShiftLeft(guestAddress, Const(clearBits0)), Const(clearBits0 + level0.Index)),
                    Const(3)
                    );

                Operand tableBase = !context.HasPtc ?
                    Const(table.Base) :
                    Const(table.Base, Ptc.FunctionTableSymbol);

                Operand page = context.Load(OperandType.I64, context.Add(tableBase, index));

                // Second level
                var level1 = table.Levels[1];
                int clearBits1 = 64 - (level1.Index + level1.Length);

                Operand index2 = context.ShiftLeft(
                    context.ShiftRightUI(context.ShiftLeft(guestAddress, Const(clearBits1)), Const(clearBits1 + level1.Index)),
                    Const(3)
                    );

                hostAddress = context.Load(OperandType.I64, context.Add(page, index2));
            }
            else
            {
                hostAddress = !context.HasPtc ?
                    Const((long)context.Stubs.DispatchStub) :
                    Const((long)context.Stubs.DispatchStub, Ptc.DispatchStubSymbol);
            }

            if (isJump)
            {
                context.Tailcall(hostAddress, nativeContext);
            }
            else
            {
                OpCode op = context.CurrOp;

                Operand returnAddress = context.Call(hostAddress, OperandType.I64, nativeContext);

                context.LoadFromContext();

                // Note: The return value of a translated function is always an Int64 with the address execution has
                // returned to. We expect this address to be immediately after the current instruction, if it isn't we
                // keep returning until we reach the dispatcher.
                Operand nextAddr = Const((long)op.Address + op.OpCodeSizeInBytes);

                // Try to continue within this block.
                // If the return address isn't to our next instruction, we need to return so the JIT can figure out
                // what to do.
                Operand lblContinue = context.GetLabel(nextAddr.Value);
                context.BranchIf(lblContinue, returnAddress, nextAddr, Comparison.Equal, BasicBlockFrequency.Cold);

                context.Return(returnAddress);
            }
        }
    }
}
