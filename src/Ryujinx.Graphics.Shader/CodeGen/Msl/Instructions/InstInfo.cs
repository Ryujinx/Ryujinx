namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    readonly struct InstInfo
    {
        public InstType Type { get; }

        public string OpName { get; }

        public int Precedence { get; }

        public InstInfo(InstType type, string opName, int precedence)
        {
            Type = type;
            OpName = opName;
            Precedence = precedence;
        }
    }
}
