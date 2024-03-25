using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Engine.Threed.Blender;
using Ryujinx.Graphics.Gpu.Engine.Types;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.Gpu.Shader;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Texture;
using System;
using System.Runtime.CompilerServices;

namespace Ryujinx.Graphics.Gpu.Engine.Threed
{
    /// <summary>
    /// GPU state updater.
    /// </summary>
    class StateUpdater
    {
        public const int ShaderStateIndex = 26;
        public const int RtColorMaskIndex = 14;
        public const int RasterizerStateIndex = 15;
        public const int ScissorStateIndex = 16;
        public const int VertexBufferStateIndex = 0;
        public const int BlendStateIndex = 2;
        public const int IndexBufferStateIndex = 23;
        public const int PrimitiveRestartStateIndex = 12;
        public const int RenderTargetStateIndex = 27;

        // Vertex buffers larger than this size will be clamped to the mapped size.
        private const ulong VertexBufferSizeToMappedSizeThreshold = 256 * 1024 * 1024; // 256 MB

        private readonly GpuContext _context;
        private readonly GpuChannel _channel;
        private readonly DeviceStateWithShadow<ThreedClassState> _state;
        private readonly DrawState _drawState;
        private readonly AdvancedBlendManager _blendManager;

        private readonly StateUpdateTracker<ThreedClassState> _updateTracker;

        private readonly ShaderProgramInfo[] _currentProgramInfo;
        private ShaderSpecializationState _shaderSpecState;
        private readonly SpecializationStateUpdater _currentSpecState;

        private ProgramPipelineState _pipeline;

        private bool _fsReadsFragCoord;
        private bool _vsUsesDrawParameters;
        private bool _vtgWritesRtLayer;
        private byte _vsClipDistancesWritten;
        private uint _vbEnableMask;

        private bool _prevDrawIndexed;
        private bool _prevDrawIndirect;
        private bool _prevDrawUsesEngineState;
        private IndexType _prevIndexType;
        private uint _prevFirstVertex;
        private bool _prevTfEnable;

        private uint _prevRtNoAlphaMask;

        /// <summary>
        /// Creates a new instance of the state updater.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="channel">GPU channel</param>
        /// <param name="state">3D engine state</param>
        /// <param name="drawState">Draw state</param>
        /// <param name="blendManager">Advanced blend manager</param>
        /// <param name="spec">Specialization state updater</param>
        public StateUpdater(
            GpuContext context,
            GpuChannel channel,
            DeviceStateWithShadow<ThreedClassState> state,
            DrawState drawState,
            AdvancedBlendManager blendManager,
            SpecializationStateUpdater spec)
        {
            _context = context;
            _channel = channel;
            _state = state;
            _drawState = drawState;
            _blendManager = blendManager;
            _currentProgramInfo = new ShaderProgramInfo[Constants.ShaderStages];
            _currentSpecState = spec;

            // ShaderState must be updated after other state updates, as specialization/pipeline state is used when fetching shaders.
            // Render target state must appear after shader state as it depends on information from the currently bound shader.
            // Rasterizer and scissor states are checked by render target clear, their indexes
            // must be updated on the constants "RasterizerStateIndex" and "ScissorStateIndex" if modified.
            // The vertex buffer state may be forced dirty when a indexed draw starts, the "VertexBufferStateIndex"
            // constant must be updated if modified.
            // The order of the other state updates doesn't matter.
            _updateTracker = new StateUpdateTracker<ThreedClassState>(new[]
            {
                new StateUpdateCallbackEntry(UpdateVertexBufferState,
                    nameof(ThreedClassState.VertexBufferDrawState),
                    nameof(ThreedClassState.VertexBufferInstanced),
                    nameof(ThreedClassState.VertexBufferState),
                    nameof(ThreedClassState.VertexBufferEndAddress)),

                // Must be done after vertex buffer updates.
                new StateUpdateCallbackEntry(UpdateVertexAttribState, nameof(ThreedClassState.VertexAttribState)),

                new StateUpdateCallbackEntry(UpdateBlendState,
                    nameof(ThreedClassState.BlendUcodeEnable),
                    nameof(ThreedClassState.BlendUcodeSize),
                    nameof(ThreedClassState.BlendIndependent),
                    nameof(ThreedClassState.BlendConstant),
                    nameof(ThreedClassState.BlendStateCommon),
                    nameof(ThreedClassState.BlendEnableCommon),
                    nameof(ThreedClassState.BlendEnable),
                    nameof(ThreedClassState.BlendState)),

                new StateUpdateCallbackEntry(UpdateFaceState, nameof(ThreedClassState.FaceState)),

                new StateUpdateCallbackEntry(UpdateStencilTestState,
                    nameof(ThreedClassState.StencilBackMasks),
                    nameof(ThreedClassState.StencilTestState),
                    nameof(ThreedClassState.StencilBackTestState)),

                new StateUpdateCallbackEntry(UpdateDepthTestState,
                    nameof(ThreedClassState.DepthTestEnable),
                    nameof(ThreedClassState.DepthWriteEnable),
                    nameof(ThreedClassState.DepthTestFunc)),

                new StateUpdateCallbackEntry(UpdateTessellationState,
                    nameof(ThreedClassState.TessMode),
                    nameof(ThreedClassState.TessOuterLevel),
                    nameof(ThreedClassState.TessInnerLevel),
                    nameof(ThreedClassState.PatchVertices)),

                new StateUpdateCallbackEntry(UpdateViewportTransform,
                    nameof(ThreedClassState.DepthMode),
                    nameof(ThreedClassState.ViewportTransform),
                    nameof(ThreedClassState.ViewportExtents),
                    nameof(ThreedClassState.YControl),
                    nameof(ThreedClassState.ViewportTransformEnable)),

                new StateUpdateCallbackEntry(UpdateLogicOpState, nameof(ThreedClassState.LogicOpState)),

                new StateUpdateCallbackEntry(UpdateDepthClampState, nameof(ThreedClassState.ViewVolumeClipControl)),

                new StateUpdateCallbackEntry(UpdatePolygonMode,
                    nameof(ThreedClassState.PolygonModeFront),
                    nameof(ThreedClassState.PolygonModeBack)),

                new StateUpdateCallbackEntry(UpdateDepthBiasState,
                    nameof(ThreedClassState.DepthBiasState),
                    nameof(ThreedClassState.DepthBiasFactor),
                    nameof(ThreedClassState.DepthBiasUnits),
                    nameof(ThreedClassState.DepthBiasClamp)),

                new StateUpdateCallbackEntry(UpdatePrimitiveRestartState, nameof(ThreedClassState.PrimitiveRestartState)),

                new StateUpdateCallbackEntry(UpdateLineState,
                    nameof(ThreedClassState.LineWidthSmooth),
                    nameof(ThreedClassState.LineSmoothEnable)),

                new StateUpdateCallbackEntry(UpdateRtColorMask,
                    nameof(ThreedClassState.RtColorMaskShared),
                    nameof(ThreedClassState.RtColorMask)),

                new StateUpdateCallbackEntry(UpdateRasterizerState, nameof(ThreedClassState.RasterizeEnable)),

                new StateUpdateCallbackEntry(UpdateScissorState,
                    nameof(ThreedClassState.ScissorState),
                    nameof(ThreedClassState.ScreenScissorState)),

                new StateUpdateCallbackEntry(UpdateTfBufferState, nameof(ThreedClassState.TfBufferState)),
                new StateUpdateCallbackEntry(UpdateUserClipState, nameof(ThreedClassState.ClipDistanceEnable)),

                new StateUpdateCallbackEntry(UpdateAlphaTestState,
                    nameof(ThreedClassState.AlphaTestEnable),
                    nameof(ThreedClassState.AlphaTestRef),
                    nameof(ThreedClassState.AlphaTestFunc)),

                new StateUpdateCallbackEntry(UpdateSamplerPoolState,
                    nameof(ThreedClassState.SamplerPoolState),
                    nameof(ThreedClassState.SamplerIndex)),

                new StateUpdateCallbackEntry(UpdateTexturePoolState, nameof(ThreedClassState.TexturePoolState)),

                new StateUpdateCallbackEntry(UpdatePointState,
                    nameof(ThreedClassState.PointSize),
                    nameof(ThreedClassState.VertexProgramPointSize),
                    nameof(ThreedClassState.PointSpriteEnable),
                    nameof(ThreedClassState.PointCoordReplace)),

                new StateUpdateCallbackEntry(UpdateIndexBufferState,
                    nameof(ThreedClassState.IndexBufferState),
                    nameof(ThreedClassState.IndexBufferCount)),

                new StateUpdateCallbackEntry(UpdateMultisampleState,
                    nameof(ThreedClassState.AlphaToCoverageDitherEnable),
                    nameof(ThreedClassState.MultisampleControl)),

                new StateUpdateCallbackEntry(UpdateEarlyZState,
                    nameof(ThreedClassState.EarlyZForce)),

                new StateUpdateCallbackEntry(UpdateShaderState,
                    nameof(ThreedClassState.ShaderBaseAddress),
                    nameof(ThreedClassState.ShaderState)),

                new StateUpdateCallbackEntry(UpdateRenderTargetState,
                    nameof(ThreedClassState.RtColorState),
                    nameof(ThreedClassState.RtDepthStencilState),
                    nameof(ThreedClassState.RtControl),
                    nameof(ThreedClassState.RtDepthStencilSize),
                    nameof(ThreedClassState.RtDepthStencilEnable)),
            });
        }

        /// <summary>
        /// Sets a register at a specific offset as dirty.
        /// This must be called if the register value was modified.
        /// </summary>
        /// <param name="offset">Register offset</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDirty(int offset)
        {
            _updateTracker.SetDirty(offset);
        }

        /// <summary>
        /// Force all the guest state to be marked as dirty.
        /// The next call to <see cref="Update"/> will update all the host state.
        /// </summary>
        public void SetAllDirty()
        {
            _updateTracker.SetAllDirty();
        }

        /// <summary>
        /// Updates host state for any modified guest state, since the last time this function was called.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            // The vertex buffer size is calculated using a different
            // method when doing indexed draws, so we need to make sure
            // to update the vertex buffers if we are doing a regular
            // draw after a indexed one and vice-versa.
            // Some draws also do not update the engine state, so it is possible for it
            // to not be dirty even if the vertex counts or other state changed. We need to force it to be dirty in this case.
            if (_drawState.DrawIndexed != _prevDrawIndexed || _drawState.DrawUsesEngineState != _prevDrawUsesEngineState)
            {
                _updateTracker.ForceDirty(VertexBufferStateIndex);

                // If PrimitiveRestartDrawArrays is false and this is a non-indexed draw, we need to ensure primitive restart is disabled.
                // If PrimitiveRestartDrawArrays is false and this is a indexed draw, we need to ensure primitive restart enable matches GPU state.
                // If PrimitiveRestartDrawArrays is true, then primitive restart enable should always match GPU state.
                // That is because "PrimitiveRestartDrawArrays" is not configurable on the backend, it is always
                // true on OpenGL and always false on Vulkan.
                if (!_state.State.PrimitiveRestartDrawArrays && _state.State.PrimitiveRestartState.Enable)
                {
                    _updateTracker.ForceDirty(PrimitiveRestartStateIndex);
                }

                _prevDrawIndexed = _drawState.DrawIndexed;
                _prevDrawUsesEngineState = _drawState.DrawUsesEngineState;
            }

            // Some draw parameters are used to restrict the vertex buffer size,
            // but they can't be used on indirect draws because their values are unknown in this case.
            // When switching between indirect and non-indirect draw, we need to
            // make sure the vertex buffer sizes are still correct.
            if (_drawState.DrawIndirect != _prevDrawIndirect)
            {
                _updateTracker.ForceDirty(VertexBufferStateIndex);

                _prevDrawIndirect = _drawState.DrawIndirect;
            }

            // In some cases, the index type is also used to guess the
            // vertex buffer size, so we must update it if the type changed too.
            if (_drawState.DrawIndexed &&
                (_prevIndexType != _state.State.IndexBufferState.Type ||
                 _prevFirstVertex != _state.State.FirstVertex))
            {
                _updateTracker.ForceDirty(VertexBufferStateIndex);
                _prevIndexType = _state.State.IndexBufferState.Type;
                _prevFirstVertex = _state.State.FirstVertex;
            }

            bool tfEnable = _state.State.TfEnable && _context.Capabilities.SupportsTransformFeedback;

            if (!tfEnable && _prevTfEnable)
            {
                _context.Renderer.Pipeline.EndTransformFeedback();
                _prevTfEnable = false;
            }

            if (_updateTracker.IsDirty(RenderTargetStateIndex))
            {
                UpdateRenderTargetSpecialization();
            }

            _updateTracker.Update(ulong.MaxValue);

            // If any state that the shader depends on changed,
            // then we may need to compile/bind a different version
            // of the shader for the new state.
            if (_shaderSpecState != null && _currentSpecState.HasChanged())
            {
                if (!_shaderSpecState.MatchesGraphics(
                    _channel,
                    ref _currentSpecState.GetPoolState(),
                    ref _currentSpecState.GetGraphicsState(),
                    _drawState.VertexAsCompute != null,
                    _vsUsesDrawParameters,
                    checkTextures: false))
                {
                    // Shader must be reloaded. _vtgWritesRtLayer should not change.
                    UpdateShaderState();
                }
            }

            CommitBindings();

            if (tfEnable && !_prevTfEnable)
            {
                _context.Renderer.Pipeline.BeginTransformFeedback(_drawState.Topology);
                _prevTfEnable = true;
            }
        }

        /// <summary>
        /// Updates the host state for any modified guest state group with the respective bit set on <paramref name="mask"/>.
        /// </summary>
        /// <param name="mask">Mask, where each bit set corresponds to a group index that should be checked and updated</param>
        public void Update(ulong mask)
        {
            _updateTracker.Update(mask);
        }

        /// <summary>
        /// Ensures that the bindings are visible to the host GPU.
        /// Note: this actually performs the binding using the host graphics API.
        /// </summary>
        private void CommitBindings()
        {
            UpdateStorageBuffers();

            bool unalignedChanged = _currentSpecState.SetHasUnalignedStorageBuffer(_channel.BufferManager.HasUnalignedStorageBuffers);

            bool scaleMismatch;
            do
            {
                if (!_channel.TextureManager.CommitGraphicsBindings(_shaderSpecState, out scaleMismatch) || unalignedChanged)
                {
                    // Shader must be reloaded. _vtgWritesRtLayer should not change.
                    UpdateShaderState();
                }

                if (scaleMismatch)
                {
                    // Binding textures changed scale of the bound render targets, correct the render target scale and rebind.
                    UpdateRenderTargetState();
                }
            }
            while (scaleMismatch);

            _channel.BufferManager.CommitGraphicsBindings(_drawState.DrawIndexed);
        }

        /// <summary>
        /// Updates storage buffer bindings.
        /// </summary>
        private void UpdateStorageBuffers()
        {
            for (int stage = 0; stage < Constants.ShaderStages; stage++)
            {
                ShaderProgramInfo info = _currentProgramInfo[stage];

                if (info == null)
                {
                    continue;
                }

                for (int index = 0; index < info.SBuffers.Count; index++)
                {
                    BufferDescriptor sb = info.SBuffers[index];

                    ulong sbDescAddress = _channel.BufferManager.GetGraphicsUniformBufferAddress(stage, sb.SbCbSlot);
                    sbDescAddress += (ulong)sb.SbCbOffset * 4;

                    SbDescriptor sbDescriptor = _channel.MemoryManager.Physical.Read<SbDescriptor>(sbDescAddress);

                    uint size;
                    if (sb.SbCbSlot == Constants.DriverReservedUniformBuffer)
                    {
                        // Only trust the SbDescriptor size if it comes from slot 0.
                        size = (uint)sbDescriptor.Size;
                    }
                    else
                    {
                        // TODO: Use full mapped size and somehow speed up buffer sync.
                        size = (uint)_channel.MemoryManager.GetMappedSize(sbDescriptor.PackAddress(), Constants.MaxUnknownStorageSize);
                    }

                    _channel.BufferManager.SetGraphicsStorageBuffer(stage, sb.Slot, sbDescriptor.PackAddress(), size, sb.Flags);
                }
            }
        }

        /// <summary>
        /// Updates tessellation state based on the guest GPU state.
        /// </summary>
        private void UpdateTessellationState()
        {
            _pipeline.PatchControlPoints = (uint)_state.State.PatchVertices;

            _context.Renderer.Pipeline.SetPatchParameters(
                _state.State.PatchVertices,
                _state.State.TessOuterLevel.AsSpan(),
                _state.State.TessInnerLevel.AsSpan());

            _currentSpecState.SetTessellationMode(_state.State.TessMode);
        }

        /// <summary>
        /// Updates transform feedback buffer state based on the guest GPU state.
        /// </summary>
        private void UpdateTfBufferState()
        {
            for (int index = 0; index < Constants.TotalTransformFeedbackBuffers; index++)
            {
                TfBufferState tfb = _state.State.TfBufferState[index];

                if (!tfb.Enable)
                {
                    _channel.BufferManager.SetTransformFeedbackBuffer(index, 0, 0);

                    continue;
                }

                _channel.BufferManager.SetTransformFeedbackBuffer(index, tfb.Address.Pack(), (uint)tfb.Size);
            }
        }

        /// <summary>
        /// Updates Rasterizer primitive discard state based on guest gpu state.
        /// </summary>
        private void UpdateRasterizerState()
        {
            bool enable = _state.State.RasterizeEnable;
            _pipeline.RasterizerDiscard = !enable;
            _context.Renderer.Pipeline.SetRasterizerDiscard(!enable);
        }

        /// <summary>
        /// Updates render targets (color and depth-stencil buffers) based on current render target state.
        /// </summary>
        private void UpdateRenderTargetState()
        {
            UpdateRenderTargetState(RenderTargetUpdateFlags.UpdateAll);
        }

        /// <summary>
        /// Updates render targets (color and depth-stencil buffers) based on current render target state.
        /// </summary>
        /// <param name="updateFlags">Flags indicating which render targets should be updated and how</param>
        /// <param name="singleUse">If this is not -1, it indicates that only the given indexed target will be used.</param>
        public void UpdateRenderTargetState(RenderTargetUpdateFlags updateFlags, int singleUse = -1)
        {
            var memoryManager = _channel.MemoryManager;
            var rtControl = _state.State.RtControl;

            bool useControl = updateFlags.HasFlag(RenderTargetUpdateFlags.UseControl);
            bool layered = updateFlags.HasFlag(RenderTargetUpdateFlags.Layered);
            bool singleColor = updateFlags.HasFlag(RenderTargetUpdateFlags.SingleColor);
            bool discard = updateFlags.HasFlag(RenderTargetUpdateFlags.DiscardClip);

            int count = useControl ? rtControl.UnpackCount() : Constants.TotalRenderTargets;

            var msaaMode = _state.State.RtMsaaMode;

            int samplesInX = msaaMode.SamplesInX();
            int samplesInY = msaaMode.SamplesInY();

            var scissor = _state.State.ScreenScissorState;
            Size sizeHint = new((scissor.X + scissor.Width) * samplesInX, (scissor.Y + scissor.Height) * samplesInY, 1);

            int clipRegionWidth = int.MaxValue;
            int clipRegionHeight = int.MaxValue;

            bool changedScale = false;
            uint rtNoAlphaMask = 0;

            for (int index = 0; index < Constants.TotalRenderTargets; index++)
            {
                int rtIndex = useControl ? rtControl.UnpackPermutationIndex(index) : index;

                var colorState = _state.State.RtColorState[rtIndex];

                if (index >= count || !IsRtEnabled(colorState) || (singleColor && index != singleUse))
                {
                    changedScale |= _channel.TextureManager.SetRenderTargetColor(index, null);

                    continue;
                }

                if (colorState.Format.NoAlpha())
                {
                    rtNoAlphaMask |= 1u << index;
                }

                Image.Texture color = memoryManager.Physical.TextureCache.FindOrCreateTexture(
                    memoryManager,
                    colorState,
                    _vtgWritesRtLayer || layered,
                    discard,
                    samplesInX,
                    samplesInY,
                    sizeHint);

                changedScale |= _channel.TextureManager.SetRenderTargetColor(index, color);

                if (color != null)
                {
                    if (clipRegionWidth > color.Width / samplesInX)
                    {
                        clipRegionWidth = color.Width / samplesInX;
                    }

                    if (clipRegionHeight > color.Height / samplesInY)
                    {
                        clipRegionHeight = color.Height / samplesInY;
                    }

                    if (!_context.Capabilities.SupportsBgraFormat)
                    {
                        _context.SupportBufferUpdater.SetRenderTargetIsBgra(index, color.Format.IsBgr());
                    }
                }
            }

            bool dsEnable = _state.State.RtDepthStencilEnable;

            Image.Texture depthStencil = null;

            if (dsEnable && updateFlags.HasFlag(RenderTargetUpdateFlags.UpdateDepthStencil))
            {
                var dsState = _state.State.RtDepthStencilState;
                var dsSize = _state.State.RtDepthStencilSize;

                depthStencil = memoryManager.Physical.TextureCache.FindOrCreateTexture(
                    memoryManager,
                    dsState,
                    dsSize,
                    _vtgWritesRtLayer || layered,
                    discard,
                    samplesInX,
                    samplesInY,
                    sizeHint);

                if (depthStencil != null)
                {
                    if (clipRegionWidth > depthStencil.Width / samplesInX)
                    {
                        clipRegionWidth = depthStencil.Width / samplesInX;
                    }

                    if (clipRegionHeight > depthStencil.Height / samplesInY)
                    {
                        clipRegionHeight = depthStencil.Height / samplesInY;
                    }
                }
            }

            changedScale |= _channel.TextureManager.SetRenderTargetDepthStencil(depthStencil);

            if (changedScale)
            {
                float oldScale = _channel.TextureManager.RenderTargetScale;
                _channel.TextureManager.UpdateRenderTargetScale(singleUse);

                if (oldScale != _channel.TextureManager.RenderTargetScale)
                {
                    _context.SupportBufferUpdater.SetRenderTargetScale(_channel.TextureManager.RenderTargetScale);

                    UpdateViewportTransform();
                    UpdateScissorState();
                }
            }

            _channel.TextureManager.SetClipRegion(clipRegionWidth, clipRegionHeight);

            if (useControl && _prevRtNoAlphaMask != rtNoAlphaMask)
            {
                _prevRtNoAlphaMask = rtNoAlphaMask;

                UpdateBlendState();
            }
        }

        /// <summary>
        /// Updates specialization state based on render target state.
        /// </summary>
        public void UpdateRenderTargetSpecialization()
        {
            _currentSpecState.SetFragmentOutputTypes(_state.State.RtControl, ref _state.State.RtColorState);
        }

        /// <summary>
        /// Checks if a render target color buffer is used.
        /// </summary>
        /// <param name="colorState">Color buffer information</param>
        /// <returns>True if the specified buffer is enabled/used, false otherwise</returns>
        internal static bool IsRtEnabled(RtColorState colorState)
        {
            // Colors are disabled by writing 0 to the format.
            return colorState.Format != 0 && colorState.WidthOrStride != 0;
        }

        /// <summary>
        /// Updates host scissor test state based on current GPU state.
        /// </summary>
        public void UpdateScissorState()
        {
            const int MinX = 0;
            const int MinY = 0;
            const int MaxW = 0xffff;
            const int MaxH = 0xffff;

            Span<Rectangle<int>> regions = stackalloc Rectangle<int>[Constants.TotalViewports];

            for (int index = 0; index < Constants.TotalViewports; index++)
            {
                ScissorState scissor = _state.State.ScissorState[index];

                bool enable = scissor.Enable && (scissor.X1 != MinX ||
                                                 scissor.Y1 != MinY ||
                                                 scissor.X2 != MaxW ||
                                                 scissor.Y2 != MaxH);

                if (enable)
                {
                    int x = scissor.X1;
                    int y = scissor.Y1;
                    int width = scissor.X2 - x;
                    int height = scissor.Y2 - y;

                    if (_state.State.YControl.HasFlag(YControl.NegateY))
                    {
                        ref var screenScissor = ref _state.State.ScreenScissorState;
                        y = screenScissor.Height - height - y;

                        if (y < 0)
                        {
                            height += y;
                            y = 0;
                        }
                    }

                    float scale = _channel.TextureManager.RenderTargetScale;
                    if (scale != 1f)
                    {
                        x = (int)(x * scale);
                        y = (int)(y * scale);
                        width = (int)MathF.Ceiling(width * scale);
                        height = (int)MathF.Ceiling(height * scale);
                    }

                    regions[index] = new Rectangle<int>(x, y, width, height);
                }
                else
                {
                    regions[index] = new Rectangle<int>(MinX, MinY, MaxW, MaxH);
                }
            }

            _context.Renderer.Pipeline.SetScissors(regions);
        }

        /// <summary>
        /// Updates host depth clamp state based on current GPU state.
        /// </summary>
        /// <param name="state">Current GPU state</param>
        private void UpdateDepthClampState()
        {
            ViewVolumeClipControl clip = _state.State.ViewVolumeClipControl;
            bool clamp = (clip & ViewVolumeClipControl.DepthClampDisabled) == 0;

            _pipeline.DepthClampEnable = clamp;
            _context.Renderer.Pipeline.SetDepthClamp(clamp);
        }

        /// <summary>
        /// Updates host alpha test state based on current GPU state.
        /// </summary>
        private void UpdateAlphaTestState()
        {
            _context.Renderer.Pipeline.SetAlphaTest(
                _state.State.AlphaTestEnable,
                _state.State.AlphaTestRef,
                _state.State.AlphaTestFunc);

            _currentSpecState.SetAlphaTest(
                _state.State.AlphaTestEnable,
                _state.State.AlphaTestRef,
                _state.State.AlphaTestFunc);
        }

        /// <summary>
        /// Updates host depth test state based on current GPU state.
        /// </summary>
        private void UpdateDepthTestState()
        {
            DepthTestDescriptor descriptor = new(
                _state.State.DepthTestEnable,
                _state.State.DepthWriteEnable,
                _state.State.DepthTestFunc);

            _pipeline.DepthTest = descriptor;
            _context.Renderer.Pipeline.SetDepthTest(descriptor);
        }

        /// <summary>
        /// Updates host viewport transform and clipping state based on current GPU state.
        /// </summary>
        private void UpdateViewportTransform()
        {
            var yControl = _state.State.YControl;
            var face = _state.State.FaceState;

            bool disableTransform = _state.State.ViewportTransformEnable == 0;
            bool yNegate = yControl.HasFlag(YControl.NegateY);

            UpdateFrontFace(yControl, face.FrontFace);
            UpdateDepthMode();

            Span<Viewport> viewports = stackalloc Viewport[Constants.TotalViewports];

            for (int index = 0; index < Constants.TotalViewports; index++)
            {
                if (disableTransform)
                {
                    ref var scissor = ref _state.State.ScreenScissorState;

                    float rScale = _channel.TextureManager.RenderTargetScale;
                    var scissorRect = new Rectangle<float>(0, 0, (scissor.X + scissor.Width) * rScale, (scissor.Y + scissor.Height) * rScale);

                    viewports[index] = new Viewport(scissorRect, ViewportSwizzle.PositiveX, ViewportSwizzle.PositiveY, ViewportSwizzle.PositiveZ, ViewportSwizzle.PositiveW, 0, 1);
                    continue;
                }

                ref var transform = ref _state.State.ViewportTransform[index];
                ref var extents = ref _state.State.ViewportExtents[index];

                float scaleX = MathF.Abs(transform.ScaleX);
                float scaleY = transform.ScaleY;

                if (yNegate)
                {
                    scaleY = -scaleY;
                }

                if (!_context.Capabilities.SupportsViewportSwizzle && transform.UnpackSwizzleY() == ViewportSwizzle.NegativeY)
                {
                    scaleY = -scaleY;
                }

                float x = transform.TranslateX - scaleX;
                float y = transform.TranslateY - scaleY;

                float width = scaleX * 2;
                float height = scaleY * 2;

                float scale = _channel.TextureManager.RenderTargetScale;
                if (scale != 1f)
                {
                    x *= scale;
                    y *= scale;
                    width *= scale;
                    height *= scale;
                }

                Rectangle<float> region = new(x, y, width, height);

                ViewportSwizzle swizzleX = transform.UnpackSwizzleX();
                ViewportSwizzle swizzleY = transform.UnpackSwizzleY();
                ViewportSwizzle swizzleZ = transform.UnpackSwizzleZ();
                ViewportSwizzle swizzleW = transform.UnpackSwizzleW();

                float depthNear = extents.DepthNear;
                float depthFar = extents.DepthFar;

                if (transform.ScaleZ < 0)
                {
                    (depthFar, depthNear) = (depthNear, depthFar);
                }

                viewports[index] = new Viewport(region, swizzleX, swizzleY, swizzleZ, swizzleW, depthNear, depthFar);
            }

            _context.Renderer.Pipeline.SetDepthMode(GetDepthMode());
            _context.Renderer.Pipeline.SetViewports(viewports);

            _context.SupportBufferUpdater.SetViewportTransformDisable(
                viewports[0].Region.Width,
                viewports[0].Region.Height,
                _channel.TextureManager.RenderTargetScale,
                disableTransform);

            // Viewport size is only used on the shader when YNegate is enabled,
            // and if the fragment shader accesses gl_FragCoord,
            // so there's no need to update it in other cases.
            if (yNegate && _fsReadsFragCoord)
            {
                UpdateSupportBufferViewportSize();
            }

            _currentSpecState.SetViewportTransformDisable(disableTransform);
            _currentSpecState.SetDepthMode(GetDepthMode() == DepthMode.MinusOneToOne);
            _currentSpecState.SetYNegateEnabled(yNegate);
        }

        /// <summary>
        /// Updates the depth mode (0 to 1 or -1 to 1) based on the current viewport and depth mode register state.
        /// </summary>
        private void UpdateDepthMode()
        {
            DepthMode mode = GetDepthMode();

            _pipeline.DepthMode = mode;

            _context.Renderer.Pipeline.SetDepthMode(mode);
        }

        /// <summary>
        /// Updates polygon mode state based on current GPU state.
        /// </summary>
        private void UpdatePolygonMode()
        {
            _context.Renderer.Pipeline.SetPolygonMode(_state.State.PolygonModeFront, _state.State.PolygonModeBack);
        }

        /// <summary>
        /// Updates host depth bias (also called polygon offset) state based on current GPU state.
        /// </summary>
        private void UpdateDepthBiasState()
        {
            var depthBias = _state.State.DepthBiasState;

            float factor = _state.State.DepthBiasFactor;
            float units = _state.State.DepthBiasUnits;
            float clamp = _state.State.DepthBiasClamp;

            PolygonModeMask enables;

            enables = (depthBias.PointEnable ? PolygonModeMask.Point : 0);
            enables |= (depthBias.LineEnable ? PolygonModeMask.Line : 0);
            enables |= (depthBias.FillEnable ? PolygonModeMask.Fill : 0);

            _pipeline.BiasEnable = enables;
            _context.Renderer.Pipeline.SetDepthBias(enables, factor, units / 2f, clamp);
        }

        /// <summary>
        /// Updates host stencil test state based on current GPU state.
        /// </summary>
        private void UpdateStencilTestState()
        {
            var backMasks = _state.State.StencilBackMasks;
            var test = _state.State.StencilTestState;
            var backTest = _state.State.StencilBackTestState;

            CompareOp backFunc;
            StencilOp backSFail;
            StencilOp backDpPass;
            StencilOp backDpFail;
            int backFuncRef;
            int backFuncMask;
            int backMask;

            if (backTest.TwoSided)
            {
                backFunc = backTest.BackFunc;
                backSFail = backTest.BackSFail;
                backDpPass = backTest.BackDpPass;
                backDpFail = backTest.BackDpFail;
                backFuncRef = backMasks.FuncRef;
                backFuncMask = backMasks.FuncMask;
                backMask = backMasks.Mask;
            }
            else
            {
                backFunc = test.FrontFunc;
                backSFail = test.FrontSFail;
                backDpPass = test.FrontDpPass;
                backDpFail = test.FrontDpFail;
                backFuncRef = test.FrontFuncRef;
                backFuncMask = test.FrontFuncMask;
                backMask = test.FrontMask;
            }

            StencilTestDescriptor descriptor = new(
                test.Enable,
                test.FrontFunc,
                test.FrontSFail,
                test.FrontDpPass,
                test.FrontDpFail,
                test.FrontFuncRef,
                test.FrontFuncMask,
                test.FrontMask,
                backFunc,
                backSFail,
                backDpPass,
                backDpFail,
                backFuncRef,
                backFuncMask,
                backMask);

            _pipeline.StencilTest = descriptor;
            _context.Renderer.Pipeline.SetStencilTest(descriptor);
        }

        /// <summary>
        /// Updates user-defined clipping based on the guest GPU state.
        /// </summary>
        private void UpdateUserClipState()
        {
            uint clipMask = _state.State.ClipDistanceEnable & _vsClipDistancesWritten;

            for (int i = 0; i < Constants.TotalClipDistances; ++i)
            {
                _context.Renderer.Pipeline.SetUserClipDistance(i, (clipMask & (1 << i)) != 0);
            }
        }

        /// <summary>
        /// Updates current sampler pool address and size based on guest GPU state.
        /// </summary>
        private void UpdateSamplerPoolState()
        {
            var texturePool = _state.State.TexturePoolState;
            var samplerPool = _state.State.SamplerPoolState;

            var samplerIndex = _state.State.SamplerIndex;

            int maximumId = samplerIndex == SamplerIndex.ViaHeaderIndex
                ? texturePool.MaximumId
                : samplerPool.MaximumId;

            _channel.TextureManager.SetGraphicsSamplerPool(samplerPool.Address.Pack(), maximumId, samplerIndex);
        }

        /// <summary>
        /// Updates current texture pool address and size based on guest GPU state.
        /// </summary>
        private void UpdateTexturePoolState()
        {
            var texturePool = _state.State.TexturePoolState;

            _channel.TextureManager.SetGraphicsTexturePool(texturePool.Address.Pack(), texturePool.MaximumId);
            _channel.TextureManager.SetGraphicsTextureBufferIndex((int)_state.State.TextureBufferIndex);

            _currentSpecState.SetPoolState(GetPoolState());
        }

        /// <summary>
        /// Updates host vertex attributes based on guest GPU state.
        /// </summary>
        private void UpdateVertexAttribState()
        {
            bool supportsScaledFormats = _context.Capabilities.SupportsScaledVertexFormats;
            uint vbEnableMask = _vbEnableMask;

            Span<VertexAttribDescriptor> vertexAttribs = stackalloc VertexAttribDescriptor[Constants.TotalVertexAttribs];

            for (int index = 0; index < Constants.TotalVertexAttribs; index++)
            {
                var vertexAttrib = _state.State.VertexAttribState[index];

                int bufferIndex = vertexAttrib.UnpackBufferIndex();

                if ((vbEnableMask & (1u << bufferIndex)) == 0)
                {
                    // Using a vertex buffer that doesn't exist is invalid, so let's use a dummy attribute for those cases.
                    vertexAttribs[index] = new VertexAttribDescriptor(0, 0, true, Format.R32G32B32A32Float);
                    continue;
                }

                uint packedFormat = vertexAttrib.UnpackFormat();

                if (!supportsScaledFormats)
                {
                    packedFormat = vertexAttrib.UnpackType() switch
                    {
                        VertexAttribType.Uscaled => ((uint)VertexAttribType.Uint << 27) | (packedFormat & (0x3f << 21)),
                        VertexAttribType.Sscaled => ((uint)VertexAttribType.Sint << 27) | (packedFormat & (0x3f << 21)),
                        _ => packedFormat,
                    };
                }

                if (!FormatTable.TryGetAttribFormat(packedFormat, out Format format))
                {
                    Logger.Debug?.Print(LogClass.Gpu, $"Invalid attribute format 0x{vertexAttrib.UnpackFormat():X}.");

                    format = vertexAttrib.UnpackType() switch
                    {
                        VertexAttribType.Sint => Format.R32G32B32A32Sint,
                        VertexAttribType.Uint => Format.R32G32B32A32Uint,
                        _ => Format.R32G32B32A32Float,
                    };
                }

                vertexAttribs[index] = new VertexAttribDescriptor(
                    bufferIndex,
                    vertexAttrib.UnpackOffset(),
                    vertexAttrib.UnpackIsConstant(),
                    format);
            }

            _pipeline.SetVertexAttribs(vertexAttribs);
            _context.Renderer.Pipeline.SetVertexAttribs(vertexAttribs);
            _currentSpecState.SetAttributeTypes(ref _state.State.VertexAttribState);
        }

        /// <summary>
        /// Updates host line width based on guest GPU state.
        /// </summary>
        private void UpdateLineState()
        {
            float width = _state.State.LineWidthSmooth;
            bool smooth = _state.State.LineSmoothEnable;

            _pipeline.LineWidth = width;
            _context.Renderer.Pipeline.SetLineParameters(width, smooth);
        }

        /// <summary>
        /// Updates host point size based on guest GPU state.
        /// </summary>
        private void UpdatePointState()
        {
            float size = _state.State.PointSize;
            bool isProgramPointSize = _state.State.VertexProgramPointSize;
            bool enablePointSprite = _state.State.PointSpriteEnable;

            // TODO: Need to figure out a way to map PointCoordReplace enable bit.
            Origin origin = (_state.State.PointCoordReplace & 4) == 0 ? Origin.LowerLeft : Origin.UpperLeft;

            _context.Renderer.Pipeline.SetPointParameters(size, isProgramPointSize, enablePointSprite, origin);

            _currentSpecState.SetProgramPointSizeEnable(isProgramPointSize);
            _currentSpecState.SetPointSize(size);
        }

        /// <summary>
        /// Updates host primitive restart based on guest GPU state.
        /// </summary>
        private void UpdatePrimitiveRestartState()
        {
            PrimitiveRestartState primitiveRestart = _state.State.PrimitiveRestartState;
            bool enable = primitiveRestart.Enable && (_drawState.DrawIndexed || _state.State.PrimitiveRestartDrawArrays);

            _pipeline.PrimitiveRestartEnable = enable;
            _context.Renderer.Pipeline.SetPrimitiveRestart(enable, primitiveRestart.Index);
        }

        /// <summary>
        /// Updates host index buffer binding based on guest GPU state.
        /// </summary>
        private void UpdateIndexBufferState()
        {
            var indexBuffer = _state.State.IndexBufferState;

            if (_drawState.IndexCount == 0)
            {
                return;
            }

            ulong gpuVa = indexBuffer.Address.Pack();

            // Do not use the end address to calculate the size, because
            // the result may be much larger than the real size of the index buffer.
            ulong size = (ulong)(_drawState.FirstIndex + _drawState.IndexCount);

            switch (indexBuffer.Type)
            {
                case IndexType.UShort:
                    size *= 2;
                    break;
                case IndexType.UInt:
                    size *= 4;
                    break;
            }

            _channel.BufferManager.SetIndexBuffer(gpuVa, size, indexBuffer.Type);
        }

        /// <summary>
        /// Updates host vertex buffer bindings based on guest GPU state.
        /// </summary>
        private void UpdateVertexBufferState()
        {
            IndexType indexType = _state.State.IndexBufferState.Type;
            bool indexTypeSmall = indexType == IndexType.UByte || indexType == IndexType.UShort;

            _drawState.IsAnyVbInstanced = false;

            bool drawIndexed = _drawState.DrawIndexed;
            bool drawIndirect = _drawState.DrawIndirect;
            int drawFirstVertex = _drawState.DrawFirstVertex;
            int drawVertexCount = _drawState.DrawVertexCount;
            uint vbEnableMask = 0;

            for (int index = 0; index < Constants.TotalVertexBuffers; index++)
            {
                var vertexBuffer = _state.State.VertexBufferState[index];

                if (!vertexBuffer.UnpackEnable())
                {
                    _pipeline.VertexBuffers[index] = new BufferPipelineDescriptor(false, 0, 0);
                    _channel.BufferManager.SetVertexBuffer(index, 0, 0, 0, 0);

                    continue;
                }

                GpuVa endAddress = _state.State.VertexBufferEndAddress[index];

                ulong address = vertexBuffer.Address.Pack();

                if (_channel.MemoryManager.IsMapped(address))
                {
                    vbEnableMask |= 1u << index;
                }

                int stride = vertexBuffer.UnpackStride();

                bool instanced = _state.State.VertexBufferInstanced[index];

                int divisor = instanced ? vertexBuffer.Divisor : 0;

                _drawState.IsAnyVbInstanced |= divisor != 0;

                ulong vbSize = endAddress.Pack() - address + 1;
                ulong size;

                if (_drawState.IbStreamer.HasInlineIndexData || drawIndexed || stride == 0 || instanced)
                {
                    // This size may be (much) larger than the real vertex buffer size.
                    // Avoid calculating it this way, unless we don't have any other option.

                    size = vbSize;

                    if (stride > 0 && indexTypeSmall && drawIndexed && !drawIndirect && !instanced)
                    {
                        // If the index type is a small integer type, then we might be still able
                        // to reduce the vertex buffer size based on the maximum possible index value.

                        ulong maxVertexBufferSize = indexType == IndexType.UByte ? 0x100UL : 0x10000UL;

                        maxVertexBufferSize += _state.State.FirstVertex;
                        maxVertexBufferSize *= (uint)stride;

                        size = Math.Min(size, maxVertexBufferSize);
                    }
                    else if (size > VertexBufferSizeToMappedSizeThreshold)
                    {
                        // Make sure we have a sane vertex buffer size, since in some cases applications
                        // might set the "end address" of the vertex buffer to the end of the GPU address space,
                        // which would result in a several GBs large buffer.

                        size = _channel.MemoryManager.GetMappedSize(address, size);
                    }
                }
                else
                {
                    // For non-indexed draws, we can guess the size from the vertex count
                    // and stride.

                    int firstInstance = (int)_state.State.FirstInstance;

                    size = Math.Min(vbSize, (ulong)((firstInstance + drawFirstVertex + drawVertexCount) * stride));
                }

                _pipeline.VertexBuffers[index] = new BufferPipelineDescriptor(_channel.MemoryManager.IsMapped(address), stride, divisor);
                _channel.BufferManager.SetVertexBuffer(index, address, size, stride, divisor);
            }

            if (_vbEnableMask != vbEnableMask)
            {
                _vbEnableMask = vbEnableMask;
                UpdateVertexAttribState();
            }
        }

        /// <summary>
        /// Updates host face culling and orientation based on guest GPU state.
        /// </summary>
        private void UpdateFaceState()
        {
            var yControl = _state.State.YControl;
            var face = _state.State.FaceState;

            _pipeline.CullEnable = face.CullEnable;
            _pipeline.CullMode = face.CullFace;
            _context.Renderer.Pipeline.SetFaceCulling(face.CullEnable, face.CullFace);

            UpdateFrontFace(yControl, face.FrontFace);
        }

        /// <summary>
        /// Updates the front face based on the current front face and the origin.
        /// </summary>
        /// <param name="yControl">Y control register value, where the origin is located</param>
        /// <param name="frontFace">Front face</param>
        private void UpdateFrontFace(YControl yControl, FrontFace frontFace)
        {
            bool isUpperLeftOrigin = !yControl.HasFlag(YControl.TriangleRastFlip);

            if (isUpperLeftOrigin)
            {
                frontFace = frontFace == FrontFace.CounterClockwise ? FrontFace.Clockwise : FrontFace.CounterClockwise;
            }

            _pipeline.FrontFace = frontFace;
            _context.Renderer.Pipeline.SetFrontFace(frontFace);
        }

        /// <summary>
        /// Updates host render target color masks, based on guest GPU state.
        /// This defines which color channels are written to each color buffer.
        /// </summary>
        private void UpdateRtColorMask()
        {
            bool rtColorMaskShared = _state.State.RtColorMaskShared;

            Span<uint> componentMasks = stackalloc uint[Constants.TotalRenderTargets];

            for (int index = 0; index < Constants.TotalRenderTargets; index++)
            {
                var colorMask = _state.State.RtColorMask[rtColorMaskShared ? 0 : index];

                uint componentMask;

                componentMask = (colorMask.UnpackRed() ? 1u : 0u);
                componentMask |= (colorMask.UnpackGreen() ? 2u : 0u);
                componentMask |= (colorMask.UnpackBlue() ? 4u : 0u);
                componentMask |= (colorMask.UnpackAlpha() ? 8u : 0u);

                componentMasks[index] = componentMask;
                _pipeline.ColorWriteMask[index] = componentMask;
            }

            _context.Renderer.Pipeline.SetRenderTargetColorMasks(componentMasks);
        }

        /// <summary>
        /// Updates host render target color buffer blending state, based on guest state.
        /// </summary>
        private void UpdateBlendState()
        {
            if (_state.State.BlendUcodeEnable != BlendUcodeEnable.Disabled)
            {
                if (_context.Capabilities.SupportsBlendEquationAdvanced && _blendManager.TryGetAdvancedBlend(out var blendDescriptor))
                {
                    // Try to HLE it using advanced blend on the host if we can.
                    _context.Renderer.Pipeline.SetBlendState(blendDescriptor);
                    return;
                }
                else
                {
                    // TODO: Blend emulation fallback.
                }
            }

            bool blendIndependent = _state.State.BlendIndependent;
            ColorF blendConstant = _state.State.BlendConstant;

            bool dualSourceBlendEnabled = false;

            if (blendIndependent)
            {
                for (int index = 0; index < Constants.TotalRenderTargets; index++)
                {
                    bool enable = _state.State.BlendEnable[index];
                    var blend = _state.State.BlendState[index];

                    var descriptor = new BlendDescriptor(
                        enable,
                        blendConstant,
                        blend.ColorOp,
                        FilterBlendFactor(blend.ColorSrcFactor, index),
                        FilterBlendFactor(blend.ColorDstFactor, index),
                        blend.AlphaOp,
                        FilterBlendFactor(blend.AlphaSrcFactor, index),
                        FilterBlendFactor(blend.AlphaDstFactor, index));

                    if (enable &&
                        (blend.ColorSrcFactor.IsDualSource() ||
                        blend.ColorDstFactor.IsDualSource() ||
                        blend.AlphaSrcFactor.IsDualSource() ||
                        blend.AlphaDstFactor.IsDualSource()))
                    {
                        dualSourceBlendEnabled = true;
                    }

                    _pipeline.BlendDescriptors[index] = descriptor;
                    _context.Renderer.Pipeline.SetBlendState(index, descriptor);
                }
            }
            else
            {
                bool enable = _state.State.BlendEnable[0];
                var blend = _state.State.BlendStateCommon;

                var descriptor = new BlendDescriptor(
                    enable,
                    blendConstant,
                    blend.ColorOp,
                    FilterBlendFactor(blend.ColorSrcFactor, 0),
                    FilterBlendFactor(blend.ColorDstFactor, 0),
                    blend.AlphaOp,
                    FilterBlendFactor(blend.AlphaSrcFactor, 0),
                    FilterBlendFactor(blend.AlphaDstFactor, 0));

                if (enable &&
                    (blend.ColorSrcFactor.IsDualSource() ||
                    blend.ColorDstFactor.IsDualSource() ||
                    blend.AlphaSrcFactor.IsDualSource() ||
                    blend.AlphaDstFactor.IsDualSource()))
                {
                    dualSourceBlendEnabled = true;
                }

                for (int index = 0; index < Constants.TotalRenderTargets; index++)
                {
                    _pipeline.BlendDescriptors[index] = descriptor;
                    _context.Renderer.Pipeline.SetBlendState(index, descriptor);
                }
            }

            _currentSpecState.SetDualSourceBlendEnabled(dualSourceBlendEnabled);
        }

        /// <summary>
        /// Gets a blend factor for the color target currently.
        /// This will return <paramref name="factor"/> unless the target format has no alpha component,
        /// in which case it will replace destination alpha factor with a constant factor of one or zero.
        /// </summary>
        /// <param name="factor">Input factor</param>
        /// <param name="index">Color target index</param>
        /// <returns>New blend factor</returns>
        private BlendFactor FilterBlendFactor(BlendFactor factor, int index)
        {
            // If any color target format without alpha is being used, we need to make sure that
            // if blend is active, it will not use destination alpha as a factor.
            // That is required because RGBX formats are emulated using host RGBA formats.

            if (_state.State.RtColorState[index].Format.NoAlpha())
            {
                switch (factor)
                {
                    case BlendFactor.DstAlpha:
                    case BlendFactor.DstAlphaGl:
                        factor = BlendFactor.One;
                        break;
                    case BlendFactor.OneMinusDstAlpha:
                    case BlendFactor.OneMinusDstAlphaGl:
                        factor = BlendFactor.Zero;
                        break;
                }
            }

            return factor;
        }

        /// <summary>
        /// Updates host logical operation state, based on guest state.
        /// </summary>
        private void UpdateLogicOpState()
        {
            LogicalOpState logicOpState = _state.State.LogicOpState;

            _pipeline.SetLogicOpState(logicOpState.Enable, logicOpState.LogicalOp);
            _context.Renderer.Pipeline.SetLogicOpState(logicOpState.Enable, logicOpState.LogicalOp);
        }

        /// <summary>
        /// Updates multisample state, based on guest state.
        /// </summary>
        private void UpdateMultisampleState()
        {
            bool alphaToCoverageEnable = (_state.State.MultisampleControl & 1) != 0;
            bool alphaToOneEnable = (_state.State.MultisampleControl & 0x10) != 0;

            _context.Renderer.Pipeline.SetMultisampleState(new MultisampleDescriptor(
                alphaToCoverageEnable,
                _state.State.AlphaToCoverageDitherEnable,
                alphaToOneEnable));

            _currentSpecState.SetAlphaToCoverageEnable(alphaToCoverageEnable, _state.State.AlphaToCoverageDitherEnable);
        }

        /// <summary>
        /// Updates the early z flag, based on guest state.
        /// </summary>
        private void UpdateEarlyZState()
        {
            _currentSpecState.SetEarlyZForce(_state.State.EarlyZForce);
        }

        /// <summary>
        /// Updates host shaders based on the guest GPU state.
        /// </summary>
        private void UpdateShaderState()
        {
            var shaderCache = _channel.MemoryManager.Physical.ShaderCache;

            _vtgWritesRtLayer = false;

            ShaderAddresses addresses = new();
            Span<ulong> addressesSpan = addresses.AsSpan();

            ulong baseAddress = _state.State.ShaderBaseAddress.Pack();

            for (int index = 0; index < 6; index++)
            {
                var shader = _state.State.ShaderState[index];
                if (!shader.UnpackEnable() && index != 1)
                {
                    continue;
                }

                addressesSpan[index] = baseAddress + shader.Offset;
            }

            CachedShaderProgram gs = shaderCache.GetGraphicsShader(ref _state.State, ref _pipeline, _channel, ref _currentSpecState.GetPoolState(), ref _currentSpecState.GetGraphicsState(), addresses);

            // Consume the modified flag for spec state so that it isn't checked again.
            _currentSpecState.SetShader(gs);

            _shaderSpecState = gs.SpecializationState;

            byte oldVsClipDistancesWritten = _vsClipDistancesWritten;

            _drawState.VsUsesInstanceId = gs.Shaders[1]?.Info.UsesInstanceId ?? false;
            _vsUsesDrawParameters = gs.Shaders[1]?.Info.UsesDrawParameters ?? false;
            _vsClipDistancesWritten = gs.Shaders[1]?.Info.ClipDistancesWritten ?? 0;

            bool hasTransformFeedback = gs.SpecializationState.TransformFeedbackDescriptors != null;
            if (hasTransformFeedback != _channel.BufferManager.HasTransformFeedbackOutputs)
            {
                if (!_context.Capabilities.SupportsTransformFeedback)
                {
                    // If host does not support transform feedback, and the shader changed,
                    // we might need to update bindings as transform feedback emulation
                    // uses storage buffer bindings that might have been used for something
                    // else in a previous draw.

                    _channel.BufferManager.ForceTransformFeedbackAndStorageBuffersDirty();
                }

                _channel.BufferManager.HasTransformFeedbackOutputs = hasTransformFeedback;
            }

            if (oldVsClipDistancesWritten != _vsClipDistancesWritten)
            {
                UpdateUserClipState();
            }

            UpdateShaderBindings(gs.Bindings);

            for (int stageIndex = 0; stageIndex < Constants.ShaderStages; stageIndex++)
            {
                ShaderProgramInfo info = gs.Shaders[stageIndex + 1]?.Info;

                if (info?.UsesRtLayer == true)
                {
                    _vtgWritesRtLayer = true;
                }

                _currentProgramInfo[stageIndex] = info;
            }

            if (gs.Shaders[5]?.Info.UsesFragCoord == true)
            {
                // Make sure we update the viewport size on the support buffer if it will be consumed on the new shader.

                if (!_fsReadsFragCoord && _state.State.YControl.HasFlag(YControl.NegateY))
                {
                    UpdateSupportBufferViewportSize();
                }

                _fsReadsFragCoord = true;
            }
            else
            {
                _fsReadsFragCoord = false;
            }

            if (gs.VertexAsCompute != null)
            {
                _drawState.VertexAsCompute = gs.VertexAsCompute;
                _drawState.GeometryAsCompute = gs.GeometryAsCompute;
                _drawState.VertexPassthrough = gs.HostProgram;
            }
            else
            {
                _drawState.VertexAsCompute = null;
                _drawState.GeometryAsCompute = null;
                _drawState.VertexPassthrough = null;
            }

            _context.Renderer.Pipeline.SetProgram(gs.HostProgram);
        }

        /// <summary>
        /// Updates the viewport size on the support buffer for fragment shader access.
        /// </summary>
        private void UpdateSupportBufferViewportSize()
        {
            ref var transform = ref _state.State.ViewportTransform[0];

            float scaleX = MathF.Abs(transform.ScaleX);
            float scaleY = transform.ScaleY;

            float width = scaleX * 2;
            float height = scaleY * 2;

            _context.SupportBufferUpdater.SetViewportSize(width, MathF.Abs(height));
        }

        /// <summary>
        /// Updates bindings consumed by the shader on the texture and buffer managers.
        /// </summary>
        /// <param name="bindings">Bindings for the active shader</param>
        private void UpdateShaderBindings(CachedShaderBindings bindings)
        {
            _channel.TextureManager.SetGraphicsBindings(bindings);
            _channel.BufferManager.SetGraphicsBufferBindings(bindings);
        }

        /// <summary>
        /// Gets the current texture pool state.
        /// </summary>
        /// <returns>Texture pool state</returns>
        private GpuChannelPoolState GetPoolState()
        {
            return new GpuChannelPoolState(
                _state.State.TexturePoolState.Address.Pack(),
                _state.State.TexturePoolState.MaximumId,
                (int)_state.State.TextureBufferIndex);
        }

        /// <summary>
        /// Gets the depth mode that is currently being used (zero to one or minus one to one).
        /// </summary>
        /// <returns>Current depth mode</returns>
        private DepthMode GetDepthMode()
        {
            ref var transform = ref _state.State.ViewportTransform[0];
            ref var extents = ref _state.State.ViewportExtents[0];

            DepthMode depthMode;

            if (!float.IsInfinity(extents.DepthNear) &&
                !float.IsInfinity(extents.DepthFar) &&
                (extents.DepthFar - extents.DepthNear) != 0)
            {
                // Try to guess the depth mode being used on the high level API
                // based on current transform.
                // It is setup like so by said APIs:
                // If depth mode is ZeroToOne:
                //  TranslateZ = Near
                //  ScaleZ = Far - Near
                // If depth mode is MinusOneToOne:
                //  TranslateZ = (Near + Far) / 2
                //  ScaleZ = (Far - Near) / 2
                // DepthNear/Far are sorted such as that Near is always less than Far.
                depthMode = extents.DepthNear != transform.TranslateZ &&
                            extents.DepthFar != transform.TranslateZ
                    ? DepthMode.MinusOneToOne
                    : DepthMode.ZeroToOne;
            }
            else
            {
                // If we can't guess from the viewport transform, then just use the depth mode register.
                depthMode = (DepthMode)(_state.State.DepthMode & 1);
            }

            return depthMode;
        }

        /// <summary>
        /// Forces the shaders to be rebound on the next draw.
        /// </summary>
        public void ForceShaderUpdate()
        {
            _updateTracker.ForceDirty(ShaderStateIndex);
        }

        /// <summary>
        /// Forces a register group as dirty, by index.
        /// </summary>
        /// <param name="groupIndex">Index of the group to be dirtied</param>
        public void ForceDirty(int groupIndex)
        {
            _updateTracker.ForceDirty(groupIndex);
        }
    }
}
