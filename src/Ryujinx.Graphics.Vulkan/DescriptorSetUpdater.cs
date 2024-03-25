using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Silk.NET.Vulkan;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CompareOp = Ryujinx.Graphics.GAL.CompareOp;
using Format = Ryujinx.Graphics.GAL.Format;
using SamplerCreateInfo = Ryujinx.Graphics.GAL.SamplerCreateInfo;

namespace Ryujinx.Graphics.Vulkan
{
    class DescriptorSetUpdater
    {
        private const ulong StorageBufferMaxMirrorable = 0x2000;

        private const int ArrayGrowthSize = 16;

        private record struct BufferRef
        {
            public Auto<DisposableBuffer> Buffer;
            public int Offset;
            public bool Write;

            public BufferRef(Auto<DisposableBuffer> buffer)
            {
                Buffer = buffer;
                Offset = 0;
                Write = true;
            }

            public BufferRef(Auto<DisposableBuffer> buffer, ref BufferRange range)
            {
                Buffer = buffer;
                Offset = range.Offset;
                Write = range.Write;
            }
        }

        private record struct TextureRef
        {
            public ShaderStage Stage;
            public TextureStorage Storage;
            public Auto<DisposableImageView> View;
            public Auto<DisposableSampler> Sampler;

            public TextureRef(ShaderStage stage, TextureStorage storage, Auto<DisposableImageView> view, Auto<DisposableSampler> sampler)
            {
                Stage = stage;
                Storage = storage;
                View = view;
                Sampler = sampler;
            }
        }

        private record struct ImageRef
        {
            public ShaderStage Stage;
            public TextureStorage Storage;
            public Auto<DisposableImageView> View;

            public ImageRef(ShaderStage stage, TextureStorage storage, Auto<DisposableImageView> view)
            {
                Stage = stage;
                Storage = storage;
                View = view;
            }
        }

        private record struct ArrayRef<T>
        {
            public ShaderStage Stage;
            public T Array;

            public ArrayRef(ShaderStage stage, T array)
            {
                Stage = stage;
                Array = array;
            }
        }

        private readonly VulkanRenderer _gd;
        private readonly Device _device;
        private readonly PipelineBase _pipeline;
        private ShaderCollection _program;

        private readonly BufferRef[] _uniformBufferRefs;
        private readonly BufferRef[] _storageBufferRefs;
        private readonly TextureRef[] _textureRefs;
        private readonly ImageRef[] _imageRefs;
        private readonly TextureBuffer[] _bufferTextureRefs;
        private readonly TextureBuffer[] _bufferImageRefs;
        private readonly Format[] _bufferImageFormats;

        private ArrayRef<TextureArray>[] _textureArrayRefs;
        private ArrayRef<ImageArray>[] _imageArrayRefs;

        private readonly DescriptorBufferInfo[] _uniformBuffers;
        private readonly DescriptorBufferInfo[] _storageBuffers;
        private readonly DescriptorImageInfo[] _textures;
        private readonly DescriptorImageInfo[] _images;
        private readonly BufferView[] _bufferTextures;
        private readonly BufferView[] _bufferImages;

        private readonly DescriptorSetTemplateUpdater _templateUpdater;

        private BitMapStruct<Array2<long>> _uniformSet;
        private BitMapStruct<Array2<long>> _storageSet;
        private BitMapStruct<Array2<long>> _uniformMirrored;
        private BitMapStruct<Array2<long>> _storageMirrored;
        private readonly int[] _uniformSetPd;
        private int _pdSequence = 1;

        private bool _updateDescriptorCacheCbIndex;

        [Flags]
        private enum DirtyFlags
        {
            None = 0,
            Uniform = 1 << 0,
            Storage = 1 << 1,
            Texture = 1 << 2,
            Image = 1 << 3,
            All = Uniform | Storage | Texture | Image,
        }

        private DirtyFlags _dirty;

        private readonly BufferHolder _dummyBuffer;
        private readonly TextureView _dummyTexture;
        private readonly SamplerHolder _dummySampler;

        public DescriptorSetUpdater(VulkanRenderer gd, Device device, PipelineBase pipeline)
        {
            _gd = gd;
            _device = device;
            _pipeline = pipeline;

            // Some of the bindings counts needs to be multiplied by 2 because we have buffer and
            // regular textures/images interleaved on the same descriptor set.

            _uniformBufferRefs = new BufferRef[Constants.MaxUniformBufferBindings];
            _storageBufferRefs = new BufferRef[Constants.MaxStorageBufferBindings];
            _textureRefs = new TextureRef[Constants.MaxTextureBindings * 2];
            _imageRefs = new ImageRef[Constants.MaxImageBindings * 2];
            _bufferTextureRefs = new TextureBuffer[Constants.MaxTextureBindings * 2];
            _bufferImageRefs = new TextureBuffer[Constants.MaxImageBindings * 2];
            _bufferImageFormats = new Format[Constants.MaxImageBindings * 2];

            _textureArrayRefs = Array.Empty<ArrayRef<TextureArray>>();
            _imageArrayRefs = Array.Empty<ArrayRef<ImageArray>>();

            _uniformBuffers = new DescriptorBufferInfo[Constants.MaxUniformBufferBindings];
            _storageBuffers = new DescriptorBufferInfo[Constants.MaxStorageBufferBindings];
            _textures = new DescriptorImageInfo[Constants.MaxTexturesPerStage];
            _images = new DescriptorImageInfo[Constants.MaxImagesPerStage];
            _bufferTextures = new BufferView[Constants.MaxTexturesPerStage];
            _bufferImages = new BufferView[Constants.MaxImagesPerStage];

            _uniformSetPd = new int[Constants.MaxUniformBufferBindings];

            var initialImageInfo = new DescriptorImageInfo
            {
                ImageLayout = ImageLayout.General,
            };

            _textures.AsSpan().Fill(initialImageInfo);
            _images.AsSpan().Fill(initialImageInfo);

            if (gd.Capabilities.SupportsNullDescriptors)
            {
                // If null descriptors are supported, we can pass null as the handle.
                _dummyBuffer = null;
            }
            else
            {
                // If null descriptors are not supported, we need to pass the handle of a dummy buffer on unused bindings.
                _dummyBuffer = gd.BufferManager.Create(gd, 0x10000, forConditionalRendering: false, baseType: BufferAllocationType.DeviceLocal);
            }

            _dummyTexture = gd.CreateTextureView(new TextureCreateInfo(
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                4,
                Format.R8G8B8A8Unorm,
                DepthStencilMode.Depth,
                Target.Texture2D,
                SwizzleComponent.Red,
                SwizzleComponent.Green,
                SwizzleComponent.Blue,
                SwizzleComponent.Alpha));

            _dummySampler = (SamplerHolder)gd.CreateSampler(new SamplerCreateInfo(
                MinFilter.Nearest,
                MagFilter.Nearest,
                false,
                AddressMode.Repeat,
                AddressMode.Repeat,
                AddressMode.Repeat,
                CompareMode.None,
                CompareOp.Always,
                new ColorF(0, 0, 0, 0),
                0,
                0,
                0,
                1f));

            _templateUpdater = new();
        }

        public void Initialize()
        {
            Span<byte> dummyTextureData = stackalloc byte[4];
            _dummyTexture.SetData(dummyTextureData);
        }

        private static bool BindingOverlaps(ref DescriptorBufferInfo info, int bindingOffset, int offset, int size)
        {
            return offset < bindingOffset + (int)info.Range && (offset + size) > bindingOffset;
        }

        internal void Rebind(Auto<DisposableBuffer> buffer, int offset, int size)
        {
            if (_program == null)
            {
                return;
            }

            // Check stage bindings

            _uniformMirrored.Union(_uniformSet).SignalSet((int binding, int count) =>
            {
                for (int i = 0; i < count; i++)
                {
                    ref BufferRef bufferRef = ref _uniformBufferRefs[binding];
                    if (bufferRef.Buffer == buffer)
                    {
                        ref DescriptorBufferInfo info = ref _uniformBuffers[binding];
                        int bindingOffset = bufferRef.Offset;

                        if (BindingOverlaps(ref info, bindingOffset, offset, size))
                        {
                            _uniformSet.Clear(binding);
                            _uniformSetPd[binding] = 0;
                            SignalDirty(DirtyFlags.Uniform);
                        }
                    }

                    binding++;
                }
            });

            _storageMirrored.Union(_storageSet).SignalSet((int binding, int count) =>
            {
                for (int i = 0; i < count; i++)
                {
                    ref BufferRef bufferRef = ref _storageBufferRefs[binding];
                    if (bufferRef.Buffer == buffer)
                    {
                        ref DescriptorBufferInfo info = ref _storageBuffers[binding];
                        int bindingOffset = bufferRef.Offset;

                        if (BindingOverlaps(ref info, bindingOffset, offset, size))
                        {
                            _storageSet.Clear(binding);
                            SignalDirty(DirtyFlags.Storage);
                        }
                    }

                    binding++;
                }
            });
        }

        public void InsertBindingBarriers(CommandBufferScoped cbs)
        {
            foreach (ResourceBindingSegment segment in _program.BindingSegments[PipelineBase.TextureSetIndex])
            {
                if (segment.Type == ResourceType.TextureAndSampler)
                {
                    if (!segment.IsArray)
                    {
                        for (int i = 0; i < segment.Count; i++)
                        {
                            ref var texture = ref _textureRefs[segment.Binding + i];
                            texture.Storage?.QueueWriteToReadBarrier(cbs, AccessFlags.ShaderReadBit, texture.Stage.ConvertToPipelineStageFlags());
                        }
                    }
                    else
                    {
                        PipelineStageFlags stageFlags = _textureArrayRefs[segment.Binding].Stage.ConvertToPipelineStageFlags();
                        _textureArrayRefs[segment.Binding].Array?.QueueWriteToReadBarriers(cbs, stageFlags);
                    }
                }
            }

            foreach (ResourceBindingSegment segment in _program.BindingSegments[PipelineBase.ImageSetIndex])
            {
                if (segment.Type == ResourceType.Image)
                {
                    if (!segment.IsArray)
                    {
                        for (int i = 0; i < segment.Count; i++)
                        {
                            ref var image = ref _imageRefs[segment.Binding + i];
                            image.Storage?.QueueWriteToReadBarrier(cbs, AccessFlags.ShaderReadBit, image.Stage.ConvertToPipelineStageFlags());
                        }
                    }
                    else
                    {
                        PipelineStageFlags stageFlags = _imageArrayRefs[segment.Binding].Stage.ConvertToPipelineStageFlags();
                        _imageArrayRefs[segment.Binding].Array?.QueueWriteToReadBarriers(cbs, stageFlags);
                    }
                }
            }
        }

        public void AdvancePdSequence()
        {
            if (++_pdSequence == 0)
            {
                _pdSequence = 1;
            }
        }

        public void SetProgram(CommandBufferScoped cbs, ShaderCollection program, bool isBound)
        {
            if (!program.HasSameLayout(_program))
            {
                // When the pipeline layout changes, push descriptor bindings are invalidated.

                AdvancePdSequence();
            }

            _program = program;
            _updateDescriptorCacheCbIndex = true;
            _dirty = DirtyFlags.All;
        }

        public void SetImage(
            CommandBufferScoped cbs,
            ShaderStage stage,
            int binding,
            ITexture image,
            Format imageFormat)
        {
            if (image is TextureBuffer imageBuffer)
            {
                _bufferImageRefs[binding] = imageBuffer;
                _bufferImageFormats[binding] = imageFormat;
            }
            else if (image is TextureView view)
            {
                view.Storage.QueueWriteToReadBarrier(cbs, AccessFlags.ShaderReadBit, stage.ConvertToPipelineStageFlags());

                _imageRefs[binding] = new(stage, view.Storage, view.GetView(imageFormat).GetIdentityImageView());
            }
            else
            {
                _imageRefs[binding] = default;
                _bufferImageRefs[binding] = null;
                _bufferImageFormats[binding] = default;
            }

            SignalDirty(DirtyFlags.Image);
        }

        public void SetImage(int binding, Auto<DisposableImageView> image)
        {
            _imageRefs[binding] = new(ShaderStage.Compute, null, image);

            SignalDirty(DirtyFlags.Image);
        }

        public void SetStorageBuffers(CommandBuffer commandBuffer, ReadOnlySpan<BufferAssignment> buffers)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                var assignment = buffers[i];
                var buffer = assignment.Range;
                int index = assignment.Binding;

                Auto<DisposableBuffer> vkBuffer = buffer.Handle == BufferHandle.Null
                    ? null
                    : _gd.BufferManager.GetBuffer(commandBuffer, buffer.Handle, buffer.Write, isSSBO: true);

                ref BufferRef currentBufferRef = ref _storageBufferRefs[index];

                DescriptorBufferInfo info = new()
                {
                    Offset = (ulong)buffer.Offset,
                    Range = (ulong)buffer.Size,
                };

                var newRef = new BufferRef(vkBuffer, ref buffer);

                ref DescriptorBufferInfo currentInfo = ref _storageBuffers[index];

                if (!currentBufferRef.Equals(newRef) || currentInfo.Range != info.Range)
                {
                    _storageSet.Clear(index);

                    currentInfo = info;
                    currentBufferRef = newRef;
                }
            }

            SignalDirty(DirtyFlags.Storage);
        }

        public void SetStorageBuffers(CommandBuffer commandBuffer, int first, ReadOnlySpan<Auto<DisposableBuffer>> buffers)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                var vkBuffer = buffers[i];
                int index = first + i;

                ref BufferRef currentBufferRef = ref _storageBufferRefs[index];

                DescriptorBufferInfo info = new()
                {
                    Offset = 0,
                    Range = Vk.WholeSize,
                };

                BufferRef newRef = new(vkBuffer);

                ref DescriptorBufferInfo currentInfo = ref _storageBuffers[index];

                if (!currentBufferRef.Equals(newRef) || currentInfo.Range != info.Range)
                {
                    _storageSet.Clear(index);

                    currentInfo = info;
                    currentBufferRef = newRef;
                }
            }

            SignalDirty(DirtyFlags.Storage);
        }

        public void SetTextureAndSampler(
            CommandBufferScoped cbs,
            ShaderStage stage,
            int binding,
            ITexture texture,
            ISampler sampler)
        {
            if (texture is TextureBuffer textureBuffer)
            {
                _bufferTextureRefs[binding] = textureBuffer;
            }
            else if (texture is TextureView view)
            {
                view.Storage.QueueWriteToReadBarrier(cbs, AccessFlags.ShaderReadBit, stage.ConvertToPipelineStageFlags());

                _textureRefs[binding] = new(stage, view.Storage, view.GetImageView(), ((SamplerHolder)sampler)?.GetSampler());
            }
            else
            {
                _textureRefs[binding] = default;
                _bufferTextureRefs[binding] = null;
            }

            SignalDirty(DirtyFlags.Texture);
        }

        public void SetTextureAndSamplerIdentitySwizzle(
            CommandBufferScoped cbs,
            ShaderStage stage,
            int binding,
            ITexture texture,
            ISampler sampler)
        {
            if (texture is TextureView view)
            {
                view.Storage.QueueWriteToReadBarrier(cbs, AccessFlags.ShaderReadBit, stage.ConvertToPipelineStageFlags());

                _textureRefs[binding] = new(stage, view.Storage, view.GetIdentityImageView(), ((SamplerHolder)sampler)?.GetSampler());

                SignalDirty(DirtyFlags.Texture);
            }
            else
            {
                SetTextureAndSampler(cbs, stage, binding, texture, sampler);
            }
        }

        public void SetTextureArray(CommandBufferScoped cbs, ShaderStage stage, int binding, ITextureArray array)
        {
            if (_textureArrayRefs.Length <= binding)
            {
                Array.Resize(ref _textureArrayRefs, binding + ArrayGrowthSize);
            }

            if (_textureArrayRefs[binding].Stage != stage || _textureArrayRefs[binding].Array != array)
            {
                _textureArrayRefs[binding] = new ArrayRef<TextureArray>(stage, array as TextureArray);
                _textureArrayRefs[binding].Array?.QueueWriteToReadBarriers(cbs, stage.ConvertToPipelineStageFlags());

                SignalDirty(DirtyFlags.Texture);
            }
        }

        public void SetImageArray(CommandBufferScoped cbs, ShaderStage stage, int binding, IImageArray array)
        {
            if (_imageArrayRefs.Length <= binding)
            {
                Array.Resize(ref _imageArrayRefs, binding + ArrayGrowthSize);
            }

            if (_imageArrayRefs[binding].Stage != stage || _imageArrayRefs[binding].Array != array)
            {
                _imageArrayRefs[binding] = new ArrayRef<ImageArray>(stage, array as ImageArray);
                _imageArrayRefs[binding].Array?.QueueWriteToReadBarriers(cbs, stage.ConvertToPipelineStageFlags());

                SignalDirty(DirtyFlags.Image);
            }
        }

        public void SetUniformBuffers(CommandBuffer commandBuffer, ReadOnlySpan<BufferAssignment> buffers)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                var assignment = buffers[i];
                var buffer = assignment.Range;
                int index = assignment.Binding;

                Auto<DisposableBuffer> vkBuffer = buffer.Handle == BufferHandle.Null
                    ? null
                    : _gd.BufferManager.GetBuffer(commandBuffer, buffer.Handle, false);

                ref BufferRef currentBufferRef = ref _uniformBufferRefs[index];

                DescriptorBufferInfo info = new()
                {
                    Offset = (ulong)buffer.Offset,
                    Range = (ulong)buffer.Size,
                };

                BufferRef newRef = new(vkBuffer, ref buffer);

                ref DescriptorBufferInfo currentInfo = ref _uniformBuffers[index];

                if (!currentBufferRef.Equals(newRef) || currentInfo.Range != info.Range)
                {
                    _uniformSet.Clear(index);
                    _uniformSetPd[index] = 0;

                    currentInfo = info;
                    currentBufferRef = newRef;
                }
            }

            SignalDirty(DirtyFlags.Uniform);
        }

        private void SignalDirty(DirtyFlags flag)
        {
            _dirty |= flag;
        }

        public void UpdateAndBindDescriptorSets(CommandBufferScoped cbs, PipelineBindPoint pbp)
        {
            if ((_dirty & DirtyFlags.All) == 0)
            {
                return;
            }

            if (_dirty.HasFlag(DirtyFlags.Uniform))
            {
                if (_program.UsePushDescriptors)
                {
                    UpdateAndBindUniformBufferPd(cbs, pbp);
                }
                else
                {
                    UpdateAndBind(cbs, PipelineBase.UniformSetIndex, pbp);
                }
            }

            if (_dirty.HasFlag(DirtyFlags.Storage))
            {
                UpdateAndBind(cbs, PipelineBase.StorageSetIndex, pbp);
            }

            if (_dirty.HasFlag(DirtyFlags.Texture))
            {
                UpdateAndBind(cbs, PipelineBase.TextureSetIndex, pbp);
            }

            if (_dirty.HasFlag(DirtyFlags.Image))
            {
                UpdateAndBind(cbs, PipelineBase.ImageSetIndex, pbp);
            }

            _dirty = DirtyFlags.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool UpdateBuffer(
            CommandBufferScoped cbs,
            ref DescriptorBufferInfo info,
            ref BufferRef buffer,
            Auto<DisposableBuffer> dummyBuffer,
            bool mirrorable)
        {
            int offset = buffer.Offset;
            bool mirrored = false;

            if (mirrorable)
            {
                info.Buffer = buffer.Buffer?.GetMirrorable(cbs, ref offset, (int)info.Range, out mirrored).Value ?? default;
            }
            else
            {
                info.Buffer = buffer.Buffer?.Get(cbs, offset, (int)info.Range, buffer.Write).Value ?? default;
            }

            info.Offset = (ulong)offset;

            // The spec requires that buffers with null handle have offset as 0 and range as VK_WHOLE_SIZE.
            if (info.Buffer.Handle == 0)
            {
                info.Buffer = dummyBuffer?.Get(cbs).Value ?? default;
                info.Offset = 0;
                info.Range = Vk.WholeSize;
            }

            return mirrored;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateAndBind(CommandBufferScoped cbs, int setIndex, PipelineBindPoint pbp)
        {
            var program = _program;
            var bindingSegments = program.BindingSegments[setIndex];

            if (bindingSegments.Length == 0)
            {
                return;
            }

            var dummyBuffer = _dummyBuffer?.GetBuffer();

            if (_updateDescriptorCacheCbIndex)
            {
                _updateDescriptorCacheCbIndex = false;
                program.UpdateDescriptorCacheCommandBufferIndex(cbs.CommandBufferIndex);
            }

            var dsc = program.GetNewDescriptorSetCollection(setIndex, out var isNew).Get(cbs);

            if (!program.HasMinimalLayout)
            {
                if (isNew)
                {
                    Initialize(cbs, setIndex, dsc);
                }
            }

            DescriptorSetTemplate template = program.Templates[setIndex];

            DescriptorSetTemplateWriter tu = _templateUpdater.Begin(template);

            foreach (ResourceBindingSegment segment in bindingSegments)
            {
                int binding = segment.Binding;
                int count = segment.Count;

                if (setIndex == PipelineBase.UniformSetIndex)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int index = binding + i;

                        if (_uniformSet.Set(index))
                        {
                            ref BufferRef buffer = ref _uniformBufferRefs[index];

                            bool mirrored = UpdateBuffer(cbs, ref _uniformBuffers[index], ref buffer, dummyBuffer, true);

                            _uniformMirrored.Set(index, mirrored);
                        }
                    }

                    ReadOnlySpan<DescriptorBufferInfo> uniformBuffers = _uniformBuffers;

                    tu.Push(uniformBuffers.Slice(binding, count));
                }
                else if (setIndex == PipelineBase.StorageSetIndex)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int index = binding + i;

                        ref BufferRef buffer = ref _storageBufferRefs[index];

                        if (_storageSet.Set(index))
                        {
                            ref var info = ref _storageBuffers[index];

                            bool mirrored = UpdateBuffer(cbs,
                                ref info,
                                ref _storageBufferRefs[index],
                                dummyBuffer,
                                !buffer.Write && info.Range <= StorageBufferMaxMirrorable);

                            _storageMirrored.Set(index, mirrored);
                        }
                    }

                    ReadOnlySpan<DescriptorBufferInfo> storageBuffers = _storageBuffers;

                    tu.Push(storageBuffers.Slice(binding, count));
                }
                else if (setIndex == PipelineBase.TextureSetIndex)
                {
                    if (!segment.IsArray)
                    {
                        if (segment.Type != ResourceType.BufferTexture)
                        {
                            Span<DescriptorImageInfo> textures = _textures;

                            for (int i = 0; i < count; i++)
                            {
                                ref var texture = ref textures[i];
                                ref var refs = ref _textureRefs[binding + i];

                                texture.ImageView = refs.View?.Get(cbs).Value ?? default;
                                texture.Sampler = refs.Sampler?.Get(cbs).Value ?? default;

                                if (texture.ImageView.Handle == 0)
                                {
                                    texture.ImageView = _dummyTexture.GetImageView().Get(cbs).Value;
                                }

                                if (texture.Sampler.Handle == 0)
                                {
                                    texture.Sampler = _dummySampler.GetSampler().Get(cbs).Value;
                                }
                            }

                            tu.Push<DescriptorImageInfo>(textures[..count]);
                        }
                        else
                        {
                            Span<BufferView> bufferTextures = _bufferTextures;

                            for (int i = 0; i < count; i++)
                            {
                                bufferTextures[i] = _bufferTextureRefs[binding + i]?.GetBufferView(cbs, false) ?? default;
                            }

                            tu.Push<BufferView>(bufferTextures[..count]);
                        }
                    }
                    else
                    {
                        if (segment.Type != ResourceType.BufferTexture)
                        {
                            tu.Push(_textureArrayRefs[binding].Array.GetImageInfos(_gd, cbs, _dummyTexture, _dummySampler));
                        }
                        else
                        {
                            tu.Push(_textureArrayRefs[binding].Array.GetBufferViews(cbs));
                        }
                    }
                }
                else if (setIndex == PipelineBase.ImageSetIndex)
                {
                    if (!segment.IsArray)
                    {
                        if (segment.Type != ResourceType.BufferImage)
                        {
                            Span<DescriptorImageInfo> images = _images;

                            for (int i = 0; i < count; i++)
                            {
                                images[i].ImageView = _imageRefs[binding + i].View?.Get(cbs).Value ?? default;
                            }

                            tu.Push<DescriptorImageInfo>(images[..count]);
                        }
                        else
                        {
                            Span<BufferView> bufferImages = _bufferImages;

                            for (int i = 0; i < count; i++)
                            {
                                bufferImages[i] = _bufferImageRefs[binding + i]?.GetBufferView(cbs, _bufferImageFormats[binding + i], true) ?? default;
                            }

                            tu.Push<BufferView>(bufferImages[..count]);
                        }
                    }
                    else
                    {
                        if (segment.Type != ResourceType.BufferTexture)
                        {
                            tu.Push(_imageArrayRefs[binding].Array.GetImageInfos(_gd, cbs, _dummyTexture));
                        }
                        else
                        {
                            tu.Push(_imageArrayRefs[binding].Array.GetBufferViews(cbs));
                        }
                    }
                }
            }

            var sets = dsc.GetSets();
            _templateUpdater.Commit(_gd, _device, sets[0]);

            _gd.Api.CmdBindDescriptorSets(cbs.CommandBuffer, pbp, _program.PipelineLayout, (uint)setIndex, 1, sets, 0, ReadOnlySpan<uint>.Empty);
        }

        private unsafe void UpdateBuffers(
            CommandBufferScoped cbs,
            PipelineBindPoint pbp,
            int baseBinding,
            ReadOnlySpan<DescriptorBufferInfo> bufferInfo,
            DescriptorType type)
        {
            if (bufferInfo.Length == 0)
            {
                return;
            }

            fixed (DescriptorBufferInfo* pBufferInfo = bufferInfo)
            {
                var writeDescriptorSet = new WriteDescriptorSet
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstBinding = (uint)baseBinding,
                    DescriptorType = type,
                    DescriptorCount = (uint)bufferInfo.Length,
                    PBufferInfo = pBufferInfo,
                };

                _gd.PushDescriptorApi.CmdPushDescriptorSet(cbs.CommandBuffer, pbp, _program.PipelineLayout, 0, 1, &writeDescriptorSet);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateAndBindUniformBufferPd(CommandBufferScoped cbs, PipelineBindPoint pbp)
        {
            int sequence = _pdSequence;
            var bindingSegments = _program.BindingSegments[PipelineBase.UniformSetIndex];
            var dummyBuffer = _dummyBuffer?.GetBuffer();

            long updatedBindings = 0;
            DescriptorSetTemplateWriter writer = _templateUpdater.Begin(32 * Unsafe.SizeOf<DescriptorBufferInfo>());

            foreach (ResourceBindingSegment segment in bindingSegments)
            {
                int binding = segment.Binding;
                int count = segment.Count;

                ReadOnlySpan<DescriptorBufferInfo> uniformBuffers = _uniformBuffers;

                for (int i = 0; i < count; i++)
                {
                    int index = binding + i;

                    if (_uniformSet.Set(index))
                    {
                        ref BufferRef buffer = ref _uniformBufferRefs[index];

                        bool mirrored = UpdateBuffer(cbs, ref _uniformBuffers[index], ref buffer, dummyBuffer, true);

                        _uniformMirrored.Set(index, mirrored);
                    }

                    if (_uniformSetPd[index] != sequence)
                    {
                        // Need to set this push descriptor (even if the buffer binding has not changed)

                        _uniformSetPd[index] = sequence;
                        updatedBindings |= 1L << index;

                        writer.Push(MemoryMarshal.CreateReadOnlySpan(ref _uniformBuffers[index], 1));
                    }
                }
            }

            if (updatedBindings > 0)
            {
                DescriptorSetTemplate template = _program.GetPushDescriptorTemplate(updatedBindings);
                _templateUpdater.CommitPushDescriptor(_gd, cbs, template, _program.PipelineLayout);
            }
        }

        private void Initialize(CommandBufferScoped cbs, int setIndex, DescriptorSetCollection dsc)
        {
            // We don't support clearing texture descriptors currently.
            if (setIndex != PipelineBase.UniformSetIndex && setIndex != PipelineBase.StorageSetIndex)
            {
                return;
            }

            var dummyBuffer = _dummyBuffer?.GetBuffer().Get(cbs).Value ?? default;

            foreach (ResourceBindingSegment segment in _program.ClearSegments[setIndex])
            {
                dsc.InitializeBuffers(0, segment.Binding, segment.Count, segment.Type.Convert(), dummyBuffer);
            }
        }

        public void SignalCommandBufferChange()
        {
            _updateDescriptorCacheCbIndex = true;
            _dirty = DirtyFlags.All;

            _uniformSet.Clear();
            _storageSet.Clear();
            AdvancePdSequence();
        }

        private static void SwapBuffer(BufferRef[] list, Auto<DisposableBuffer> from, Auto<DisposableBuffer> to)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Buffer == from)
                {
                    list[i].Buffer = to;
                }
            }
        }

        public void SwapBuffer(Auto<DisposableBuffer> from, Auto<DisposableBuffer> to)
        {
            SwapBuffer(_uniformBufferRefs, from, to);
            SwapBuffer(_storageBufferRefs, from, to);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dummyTexture.Dispose();
                _dummySampler.Dispose();
                _templateUpdater.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
