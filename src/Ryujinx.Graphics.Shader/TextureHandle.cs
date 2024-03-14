using System;
using System.Runtime.CompilerServices;

namespace Ryujinx.Graphics.Shader
{
    public enum TextureHandleType
    {
        CombinedSampler = 0, // Must be 0.
        SeparateSamplerHandle = 1,
        SeparateSamplerId = 2,
        SeparateConstantSamplerHandle = 3,
    }

    public static class TextureHandle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PackSlots(int cbufSlot0, int cbufSlot1)
        {
            return cbufSlot0 | ((cbufSlot1 + 1) << 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int, int) UnpackSlots(int slots, int defaultTextureBufferIndex)
        {
            int textureBufferIndex;
            int samplerBufferIndex;

            if (slots < 0)
            {
                textureBufferIndex = defaultTextureBufferIndex;
                samplerBufferIndex = textureBufferIndex;
            }
            else
            {
                uint high = (uint)slots >> 16;

                textureBufferIndex = (ushort)slots;
                samplerBufferIndex = high != 0 ? (int)high - 1 : textureBufferIndex;
            }

            return (textureBufferIndex, samplerBufferIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PackOffsets(int cbufOffset0, int cbufOffset1, TextureHandleType type)
        {
            return cbufOffset0 | (cbufOffset1 << 14) | ((int)type << 28);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int, int, TextureHandleType) UnpackOffsets(int handle)
        {
            return (handle & 0x3fff, (handle >> 14) & 0x3fff, (TextureHandleType)((uint)handle >> 28));
        }

        /// <summary>
        /// Unpacks the texture ID from the real texture handle.
        /// </summary>
        /// <param name="packedId">The real texture handle</param>
        /// <returns>The texture ID</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UnpackTextureId(int packedId)
        {
            return packedId & 0xfffff;
        }

        /// <summary>
        /// Unpacks the sampler ID from the real texture handle.
        /// </summary>
        /// <param name="packedId">The real texture handle</param>
        /// <returns>The sampler ID</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UnpackSamplerId(int packedId)
        {
            return (packedId >> 20) & 0xfff;
        }

        /// <summary>
        /// Reads a packed texture and sampler ID (basically, the real texture handle)
        /// from a given texture/sampler constant buffer.
        /// </summary>
        /// <param name="wordOffset">A word offset of the handle on the buffer (the "fake" shader handle)</param>
        /// <param name="cachedTextureBuffer">The constant buffer to fetch texture IDs from</param>
        /// <param name="cachedSamplerBuffer">The constant buffer to fetch sampler IDs from</param>
        /// <returns>The packed texture and sampler ID (the real texture handle)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadPackedId(int wordOffset, ReadOnlySpan<int> cachedTextureBuffer, ReadOnlySpan<int> cachedSamplerBuffer)
        {
            (int textureWordOffset, int samplerWordOffset, TextureHandleType handleType) = UnpackOffsets(wordOffset);

            int handle = textureWordOffset < cachedTextureBuffer.Length ? cachedTextureBuffer[textureWordOffset] : 0;

            // The "wordOffset" (which is really the immediate value used on texture instructions on the shader)
            // is a 13-bit value. However, in order to also support separate samplers and textures (which uses
            // bindless textures on the shader), we extend it with another value on the higher 16 bits with
            // another offset for the sampler.
            // The shader translator has code to detect separate texture and sampler uses with a bindless texture,
            // turn that into a regular texture access and produce those special handles with values on the higher 16 bits.
            if (handleType != TextureHandleType.CombinedSampler)
            {
                int samplerHandle;

                if (handleType != TextureHandleType.SeparateConstantSamplerHandle)
                {
                    samplerHandle = samplerWordOffset < cachedSamplerBuffer.Length ? cachedSamplerBuffer[samplerWordOffset] : 0;
                }
                else
                {
                    samplerHandle = samplerWordOffset;
                }

                if (handleType == TextureHandleType.SeparateSamplerId ||
                    handleType == TextureHandleType.SeparateConstantSamplerHandle)
                {
                    samplerHandle <<= 20;
                }

                handle |= samplerHandle;
            }

            return handle;
        }
    }
}
