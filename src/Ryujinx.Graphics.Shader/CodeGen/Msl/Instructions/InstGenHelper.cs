using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;

using static Ryujinx.Graphics.Shader.CodeGen.Msl.TypeConversion;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGenHelper
    {
        private static readonly InstInfo[] _infoTable;

        static InstGenHelper()
        {
            _infoTable = new InstInfo[(int)Instruction.Count];

#pragma warning disable IDE0055 // Disable formatting
            Add(Instruction.AtomicAdd,                InstType.AtomicBinary,   "atomic_fetch_add_explicit");
            Add(Instruction.AtomicAnd,                InstType.AtomicBinary,   "atomic_fetch_and_explicit");
            Add(Instruction.AtomicCompareAndSwap,     InstType.AtomicBinary,   "atomic_compare_exchange_weak_explicit");
            Add(Instruction.AtomicMaxU32,             InstType.AtomicBinary,   "atomic_fetch_max_explicit");
            Add(Instruction.AtomicMinU32,             InstType.AtomicBinary,   "atomic_fetch_min_explicit");
            Add(Instruction.AtomicOr,                 InstType.AtomicBinary,   "atomic_fetch_or_explicit");
            Add(Instruction.AtomicSwap,               InstType.AtomicBinary,   "atomic_exchange_explicit");
            Add(Instruction.AtomicXor,                InstType.AtomicBinary,   "atomic_fetch_xor_explicit");
            Add(Instruction.Absolute,                 InstType.CallUnary,      "abs");
            Add(Instruction.Add,                      InstType.OpBinaryCom,    "+",  2);
            Add(Instruction.Ballot,                   InstType.Special);
            Add(Instruction.Barrier,                  InstType.Special);
            Add(Instruction.BitCount,                 InstType.CallUnary,      "popcount");
            Add(Instruction.BitfieldExtractS32,       InstType.CallTernary,    "extract_bits");
            Add(Instruction.BitfieldExtractU32,       InstType.CallTernary,    "extract_bits");
            Add(Instruction.BitfieldInsert,           InstType.CallQuaternary, "insert_bits");
            Add(Instruction.BitfieldReverse,          InstType.CallUnary,      "reverse_bits");
            Add(Instruction.BitwiseAnd,               InstType.OpBinaryCom,    "&",  6);
            Add(Instruction.BitwiseExclusiveOr,       InstType.OpBinaryCom,    "^",  7);
            Add(Instruction.BitwiseNot,               InstType.OpUnary,        "~",  0);
            Add(Instruction.BitwiseOr,                InstType.OpBinaryCom,    "|",  8);
            Add(Instruction.Call,                     InstType.Special);
            Add(Instruction.Ceiling,                  InstType.CallUnary,      "ceil");
            Add(Instruction.Clamp,                    InstType.CallTernary,    "clamp");
            Add(Instruction.ClampU32,                 InstType.CallTernary,    "clamp");
            Add(Instruction.CompareEqual,             InstType.OpBinaryCom,    "==", 5);
            Add(Instruction.CompareGreater,           InstType.OpBinary,       ">",  4);
            Add(Instruction.CompareGreaterOrEqual,    InstType.OpBinary,       ">=", 4);
            Add(Instruction.CompareGreaterOrEqualU32, InstType.OpBinary,       ">=", 4);
            Add(Instruction.CompareGreaterU32,        InstType.OpBinary,       ">",  4);
            Add(Instruction.CompareLess,              InstType.OpBinary,       "<",  4);
            Add(Instruction.CompareLessOrEqual,       InstType.OpBinary,       "<=", 4);
            Add(Instruction.CompareLessOrEqualU32,    InstType.OpBinary,       "<=", 4);
            Add(Instruction.CompareLessU32,           InstType.OpBinary,       "<",  4);
            Add(Instruction.CompareNotEqual,          InstType.OpBinaryCom,    "!=", 5);
            Add(Instruction.ConditionalSelect,        InstType.OpTernary,      "?:", 12);
            Add(Instruction.ConvertFP32ToFP64,        0); // MSL does not have a 64-bit FP
            Add(Instruction.ConvertFP64ToFP32,        0); // MSL does not have a 64-bit FP
            Add(Instruction.ConvertFP32ToS32,         InstType.CallUnary,      "int");
            Add(Instruction.ConvertFP32ToU32,         InstType.CallUnary,      "uint");
            Add(Instruction.ConvertFP64ToS32,         0); // MSL does not have a 64-bit FP
            Add(Instruction.ConvertFP64ToU32,         0); // MSL does not have a 64-bit FP
            Add(Instruction.ConvertS32ToFP32,         InstType.CallUnary,      "float");
            Add(Instruction.ConvertS32ToFP64,         0); // MSL does not have a 64-bit FP
            Add(Instruction.ConvertU32ToFP32,         InstType.CallUnary,      "float");
            Add(Instruction.ConvertU32ToFP64,         0); // MSL does not have a 64-bit FP
            Add(Instruction.Cosine,                   InstType.CallUnary,      "cos");
            Add(Instruction.Ddx,                      InstType.CallUnary,      "dfdx");
            Add(Instruction.Ddy,                      InstType.CallUnary,      "dfdy");
            Add(Instruction.Discard,                  InstType.CallNullary,    "discard_fragment");
            Add(Instruction.Divide,                   InstType.OpBinary,       "/",  1);
            Add(Instruction.EmitVertex,               0); // MSL does not have geometry shaders
            Add(Instruction.EndPrimitive,             0); // MSL does not have geometry shaders
            Add(Instruction.ExponentB2,               InstType.CallUnary,      "exp2");
            Add(Instruction.FSIBegin,                 InstType.Special);
            Add(Instruction.FSIEnd,                   InstType.Special);
            Add(Instruction.FindLSB,                  InstType.CallUnary,      HelperFunctionNames.FindLSB);
            Add(Instruction.FindMSBS32,               InstType.CallUnary,      HelperFunctionNames.FindMSBS32);
            Add(Instruction.FindMSBU32,               InstType.CallUnary,      HelperFunctionNames.FindMSBU32);
            Add(Instruction.Floor,                    InstType.CallUnary,      "floor");
            Add(Instruction.FusedMultiplyAdd,         InstType.CallTernary,    "fma");
            Add(Instruction.GroupMemoryBarrier,       InstType.Special);
            Add(Instruction.ImageLoad,                InstType.Special);
            Add(Instruction.ImageStore,               InstType.Special);
            Add(Instruction.ImageAtomic,              InstType.Special); // Metal 3.1+
            Add(Instruction.IsNan,                    InstType.CallUnary,      "isnan");
            Add(Instruction.Load,                     InstType.Special);
            Add(Instruction.Lod,                      InstType.Special);
            Add(Instruction.LogarithmB2,              InstType.CallUnary,      "log2");
            Add(Instruction.LogicalAnd,               InstType.OpBinaryCom,    "&&", 9);
            Add(Instruction.LogicalExclusiveOr,       InstType.OpBinaryCom,    "^",  10);
            Add(Instruction.LogicalNot,               InstType.OpUnary,        "!",  0);
            Add(Instruction.LogicalOr,                InstType.OpBinaryCom,    "||", 11);
            Add(Instruction.LoopBreak,                InstType.OpNullary,      "break");
            Add(Instruction.LoopContinue,             InstType.OpNullary,      "continue");
            Add(Instruction.PackDouble2x32,           0); // MSL does not have a 64-bit FP
            Add(Instruction.PackHalf2x16,             InstType.Special);
            Add(Instruction.Maximum,                  InstType.CallBinary,     "max");
            Add(Instruction.MaximumU32,               InstType.CallBinary,     "max");
            Add(Instruction.MemoryBarrier,            InstType.Special);
            Add(Instruction.Minimum,                  InstType.CallBinary,     "min");
            Add(Instruction.MinimumU32,               InstType.CallBinary,     "min");
            Add(Instruction.Modulo,                   InstType.CallBinary,     "fmod");
            Add(Instruction.Multiply,                 InstType.OpBinaryCom,    "*",  1);
            Add(Instruction.MultiplyHighS32,          InstType.CallBinary,     "mulhi");
            Add(Instruction.MultiplyHighU32,          InstType.CallBinary,     "mulhi");
            Add(Instruction.Negate,                   InstType.OpUnary,        "-");
            Add(Instruction.ReciprocalSquareRoot,     InstType.CallUnary,      "rsqrt");
            Add(Instruction.Return,                   InstType.OpNullary,      "return");
            Add(Instruction.Round,                    InstType.CallUnary,      "round");
            Add(Instruction.ShiftLeft,                InstType.OpBinary,       "<<", 3);
            Add(Instruction.ShiftRightS32,            InstType.OpBinary,       ">>", 3);
            Add(Instruction.ShiftRightU32,            InstType.OpBinary,       ">>", 3);
            Add(Instruction.Shuffle,                  InstType.CallBinary,     "simd_shuffle");
            Add(Instruction.ShuffleDown,              InstType.CallBinary,     "simd_shuffle_down");
            Add(Instruction.ShuffleUp,                InstType.CallBinary,     "simd_shuffle_up");
            Add(Instruction.ShuffleXor,               InstType.CallBinary,     "simd_shuffle_xor");
            Add(Instruction.Sine,                     InstType.CallUnary,      "sin");
            Add(Instruction.SquareRoot,               InstType.CallUnary,      "sqrt");
            Add(Instruction.Store,                    InstType.Special);
            Add(Instruction.Subtract,                 InstType.OpBinary,       "-",  2);
            Add(Instruction.SwizzleAdd,               InstType.CallTernary,    HelperFunctionNames.SwizzleAdd);
            Add(Instruction.TextureSample,            InstType.Special);
            Add(Instruction.TextureQuerySamples,      InstType.Special);
            Add(Instruction.TextureQuerySize,         InstType.Special);
            Add(Instruction.Truncate,                 InstType.CallUnary,      "trunc");
            Add(Instruction.UnpackDouble2x32,         0); // MSL does not have a 64-bit FP
            Add(Instruction.UnpackHalf2x16,           InstType.Special);
            Add(Instruction.VectorExtract,            InstType.Special);
            Add(Instruction.VoteAll,                  InstType.CallUnary,      "simd_all");
            Add(Instruction.VoteAllEqual,             InstType.Special);
            Add(Instruction.VoteAny,                  InstType.CallUnary,      "simd_any");
#pragma warning restore IDE0055
        }

        private static void Add(Instruction inst, InstType flags, string opName = null, int precedence = 0)
        {
            _infoTable[(int)inst] = new InstInfo(flags, opName, precedence);
        }

        public static InstInfo GetInstructionInfo(Instruction inst)
        {
            return _infoTable[(int)(inst & Instruction.Mask)];
        }

        public static string GetSourceExpr(CodeGenContext context, IAstNode node, AggregateType dstType)
        {
            return ReinterpretCast(context, node, OperandManager.GetNodeDestType(context, node), dstType);
        }

        public static string Enclose(string expr, IAstNode node, Instruction pInst, bool isLhs)
        {
            InstInfo pInfo = GetInstructionInfo(pInst);

            return Enclose(expr, node, pInst, pInfo, isLhs);
        }

        public static string Enclose(string expr, IAstNode node, Instruction pInst, InstInfo pInfo, bool isLhs = false)
        {
            if (NeedsParenthesis(node, pInst, pInfo, isLhs))
            {
                expr = "(" + expr + ")";
            }

            return expr;
        }

        public static bool NeedsParenthesis(IAstNode node, Instruction pInst, InstInfo pInfo, bool isLhs)
        {
            // If the node isn't an operation, then it can only be an operand,
            // and those never needs to be surrounded in parentheses.
            if (node is not AstOperation operation)
            {
                // This is sort of a special case, if this is a negative constant,
                // and it is consumed by a unary operation, we need to put on the parenthesis,
                // as in MSL, while a sequence like ~-1 is valid, --2 is not.
                if (IsNegativeConst(node) && pInfo.Type == InstType.OpUnary)
                {
                    return true;
                }

                return false;
            }

            if ((pInfo.Type & (InstType.Call | InstType.Special)) != 0)
            {
                return false;
            }

            InstInfo info = _infoTable[(int)(operation.Inst & Instruction.Mask)];

            if ((info.Type & (InstType.Call | InstType.Special)) != 0)
            {
                return false;
            }

            if (info.Precedence < pInfo.Precedence)
            {
                return false;
            }

            if (info.Precedence == pInfo.Precedence && isLhs)
            {
                return false;
            }

            if (pInst == operation.Inst && info.Type == InstType.OpBinaryCom)
            {
                return false;
            }

            return true;
        }

        private static bool IsNegativeConst(IAstNode node)
        {
            if (node is not AstOperand operand)
            {
                return false;
            }

            return operand.Type == OperandType.Constant && operand.Value < 0;
        }
    }
}
