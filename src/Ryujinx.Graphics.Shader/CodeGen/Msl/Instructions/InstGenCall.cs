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

            string[] args = new string[operation.SourcesCount - 1];

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = GetSourceExpr(context, operation.GetSource(i + 1), functon.GetArgumentType(i));
            }

            return $"{functon.Name}({string.Join(", ", args)})";
        }
    }
}
