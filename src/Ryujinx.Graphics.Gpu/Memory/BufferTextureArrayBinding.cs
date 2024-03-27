using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.Shader;
using Ryujinx.Memory.Range;
using System;

namespace Ryujinx.Graphics.Gpu.Memory
{
    /// <summary>
    /// A buffer binding to apply to a buffer texture array element.
    /// </summary>
    readonly struct BufferTextureArrayBinding<T>
    {
        public T Array { get; }

        /// <summary>
        /// The buffer texture.
        /// </summary>
        public ITexture Texture { get; }

        /// <summary>
        /// Physical ranges of memory where the buffer texture data is located.
        /// </summary>
        public MultiRange Range { get; }

        /// <summary>
        /// The image or sampler binding info for the buffer texture.
        /// </summary>
        public TextureBindingInfo BindingInfo { get; }

        /// <summary>
        /// Index of the binding on the array.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// The image format for the binding.
        /// </summary>
        public Format Format { get; }

        /// <summary>
        /// Create a new buffer texture binding.
        /// </summary>
        /// <param name="texture">Buffer texture</param>
        /// <param name="range">Physical ranges of memory where the buffer texture data is located</param>
        /// <param name="bindingInfo">Binding info</param>
        /// <param name="index">Index of the binding on the array</param>
        /// <param name="format">Binding format</param>
        public BufferTextureArrayBinding(
            T array,
            ITexture texture,
            MultiRange range,
            TextureBindingInfo bindingInfo,
            int index,
            Format format)
        {
            Array = array;
            Texture = texture;
            Range = range;
            BindingInfo = bindingInfo;
            Index = index;
            Format = format;
        }
    }
}
