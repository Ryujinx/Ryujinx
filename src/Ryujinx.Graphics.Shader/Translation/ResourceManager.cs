using Ryujinx.Common;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ryujinx.Graphics.Shader.Translation
{
    class ResourceManager
    {
        // Those values are used if the shader as local or shared memory access,
        // but for some reason the supplied size was 0.
        private const int DefaultLocalMemorySize = 128;
        private const int DefaultSharedMemorySize = 4096;

        private static readonly string[] _stagePrefixes = new string[] { "cp", "vp", "tcp", "tep", "gp", "fp" };

        private readonly IGpuAccessor _gpuAccessor;
        private readonly ShaderStage _stage;
        private readonly string _stagePrefix;

        private readonly SetBindingPair[] _cbSlotToBindingMap;
        private readonly SetBindingPair[] _sbSlotToBindingMap;
        private uint _sbSlotWritten;

        private readonly Dictionary<int, int> _sbSlots;
        private readonly Dictionary<int, int> _sbSlotsReverse;

        private readonly HashSet<int> _usedConstantBufferBindings;

        private readonly record struct TextureInfo(int CbufSlot, int Handle, int ArrayLength, bool Separate, SamplerType Type, TextureFormat Format);

        private struct TextureMeta
        {
            public int Set;
            public int Binding;
            public bool AccurateType;
            public SamplerType Type;
            public TextureUsageFlags UsageFlags;
        }

        private readonly Dictionary<TextureInfo, TextureMeta> _usedTextures;
        private readonly Dictionary<TextureInfo, TextureMeta> _usedImages;

        private readonly List<BufferDefinition> _vacConstantBuffers;
        private readonly List<BufferDefinition> _vacStorageBuffers;
        private readonly List<TextureDefinition> _vacTextures;
        private readonly List<TextureDefinition> _vacImages;

        public int LocalMemoryId { get; private set; }
        public int SharedMemoryId { get; private set; }

        public int LocalVertexDataMemoryId { get; private set; }
        public int LocalTopologyRemapMemoryId { get; private set; }
        public int LocalVertexIndexVertexRateMemoryId { get; private set; }
        public int LocalVertexIndexInstanceRateMemoryId { get; private set; }
        public int LocalGeometryOutputVertexCountMemoryId { get; private set; }
        public int LocalGeometryOutputIndexCountMemoryId { get; private set; }

        public ShaderProperties Properties { get; }

        public ResourceReservations Reservations { get; }

        public ResourceManager(ShaderStage stage, IGpuAccessor gpuAccessor, ResourceReservations reservations = null)
        {
            _gpuAccessor = gpuAccessor;
            Properties = new();
            Reservations = reservations;
            _stage = stage;
            _stagePrefix = GetShaderStagePrefix(stage);

            _cbSlotToBindingMap = new SetBindingPair[18];
            _sbSlotToBindingMap = new SetBindingPair[16];
            _cbSlotToBindingMap.AsSpan().Fill(new(-1, -1));
            _sbSlotToBindingMap.AsSpan().Fill(new(-1, -1));

            _sbSlots = new();
            _sbSlotsReverse = new();

            _usedConstantBufferBindings = new();

            _usedTextures = new();
            _usedImages = new();

            _vacConstantBuffers = new();
            _vacStorageBuffers = new();
            _vacTextures = new();
            _vacImages = new();

            Properties.AddOrUpdateConstantBuffer(new(BufferLayout.Std140, 0, SupportBuffer.Binding, "support_buffer", SupportBuffer.GetStructureType()));

            LocalMemoryId = -1;
            SharedMemoryId = -1;
        }

        public void SetCurrentLocalMemory(int size, bool isUsed)
        {
            if (isUsed)
            {
                if (size <= 0)
                {
                    size = DefaultLocalMemorySize;
                }

                var lmem = new MemoryDefinition("local_memory", AggregateType.Array | AggregateType.U32, BitUtils.DivRoundUp(size, sizeof(uint)));

                LocalMemoryId = Properties.AddLocalMemory(lmem);
            }
            else
            {
                LocalMemoryId = -1;
            }
        }

        public void SetCurrentSharedMemory(int size, bool isUsed)
        {
            if (isUsed)
            {
                if (size <= 0)
                {
                    size = DefaultSharedMemorySize;
                }

                var smem = new MemoryDefinition("shared_memory", AggregateType.Array | AggregateType.U32, BitUtils.DivRoundUp(size, sizeof(uint)));

                SharedMemoryId = Properties.AddSharedMemory(smem);
            }
            else
            {
                SharedMemoryId = -1;
            }
        }

        public void SetVertexAsComputeLocalMemories(ShaderStage stage, InputTopology inputTopology)
        {
            LocalVertexDataMemoryId = AddMemoryDefinition("local_vertex_data", AggregateType.Array | AggregateType.FP32, Reservations.OutputSizePerInvocation);

            if (stage == ShaderStage.Vertex)
            {
                LocalVertexIndexVertexRateMemoryId = AddMemoryDefinition("local_vertex_index_vr", AggregateType.U32);
                LocalVertexIndexInstanceRateMemoryId = AddMemoryDefinition("local_vertex_index_ir", AggregateType.U32);
            }
            else if (stage == ShaderStage.Geometry)
            {
                LocalTopologyRemapMemoryId = AddMemoryDefinition("local_topology_remap", AggregateType.Array | AggregateType.U32, inputTopology.ToInputVertices());

                LocalGeometryOutputVertexCountMemoryId = AddMemoryDefinition("local_geometry_output_vertex", AggregateType.U32);
                LocalGeometryOutputIndexCountMemoryId = AddMemoryDefinition("local_geometry_output_index", AggregateType.U32);
            }
        }

        private int AddMemoryDefinition(string name, AggregateType type, int arrayLength = 1)
        {
            return Properties.AddLocalMemory(new MemoryDefinition(name, type, arrayLength));
        }

        public int GetConstantBufferBinding(int slot)
        {
            SetBindingPair setAndBinding = _cbSlotToBindingMap[slot];
            if (setAndBinding.Binding < 0)
            {
                setAndBinding = _gpuAccessor.CreateConstantBufferBinding(slot);
                _cbSlotToBindingMap[slot] = setAndBinding;
                string slotNumber = slot.ToString(CultureInfo.InvariantCulture);
                AddNewConstantBuffer(setAndBinding.SetIndex, setAndBinding.Binding, $"{_stagePrefix}_c{slotNumber}");
            }

            return setAndBinding.Binding;
        }

        public bool TryGetStorageBufferBinding(int sbCbSlot, int sbCbOffset, bool write, out int binding)
        {
            if (!TryGetSbSlot((byte)sbCbSlot, (ushort)sbCbOffset, out int slot))
            {
                binding = 0;
                return false;
            }

            SetBindingPair setAndBinding = _sbSlotToBindingMap[slot];

            if (setAndBinding.Binding < 0)
            {
                setAndBinding = _gpuAccessor.CreateStorageBufferBinding(slot);
                _sbSlotToBindingMap[slot] = setAndBinding;
                string slotNumber = slot.ToString(CultureInfo.InvariantCulture);
                AddNewStorageBuffer(setAndBinding.SetIndex, setAndBinding.Binding, $"{_stagePrefix}_s{slotNumber}");
            }

            if (write)
            {
                _sbSlotWritten |= 1u << slot;
            }

            binding = setAndBinding.Binding;
            return true;
        }

        private bool TryGetSbSlot(byte sbCbSlot, ushort sbCbOffset, out int slot)
        {
            int key = PackSbCbInfo(sbCbSlot, sbCbOffset);

            if (!_sbSlots.TryGetValue(key, out slot))
            {
                slot = _sbSlots.Count;

                if (slot >= _sbSlotToBindingMap.Length)
                {
                    return false;
                }

                _sbSlots.Add(key, slot);
                _sbSlotsReverse.Add(slot, key);
            }

            return true;
        }

        public bool TryGetConstantBufferSlot(int binding, out int slot)
        {
            for (slot = 0; slot < _cbSlotToBindingMap.Length; slot++)
            {
                if (_cbSlotToBindingMap[slot].Binding == binding)
                {
                    return true;
                }
            }

            slot = 0;
            return false;
        }

        public SetBindingPair GetTextureOrImageBinding(
            Instruction inst,
            SamplerType type,
            TextureFormat format,
            TextureFlags flags,
            int cbufSlot,
            int handle,
            int arrayLength = 1,
            bool separate = false)
        {
            inst &= Instruction.Mask;
            bool isImage = inst.IsImage();
            bool isWrite = inst.IsImageStore();
            bool accurateType = !inst.IsTextureQuery();
            bool intCoords = isImage || flags.HasFlag(TextureFlags.IntCoords) || inst == Instruction.TextureQuerySize;
            bool coherent = flags.HasFlag(TextureFlags.Coherent);

            if (!isImage)
            {
                format = TextureFormat.Unknown;
            }

            SetBindingPair setAndBinding = GetTextureOrImageBinding(
                cbufSlot,
                handle,
                arrayLength,
                type,
                format,
                isImage,
                intCoords,
                isWrite,
                accurateType,
                coherent,
                separate);

            _gpuAccessor.RegisterTexture(handle, cbufSlot);

            return setAndBinding;
        }

        private SetBindingPair GetTextureOrImageBinding(
            int cbufSlot,
            int handle,
            int arrayLength,
            SamplerType type,
            TextureFormat format,
            bool isImage,
            bool intCoords,
            bool write,
            bool accurateType,
            bool coherent,
            bool separate)
        {
            var dimensions = type == SamplerType.None ? 0 : type.GetDimensions();
            var dict = isImage ? _usedImages : _usedTextures;

            var usageFlags = TextureUsageFlags.None;

            if (intCoords)
            {
                usageFlags |= TextureUsageFlags.NeedsScaleValue;

                var canScale = _stage.SupportsRenderScale() && arrayLength == 1 && !write && dimensions == 2;

                if (!canScale)
                {
                    // Resolution scaling cannot be applied to this texture right now.
                    // Flag so that we know to blacklist scaling on related textures when binding them.
                    usageFlags |= TextureUsageFlags.ResScaleUnsupported;
                }
            }

            if (write)
            {
                usageFlags |= TextureUsageFlags.ImageStore;
            }

            if (coherent)
            {
                usageFlags |= TextureUsageFlags.ImageCoherent;
            }

            // For array textures, we also want to use type as key,
            // since we may have texture handles stores in the same buffer, but for textures with different types.
            var keyType = arrayLength > 1 ? type : SamplerType.None;
            var info = new TextureInfo(cbufSlot, handle, arrayLength, separate, keyType, format);
            var meta = new TextureMeta()
            {
                AccurateType = accurateType,
                Type = type,
                UsageFlags = usageFlags,
            };

            int setIndex;
            int binding;

            if (dict.TryGetValue(info, out var existingMeta))
            {
                dict[info] = MergeTextureMeta(meta, existingMeta);
                setIndex = existingMeta.Set;
                binding = existingMeta.Binding;
            }
            else
            {
                if (arrayLength > 1 && (setIndex = _gpuAccessor.CreateExtraSet()) >= 0)
                {
                    // We reserved an "extra set" for the array.
                    // In this case the binding is always the first one (0).
                    // Using separate sets for array is better as we need to do less descriptor set updates.

                    binding = 0;
                }
                else
                {
                    bool isBuffer = (type & SamplerType.Mask) == SamplerType.TextureBuffer;

                    SetBindingPair setAndBinding = isImage
                        ? _gpuAccessor.CreateImageBinding(arrayLength, isBuffer)
                        : _gpuAccessor.CreateTextureBinding(arrayLength, isBuffer);

                    setIndex = setAndBinding.SetIndex;
                    binding = setAndBinding.Binding;
                }

                meta.Set = setIndex;
                meta.Binding = binding;

                dict.Add(info, meta);
            }

            string nameSuffix;
            string prefix = isImage ? "i" : "t";

            if (arrayLength != 1 && type != SamplerType.None)
            {
                prefix += type.ToShortSamplerType();
            }

            if (isImage)
            {
                nameSuffix = cbufSlot < 0
                    ? $"{prefix}_tcb_{handle:X}_{format.ToGlslFormat()}"
                    : $"{prefix}_cb{cbufSlot}_{handle:X}_{format.ToGlslFormat()}";
            }
            else if (type == SamplerType.None)
            {
                nameSuffix = cbufSlot < 0 ? $"s_tcb_{handle:X}" : $"s_cb{cbufSlot}_{handle:X}";
            }
            else
            {
                nameSuffix = cbufSlot < 0 ? $"{prefix}_tcb_{handle:X}" : $"{prefix}_cb{cbufSlot}_{handle:X}";
            }

            var definition = new TextureDefinition(
                setIndex,
                binding,
                arrayLength,
                separate,
                $"{_stagePrefix}_{nameSuffix}",
                meta.Type,
                info.Format,
                meta.UsageFlags);

            if (isImage)
            {
                Properties.AddOrUpdateImage(definition);
            }
            else
            {
                Properties.AddOrUpdateTexture(definition);
            }

            return new SetBindingPair(setIndex, binding);
        }

        private static TextureMeta MergeTextureMeta(TextureMeta meta, TextureMeta existingMeta)
        {
            meta.Set = existingMeta.Set;
            meta.Binding = existingMeta.Binding;
            meta.UsageFlags |= existingMeta.UsageFlags;

            // If the texture we have has inaccurate type information, then
            // we prefer the most accurate one.
            if (existingMeta.AccurateType)
            {
                meta.AccurateType = true;
                meta.Type = existingMeta.Type;
            }

            return meta;
        }

        public void SetUsageFlagsForTextureQuery(int binding, SamplerType type)
        {
            TextureInfo selectedInfo = default;
            TextureMeta selectedMeta = default;
            bool found = false;

            foreach ((TextureInfo info, TextureMeta meta) in _usedTextures)
            {
                if (meta.Binding == binding)
                {
                    selectedInfo = info;
                    selectedMeta = meta;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                selectedMeta.UsageFlags |= TextureUsageFlags.NeedsScaleValue;

                var dimensions = type.GetDimensions();
                var canScale = _stage.SupportsRenderScale() && selectedInfo.ArrayLength == 1 && dimensions == 2;

                if (!canScale)
                {
                    // Resolution scaling cannot be applied to this texture right now.
                    // Flag so that we know to blacklist scaling on related textures when binding them.
                    selectedMeta.UsageFlags |= TextureUsageFlags.ResScaleUnsupported;
                }

                _usedTextures[selectedInfo] = selectedMeta;
            }
        }

        public void SetUsedConstantBufferBinding(int binding)
        {
            _usedConstantBufferBindings.Add(binding);
        }

        public BufferDescriptor[] GetConstantBufferDescriptors()
        {
            var descriptors = new BufferDescriptor[_usedConstantBufferBindings.Count];

            int descriptorIndex = 0;

            for (int slot = 0; slot < _cbSlotToBindingMap.Length; slot++)
            {
                SetBindingPair setAndBinding = _cbSlotToBindingMap[slot];

                if (setAndBinding.Binding >= 0 && _usedConstantBufferBindings.Contains(setAndBinding.Binding))
                {
                    descriptors[descriptorIndex++] = new BufferDescriptor(setAndBinding.SetIndex, setAndBinding.Binding, slot);
                }
            }

            if (descriptors.Length != descriptorIndex)
            {
                Array.Resize(ref descriptors, descriptorIndex);
            }

            return descriptors;
        }

        public BufferDescriptor[] GetStorageBufferDescriptors()
        {
            var descriptors = new BufferDescriptor[_sbSlots.Count];

            int descriptorIndex = 0;

            foreach ((int key, int slot) in _sbSlots)
            {
                SetBindingPair setAndBinding = _sbSlotToBindingMap[slot];

                if (setAndBinding.Binding >= 0)
                {
                    (int sbCbSlot, int sbCbOffset) = UnpackSbCbInfo(key);
                    BufferUsageFlags flags = (_sbSlotWritten & (1u << slot)) != 0 ? BufferUsageFlags.Write : BufferUsageFlags.None;
                    descriptors[descriptorIndex++] = new BufferDescriptor(setAndBinding.SetIndex, setAndBinding.Binding, slot, sbCbSlot, sbCbOffset, flags);
                }
            }

            if (descriptors.Length != descriptorIndex)
            {
                Array.Resize(ref descriptors, descriptorIndex);
            }

            return descriptors;
        }

        public TextureDescriptor[] GetTextureDescriptors(bool includeArrays = true)
        {
            return GetDescriptors(_usedTextures, includeArrays);
        }

        public TextureDescriptor[] GetImageDescriptors(bool includeArrays = true)
        {
            return GetDescriptors(_usedImages, includeArrays);
        }

        private static TextureDescriptor[] GetDescriptors(IReadOnlyDictionary<TextureInfo, TextureMeta> usedResources, bool includeArrays)
        {
            List<TextureDescriptor> descriptors = new();

            bool hasAnyArray = false;

            foreach ((TextureInfo info, TextureMeta meta) in usedResources)
            {
                if (info.ArrayLength > 1)
                {
                    hasAnyArray = true;
                    continue;
                }

                descriptors.Add(new TextureDescriptor(
                    meta.Set,
                    meta.Binding,
                    meta.Type,
                    info.Format,
                    info.CbufSlot,
                    info.Handle,
                    info.ArrayLength,
                    info.Separate,
                    meta.UsageFlags));
            }

            if (hasAnyArray && includeArrays)
            {
                foreach ((TextureInfo info, TextureMeta meta) in usedResources)
                {
                    if (info.ArrayLength <= 1)
                    {
                        continue;
                    }

                    descriptors.Add(new TextureDescriptor(
                        meta.Set,
                        meta.Binding,
                        meta.Type,
                        info.Format,
                        info.CbufSlot,
                        info.Handle,
                        info.ArrayLength,
                        info.Separate,
                        meta.UsageFlags));
                }
            }

            return descriptors.ToArray();
        }

        public ShaderProgramInfo GetVertexAsComputeInfo(bool isVertex = false)
        {
            var cbDescriptors = new BufferDescriptor[_vacConstantBuffers.Count];
            int cbDescriptorIndex = 0;

            foreach (BufferDefinition definition in _vacConstantBuffers)
            {
                cbDescriptors[cbDescriptorIndex++] = new BufferDescriptor(definition.Set, definition.Binding, 0, 0, 0, BufferUsageFlags.None);
            }

            var sbDescriptors = new BufferDescriptor[_vacStorageBuffers.Count];
            int sbDescriptorIndex = 0;

            foreach (BufferDefinition definition in _vacStorageBuffers)
            {
                sbDescriptors[sbDescriptorIndex++] = new BufferDescriptor(definition.Set, definition.Binding, 0, 0, 0, BufferUsageFlags.Write);
            }

            var tDescriptors = new TextureDescriptor[_vacTextures.Count];
            int tDescriptorIndex = 0;

            foreach (TextureDefinition definition in _vacTextures)
            {
                tDescriptors[tDescriptorIndex++] = new TextureDescriptor(
                    definition.Set,
                    definition.Binding,
                    definition.Type,
                    definition.Format,
                    0,
                    0,
                    definition.ArrayLength,
                    definition.Separate,
                    definition.Flags);
            }

            var iDescriptors = new TextureDescriptor[_vacImages.Count];
            int iDescriptorIndex = 0;

            foreach (TextureDefinition definition in _vacImages)
            {
                iDescriptors[iDescriptorIndex++] = new TextureDescriptor(
                    definition.Set,
                    definition.Binding,
                    definition.Type,
                    definition.Format,
                    0,
                    0,
                    definition.ArrayLength,
                    definition.Separate,
                    definition.Flags);
            }

            return new ShaderProgramInfo(
                cbDescriptors,
                sbDescriptors,
                tDescriptors,
                iDescriptors,
                isVertex ? ShaderStage.Vertex : ShaderStage.Compute,
                0,
                0,
                0,
                false,
                false,
                false,
                false,
                0,
                0);
        }

        public bool TryGetCbufSlotAndHandleForTexture(int binding, out int cbufSlot, out int handle)
        {
            foreach ((TextureInfo info, TextureMeta meta) in _usedTextures)
            {
                if (meta.Binding == binding)
                {
                    cbufSlot = info.CbufSlot;
                    handle = info.Handle;

                    return true;
                }
            }

            cbufSlot = 0;
            handle = 0;
            return false;
        }

        private static int FindDescriptorIndex(TextureDescriptor[] array, int binding)
        {
            return Array.FindIndex(array, x => x.Binding == binding);
        }

        public int FindTextureDescriptorIndex(int binding)
        {
            return FindDescriptorIndex(GetTextureDescriptors(), binding);
        }

        public int FindImageDescriptorIndex(int binding)
        {
            return FindDescriptorIndex(GetImageDescriptors(), binding);
        }

        public bool IsArrayOfTexturesOrImages(int binding, bool isImage)
        {
            foreach ((TextureInfo info, TextureMeta meta) in isImage ? _usedImages : _usedTextures)
            {
                if (meta.Binding == binding)
                {
                    return info.ArrayLength != 1;
                }
            }

            return false;
        }

        private void AddNewConstantBuffer(int setIndex, int binding, string name)
        {
            StructureType type = new(new[]
            {
                new StructureField(AggregateType.Array | AggregateType.Vector4 | AggregateType.FP32, "data", Constants.ConstantBufferSize / 16),
            });

            Properties.AddOrUpdateConstantBuffer(new(BufferLayout.Std140, setIndex, binding, name, type));
        }

        private void AddNewStorageBuffer(int setIndex, int binding, string name)
        {
            StructureType type = new(new[]
            {
                new StructureField(AggregateType.Array | AggregateType.U32, "data", 0),
            });

            Properties.AddOrUpdateStorageBuffer(new(BufferLayout.Std430, setIndex, binding, name, type));
        }

        public void AddVertexAsComputeConstantBuffer(BufferDefinition definition)
        {
            _vacConstantBuffers.Add(definition);
            Properties.AddOrUpdateConstantBuffer(definition);
        }

        public void AddVertexAsComputeStorageBuffer(BufferDefinition definition)
        {
            _vacStorageBuffers.Add(definition);
            Properties.AddOrUpdateStorageBuffer(definition);
        }

        public void AddVertexAsComputeTexture(TextureDefinition definition)
        {
            _vacTextures.Add(definition);
            Properties.AddOrUpdateTexture(definition);
        }

        public void AddVertexAsComputeImage(TextureDefinition definition)
        {
            _vacImages.Add(definition);
            Properties.AddOrUpdateImage(definition);
        }

        public static string GetShaderStagePrefix(ShaderStage stage)
        {
            uint index = (uint)stage;

            return index >= _stagePrefixes.Length ? "invalid" : _stagePrefixes[index];
        }

        private static int PackSbCbInfo(int sbCbSlot, int sbCbOffset)
        {
            return sbCbOffset | (sbCbSlot << 16);
        }

        private static (int, int) UnpackSbCbInfo(int key)
        {
            return ((byte)(key >> 16), (ushort)key);
        }
    }
}
