using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class MslGenerator
    {
        public static string Generate(StructuredProgramInfo info, ShaderConfig config)
        {
            if (config.Stage is not (ShaderStage.Vertex or ShaderStage.Fragment or ShaderStage.Compute))
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Attempted to generate unsupported shader type {config.Stage}!");
                return "";
            }

            CodeGenContext context = new(info, config);

            Declarations.Declare(context, info);

            return context.GetCode();
        }
    }
}