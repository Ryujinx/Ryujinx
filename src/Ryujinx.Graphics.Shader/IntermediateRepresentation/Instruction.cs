using System;

namespace Ryujinx.Graphics.Shader.IntermediateRepresentation
{
    [Flags]
    enum Instruction
    {
        Absolute = 1,
        Add,
        AtomicAdd,
        AtomicAnd,
        AtomicCompareAndSwap,
        AtomicMinS32,
        AtomicMinU32,
        AtomicMaxS32,
        AtomicMaxU32,
        AtomicOr,
        AtomicSwap,
        AtomicXor,
        Ballot,
        Barrier,
        BitCount,
        BitfieldExtractS32,
        BitfieldExtractU32,
        BitfieldInsert,
        BitfieldReverse,
        BitwiseAnd,
        BitwiseExclusiveOr,
        BitwiseNot,
        BitwiseOr,
        Branch,
        BranchIfFalse,
        BranchIfTrue,
        Call,
        Ceiling,
        Clamp,
        ClampU32,
        Comment,
        CompareEqual,
        CompareGreater,
        CompareGreaterOrEqual,
        CompareGreaterOrEqualU32,
        CompareGreaterU32,
        CompareLess,
        CompareLessOrEqual,
        CompareLessOrEqualU32,
        CompareLessU32,
        CompareNotEqual,
        ConditionalSelect,
        ConvertFP32ToFP64,
        ConvertFP64ToFP32,
        ConvertFP32ToS32,
        ConvertFP32ToU32,
        ConvertFP64ToS32,
        ConvertFP64ToU32,
        ConvertS32ToFP32,
        ConvertS32ToFP64,
        ConvertU32ToFP32,
        ConvertU32ToFP64,
        Copy,
        Cosine,
        Ddx,
        Ddy,
        Discard,
        Divide,
        EmitVertex,
        EndPrimitive,
        ExponentB2,
        FSIBegin,
        FSIEnd,
        FindLSB,
        FindMSBS32,
        FindMSBU32,
        Floor,
        FusedMultiplyAdd,
        GroupMemoryBarrier,
        ImageLoad,
        ImageStore,
        ImageAtomic,
        IsNan,
        Load,
        Lod,
        LogarithmB2,
        LogicalAnd,
        LogicalExclusiveOr,
        LogicalNot,
        LogicalOr,
        LoopBreak,
        LoopContinue,
        MarkLabel,
        Maximum,
        MaximumU32,
        MemoryBarrier,
        Minimum,
        MinimumU32,
        Modulo,
        Multiply,
        MultiplyHighS32,
        MultiplyHighU32,
        Negate,
        PackDouble2x32,
        PackHalf2x16,
        ReciprocalSquareRoot,
        Return,
        Round,
        ShiftLeft,
        ShiftRightS32,
        ShiftRightU32,
        Shuffle,
        ShuffleDown,
        ShuffleUp,
        ShuffleXor,
        Sine,
        SquareRoot,
        Store,
        Subtract,
        SwizzleAdd,
        TextureSample,
        TextureQuerySamples,
        TextureQuerySize,
        Truncate,
        UnpackDouble2x32,
        UnpackHalf2x16,
        VectorExtract,
        VoteAll,
        VoteAllEqual,
        VoteAny,

        Count,

        FP32 = 1 << 16,
        FP64 = 1 << 17,

        Mask = 0xffff,
    }

    static class InstructionExtensions
    {
        public static bool IsAtomic(this Instruction inst)
        {
            switch (inst & Instruction.Mask)
            {
                case Instruction.AtomicAdd:
                case Instruction.AtomicAnd:
                case Instruction.AtomicCompareAndSwap:
                case Instruction.AtomicMaxS32:
                case Instruction.AtomicMaxU32:
                case Instruction.AtomicMinS32:
                case Instruction.AtomicMinU32:
                case Instruction.AtomicOr:
                case Instruction.AtomicSwap:
                case Instruction.AtomicXor:
                    return true;
            }

            return false;
        }

        public static bool IsTextureQuery(this Instruction inst)
        {
            inst &= Instruction.Mask;
            return inst == Instruction.Lod || inst == Instruction.TextureQuerySamples || inst == Instruction.TextureQuerySize;
        }

        public static bool IsImage(this Instruction inst)
        {
            inst &= Instruction.Mask;
            return inst == Instruction.ImageAtomic || inst == Instruction.ImageLoad || inst == Instruction.ImageStore;
        }

        public static bool IsImageStore(this Instruction inst)
        {
            inst &= Instruction.Mask;
            return inst == Instruction.ImageAtomic || inst == Instruction.ImageStore;
        }
    }
}
