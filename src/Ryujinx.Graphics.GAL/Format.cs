namespace Ryujinx.Graphics.GAL
{
    public enum Format
    {
        R8Unorm,
        R8Snorm,
        R8Uint,
        R8Sint,
        R16Float,
        R16Unorm,
        R16Snorm,
        R16Uint,
        R16Sint,
        R32Float,
        R32Uint,
        R32Sint,
        R8G8Unorm,
        R8G8Snorm,
        R8G8Uint,
        R8G8Sint,
        R16G16Float,
        R16G16Unorm,
        R16G16Snorm,
        R16G16Uint,
        R16G16Sint,
        R32G32Float,
        R32G32Uint,
        R32G32Sint,
        R8G8B8Unorm,
        R8G8B8Snorm,
        R8G8B8Uint,
        R8G8B8Sint,
        R16G16B16Float,
        R16G16B16Unorm,
        R16G16B16Snorm,
        R16G16B16Uint,
        R16G16B16Sint,
        R32G32B32Float,
        R32G32B32Uint,
        R32G32B32Sint,
        R8G8B8A8Unorm,
        R8G8B8A8Snorm,
        R8G8B8A8Uint,
        R8G8B8A8Sint,
        R16G16B16A16Float,
        R16G16B16A16Unorm,
        R16G16B16A16Snorm,
        R16G16B16A16Uint,
        R16G16B16A16Sint,
        R32G32B32A32Float,
        R32G32B32A32Uint,
        R32G32B32A32Sint,
        S8Uint,
        D16Unorm,
        S8UintD24Unorm,
        D32Float,
        D24UnormS8Uint,
        D32FloatS8Uint,
        R8G8B8A8Srgb,
        R4G4Unorm,
        R4G4B4A4Unorm,
        R5G5B5X1Unorm,
        R5G5B5A1Unorm,
        R5G6B5Unorm,
        R10G10B10A2Unorm,
        R10G10B10A2Uint,
        R11G11B10Float,
        R9G9B9E5Float,
        Bc1RgbaUnorm,
        Bc2Unorm,
        Bc3Unorm,
        Bc1RgbaSrgb,
        Bc2Srgb,
        Bc3Srgb,
        Bc4Unorm,
        Bc4Snorm,
        Bc5Unorm,
        Bc5Snorm,
        Bc7Unorm,
        Bc7Srgb,
        Bc6HSfloat,
        Bc6HUfloat,
        Etc2RgbUnorm,
        Etc2RgbaUnorm,
        Etc2RgbPtaUnorm,
        Etc2RgbSrgb,
        Etc2RgbaSrgb,
        Etc2RgbPtaSrgb,
        R8Uscaled,
        R8Sscaled,
        R16Uscaled,
        R16Sscaled,
        R32Uscaled,
        R32Sscaled,
        R8G8Uscaled,
        R8G8Sscaled,
        R16G16Uscaled,
        R16G16Sscaled,
        R32G32Uscaled,
        R32G32Sscaled,
        R8G8B8Uscaled,
        R8G8B8Sscaled,
        R16G16B16Uscaled,
        R16G16B16Sscaled,
        R32G32B32Uscaled,
        R32G32B32Sscaled,
        R8G8B8A8Uscaled,
        R8G8B8A8Sscaled,
        R16G16B16A16Uscaled,
        R16G16B16A16Sscaled,
        R32G32B32A32Uscaled,
        R32G32B32A32Sscaled,
        R10G10B10A2Snorm,
        R10G10B10A2Sint,
        R10G10B10A2Uscaled,
        R10G10B10A2Sscaled,
        Astc4x4Unorm,
        Astc5x4Unorm,
        Astc5x5Unorm,
        Astc6x5Unorm,
        Astc6x6Unorm,
        Astc8x5Unorm,
        Astc8x6Unorm,
        Astc8x8Unorm,
        Astc10x5Unorm,
        Astc10x6Unorm,
        Astc10x8Unorm,
        Astc10x10Unorm,
        Astc12x10Unorm,
        Astc12x12Unorm,
        Astc4x4Srgb,
        Astc5x4Srgb,
        Astc5x5Srgb,
        Astc6x5Srgb,
        Astc6x6Srgb,
        Astc8x5Srgb,
        Astc8x6Srgb,
        Astc8x8Srgb,
        Astc10x5Srgb,
        Astc10x6Srgb,
        Astc10x8Srgb,
        Astc10x10Srgb,
        Astc12x10Srgb,
        Astc12x12Srgb,
        B5G6R5Unorm,
        B5G5R5A1Unorm,
        A1B5G5R5Unorm,
        B8G8R8A8Unorm,
        B8G8R8A8Srgb,
        B10G10R10A2Unorm,
        X8UintD24Unorm,
    }

    public static class FormatExtensions
    {
        /// <summary>
        /// The largest scalar size for a buffer format.
        /// </summary>
        public const int MaxBufferFormatScalarSize = 4;

        /// <summary>
        /// Gets the byte size for a single component of this format, or its packed size.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>Byte size for a single component, or packed size</returns>
        public static int GetScalarSize(this Format format)
        {
            switch (format)
            {
                case Format.R8Unorm:
                case Format.R8Snorm:
                case Format.R8Uint:
                case Format.R8Sint:
                case Format.R8G8Unorm:
                case Format.R8G8Snorm:
                case Format.R8G8Uint:
                case Format.R8G8Sint:
                case Format.R8G8B8Unorm:
                case Format.R8G8B8Snorm:
                case Format.R8G8B8Uint:
                case Format.R8G8B8Sint:
                case Format.R8G8B8A8Unorm:
                case Format.R8G8B8A8Snorm:
                case Format.R8G8B8A8Uint:
                case Format.R8G8B8A8Sint:
                case Format.R8G8B8A8Srgb:
                case Format.R4G4Unorm:
                case Format.R8Uscaled:
                case Format.R8Sscaled:
                case Format.R8G8Uscaled:
                case Format.R8G8Sscaled:
                case Format.R8G8B8Uscaled:
                case Format.R8G8B8Sscaled:
                case Format.R8G8B8A8Uscaled:
                case Format.R8G8B8A8Sscaled:
                case Format.B8G8R8A8Unorm:
                case Format.B8G8R8A8Srgb:
                    return 1;

                case Format.R16Float:
                case Format.R16Unorm:
                case Format.R16Snorm:
                case Format.R16Uint:
                case Format.R16Sint:
                case Format.R16G16Float:
                case Format.R16G16Unorm:
                case Format.R16G16Snorm:
                case Format.R16G16Uint:
                case Format.R16G16Sint:
                case Format.R16G16B16Float:
                case Format.R16G16B16Unorm:
                case Format.R16G16B16Snorm:
                case Format.R16G16B16Uint:
                case Format.R16G16B16Sint:
                case Format.R16G16B16A16Float:
                case Format.R16G16B16A16Unorm:
                case Format.R16G16B16A16Snorm:
                case Format.R16G16B16A16Uint:
                case Format.R16G16B16A16Sint:
                case Format.R4G4B4A4Unorm:
                case Format.R5G5B5X1Unorm:
                case Format.R5G5B5A1Unorm:
                case Format.R5G6B5Unorm:
                case Format.R16Uscaled:
                case Format.R16Sscaled:
                case Format.R16G16Uscaled:
                case Format.R16G16Sscaled:
                case Format.R16G16B16Uscaled:
                case Format.R16G16B16Sscaled:
                case Format.R16G16B16A16Uscaled:
                case Format.R16G16B16A16Sscaled:
                case Format.B5G6R5Unorm:
                case Format.B5G5R5A1Unorm:
                case Format.A1B5G5R5Unorm:
                    return 2;

                case Format.R32Float:
                case Format.R32Uint:
                case Format.R32Sint:
                case Format.R32G32Float:
                case Format.R32G32Uint:
                case Format.R32G32Sint:
                case Format.R32G32B32Float:
                case Format.R32G32B32Uint:
                case Format.R32G32B32Sint:
                case Format.R32G32B32A32Float:
                case Format.R32G32B32A32Uint:
                case Format.R32G32B32A32Sint:
                case Format.R10G10B10A2Unorm:
                case Format.R10G10B10A2Uint:
                case Format.R11G11B10Float:
                case Format.R9G9B9E5Float:
                case Format.R32Uscaled:
                case Format.R32Sscaled:
                case Format.R32G32Uscaled:
                case Format.R32G32Sscaled:
                case Format.R32G32B32Uscaled:
                case Format.R32G32B32Sscaled:
                case Format.R32G32B32A32Uscaled:
                case Format.R32G32B32A32Sscaled:
                case Format.R10G10B10A2Snorm:
                case Format.R10G10B10A2Sint:
                case Format.R10G10B10A2Uscaled:
                case Format.R10G10B10A2Sscaled:
                case Format.B10G10R10A2Unorm:
                    return 4;

                case Format.S8Uint:
                    return 1;
                case Format.D16Unorm:
                    return 2;
                case Format.S8UintD24Unorm:
                case Format.X8UintD24Unorm:
                case Format.D32Float:
                case Format.D24UnormS8Uint:
                    return 4;
                case Format.D32FloatS8Uint:
                    return 8;

                case Format.Bc1RgbaUnorm:
                case Format.Bc1RgbaSrgb:
                    return 8;

                case Format.Bc2Unorm:
                case Format.Bc3Unorm:
                case Format.Bc2Srgb:
                case Format.Bc3Srgb:
                case Format.Bc4Unorm:
                case Format.Bc4Snorm:
                case Format.Bc5Unorm:
                case Format.Bc5Snorm:
                case Format.Bc7Unorm:
                case Format.Bc7Srgb:
                case Format.Bc6HSfloat:
                case Format.Bc6HUfloat:
                    return 16;

                case Format.Etc2RgbUnorm:
                case Format.Etc2RgbPtaUnorm:
                case Format.Etc2RgbSrgb:
                case Format.Etc2RgbPtaSrgb:
                    return 8;

                case Format.Etc2RgbaUnorm:
                case Format.Etc2RgbaSrgb:
                    return 16;

                case Format.Astc4x4Unorm:
                case Format.Astc5x4Unorm:
                case Format.Astc5x5Unorm:
                case Format.Astc6x5Unorm:
                case Format.Astc6x6Unorm:
                case Format.Astc8x5Unorm:
                case Format.Astc8x6Unorm:
                case Format.Astc8x8Unorm:
                case Format.Astc10x5Unorm:
                case Format.Astc10x6Unorm:
                case Format.Astc10x8Unorm:
                case Format.Astc10x10Unorm:
                case Format.Astc12x10Unorm:
                case Format.Astc12x12Unorm:
                case Format.Astc4x4Srgb:
                case Format.Astc5x4Srgb:
                case Format.Astc5x5Srgb:
                case Format.Astc6x5Srgb:
                case Format.Astc6x6Srgb:
                case Format.Astc8x5Srgb:
                case Format.Astc8x6Srgb:
                case Format.Astc8x8Srgb:
                case Format.Astc10x5Srgb:
                case Format.Astc10x6Srgb:
                case Format.Astc10x8Srgb:
                case Format.Astc10x10Srgb:
                case Format.Astc12x10Srgb:
                case Format.Astc12x12Srgb:
                    return 16;
            }

            return 1;
        }

        /// <summary>
        /// Get bytes per element for this format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>Byte size for an element of this format (pixel, vertex attribute, etc)</returns>
        public static int GetBytesPerElement(this Format format)
        {
            int scalarSize = format.GetScalarSize();

            switch (format)
            {
                case Format.R8G8Unorm:
                case Format.R8G8Snorm:
                case Format.R8G8Uint:
                case Format.R8G8Sint:
                case Format.R8G8Uscaled:
                case Format.R8G8Sscaled:
                case Format.R16G16Float:
                case Format.R16G16Unorm:
                case Format.R16G16Snorm:
                case Format.R16G16Uint:
                case Format.R16G16Sint:
                case Format.R16G16Uscaled:
                case Format.R16G16Sscaled:
                case Format.R32G32Float:
                case Format.R32G32Uint:
                case Format.R32G32Sint:
                case Format.R32G32Uscaled:
                case Format.R32G32Sscaled:
                    return 2 * scalarSize;

                case Format.R8G8B8Unorm:
                case Format.R8G8B8Snorm:
                case Format.R8G8B8Uint:
                case Format.R8G8B8Sint:
                case Format.R8G8B8Uscaled:
                case Format.R8G8B8Sscaled:
                case Format.R16G16B16Float:
                case Format.R16G16B16Unorm:
                case Format.R16G16B16Snorm:
                case Format.R16G16B16Uint:
                case Format.R16G16B16Sint:
                case Format.R16G16B16Uscaled:
                case Format.R16G16B16Sscaled:
                case Format.R32G32B32Float:
                case Format.R32G32B32Uint:
                case Format.R32G32B32Sint:
                case Format.R32G32B32Uscaled:
                case Format.R32G32B32Sscaled:
                    return 3 * scalarSize;

                case Format.R8G8B8A8Unorm:
                case Format.R8G8B8A8Snorm:
                case Format.R8G8B8A8Uint:
                case Format.R8G8B8A8Sint:
                case Format.R8G8B8A8Srgb:
                case Format.R8G8B8A8Uscaled:
                case Format.R8G8B8A8Sscaled:
                case Format.B8G8R8A8Unorm:
                case Format.B8G8R8A8Srgb:
                case Format.R16G16B16A16Float:
                case Format.R16G16B16A16Unorm:
                case Format.R16G16B16A16Snorm:
                case Format.R16G16B16A16Uint:
                case Format.R16G16B16A16Sint:
                case Format.R16G16B16A16Uscaled:
                case Format.R16G16B16A16Sscaled:
                case Format.R32G32B32A32Float:
                case Format.R32G32B32A32Uint:
                case Format.R32G32B32A32Sint:
                case Format.R32G32B32A32Uscaled:
                case Format.R32G32B32A32Sscaled:
                    return 4 * scalarSize;
            }

            return scalarSize;
        }

        /// <summary>
        /// Checks if the texture format is a depth or depth-stencil format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the format is a depth or depth-stencil format, false otherwise</returns>
        public static bool HasDepth(this Format format)
        {
            switch (format)
            {
                case Format.D16Unorm:
                case Format.D24UnormS8Uint:
                case Format.S8UintD24Unorm:
                case Format.X8UintD24Unorm:
                case Format.D32Float:
                case Format.D32FloatS8Uint:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is a stencil or depth-stencil format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the format is a stencil or depth-stencil format, false otherwise</returns>
        public static bool HasStencil(this Format format)
        {
            switch (format)
            {
                case Format.D24UnormS8Uint:
                case Format.S8UintD24Unorm:
                case Format.D32FloatS8Uint:
                case Format.S8Uint:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is valid to use as image format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture can be used as image, false otherwise</returns>
        public static bool IsImageCompatible(this Format format)
        {
            switch (format)
            {
                case Format.R8Unorm:
                case Format.R8Snorm:
                case Format.R8Uint:
                case Format.R8Sint:
                case Format.R16Float:
                case Format.R16Unorm:
                case Format.R16Snorm:
                case Format.R16Uint:
                case Format.R16Sint:
                case Format.R32Float:
                case Format.R32Uint:
                case Format.R32Sint:
                case Format.R8G8Unorm:
                case Format.R8G8Snorm:
                case Format.R8G8Uint:
                case Format.R8G8Sint:
                case Format.R16G16Float:
                case Format.R16G16Unorm:
                case Format.R16G16Snorm:
                case Format.R16G16Uint:
                case Format.R16G16Sint:
                case Format.R32G32Float:
                case Format.R32G32Uint:
                case Format.R32G32Sint:
                case Format.R8G8B8A8Unorm:
                case Format.R8G8B8A8Snorm:
                case Format.R8G8B8A8Uint:
                case Format.R8G8B8A8Sint:
                case Format.R16G16B16A16Float:
                case Format.R16G16B16A16Unorm:
                case Format.R16G16B16A16Snorm:
                case Format.R16G16B16A16Uint:
                case Format.R16G16B16A16Sint:
                case Format.R32G32B32A32Float:
                case Format.R32G32B32A32Uint:
                case Format.R32G32B32A32Sint:
                case Format.R10G10B10A2Unorm:
                case Format.R10G10B10A2Uint:
                case Format.R11G11B10Float:
                case Format.B8G8R8A8Unorm:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is valid to use as render target color format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture can be used as render target, false otherwise</returns>
        public static bool IsRtColorCompatible(this Format format)
        {
            switch (format)
            {
                case Format.R32G32B32A32Float:
                case Format.R32G32B32A32Sint:
                case Format.R32G32B32A32Uint:
                case Format.R16G16B16A16Unorm:
                case Format.R16G16B16A16Snorm:
                case Format.R16G16B16A16Sint:
                case Format.R16G16B16A16Uint:
                case Format.R16G16B16A16Float:
                case Format.R32G32Float:
                case Format.R32G32Sint:
                case Format.R32G32Uint:
                case Format.B8G8R8A8Unorm:
                case Format.B8G8R8A8Srgb:
                case Format.B10G10R10A2Unorm:
                case Format.R10G10B10A2Unorm:
                case Format.R10G10B10A2Uint:
                case Format.R8G8B8A8Unorm:
                case Format.R8G8B8A8Srgb:
                case Format.R8G8B8A8Snorm:
                case Format.R8G8B8A8Sint:
                case Format.R8G8B8A8Uint:
                case Format.R16G16Unorm:
                case Format.R16G16Snorm:
                case Format.R16G16Sint:
                case Format.R16G16Uint:
                case Format.R16G16Float:
                case Format.R11G11B10Float:
                case Format.R32Sint:
                case Format.R32Uint:
                case Format.R32Float:
                case Format.B5G6R5Unorm:
                case Format.B5G5R5A1Unorm:
                case Format.R8G8Unorm:
                case Format.R8G8Snorm:
                case Format.R8G8Sint:
                case Format.R8G8Uint:
                case Format.R16Unorm:
                case Format.R16Snorm:
                case Format.R16Sint:
                case Format.R16Uint:
                case Format.R16Float:
                case Format.R8Unorm:
                case Format.R8Snorm:
                case Format.R8Sint:
                case Format.R8Uint:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is 16 bit packed.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture format is 16 bit packed, false otherwise</returns>
        public static bool Is16BitPacked(this Format format)
        {
            switch (format)
            {
                case Format.B5G6R5Unorm:
                case Format.B5G5R5A1Unorm:
                case Format.R5G5B5X1Unorm:
                case Format.R5G5B5A1Unorm:
                case Format.R5G6B5Unorm:
                case Format.R4G4B4A4Unorm:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is an ASTC format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture format is an ASTC format, false otherwise</returns>
        public static bool IsAstc(this Format format)
        {
            return format.IsAstcUnorm() || format.IsAstcSrgb();
        }

        /// <summary>
        /// Checks if the texture format is an ASTC Unorm format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture format is an ASTC Unorm format, false otherwise</returns>
        public static bool IsAstcUnorm(this Format format)
        {
            switch (format)
            {
                case Format.Astc4x4Unorm:
                case Format.Astc5x4Unorm:
                case Format.Astc5x5Unorm:
                case Format.Astc6x5Unorm:
                case Format.Astc6x6Unorm:
                case Format.Astc8x5Unorm:
                case Format.Astc8x6Unorm:
                case Format.Astc8x8Unorm:
                case Format.Astc10x5Unorm:
                case Format.Astc10x6Unorm:
                case Format.Astc10x8Unorm:
                case Format.Astc10x10Unorm:
                case Format.Astc12x10Unorm:
                case Format.Astc12x12Unorm:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is an ASTC SRGB format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture format is an ASTC SRGB format, false otherwise</returns>
        public static bool IsAstcSrgb(this Format format)
        {
            switch (format)
            {
                case Format.Astc4x4Srgb:
                case Format.Astc5x4Srgb:
                case Format.Astc5x5Srgb:
                case Format.Astc6x5Srgb:
                case Format.Astc6x6Srgb:
                case Format.Astc8x5Srgb:
                case Format.Astc8x6Srgb:
                case Format.Astc8x8Srgb:
                case Format.Astc10x5Srgb:
                case Format.Astc10x6Srgb:
                case Format.Astc10x8Srgb:
                case Format.Astc10x10Srgb:
                case Format.Astc12x10Srgb:
                case Format.Astc12x12Srgb:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is an ETC2 format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture format is an ETC2 format, false otherwise</returns>
        public static bool IsEtc2(this Format format)
        {
            switch (format)
            {
                case Format.Etc2RgbaSrgb:
                case Format.Etc2RgbaUnorm:
                case Format.Etc2RgbPtaSrgb:
                case Format.Etc2RgbPtaUnorm:
                case Format.Etc2RgbSrgb:
                case Format.Etc2RgbUnorm:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is a BGR format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture format is a BGR format, false otherwise</returns>
        public static bool IsBgr(this Format format)
        {
            switch (format)
            {
                case Format.B5G6R5Unorm:
                case Format.B5G5R5A1Unorm:
                case Format.B8G8R8A8Unorm:
                case Format.B8G8R8A8Srgb:
                case Format.B10G10R10A2Unorm:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is a depth, stencil or depth-stencil format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the format is a depth, stencil or depth-stencil format, false otherwise</returns>
        public static bool IsDepthOrStencil(this Format format)
        {
            switch (format)
            {
                case Format.D16Unorm:
                case Format.D24UnormS8Uint:
                case Format.S8UintD24Unorm:
                case Format.X8UintD24Unorm:
                case Format.D32Float:
                case Format.D32FloatS8Uint:
                case Format.S8Uint:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is an unsigned integer color format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture format is an unsigned integer color format, false otherwise</returns>
        public static bool IsUint(this Format format)
        {
            switch (format)
            {
                case Format.R8Uint:
                case Format.R16Uint:
                case Format.R32Uint:
                case Format.R8G8Uint:
                case Format.R16G16Uint:
                case Format.R32G32Uint:
                case Format.R8G8B8Uint:
                case Format.R16G16B16Uint:
                case Format.R32G32B32Uint:
                case Format.R8G8B8A8Uint:
                case Format.R16G16B16A16Uint:
                case Format.R32G32B32A32Uint:
                case Format.R10G10B10A2Uint:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is a signed integer color format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture format is a signed integer color format, false otherwise</returns>
        public static bool IsSint(this Format format)
        {
            switch (format)
            {
                case Format.R8Sint:
                case Format.R16Sint:
                case Format.R32Sint:
                case Format.R8G8Sint:
                case Format.R16G16Sint:
                case Format.R32G32Sint:
                case Format.R8G8B8Sint:
                case Format.R16G16B16Sint:
                case Format.R32G32B32Sint:
                case Format.R8G8B8A8Sint:
                case Format.R16G16B16A16Sint:
                case Format.R32G32B32A32Sint:
                case Format.R10G10B10A2Sint:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the texture format is an integer color format.
        /// </summary>
        /// <param name="format">Texture format</param>
        /// <returns>True if the texture format is an integer color format, false otherwise</returns>
        public static bool IsInteger(this Format format)
        {
            return format.IsUint() || format.IsSint();
        }

        /// <summary>
        /// Checks if the texture format is a float or sRGB color format.
        /// </summary>
        /// <remarks>
        /// Does not include normalized, compressed or depth formats.
        /// Float and sRGB formats do not participate in logical operations.
        /// </remarks>
        /// <param name="format">Texture format</param>
        /// <returns>True if the format is a float or sRGB color format, false otherwise</returns>
        public static bool IsFloatOrSrgb(this Format format)
        {
            switch (format)
            {
                case Format.R8G8B8A8Srgb:
                case Format.B8G8R8A8Srgb:
                case Format.R16Float:
                case Format.R16G16Float:
                case Format.R16G16B16Float:
                case Format.R16G16B16A16Float:
                case Format.R32Float:
                case Format.R32G32Float:
                case Format.R32G32B32Float:
                case Format.R32G32B32A32Float:
                case Format.R11G11B10Float:
                case Format.R9G9B9E5Float:
                    return true;
            }

            return false;
        }
    }
}
