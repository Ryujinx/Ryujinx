using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;

using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenHelper;
using static Ryujinx.Graphics.Shader.StructuredIr.InstructionInfo;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGenBallot
    {
        public static string Ballot(CodeGenContext context, AstOperation operation)
        {
            AggregateType dstType = GetSrcVarType(operation.Inst, 0);

            string arg = GetSourceExpr(context, operation.GetSource(0), dstType);
            char component = "xyzw"[operation.Index];

            return $"uint4(as_type<uint2>((simd_vote::vote_t)simd_ballot({arg})), 0, 0).{component}";
        }

        public static string VoteAllEqual(CodeGenContext context, AstOperation operation)
        {
            AggregateType dstType = GetSrcVarType(operation.Inst, 0);

            string arg = GetSourceExpr(context, operation.GetSource(0), dstType);

            return $"simd_all({arg}) || !simd_any({arg})";
        }
    }
}
