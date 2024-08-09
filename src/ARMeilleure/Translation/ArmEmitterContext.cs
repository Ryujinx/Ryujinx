using ARMeilleure.CodeGen.Linking;
using ARMeilleure.Common;
using ARMeilleure.Decoders;
using ARMeilleure.Diagnostics;
using ARMeilleure.Instructions;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Memory;
using ARMeilleure.State;
using System;
using System.Collections.Generic;
using System.Reflection;
using static ARMeilleure.IntermediateRepresentation.Operand.Factory;

namespace ARMeilleure.Translation
{
    class ArmEmitterContext : EmitterContext
    {
        private readonly Dictionary<ulong, Operand> _labels;

        private OpCode _optOpLastCompare;
        private OpCode _optOpLastFlagSet;

        private Operand _optCmpTempN;
        private Operand _optCmpTempM;

        private Block _currBlock;

        public Block CurrBlock
        {
            get
            {
                return _currBlock;
            }
            set
            {
                _currBlock = value;

                ResetBlockState();
            }
        }

        private bool _pendingQcFlagSync;

        public OpCode CurrOp { get; set; }

        public IMemoryManager Memory { get; }

        public EntryTable<uint> CountTable { get; }
        public IAddressTable<ulong> FunctionTable { get; }
        public TranslatorStubs Stubs { get; }

        public ulong EntryAddress { get; }
        public bool HighCq { get; }
        public bool HasPtc { get; }
        public Aarch32Mode Mode { get; }

        private int _ifThenBlockStateIndex = 0;
        private Condition[] _ifThenBlockState = Array.Empty<Condition>();
        public bool IsInIfThenBlock => _ifThenBlockStateIndex < _ifThenBlockState.Length;
        public Condition CurrentIfThenBlockCond => _ifThenBlockState[_ifThenBlockStateIndex];

        public ArmEmitterContext(
            IMemoryManager memory,
            EntryTable<uint> countTable,
            IAddressTable<ulong> funcTable,
            TranslatorStubs stubs,
            ulong entryAddress,
            bool highCq,
            bool hasPtc,
            Aarch32Mode mode)
        {
            Memory = memory;
            CountTable = countTable;
            FunctionTable = funcTable;
            Stubs = stubs;
            EntryAddress = entryAddress;
            HighCq = highCq;
            HasPtc = hasPtc;
            Mode = mode;

            _labels = new Dictionary<ulong, Operand>();
        }

        public override Operand Call(MethodInfo info, params Operand[] callArgs)
        {
            SyncQcFlag();

            if (!HasPtc)
            {
                return base.Call(info, callArgs);
            }
            else
            {
                int index = Delegates.GetDelegateIndex(info);
                IntPtr funcPtr = Delegates.GetDelegateFuncPtrByIndex(index);

                OperandType returnType = GetOperandType(info.ReturnType);

                Symbol symbol = new(SymbolType.DelegateTable, (ulong)index);

                Symbols.Add((ulong)funcPtr.ToInt64(), info.Name);

                return Call(Const(funcPtr.ToInt64(), symbol), returnType, callArgs);
            }
        }

        public Operand GetLabel(ulong address)
        {
            if (!_labels.TryGetValue(address, out Operand label))
            {
                label = Label();

                _labels.Add(address, label);
            }

            return label;
        }

        public void MarkComparison(Operand n, Operand m)
        {
            _optOpLastCompare = CurrOp;

            _optCmpTempN = Copy(n);
            _optCmpTempM = Copy(m);
        }

        public void MarkFlagSet(PState stateFlag)
        {
            // Set this only if any of the NZCV flag bits were modified.
            // This is used to ensure that when emiting a direct IL branch
            // instruction for compare + branch sequences, we're not expecting
            // to use comparison values from an old instruction, when in fact
            // the flags were already overwritten by another instruction further along.
            if (stateFlag >= PState.VFlag)
            {
                _optOpLastFlagSet = CurrOp;
            }
        }

        private void ResetBlockState()
        {
            _optOpLastCompare = null;
            _optOpLastFlagSet = null;
        }

        public void SetPendingQcFlagSync()
        {
            _pendingQcFlagSync = true;
        }

        public void SyncQcFlag()
        {
            if (_pendingQcFlagSync)
            {
                if (Optimizations.UseAdvSimd)
                {
                    Operand fpsr = AddIntrinsicInt(Intrinsic.Arm64MrsFpsr);

                    uint qcFlagMask = (uint)FPSR.Qc;

                    Operand qcClearLabel = Label();

                    BranchIfFalse(qcClearLabel, BitwiseAnd(fpsr, Const(qcFlagMask)));

                    AddIntrinsicNoRet(Intrinsic.Arm64MsrFpsr, Const(0));
                    InstEmitHelper.SetFpFlag(this, FPState.QcFlag, Const(1));

                    MarkLabel(qcClearLabel);
                }

                _pendingQcFlagSync = false;
            }
        }

        public void ClearQcFlag()
        {
            if (Optimizations.UseAdvSimd)
            {
                AddIntrinsicNoRet(Intrinsic.Arm64MsrFpsr, Const(0));
            }
        }

        public void ClearQcFlagIfModified()
        {
            if (_pendingQcFlagSync && Optimizations.UseAdvSimd)
            {
                AddIntrinsicNoRet(Intrinsic.Arm64MsrFpsr, Const(0));
            }
        }

        public void EnterArmFpMode()
        {
            InstEmitSimdHelper.EnterArmFpMode(this, InstEmitHelper.GetFpFlag);
        }

        public void UpdateArmFpMode()
        {
            EnterArmFpMode();
        }

        public void ExitArmFpMode()
        {
            InstEmitSimdHelper.ExitArmFpMode(this, (flag, value) => InstEmitHelper.SetFpFlag(this, flag, value));
        }

        public Operand TryGetComparisonResult(Condition condition)
        {
            if (_optOpLastCompare == null || _optOpLastCompare != _optOpLastFlagSet)
            {
                return default;
            }

            Operand n = _optCmpTempN;
            Operand m = _optCmpTempM;

            InstName cmpName = _optOpLastCompare.Instruction.Name;

            if (cmpName == InstName.Subs)
            {
                switch (condition)
                {
#pragma warning disable IDE0055 // Disable formatting
                    case Condition.Eq:   return ICompareEqual           (n, m);
                    case Condition.Ne:   return ICompareNotEqual        (n, m);
                    case Condition.GeUn: return ICompareGreaterOrEqualUI(n, m);
                    case Condition.LtUn: return ICompareLessUI          (n, m);
                    case Condition.GtUn: return ICompareGreaterUI       (n, m);
                    case Condition.LeUn: return ICompareLessOrEqualUI   (n, m);
                    case Condition.Ge:   return ICompareGreaterOrEqual  (n, m);
                    case Condition.Lt:   return ICompareLess            (n, m);
                    case Condition.Gt:   return ICompareGreater         (n, m);
                    case Condition.Le:   return ICompareLessOrEqual     (n, m);
#pragma warning restore IDE0055
                }
            }
            else if (cmpName == InstName.Adds && _optOpLastCompare is IOpCodeAluImm op)
            {
                // There are several limitations that needs to be taken into account for CMN comparisons:
                // - The unsigned comparisons are not valid, as they depend on the
                // carry flag value, and they will have different values for addition and
                // subtraction. For addition, it's carry, and for subtraction, it's borrow.
                // So, we need to make sure we're not doing a unsigned compare for the CMN case.
                // - We can only do the optimization for the immediate variants,
                // because when the second operand value is exactly INT_MIN, we can't
                // negate the value as theres no positive counterpart.
                // Such invalid values can't be encoded on the immediate encodings.
                if (op.RegisterSize == RegisterSize.Int32)
                {
                    m = Const((int)-op.Immediate);
                }
                else
                {
                    m = Const(-op.Immediate);
                }

                switch (condition)
                {
#pragma warning disable IDE0055 // Disable formatting
                    case Condition.Eq: return ICompareEqual         (n, m);
                    case Condition.Ne: return ICompareNotEqual      (n, m);
                    case Condition.Ge: return ICompareGreaterOrEqual(n, m);
                    case Condition.Lt: return ICompareLess          (n, m);
                    case Condition.Gt: return ICompareGreater       (n, m);
                    case Condition.Le: return ICompareLessOrEqual   (n, m);
#pragma warning restore IDE0055
                }
            }

            return default;
        }

        public void SetIfThenBlockState(Condition[] state)
        {
            _ifThenBlockState = state;
            _ifThenBlockStateIndex = 0;
        }

        public void AdvanceIfThenBlockState()
        {
            if (IsInIfThenBlock)
            {
                _ifThenBlockStateIndex++;
            }
        }
    }
}
