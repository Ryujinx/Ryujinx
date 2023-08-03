using Ryujinx.Graphics.Shader.StructuredIr;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class Declarations
    {
        public static void Declare(CodeGenContext context, StructuredProgramInfo info)
        {
            context.AppendLine("#include <metal_stdlib>");
            context.AppendLine("#include <simd/simd.h>");
            context.AppendLine();
            context.AppendLine("using namespace metal;");
        }
    }
}