namespace Ryujinx.Graphics.Metal
{
    static class Constants
    {
        public const int MaxShaderStages = 5;
        public const int MaxVertexBuffers = 16;
        public const int MaxUniformBuffersPerStage = 18;
        public const int MaxStorageBuffersPerStage = 16;
        public const int MaxTexturesPerStage = 64;
        public const int MaxImagesPerStage = 16;

        public const int MaxUniformBufferBindings = MaxUniformBuffersPerStage * MaxShaderStages;
        public const int MaxStorageBufferBindings = MaxStorageBuffersPerStage * MaxShaderStages;
        public const int MaxTextureBindings = MaxTexturesPerStage * MaxShaderStages;
        public const int MaxImageBindings = MaxImagesPerStage * MaxShaderStages;
        public const int MaxColorAttachments = 8;
        public const int MaxViewports = 16;
        // TODO: Check this value
        public const int MaxVertexAttributes = 31;

        public const int MinResourceAlignment = 16;

        // Must match constants set in shader generation
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

        public const uint MaximumBufferArgumentTableEntries = 31;

        public const uint MaximumExtraSets = MaximumBufferArgumentTableEntries - ImagesIndex;
    }
}
