using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System.Text;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    class CodeGenContext
    {
        public const string Tab = "    ";

        public StructuredFunction CurrentFunction { get; set; }

        public StructuredProgramInfo Info { get; }

        public AttributeUsage AttributeUsage { get; }
        public ShaderDefinitions Definitions { get; }
        public ShaderProperties Properties { get; }
        public HostCapabilities HostCapabilities { get; }
        public ILogger Logger { get; }
        public TargetApi TargetApi { get; }

        public OperandManager OperandManager { get; }

        private readonly StringBuilder _sb;

        private int _level;

        private string _indentation;

        public CodeGenContext(StructuredProgramInfo info, CodeGenParameters parameters)
        {
            Info = info;
            AttributeUsage = parameters.AttributeUsage;
            Definitions = parameters.Definitions;
            Properties = parameters.Properties;
            HostCapabilities = parameters.HostCapabilities;
            Logger = parameters.Logger;
            TargetApi = parameters.TargetApi;

            OperandManager = new OperandManager();

            _sb = new StringBuilder();
        }

        public void AppendLine()
        {
            _sb.AppendLine();
        }

        public void AppendLine(string str)
        {
            _sb.AppendLine(_indentation + str);
        }

        public string GetCode()
        {
            return _sb.ToString();
        }

        public void EnterScope()
        {
            AppendLine("{");

            _level++;

            UpdateIndentation();
        }

        public void LeaveScope(string suffix = "")
        {
            if (_level == 0)
            {
                return;
            }

            _level--;

            UpdateIndentation();

            AppendLine("}" + suffix);
        }

        public StructuredFunction GetFunction(int id)
        {
            return Info.Functions[id];
        }

        private void UpdateIndentation()
        {
            _indentation = GetIndentation(_level);
        }

        private static string GetIndentation(int level)
        {
            string indentation = string.Empty;

            for (int index = 0; index < level; index++)
            {
                indentation += Tab;
            }

            return indentation;
        }
    }
}
