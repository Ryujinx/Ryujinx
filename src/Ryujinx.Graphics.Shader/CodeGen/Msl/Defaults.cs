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

        public const int MaxUniformBuffersPerStage = 18;
        public const int MaxStorageBuffersPerStage = 16;
        public const int MaxTexturesPerStage = 64;

        public const uint ConstantBuffersIndex = 20;
        public const uint StorageBuffersIndex = 21;
        public const uint TexturesIndex = 22;
    }
}
