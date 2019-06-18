namespace ARMeilleure.IntermediateRepresentation
{
    enum Instruction
    {
        Add,
        BitwiseAnd,
        BitwiseExclusiveOr,
        BitwiseNot,
        BitwiseOr,
        Branch,
        BranchIfFalse,
        BranchIfTrue,
        ByteSwap,
        Call,
        CompareEqual,
        CompareGreater,
        CompareGreaterOrEqual,
        CompareGreaterOrEqualUI,
        CompareGreaterUI,
        CompareLess,
        CompareLessOrEqual,
        CompareLessOrEqualUI,
        CompareLessUI,
        CompareNotEqual,
        ConditionalSelect,
        ConvertToFP,
        ConvertToFPUI,
        Copy,
        CountLeadingZeros,
        Divide,
        DivideUI,
        Fill,
        Load,
        LoadFromContext,
        LoadSx16,
        LoadSx32,
        LoadSx8,
        LoadZx16,
        LoadZx8,
        Multiply,
        Multiply64HighSI,
        Multiply64HighUI,
        Negate,
        Return,
        RotateRight,
        ShiftLeft,
        ShiftRightSI,
        ShiftRightUI,
        SignExtend8,
        SignExtend16,
        SignExtend32,
        Spill,
        SpillArg,
        StackAlloc,
        Store,
        Store16,
        Store8,
        StoreToContext,
        Subtract,
        VectorExtract,
        VectorExtract16,
        VectorExtract8,
        VectorInsert,
        VectorInsert16,
        VectorInsert8,
        VectorZero,
        VectorZeroUpper64,
        VectorZeroUpper96,

        //Intrinsics
        X86Intrinsic_Start,
        X86Addpd,
        X86Addps,
        X86Addsd,
        X86Addss,
        X86Andnpd,
        X86Andnps,
        X86Cmppd,
        X86Cmpps,
        X86Cmpsd,
        X86Cmpss,
        X86Comisdeq,
        X86Comisdge,
        X86Comisdlt,
        X86Comisseq,
        X86Comissge,
        X86Comisslt,
        X86Cvtdq2pd,
        X86Cvtdq2ps,
        X86Cvtpd2dq,
        X86Cvtpd2ps,
        X86Cvtps2dq,
        X86Cvtps2pd,
        X86Cvtsd2si,
        X86Cvtsd2ss,
        X86Cvtss2sd,
        X86Divpd,
        X86Divps,
        X86Divsd,
        X86Divss,
        X86Haddpd,
        X86Haddps,
        X86Maxpd,
        X86Maxps,
        X86Maxsd,
        X86Maxss,
        X86Minpd,
        X86Minps,
        X86Minsd,
        X86Minss,
        X86Movhlps,
        X86Movlhps,
        X86Mulpd,
        X86Mulps,
        X86Mulsd,
        X86Mulss,
        X86Paddb,
        X86Paddd,
        X86Paddq,
        X86Paddw,
        X86Pand,
        X86Pandn,
        X86Pavgb,
        X86Pavgw,
        X86Pblendvb,
        X86Pcmpeqb,
        X86Pcmpeqd,
        X86Pcmpeqq,
        X86Pcmpeqw,
        X86Pcmpgtb,
        X86Pcmpgtd,
        X86Pcmpgtq,
        X86Pcmpgtw,
        X86Pmaxsb,
        X86Pmaxsd,
        X86Pmaxsw,
        X86Pmaxub,
        X86Pmaxud,
        X86Pmaxuw,
        X86Pminsb,
        X86Pminsd,
        X86Pminsw,
        X86Pminub,
        X86Pminud,
        X86Pminuw,
        X86Pmovsxbw,
        X86Pmovsxdq,
        X86Pmovsxwd,
        X86Pmovzxbw,
        X86Pmovzxdq,
        X86Pmovzxwd,
        X86Pmulld,
        X86Pmullw,
        X86Popcnt,
        X86Por,
        X86Pshufb,
        X86Pslld,
        X86Pslldq,
        X86Psllq,
        X86Psllw,
        X86Psrad,
        X86Psraw,
        X86Psrld,
        X86Psrlq,
        X86Psrldq,
        X86Psrlw,
        X86Psubb,
        X86Psubd,
        X86Psubq,
        X86Psubw,
        X86Punpckhbw,
        X86Punpckhdq,
        X86Punpckhqdq,
        X86Punpckhwd,
        X86Punpcklbw,
        X86Punpckldq,
        X86Punpcklqdq,
        X86Punpcklwd,
        X86Pxor,
        X86Rcpps,
        X86Rcpss,
        X86Roundpd,
        X86Roundps,
        X86Roundsd,
        X86Roundss,
        X86Rsqrtps,
        X86Rsqrtss,
        X86Shufpd,
        X86Shufps,
        X86Sqrtpd,
        X86Sqrtps,
        X86Sqrtsd,
        X86Sqrtss,
        X86Subpd,
        X86Subps,
        X86Subsd,
        X86Subss,
        X86Unpckhpd,
        X86Unpckhps,
        X86Unpcklpd,
        X86Unpcklps,
        X86Xorpd,
        X86Xorps,
        X86Intrinsic_End,

        Count
    }

    static class InstructionExtensions
    {
        public static bool IsComparison(this Instruction inst)
        {
            switch (inst)
            {
                case Instruction.CompareEqual:
                case Instruction.CompareGreater:
                case Instruction.CompareGreaterOrEqual:
                case Instruction.CompareGreaterOrEqualUI:
                case Instruction.CompareGreaterUI:
                case Instruction.CompareLess:
                case Instruction.CompareLessOrEqual:
                case Instruction.CompareLessOrEqualUI:
                case Instruction.CompareLessUI:
                case Instruction.CompareNotEqual:
                    return true;
            }

            return false;
        }

        public static bool IsMemory(this Instruction inst)
        {
            switch (inst)
            {
                case Instruction.Load:
                case Instruction.LoadSx16:
                case Instruction.LoadSx32:
                case Instruction.LoadSx8:
                case Instruction.LoadZx16:
                case Instruction.LoadZx8:
                case Instruction.Store:
                case Instruction.Store16:
                case Instruction.Store8:
                    return true;
            }

            return false;
        }

        public static bool IsShift(this Instruction inst)
        {
            switch (inst)
            {
                case Instruction.RotateRight:
                case Instruction.ShiftLeft:
                case Instruction.ShiftRightSI:
                case Instruction.ShiftRightUI:
                    return true;
            }

            return false;
        }
    }
}