using Ryujinx.Graphics.Shader.StructuredIr;

using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenHelper;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGenCall
    {
        public static string Call(CodeGenContext context, AstOperation operation)
        {
            AstOperand funcId = (AstOperand)operation.GetSource(0);

            var function = context.GetFunction(funcId.Value);

            int argCount = operation.SourcesCount - 1;
            int additionalArgCount = CodeGenContext.AdditionalArgCount + (context.Definitions.Stage != ShaderStage.Compute ? 1 : 0);
            bool needsThreadIndex = false;

            // TODO: Replace this with a proper flag
            if (function.Name.Contains("Shuffle"))
            {
                needsThreadIndex = true;
                additionalArgCount++;
            }

            string[] args = new string[argCount + additionalArgCount];

            // Additional arguments
            if (context.Definitions.Stage != ShaderStage.Compute)
            {
                args[0] = "in";
                args[1] = "constant_buffers";
                args[2] = "storage_buffers";

                if (needsThreadIndex)
                {
                    args[3] = "thread_index_in_simdgroup";
                }
            }
            else
            {
                args[0] = "constant_buffers";
                args[1] = "storage_buffers";

                if (needsThreadIndex)
                {
                    args[2] = "thread_index_in_simdgroup";
                }
            }

            int argIndex = additionalArgCount;
            for (int i = 0; i < argCount; i++)
            {
                args[argIndex++] = GetSourceExpr(context, operation.GetSource(i + 1), function.GetArgumentType(i));
            }

            return $"{function.Name}({string.Join(", ", args)})";
        }
    }
}
