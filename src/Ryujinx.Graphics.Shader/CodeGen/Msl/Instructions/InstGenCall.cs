using Ryujinx.Graphics.Shader.StructuredIr;

using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenHelper;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGenCall
    {
        public static string Call(CodeGenContext context, AstOperation operation)
        {
            AstOperand funcId = (AstOperand)operation.GetSource(0);

            var functon = context.GetFunction(funcId.Value);

            int argCount = operation.SourcesCount - 1;
            int additionalArgCount = CodeGenContext.AdditionalArgCount + (context.Definitions.Stage != ShaderStage.Compute ? 1 : 0);

            string[] args = new string[argCount + additionalArgCount];

            // Additional arguments
            if (context.Definitions.Stage != ShaderStage.Compute)
            {
                args[0] = "in";
                args[1] = "constant_buffers";
                args[2] = "storage_buffers";
            }
            else
            {
                args[0] = "constant_buffers";
                args[1] = "storage_buffers";
            }

            int argIndex = additionalArgCount;
            for (int i = 0; i < argCount; i++)
            {
                args[argIndex++] = GetSourceExpr(context, operation.GetSource(i + 1), functon.GetArgumentType(i));
            }

            return $"{functon.Name}({string.Join(", ", args)})";
        }
    }
}
