namespace Ryujinx.Graphics.Gpu.Shader.DiskCache
{
    /// <summary>
    /// Result of a shader cache load operation.
    /// </summary>
    enum DiskCacheLoadResult
    {
        /// <summary>
        /// No error.
        /// </summary>
        Success,

        /// <summary>
        /// File can't be accessed.
        /// </summary>
        NoAccess,

        /// <summary>
        /// The constant buffer 1 data length is too low for the translation of the guest shader.
        /// </summary>
        InvalidCb1DataLength,

        /// <summary>
        /// The cache is missing the length of a texture array used by the shader.
        /// </summary>
        MissingTextureArrayLength,

        /// <summary>
        /// The cache is missing the descriptor of a texture used by the shader.
        /// </summary>
        MissingTextureDescriptor,

        /// <summary>
        /// File is corrupted.
        /// </summary>
        FileCorruptedGeneric,

        /// <summary>
        /// File is corrupted, detected by magic value check.
        /// </summary>
        FileCorruptedInvalidMagic,

        /// <summary>
        /// File is corrupted, detected by length check.
        /// </summary>
        FileCorruptedInvalidLength,

        /// <summary>
        /// File might be valid, but is incompatible with the current emulator version.
        /// </summary>
        IncompatibleVersion,
    }

    static class DiskCacheLoadResultExtensions
    {
        /// <summary>
        /// Gets an error message from a result code.
        /// </summary>
        /// <param name="result">Result code</param>
        /// <returns>Error message</returns>
        public static string GetMessage(this DiskCacheLoadResult result)
        {
            return result switch
            {
                DiskCacheLoadResult.Success => "No error.",
                DiskCacheLoadResult.NoAccess => "Could not access the cache file.",
                DiskCacheLoadResult.InvalidCb1DataLength => "Constant buffer 1 data length is too low.",
                DiskCacheLoadResult.MissingTextureArrayLength => "Texture array length missing from the cache file.",
                DiskCacheLoadResult.MissingTextureDescriptor => "Texture descriptor missing from the cache file.",
                DiskCacheLoadResult.FileCorruptedGeneric => "The cache file is corrupted.",
                DiskCacheLoadResult.FileCorruptedInvalidMagic => "Magic check failed, the cache file is corrupted.",
                DiskCacheLoadResult.FileCorruptedInvalidLength => "Length check failed, the cache file is corrupted.",
                DiskCacheLoadResult.IncompatibleVersion => "The version of the disk cache is not compatible with this version of the emulator.",
                _ => "Unknown error.",
            };
        }
    }
}
