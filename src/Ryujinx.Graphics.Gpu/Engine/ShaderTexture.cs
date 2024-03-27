using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;

namespace Ryujinx.Graphics.Gpu.Engine
{
    /// <summary>
    /// Shader texture properties conversion methods.
    /// </summary>
    static class ShaderTexture
    {
        /// <summary>
        /// Gets a texture target from a sampler type.
        /// </summary>
        /// <param name="type">Sampler type</param>
        /// <returns>Texture target value</returns>
        public static Target GetTarget(SamplerType type)
        {
            type &= ~SamplerType.Shadow;

            switch (type)
            {
                case SamplerType.Texture1D:
                    return Target.Texture1D;

                case SamplerType.TextureBuffer:
                    return Target.TextureBuffer;

                case SamplerType.Texture1D | SamplerType.Array:
                    return Target.Texture1DArray;

                case SamplerType.Texture2D:
                    return Target.Texture2D;

                case SamplerType.Texture2D | SamplerType.Array:
                    return Target.Texture2DArray;

                case SamplerType.Texture2D | SamplerType.Multisample:
                    return Target.Texture2DMultisample;

                case SamplerType.Texture2D | SamplerType.Multisample | SamplerType.Array:
                    return Target.Texture2DMultisampleArray;

                case SamplerType.Texture3D:
                    return Target.Texture3D;

                case SamplerType.TextureCube:
                    return Target.Cubemap;

                case SamplerType.TextureCube | SamplerType.Array:
                    return Target.CubemapArray;
            }

            Logger.Warning?.Print(LogClass.Gpu, $"Invalid sampler type \"{type}\".");

            return Target.Texture2D;
        }

        /// <summary>
        /// Gets a texture format from a shader image format.
        /// </summary>
        /// <param name="format">Shader image format</param>
        /// <returns>Texture format</returns>
        public static Format GetFormat(TextureFormat format)
        {
            return format switch
            {
#pragma warning disable IDE0055 // Disable formatting
                TextureFormat.R8Unorm           => Format.R8Unorm,
                TextureFormat.R8Snorm           => Format.R8Snorm,
                TextureFormat.R8Uint            => Format.R8Uint,
                TextureFormat.R8Sint            => Format.R8Sint,
                TextureFormat.R16Float          => Format.R16Float,
                TextureFormat.R16Unorm          => Format.R16Unorm,
                TextureFormat.R16Snorm          => Format.R16Snorm,
                TextureFormat.R16Uint           => Format.R16Uint,
                TextureFormat.R16Sint           => Format.R16Sint,
                TextureFormat.R32Float          => Format.R32Float,
                TextureFormat.R32Uint           => Format.R32Uint,
                TextureFormat.R32Sint           => Format.R32Sint,
                TextureFormat.R8G8Unorm         => Format.R8G8Unorm,
                TextureFormat.R8G8Snorm         => Format.R8G8Snorm,
                TextureFormat.R8G8Uint          => Format.R8G8Uint,
                TextureFormat.R8G8Sint          => Format.R8G8Sint,
                TextureFormat.R16G16Float       => Format.R16G16Float,
                TextureFormat.R16G16Unorm       => Format.R16G16Unorm,
                TextureFormat.R16G16Snorm       => Format.R16G16Snorm,
                TextureFormat.R16G16Uint        => Format.R16G16Uint,
                TextureFormat.R16G16Sint        => Format.R16G16Sint,
                TextureFormat.R32G32Float       => Format.R32G32Float,
                TextureFormat.R32G32Uint        => Format.R32G32Uint,
                TextureFormat.R32G32Sint        => Format.R32G32Sint,
                TextureFormat.R8G8B8A8Unorm     => Format.R8G8B8A8Unorm,
                TextureFormat.R8G8B8A8Snorm     => Format.R8G8B8A8Snorm,
                TextureFormat.R8G8B8A8Uint      => Format.R8G8B8A8Uint,
                TextureFormat.R8G8B8A8Sint      => Format.R8G8B8A8Sint,
                TextureFormat.R16G16B16A16Float => Format.R16G16B16A16Float,
                TextureFormat.R16G16B16A16Unorm => Format.R16G16B16A16Unorm,
                TextureFormat.R16G16B16A16Snorm => Format.R16G16B16A16Snorm,
                TextureFormat.R16G16B16A16Uint  => Format.R16G16B16A16Uint,
                TextureFormat.R16G16B16A16Sint  => Format.R16G16B16A16Sint,
                TextureFormat.R32G32B32A32Float => Format.R32G32B32A32Float,
                TextureFormat.R32G32B32A32Uint  => Format.R32G32B32A32Uint,
                TextureFormat.R32G32B32A32Sint  => Format.R32G32B32A32Sint,
                TextureFormat.R10G10B10A2Unorm  => Format.R10G10B10A2Unorm,
                TextureFormat.R10G10B10A2Uint   => Format.R10G10B10A2Uint,
                TextureFormat.R11G11B10Float    => Format.R11G11B10Float,
                _                               => 0,
#pragma warning restore IDE0055
            };
        }
    }
}
