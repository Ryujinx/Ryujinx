namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    readonly struct InstInfo
    {
        public InstType Type { get; }

        public string OpName { get; }

        public InstInfo(InstType type, string opName)
        {
            Type = type;
            OpName = opName;
        }
    }
}
