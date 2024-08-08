using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGenBarrier
    {
        public static string Barrier(CodeGenContext context, AstOperation operation)
        {
            var device = (operation.Inst & Instruction.Mask) == Instruction.MemoryBarrier;

            return $"threadgroup_barrier(mem_flags::mem_{(device ? "device" : "threadgroup")})";
        }
    }
}
