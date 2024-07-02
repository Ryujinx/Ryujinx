namespace Ryujinx.Graphics.Metal
{
    static class Constants
    {
        // TODO: Check these values, these were largely copied from Vulkan
        public const int MaxShaderStages = 5;
        public const int MaxVertexBuffers = 16;
        public const int MaxUniformBuffersPerStage = 18;
        public const int MaxStorageBuffersPerStage = 16;
        public const int MaxTexturesPerStage = 64;
        public const int MaxUniformBufferBindings = MaxUniformBuffersPerStage * MaxShaderStages;
        public const int MaxStorageBufferBindings = MaxStorageBuffersPerStage * MaxShaderStages;
        public const int MaxTextureBindings = MaxTexturesPerStage * MaxShaderStages;
        public const int MaxColorAttachments = 8;
        public const int MaxViewports = 16;
        // TODO: Check this value
        public const int MaxVertexAttributes = 31;
        // TODO: Check this value
        public const int MaxVertexLayouts = 31;

        public const int MinResourceAlignment = 16;

        // Must match constants set in shader generation
        public const uint ZeroBufferIndex = 18;

        public const uint ConstantBuffersIndex = 20;
        public const uint StorageBuffersIndex = 21;
        public const uint TexturesIndex = 22;
        public const uint ImagessIndex = 23;
    }
}
