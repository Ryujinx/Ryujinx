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

            if ((info.HelperFunctionsMask & HelperFunctionsMask.MultiplyHighS32) != 0)
            {

            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.MultiplyHighU32) != 0)
            {

            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.Shuffle) != 0)
            {

            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.ShuffleDown) != 0)
            {

            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.ShuffleUp) != 0)
            {

            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.ShuffleXor) != 0)
            {

            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.SwizzleAdd) != 0)
            {

            }
        }
    }
}