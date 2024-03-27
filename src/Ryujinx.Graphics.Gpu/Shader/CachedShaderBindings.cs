using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Engine;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.Shader;
using System;
using System.Linq;

namespace Ryujinx.Graphics.Gpu.Shader
{
    /// <summary>
    /// A collection of shader bindings ready for insertion into the buffer and texture managers.
    /// </summary>
    internal class CachedShaderBindings
    {
        public TextureBindingInfo[][] TextureBindings { get; }
        public TextureBindingInfo[][] ImageBindings { get; }
        public BufferDescriptor[][] ConstantBufferBindings { get; }
        public BufferDescriptor[][] StorageBufferBindings { get; }

        public int[] TextureCounts { get; }

        public int MaxTextureBinding { get; }
        public int MaxImageBinding { get; }

        /// <summary>
        /// Create a new cached shader bindings collection.
        /// </summary>
        /// <param name="isCompute">Whether the shader is for compute</param>
        /// <param name="stages">The stages used by the shader</param>
        public CachedShaderBindings(bool isCompute, CachedShaderStage[] stages)
        {
            int stageCount = isCompute ? 1 : Constants.ShaderStages;

            TextureBindings = new TextureBindingInfo[stageCount][];
            ImageBindings = new TextureBindingInfo[stageCount][];
            ConstantBufferBindings = new BufferDescriptor[stageCount][];
            StorageBufferBindings = new BufferDescriptor[stageCount][];

            TextureCounts = new int[stageCount];

            int maxTextureBinding = -1;
            int maxImageBinding = -1;
            int offset = isCompute ? 0 : 1;

            for (int i = 0; i < stageCount; i++)
            {
                CachedShaderStage stage = stages[i + offset];

                if (stage == null)
                {
                    TextureBindings[i] = Array.Empty<TextureBindingInfo>();
                    ImageBindings[i] = Array.Empty<TextureBindingInfo>();
                    ConstantBufferBindings[i] = Array.Empty<BufferDescriptor>();
                    StorageBufferBindings[i] = Array.Empty<BufferDescriptor>();

                    continue;
                }

                TextureBindings[i] = stage.Info.Textures.Select(descriptor =>
                {
                    Target target = ShaderTexture.GetTarget(descriptor.Type);

                    var result = new TextureBindingInfo(
                        target,
                        descriptor.Binding,
                        descriptor.ArrayLength,
                        descriptor.CbufSlot,
                        descriptor.HandleIndex,
                        descriptor.Flags);

                    if (descriptor.ArrayLength <= 1)
                    {
                        if (descriptor.Binding > maxTextureBinding)
                        {
                            maxTextureBinding = descriptor.Binding;
                        }

                        TextureCounts[i]++;
                    }

                    return result;
                }).ToArray();

                ImageBindings[i] = stage.Info.Images.Select(descriptor =>
                {
                    Target target = ShaderTexture.GetTarget(descriptor.Type);
                    Format format = ShaderTexture.GetFormat(descriptor.Format);

                    var result = new TextureBindingInfo(
                        target,
                        format,
                        descriptor.Binding,
                        descriptor.ArrayLength,
                        descriptor.CbufSlot,
                        descriptor.HandleIndex,
                        descriptor.Flags);

                    if (descriptor.ArrayLength <= 1 && descriptor.Binding > maxImageBinding)
                    {
                        maxImageBinding = descriptor.Binding;
                    }

                    return result;
                }).ToArray();

                ConstantBufferBindings[i] = stage.Info.CBuffers.ToArray();
                StorageBufferBindings[i] = stage.Info.SBuffers.ToArray();
            }

            MaxTextureBinding = maxTextureBinding;
            MaxImageBinding = maxImageBinding;
        }
    }
}
