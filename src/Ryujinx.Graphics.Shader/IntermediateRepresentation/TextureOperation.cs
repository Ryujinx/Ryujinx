namespace Ryujinx.Graphics.Shader.IntermediateRepresentation
{
    class TextureOperation : Operation
    {
        public const int DefaultCbufSlot = -1;

        public SamplerType Type { get; set; }
        public TextureFormat Format { get; set; }
        public TextureFlags Flags { get; private set; }

        public int Binding { get; private set; }

        public TextureOperation(
            Instruction inst,
            SamplerType type,
            TextureFormat format,
            TextureFlags flags,
            int binding,
            int compIndex,
            Operand[] dests,
            Operand[] sources) : base(inst, compIndex, dests, sources)
        {
            Type = type;
            Format = format;
            Flags = flags;
            Binding = binding;
        }

        public void TurnIntoArray(int binding)
        {
            Flags &= ~TextureFlags.Bindless;
            Binding = binding;
        }

        public void SetBinding(int binding)
        {
            if ((Flags & TextureFlags.Bindless) != 0)
            {
                Flags &= ~TextureFlags.Bindless;

                RemoveSource(0);
            }

            Binding = binding;
        }

        public void SetLodLevelFlag()
        {
            Flags |= TextureFlags.LodLevel;
        }
    }
}
