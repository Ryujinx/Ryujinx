namespace Ryujinx.Graphics.Shader
{
    readonly struct TextureDefinition
    {
        public int Set { get; }
        public int Binding { get; }
        public int ArrayLength { get; }
        public string Name { get; }
        public SamplerType Type { get; }
        public TextureFormat Format { get; }
        public TextureUsageFlags Flags { get; }

        public TextureDefinition(int set, int binding, int arrayLength, string name, SamplerType type, TextureFormat format, TextureUsageFlags flags)
        {
            Set = set;
            Binding = binding;
            ArrayLength = arrayLength;
            Name = name;
            Type = type;
            Format = format;
            Flags = flags;
        }

        public TextureDefinition SetFlag(TextureUsageFlags flag)
        {
            return new TextureDefinition(Set, Binding, ArrayLength, Name, Type, Format, Flags | flag);
        }
    }
}
