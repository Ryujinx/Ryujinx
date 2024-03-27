using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Engine.Types;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.Graphics.Gpu.Shader;
using Ryujinx.Graphics.Shader;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gpu.Image
{
    /// <summary>
    /// Texture bindings manager.
    /// </summary>
    class TextureBindingsManager
    {
        private const int InitialTextureStateSize = 32;
        private const int InitialImageStateSize = 8;

        private readonly GpuContext _context;

        private readonly bool _isCompute;

        private ulong _texturePoolGpuVa;
        private int _texturePoolMaximumId;
        private TexturePool _texturePool;
        private ulong _samplerPoolGpuVa;
        private int _samplerPoolMaximumId;
        private SamplerIndex _samplerIndex;
        private SamplerPool _samplerPool;

        private readonly GpuChannel _channel;
        private readonly TexturePoolCache _texturePoolCache;
        private readonly SamplerPoolCache _samplerPoolCache;

        private readonly TextureBindingsArrayCache _arrayBindingsCache;

        private TexturePool _cachedTexturePool;
        private SamplerPool _cachedSamplerPool;

        private TextureBindingInfo[][] _textureBindings;
        private TextureBindingInfo[][] _imageBindings;

        private struct TextureState
        {
            public ITexture Texture;
            public ISampler Sampler;

            public int TextureHandle;
            public int SamplerHandle;
            public Format ImageFormat;
            public int InvalidatedSequence;
            public Texture CachedTexture;
            public Sampler CachedSampler;
        }

        private TextureState[] _textureState;
        private TextureState[] _imageState;

        private int[] _textureCounts;

        private int _texturePoolSequence;
        private int _samplerPoolSequence;

        private int _textureBufferIndex;

        private int _lastFragmentTotal;

        /// <summary>
        /// Constructs a new instance of the texture bindings manager.
        /// </summary>
        /// <param name="context">The GPU context that the texture bindings manager belongs to</param>
        /// <param name="channel">The GPU channel that the texture bindings manager belongs to</param>
        /// <param name="texturePoolCache">Texture pools cache used to get texture pools from</param>
        /// <param name="samplerPoolCache">Sampler pools cache used to get sampler pools from</param>
        /// <param name="isCompute">True if the bindings manager is used for the compute engine</param>
        public TextureBindingsManager(
            GpuContext context,
            GpuChannel channel,
            TexturePoolCache texturePoolCache,
            SamplerPoolCache samplerPoolCache,
            bool isCompute)
        {
            _context = context;
            _channel = channel;
            _texturePoolCache = texturePoolCache;
            _samplerPoolCache = samplerPoolCache;

            _isCompute = isCompute;

            _arrayBindingsCache = new TextureBindingsArrayCache(context, channel, isCompute);

            int stages = isCompute ? 1 : Constants.ShaderStages;

            _textureBindings = new TextureBindingInfo[stages][];
            _imageBindings = new TextureBindingInfo[stages][];

            _textureState = new TextureState[InitialTextureStateSize];
            _imageState = new TextureState[InitialImageStateSize];

            for (int stage = 0; stage < stages; stage++)
            {
                _textureBindings[stage] = Array.Empty<TextureBindingInfo>();
                _imageBindings[stage] = Array.Empty<TextureBindingInfo>();
            }

            _textureCounts = Array.Empty<int>();
        }

        /// <summary>
        /// Sets the texture and image bindings.
        /// </summary>
        /// <param name="bindings">Bindings for the active shader</param>
        public void SetBindings(CachedShaderBindings bindings)
        {
            _textureBindings = bindings.TextureBindings;
            _imageBindings = bindings.ImageBindings;

            _textureCounts = bindings.TextureCounts;

            SetMaxBindings(bindings.MaxTextureBinding, bindings.MaxImageBinding);
        }

        /// <summary>
        /// Sets the max binding indexes for textures and images.
        /// </summary>
        /// <param name="maxTextureBinding">The maximum texture binding</param>
        /// <param name="maxImageBinding">The maximum image binding</param>
        public void SetMaxBindings(int maxTextureBinding, int maxImageBinding)
        {
            if (maxTextureBinding >= _textureState.Length)
            {
                Array.Resize(ref _textureState, maxTextureBinding + 1);
            }

            if (maxImageBinding >= _imageState.Length)
            {
                Array.Resize(ref _imageState, maxImageBinding + 1);
            }
        }

        /// <summary>
        /// Sets the textures constant buffer index.
        /// The constant buffer specified holds the texture handles.
        /// </summary>
        /// <param name="index">Constant buffer index</param>
        public void SetTextureBufferIndex(int index)
        {
            _textureBufferIndex = index;
        }

        /// <summary>
        /// Sets the current texture sampler pool to be used.
        /// </summary>
        /// <param name="gpuVa">Start GPU virtual address of the pool</param>
        /// <param name="maximumId">Maximum ID of the pool (total count minus one)</param>
        /// <param name="samplerIndex">Type of the sampler pool indexing used for bound samplers</param>
        public void SetSamplerPool(ulong gpuVa, int maximumId, SamplerIndex samplerIndex)
        {
            _samplerPoolGpuVa = gpuVa;
            _samplerPoolMaximumId = maximumId;
            _samplerIndex = samplerIndex;
            _samplerPool = null;
        }

        /// <summary>
        /// Sets the current texture pool to be used.
        /// </summary>
        /// <param name="gpuVa">Start GPU virtual address of the pool</param>
        /// <param name="maximumId">Maximum ID of the pool (total count minus one)</param>
        public void SetTexturePool(ulong gpuVa, int maximumId)
        {
            _texturePoolGpuVa = gpuVa;
            _texturePoolMaximumId = maximumId;
            _texturePool = null;
        }

        /// <summary>
        /// Gets a texture and a sampler from their respective pools from a texture ID and a sampler ID.
        /// </summary>
        /// <param name="textureId">ID of the texture</param>
        /// <param name="samplerId">ID of the sampler</param>
        public (Texture, Sampler) GetTextureAndSampler(int textureId, int samplerId)
        {
            (TexturePool texturePool, SamplerPool samplerPool) = GetPools();

            return (texturePool.Get(textureId), samplerPool.Get(samplerId));
        }

        /// <summary>
        /// Updates the texture scale for a given texture or image.
        /// </summary>
        /// <param name="texture">Start GPU virtual address of the pool</param>
        /// <param name="usageFlags">The related texture usage flags</param>
        /// <param name="index">The texture/image binding index</param>
        /// <param name="stage">The active shader stage</param>
        /// <returns>True if the given texture has become blacklisted, indicating that its host texture may have changed.</returns>
        private bool UpdateScale(Texture texture, TextureUsageFlags usageFlags, int index, ShaderStage stage)
        {
            float result = 1f;
            bool changed = false;

            if ((usageFlags & TextureUsageFlags.NeedsScaleValue) != 0 && texture != null)
            {
                if ((usageFlags & TextureUsageFlags.ResScaleUnsupported) != 0)
                {
                    changed = texture.ScaleMode != TextureScaleMode.Blacklisted;
                    texture.BlacklistScale();
                }
                else
                {
                    switch (stage)
                    {
                        case ShaderStage.Fragment:
                            float scale = texture.ScaleFactor;

                            if (scale != 1)
                            {
                                Texture activeTarget = _channel.TextureManager.GetAnyRenderTarget();

                                if (activeTarget != null && (activeTarget.Info.Width / (float)texture.Info.Width) == (activeTarget.Info.Height / (float)texture.Info.Height))
                                {
                                    // If the texture's size is a multiple of the sampler size, enable interpolation using gl_FragCoord. (helps "invent" new integer values between scaled pixels)
                                    result = -scale;
                                    break;
                                }
                            }

                            result = scale;
                            break;

                        case ShaderStage.Vertex:
                            int fragmentIndex = (int)ShaderStage.Fragment - 1;
                            index += _textureBindings[fragmentIndex].Length + _imageBindings[fragmentIndex].Length;

                            result = texture.ScaleFactor;
                            break;

                        case ShaderStage.Compute:
                            result = texture.ScaleFactor;
                            break;
                    }
                }
            }

            _context.SupportBufferUpdater.UpdateRenderScale(index, result);

            return changed;
        }

        /// <summary>
        /// Determines if the vertex stage requires a scale value.
        /// </summary>
        private bool VertexRequiresScale()
        {
            for (int i = 0; i < _textureBindings[0].Length; i++)
            {
                if ((_textureBindings[0][i].Flags & TextureUsageFlags.NeedsScaleValue) != 0)
                {
                    return true;
                }
            }

            for (int i = 0; i < _imageBindings[0].Length; i++)
            {
                if ((_imageBindings[0][i].Flags & TextureUsageFlags.NeedsScaleValue) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Uploads texture and image scales to the backend when they are used.
        /// </summary>
        private void CommitRenderScale()
        {
            // Stage 0 total: Compute or Vertex.
            int total = _textureBindings[0].Length + _imageBindings[0].Length;

            int fragmentIndex = (int)ShaderStage.Fragment - 1;
            int fragmentTotal = _isCompute ? 0 : (_textureBindings[fragmentIndex].Length + _imageBindings[fragmentIndex].Length);

            if (total != 0 && fragmentTotal != _lastFragmentTotal && VertexRequiresScale())
            {
                // Must update scales in the support buffer if:
                // - Vertex stage has bindings that require scale.
                // - Fragment stage binding count has been updated since last render scale update.

                if (!_isCompute)
                {
                    total += fragmentTotal; // Add the fragment bindings to the total.
                }

                _lastFragmentTotal = fragmentTotal;

                _context.SupportBufferUpdater.UpdateRenderScaleFragmentCount(total, fragmentTotal);
            }
        }

        /// <summary>
        /// Ensures that the bindings are visible to the host GPU.
        /// Note: this actually performs the binding using the host graphics API.
        /// </summary>
        /// <param name="specState">Specialization state for the bound shader</param>
        /// <returns>True if all bound textures match the current shader specialiation state, false otherwise</returns>
        public bool CommitBindings(ShaderSpecializationState specState)
        {
            (TexturePool texturePool, SamplerPool samplerPool) = GetPools();

            // Check if the texture pool has been modified since bindings were last committed.
            // If it wasn't, then it's possible to avoid looking up textures again when the handle remains the same.
            if (_cachedTexturePool != texturePool || _cachedSamplerPool != samplerPool)
            {
                Rebind();

                _cachedTexturePool = texturePool;
                _cachedSamplerPool = samplerPool;
            }

            bool poolModified = false;

            if (texturePool != null)
            {
                int texturePoolSequence = texturePool.CheckModified();

                if (_texturePoolSequence != texturePoolSequence)
                {
                    poolModified = true;
                    _texturePoolSequence = texturePoolSequence;
                }
            }

            if (samplerPool != null)
            {
                int samplerPoolSequence = samplerPool.CheckModified();

                if (_samplerPoolSequence != samplerPoolSequence)
                {
                    poolModified = true;
                    _samplerPoolSequence = samplerPoolSequence;
                }
            }

            bool specStateMatches = true;

            if (_isCompute)
            {
                specStateMatches &= CommitTextureBindings(texturePool, samplerPool, ShaderStage.Compute, 0, poolModified, specState);
                specStateMatches &= CommitImageBindings(texturePool, ShaderStage.Compute, 0, poolModified, specState);
            }
            else
            {
                for (ShaderStage stage = ShaderStage.Vertex; stage <= ShaderStage.Fragment; stage++)
                {
                    int stageIndex = (int)stage - 1;

                    specStateMatches &= CommitTextureBindings(texturePool, samplerPool, stage, stageIndex, poolModified, specState);
                    specStateMatches &= CommitImageBindings(texturePool, stage, stageIndex, poolModified, specState);
                }
            }

            CommitRenderScale();

            return specStateMatches;
        }

        /// <summary>
        /// Fetch the constant buffers used for a texture to cache.
        /// </summary>
        /// <param name="stageIndex">Stage index of the constant buffer</param>
        /// <param name="cachedTextureBufferIndex">The currently cached texture buffer index</param>
        /// <param name="cachedSamplerBufferIndex">The currently cached sampler buffer index</param>
        /// <param name="cachedTextureBuffer">The currently cached texture buffer data</param>
        /// <param name="cachedSamplerBuffer">The currently cached sampler buffer data</param>
        /// <param name="textureBufferIndex">The new texture buffer index</param>
        /// <param name="samplerBufferIndex">The new sampler buffer index</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCachedBuffer(
            int stageIndex,
            scoped ref int cachedTextureBufferIndex,
            scoped ref int cachedSamplerBufferIndex,
            scoped ref ReadOnlySpan<int> cachedTextureBuffer,
            scoped ref ReadOnlySpan<int> cachedSamplerBuffer,
            int textureBufferIndex,
            int samplerBufferIndex)
        {
            if (textureBufferIndex != cachedTextureBufferIndex)
            {
                ref BufferBounds bounds = ref _channel.BufferManager.GetUniformBufferBounds(_isCompute, stageIndex, textureBufferIndex);

                cachedTextureBuffer = MemoryMarshal.Cast<byte, int>(_channel.MemoryManager.Physical.GetSpan(bounds.Range));
                cachedTextureBufferIndex = textureBufferIndex;

                if (samplerBufferIndex == textureBufferIndex)
                {
                    cachedSamplerBuffer = cachedTextureBuffer;
                    cachedSamplerBufferIndex = samplerBufferIndex;
                }
            }

            if (samplerBufferIndex != cachedSamplerBufferIndex)
            {
                ref BufferBounds bounds = ref _channel.BufferManager.GetUniformBufferBounds(_isCompute, stageIndex, samplerBufferIndex);

                cachedSamplerBuffer = MemoryMarshal.Cast<byte, int>(_channel.MemoryManager.Physical.GetSpan(bounds.Range));
                cachedSamplerBufferIndex = samplerBufferIndex;
            }
        }

        /// <summary>
        /// Ensures that the texture bindings are visible to the host GPU.
        /// Note: this actually performs the binding using the host graphics API.
        /// </summary>
        /// <param name="texturePool">The current texture pool</param>
        /// <param name="samplerPool">The current sampler pool</param>
        /// <param name="stage">The shader stage using the textures to be bound</param>
        /// <param name="stageIndex">The stage number of the specified shader stage</param
        /// <param name="poolModified">True if either the texture or sampler pool was modified, false otherwise</param>
        /// <param name="specState">Specialization state for the bound shader</param>
        /// <returns>True if all bound textures match the current shader specialiation state, false otherwise</returns>
        private bool CommitTextureBindings(
            TexturePool texturePool,
            SamplerPool samplerPool,
            ShaderStage stage,
            int stageIndex,
            bool poolModified,
            ShaderSpecializationState specState)
        {
            int textureCount = _textureBindings[stageIndex].Length;
            if (textureCount == 0)
            {
                return true;
            }

            if (texturePool == null)
            {
                Logger.Error?.Print(LogClass.Gpu, $"Shader stage \"{stage}\" uses textures, but texture pool was not set.");
                return true;
            }

            bool specStateMatches = true;

            int cachedTextureBufferIndex = -1;
            int cachedSamplerBufferIndex = -1;
            ReadOnlySpan<int> cachedTextureBuffer = Span<int>.Empty;
            ReadOnlySpan<int> cachedSamplerBuffer = Span<int>.Empty;

            for (int index = 0; index < textureCount; index++)
            {
                TextureBindingInfo bindingInfo = _textureBindings[stageIndex][index];
                TextureUsageFlags usageFlags = bindingInfo.Flags;

                if (bindingInfo.ArrayLength > 1)
                {
                    _arrayBindingsCache.UpdateTextureArray(texturePool, samplerPool, stage, stageIndex, _textureBufferIndex, _samplerIndex, bindingInfo);

                    continue;
                }

                (int textureBufferIndex, int samplerBufferIndex) = TextureHandle.UnpackSlots(bindingInfo.CbufSlot, _textureBufferIndex);

                UpdateCachedBuffer(stageIndex, ref cachedTextureBufferIndex, ref cachedSamplerBufferIndex, ref cachedTextureBuffer, ref cachedSamplerBuffer, textureBufferIndex, samplerBufferIndex);

                int packedId = TextureHandle.ReadPackedId(bindingInfo.Handle, cachedTextureBuffer, cachedSamplerBuffer);
                int textureId = TextureHandle.UnpackTextureId(packedId);
                int samplerId;

                if (_samplerIndex == SamplerIndex.ViaHeaderIndex)
                {
                    samplerId = textureId;
                }
                else
                {
                    samplerId = TextureHandle.UnpackSamplerId(packedId);
                }

                ref TextureState state = ref _textureState[bindingInfo.Binding];

                if (!poolModified &&
                    state.TextureHandle == textureId &&
                    state.SamplerHandle == samplerId &&
                    state.CachedTexture != null &&
                    state.CachedTexture.InvalidatedSequence == state.InvalidatedSequence &&
                    state.CachedSampler?.IsDisposed != true)
                {
                    // The texture is already bound.
                    state.CachedTexture.SynchronizeMemory();

                    if ((usageFlags & TextureUsageFlags.NeedsScaleValue) != 0 &&
                        UpdateScale(state.CachedTexture, usageFlags, index, stage))
                    {
                        ITexture hostTextureRebind = state.CachedTexture.GetTargetTexture(bindingInfo.Target);

                        state.Texture = hostTextureRebind;

                        _context.Renderer.Pipeline.SetTextureAndSampler(stage, bindingInfo.Binding, hostTextureRebind, state.Sampler);
                    }

                    continue;
                }

                state.TextureHandle = textureId;
                state.SamplerHandle = samplerId;

                ref readonly TextureDescriptor descriptor = ref texturePool.GetForBinding(textureId, out Texture texture);

                specStateMatches &= specState.MatchesTexture(stage, index, descriptor);

                Sampler sampler = samplerPool?.Get(samplerId);

                ITexture hostTexture = texture?.GetTargetTexture(bindingInfo.Target);
                ISampler hostSampler = sampler?.GetHostSampler(texture);

                if (hostTexture != null && texture.Target == Target.TextureBuffer)
                {
                    // Ensure that the buffer texture is using the correct buffer as storage.
                    // Buffers are frequently re-created to accommodate larger data, so we need to re-bind
                    // to ensure we're not using a old buffer that was already deleted.
                    _channel.BufferManager.SetBufferTextureStorage(stage, hostTexture, texture.Range, bindingInfo, bindingInfo.Format, false);

                    // Cache is not used for buffer texture, it must always rebind.
                    state.CachedTexture = null;
                }
                else
                {
                    bool textureOrSamplerChanged = state.Texture != hostTexture || state.Sampler != hostSampler;

                    if ((usageFlags & TextureUsageFlags.NeedsScaleValue) != 0 &&
                        UpdateScale(texture, usageFlags, index, stage))
                    {
                        hostTexture = texture?.GetTargetTexture(bindingInfo.Target);
                        textureOrSamplerChanged = true;
                    }

                    if (textureOrSamplerChanged)
                    {
                        state.Texture = hostTexture;
                        state.Sampler = hostSampler;

                        _context.Renderer.Pipeline.SetTextureAndSampler(stage, bindingInfo.Binding, hostTexture, hostSampler);
                    }

                    state.CachedTexture = texture;
                    state.CachedSampler = sampler;
                    state.InvalidatedSequence = texture?.InvalidatedSequence ?? 0;
                }
            }

            return specStateMatches;
        }

        /// <summary>
        /// Ensures that the image bindings are visible to the host GPU.
        /// Note: this actually performs the binding using the host graphics API.
        /// </summary>
        /// <param name="pool">The current texture pool</param>
        /// <param name="stage">The shader stage using the textures to be bound</param>
        /// <param name="stageIndex">The stage number of the specified shader stage</param>
        /// <param name="poolModified">True if either the texture or sampler pool was modified, false otherwise</param>
        /// <param name="specState">Specialization state for the bound shader</param>
        /// <returns>True if all bound images match the current shader specialiation state, false otherwise</returns>
        private bool CommitImageBindings(TexturePool pool, ShaderStage stage, int stageIndex, bool poolModified, ShaderSpecializationState specState)
        {
            int imageCount = _imageBindings[stageIndex].Length;
            if (imageCount == 0)
            {
                return true;
            }

            if (pool == null)
            {
                Logger.Error?.Print(LogClass.Gpu, $"Shader stage \"{stage}\" uses images, but texture pool was not set.");
                return true;
            }

            // Scales for images appear after the texture ones.
            int baseScaleIndex = _textureCounts[stageIndex];

            int cachedTextureBufferIndex = -1;
            int cachedSamplerBufferIndex = -1;
            ReadOnlySpan<int> cachedTextureBuffer = Span<int>.Empty;
            ReadOnlySpan<int> cachedSamplerBuffer = Span<int>.Empty;

            bool specStateMatches = true;

            for (int index = 0; index < imageCount; index++)
            {
                TextureBindingInfo bindingInfo = _imageBindings[stageIndex][index];
                TextureUsageFlags usageFlags = bindingInfo.Flags;

                if (bindingInfo.ArrayLength > 1)
                {
                    _arrayBindingsCache.UpdateImageArray(pool, stage, stageIndex, _textureBufferIndex, bindingInfo);

                    continue;
                }

                int scaleIndex = baseScaleIndex + index;

                (int textureBufferIndex, int samplerBufferIndex) = TextureHandle.UnpackSlots(bindingInfo.CbufSlot, _textureBufferIndex);

                UpdateCachedBuffer(stageIndex, ref cachedTextureBufferIndex, ref cachedSamplerBufferIndex, ref cachedTextureBuffer, ref cachedSamplerBuffer, textureBufferIndex, samplerBufferIndex);

                int packedId = TextureHandle.ReadPackedId(bindingInfo.Handle, cachedTextureBuffer, cachedSamplerBuffer);
                int textureId = TextureHandle.UnpackTextureId(packedId);

                ref TextureState state = ref _imageState[bindingInfo.Binding];

                bool isStore = bindingInfo.Flags.HasFlag(TextureUsageFlags.ImageStore);

                if (!poolModified &&
                    state.TextureHandle == textureId &&
                    state.CachedTexture != null &&
                    state.CachedTexture.InvalidatedSequence == state.InvalidatedSequence)
                {
                    Texture cachedTexture = state.CachedTexture;

                    // The texture is already bound.
                    cachedTexture.SynchronizeMemory();

                    if (isStore)
                    {
                        cachedTexture.SignalModified();
                    }

                    Format format = bindingInfo.Format == 0 ? cachedTexture.Format : bindingInfo.Format;

                    if (state.ImageFormat != format ||
                        ((usageFlags & TextureUsageFlags.NeedsScaleValue) != 0 &&
                        UpdateScale(state.CachedTexture, usageFlags, scaleIndex, stage)))
                    {
                        ITexture hostTextureRebind = state.CachedTexture.GetTargetTexture(bindingInfo.Target);

                        state.Texture = hostTextureRebind;
                        state.ImageFormat = format;

                        _context.Renderer.Pipeline.SetImage(stage, bindingInfo.Binding, hostTextureRebind, format);
                    }

                    continue;
                }

                state.TextureHandle = textureId;

                ref readonly TextureDescriptor descriptor = ref pool.GetForBinding(textureId, out Texture texture);

                specStateMatches &= specState.MatchesImage(stage, index, descriptor);

                ITexture hostTexture = texture?.GetTargetTexture(bindingInfo.Target);

                if (hostTexture != null && texture.Target == Target.TextureBuffer)
                {
                    // Ensure that the buffer texture is using the correct buffer as storage.
                    // Buffers are frequently re-created to accommodate larger data, so we need to re-bind
                    // to ensure we're not using a old buffer that was already deleted.

                    Format format = bindingInfo.Format;

                    if (format == 0 && texture != null)
                    {
                        format = texture.Format;
                    }

                    _channel.BufferManager.SetBufferTextureStorage(stage, hostTexture, texture.Range, bindingInfo, format, true);

                    // Cache is not used for buffer texture, it must always rebind.
                    state.CachedTexture = null;
                }
                else
                {
                    if (isStore)
                    {
                        texture?.SignalModified();
                    }

                    if ((usageFlags & TextureUsageFlags.NeedsScaleValue) != 0 &&
                        UpdateScale(texture, usageFlags, scaleIndex, stage))
                    {
                        hostTexture = texture?.GetTargetTexture(bindingInfo.Target);
                    }

                    if (state.Texture != hostTexture)
                    {
                        state.Texture = hostTexture;

                        Format format = bindingInfo.Format;

                        if (format == 0 && texture != null)
                        {
                            format = texture.Format;
                        }

                        state.ImageFormat = format;

                        _context.Renderer.Pipeline.SetImage(stage, bindingInfo.Binding, hostTexture, format);
                    }

                    state.CachedTexture = texture;
                    state.InvalidatedSequence = texture?.InvalidatedSequence ?? 0;
                }
            }

            return specStateMatches;
        }

        /// <summary>
        /// Gets the texture descriptor for a given texture handle.
        /// </summary>
        /// <param name="poolGpuVa">GPU virtual address of the texture pool</param>
        /// <param name="bufferIndex">Index of the constant buffer with texture handles</param>
        /// <param name="maximumId">Maximum ID of the texture pool</param>
        /// <param name="stageIndex">The stage number where the texture is bound</param>
        /// <param name="handle">The texture handle</param>
        /// <param name="cbufSlot">The texture handle's constant buffer slot</param>
        /// <returns>The texture descriptor for the specified texture</returns>
        public TextureDescriptor GetTextureDescriptor(
            ulong poolGpuVa,
            int bufferIndex,
            int maximumId,
            int stageIndex,
            int handle,
            int cbufSlot)
        {
            (int textureBufferIndex, int samplerBufferIndex) = TextureHandle.UnpackSlots(cbufSlot, bufferIndex);

            int packedId = ReadPackedId(stageIndex, handle, textureBufferIndex, samplerBufferIndex);
            int textureId = TextureHandle.UnpackTextureId(packedId);

            ulong poolAddress = _channel.MemoryManager.Translate(poolGpuVa);

            TexturePool texturePool = _texturePoolCache.FindOrCreate(_channel, poolAddress, maximumId);

            TextureDescriptor descriptor;

            if (texturePool.IsValidId(textureId))
            {
                descriptor = texturePool.GetDescriptor(textureId);
            }
            else
            {
                // If the ID is not valid, we just return a default descriptor with the most common state.
                // Since this is used for shader specialization, doing so might avoid the need for recompilations.
                descriptor = new TextureDescriptor();
                descriptor.Word4 |= (uint)TextureTarget.Texture2D << 23;
                descriptor.Word5 |= 1u << 31; // Coords normalized.
            }

            return descriptor;
        }

        /// <summary>
        /// Reads a packed texture and sampler ID (basically, the real texture handle)
        /// from the texture constant buffer.
        /// </summary>
        /// <param name="stageIndex">The number of the shader stage where the texture is bound</param>
        /// <param name="wordOffset">A word offset of the handle on the buffer (the "fake" shader handle)</param>
        /// <param name="textureBufferIndex">Index of the constant buffer holding the texture handles</param>
        /// <param name="samplerBufferIndex">Index of the constant buffer holding the sampler handles</param>
        /// <returns>The packed texture and sampler ID (the real texture handle)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadPackedId(int stageIndex, int wordOffset, int textureBufferIndex, int samplerBufferIndex)
        {
            (int textureWordOffset, int samplerWordOffset, TextureHandleType handleType) = TextureHandle.UnpackOffsets(wordOffset);

            ulong textureBufferAddress = _isCompute
                ? _channel.BufferManager.GetComputeUniformBufferAddress(textureBufferIndex)
                : _channel.BufferManager.GetGraphicsUniformBufferAddress(stageIndex, textureBufferIndex);

            int handle = textureBufferAddress != 0
                ? _channel.MemoryManager.Physical.Read<int>(textureBufferAddress + (uint)textureWordOffset * 4)
                : 0;

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
                    ulong samplerBufferAddress = _isCompute
                        ? _channel.BufferManager.GetComputeUniformBufferAddress(samplerBufferIndex)
                        : _channel.BufferManager.GetGraphicsUniformBufferAddress(stageIndex, samplerBufferIndex);

                    samplerHandle = samplerBufferAddress != 0
                        ? _channel.MemoryManager.Physical.Read<int>(samplerBufferAddress + (uint)samplerWordOffset * 4)
                        : 0;
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

        /// <summary>
        /// Gets the texture and sampler pool for the GPU virtual address that are currently set.
        /// </summary>
        /// <returns>The texture and sampler pools</returns>
        private (TexturePool, SamplerPool) GetPools()
        {
            MemoryManager memoryManager = _channel.MemoryManager;

            TexturePool texturePool = _texturePool;
            SamplerPool samplerPool = _samplerPool;

            if (texturePool == null)
            {
                ulong poolAddress = memoryManager.Translate(_texturePoolGpuVa);

                if (poolAddress != MemoryManager.PteUnmapped)
                {
                    texturePool = _texturePoolCache.FindOrCreate(_channel, poolAddress, _texturePoolMaximumId);
                    _texturePool = texturePool;
                }
            }

            if (samplerPool == null)
            {
                ulong poolAddress = memoryManager.Translate(_samplerPoolGpuVa);

                if (poolAddress != MemoryManager.PteUnmapped)
                {
                    samplerPool = _samplerPoolCache.FindOrCreate(_channel, poolAddress, _samplerPoolMaximumId);
                    _samplerPool = samplerPool;
                }
            }

            return (texturePool, samplerPool);
        }

        /// <summary>
        /// Forces the texture and sampler pools to be re-loaded from the cache on next use.
        /// </summary>
        /// <remarks>
        /// This should be called if the memory mappings change, to ensure the correct pools are being used.
        /// </remarks>
        public void ReloadPools()
        {
            _samplerPool = null;
            _texturePool = null;
        }

        /// <summary>
        /// Force all bound textures and images to be rebound the next time CommitBindings is called.
        /// </summary>
        public void Rebind()
        {
            Array.Clear(_textureState);
            Array.Clear(_imageState);
        }
    }
}
