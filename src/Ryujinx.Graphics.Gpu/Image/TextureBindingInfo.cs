using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;

namespace Ryujinx.Graphics.Gpu.Image
{
    /// <summary>
    /// Texture binding information.
    /// This is used for textures that needs to be accessed from shaders.
    /// </summary>
    readonly struct TextureBindingInfo
    {
        /// <summary>
        /// Shader sampler target type.
        /// </summary>
        public Target Target { get; }

        /// <summary>
        /// For images, indicates the format specified on the shader.
        /// </summary>
        public Format Format { get; }

        /// <summary>
        /// Shader texture host binding point.
        /// </summary>
        public int Binding { get; }

        /// <summary>
        /// For array of textures, this indicates the length of the array. A value of one indicates it is not an array.
        /// </summary>
        public int ArrayLength { get; }

        /// <summary>
        /// Constant buffer slot with the texture handle.
        /// </summary>
        public int CbufSlot { get; }

        /// <summary>
        /// Index of the texture handle on the constant buffer at slot <see cref="CbufSlot"/>.
        /// </summary>
        public int Handle { get; }

        /// <summary>
        /// Flags from the texture descriptor that indicate how the texture is used.
        /// </summary>
        public TextureUsageFlags Flags { get; }

        /// <summary>
        /// Constructs the texture binding information structure.
        /// </summary>
        /// <param name="target">The shader sampler target type</param>
        /// <param name="format">Format of the image as declared on the shader</param>
        /// <param name="binding">The shader texture binding point</param>
        /// <param name="arrayLength">For array of textures, this indicates the length of the array. A value of one indicates it is not an array</param>
        /// <param name="cbufSlot">Constant buffer slot where the texture handle is located</param>
        /// <param name="handle">The shader texture handle (read index into the texture constant buffer)</param>
        /// <param name="flags">The texture's usage flags, indicating how it is used in the shader</param>
        public TextureBindingInfo(Target target, Format format, int binding, int arrayLength, int cbufSlot, int handle, TextureUsageFlags flags)
        {
            Target = target;
            Format = format;
            Binding = binding;
            ArrayLength = arrayLength;
            CbufSlot = cbufSlot;
            Handle = handle;
            Flags = flags;
        }

        /// <summary>
        /// Constructs the texture binding information structure.
        /// </summary>
        /// <param name="target">The shader sampler target type</param>
        /// <param name="binding">The shader texture binding point</param>
        /// <param name="arrayLength">For array of textures, this indicates the length of the array. A value of one indicates it is not an array</param>
        /// <param name="cbufSlot">Constant buffer slot where the texture handle is located</param>
        /// <param name="handle">The shader texture handle (read index into the texture constant buffer)</param>
        /// <param name="flags">The texture's usage flags, indicating how it is used in the shader</param>
        public TextureBindingInfo(Target target, int binding, int arrayLength, int cbufSlot, int handle, TextureUsageFlags flags) : this(target, (Format)0, binding, arrayLength, cbufSlot, handle, flags)
        {
        }
    }
}
