namespace Ryujinx.Graphics.Metal
{
    static class Constants
    {
        // TODO: Check these values, these were largely copied from Vulkan
        public const int MaxShaderStages = 5;
        public const int MaxUniformBuffersPerStage = 18;
        public const int MaxStorageBuffersPerStage = 16;
        public const int MaxTexturesPerStage = 64;
        public const int MaxCommandBuffersPerQueue = 16;
        public const int MaxTextureBindings = MaxTexturesPerStage * MaxShaderStages;
    }
}