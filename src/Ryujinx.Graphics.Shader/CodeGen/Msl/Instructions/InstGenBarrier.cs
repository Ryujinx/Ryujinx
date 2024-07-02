using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;

using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenHelper;
using static Ryujinx.Graphics.Shader.StructuredIr.InstructionInfo;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGenBarrier
    {
        public static string Barrier(CodeGenContext context, AstOperation operation)
        {
            return "threadgroup_barrier(mem_flags::mem_threadgroup)";
        }
    }
}
