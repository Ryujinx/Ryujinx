using Ryujinx.Graphics.GAL;

namespace Ryujinx.Graphics.Gpu.Shader
{
    /// <summary>
    /// State used by the <see cref="GpuAccessor"/>.
    /// </summary>
    readonly struct GpuChannelComputeState
    {
        // New fields should be added to the end of the struct to keep disk shader cache compatibility.

        /// <summary>
        /// Local group size X of the compute shader.
        /// </summary>
        public readonly int LocalSizeX;

        /// <summary>
        /// Local group size Y of the compute shader.
        /// </summary>
        public readonly int LocalSizeY;

        /// <summary>
        /// Local group size Z of the compute shader.
        /// </summary>
        public readonly int LocalSizeZ;

        /// <summary>
        /// Local memory size of the compute shader.
        /// </summary>
        public readonly int LocalMemorySize;

        /// <summary>
        /// Shared memory size of the compute shader.
        /// </summary>
        public readonly int SharedMemorySize;

        /// <summary>
        /// Indicates that any storage buffer use is unaligned.
        /// </summary>
        public readonly bool HasUnalignedStorageBuffer;

        /// <summary>
        /// Creates a new GPU compute state.
        /// </summary>
        /// <param name="localSizeX">Local group size X of the compute shader</param>
        /// <param name="localSizeY">Local group size Y of the compute shader</param>
        /// <param name="localSizeZ">Local group size Z of the compute shader</param>
        /// <param name="localMemorySize">Local memory size of the compute shader</param>
        /// <param name="sharedMemorySize">Shared memory size of the compute shader</param>
        /// <param name="hasUnalignedStorageBuffer">Indicates that any storage buffer use is unaligned</param>
        public GpuChannelComputeState(
            int localSizeX,
            int localSizeY,
            int localSizeZ,
            int localMemorySize,
            int sharedMemorySize,
            bool hasUnalignedStorageBuffer)
        {
            LocalSizeX = localSizeX;
            LocalSizeY = localSizeY;
            LocalSizeZ = localSizeZ;
            LocalMemorySize = localMemorySize;
            SharedMemorySize = sharedMemorySize;
            HasUnalignedStorageBuffer = hasUnalignedStorageBuffer;
        }

        /// <summary>
        /// Gets the local group size of the shader in a GAL compatible struct.
        /// </summary>
        /// <returns>Local group size</returns>
        public ComputeSize GetLocalSize()
        {
            return new ComputeSize(LocalSizeX, LocalSizeY, LocalSizeZ);
        }
    }
}
