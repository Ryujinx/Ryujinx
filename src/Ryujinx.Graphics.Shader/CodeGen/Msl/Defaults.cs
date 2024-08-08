namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class Defaults
    {
        public const string LocalNamePrefix = "temp";

        public const string PerPatchAttributePrefix = "patchAttr";
        public const string IAttributePrefix = "inAttr";
        public const string OAttributePrefix = "outAttr";

        public const string StructPrefix = "struct";

        public const string ArgumentNamePrefix = "a";

        public const string UndefinedName = "0";

        public const int MaxVertexBuffers = 16;

        public const uint ZeroBufferIndex = MaxVertexBuffers;
        public const uint BaseSetIndex = MaxVertexBuffers + 1;

        public const uint ConstantBuffersIndex = BaseSetIndex;
        public const uint StorageBuffersIndex = BaseSetIndex + 1;
        public const uint TexturesIndex = BaseSetIndex + 2;
        public const uint ImagesIndex = BaseSetIndex + 3;

        public const uint ConstantBuffersSetIndex = 0;
        public const uint StorageBuffersSetIndex = 1;
        public const uint TexturesSetIndex = 2;
        public const uint ImagesSetIndex = 3;

        public const int TotalClipDistances = 8;
    }
}
