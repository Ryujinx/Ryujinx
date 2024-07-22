using System.Text;

namespace Ryujinx.HLE.Generators
{
    class CodeGenerator
    {
        private const int IndentLength = 4;

        private readonly StringBuilder _sb;
        private int _currentIndentCount;

        public CodeGenerator()
        {
            _sb = new StringBuilder();
        }

        public void EnterScope(string header = null)
        {
            if (header != null)
            {
                AppendLine(header);
            }

            AppendLine("{");
            IncreaseIndentation();
        }

        public void LeaveScope(string suffix = "")
        {
            DecreaseIndentation();
            AppendLine($"}}{suffix}");
        }

        public void IncreaseIndentation()
        {
            _currentIndentCount++;
        }

        public void DecreaseIndentation()
        {
            if (_currentIndentCount - 1 >= 0)
            {
                _currentIndentCount--;
            }
        }

        public void AppendLine()
        {
            _sb.AppendLine();
        }

        public void AppendLine(string text)
        {
            _sb.Append(' ', IndentLength * _currentIndentCount);
            _sb.AppendLine(text);
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }
}
