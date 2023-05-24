using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class MslGenerator
    {
        public static string Generate(StructuredProgramInfo info, ShaderConfig config)
        {
            CodeGenContext context = new CodeGenContext(info, config);

            Declarations.Declare(context, info);

            return context.GetCode();
        }
    }
}