using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Gpu.Shader
{
    /// <summary>
    /// Shader info structure builder.
    /// </summary>
    class ShaderInfoBuilder
    {
        private const ResourceStages SupportBufferStages =
            ResourceStages.Compute |
            ResourceStages.Vertex |
            ResourceStages.Fragment;

        private const ResourceStages VtgStages =
            ResourceStages.Vertex |
            ResourceStages.TessellationControl |
            ResourceStages.TessellationEvaluation |
            ResourceStages.Geometry;

        private readonly GpuContext _context;
        private readonly ComputeSize _computeLocalSize;

        private int _fragmentOutputMap;

        private readonly int _reservedConstantBuffers;
        private readonly int _reservedStorageBuffers;
        private readonly int _reservedTextures;
        private readonly int _reservedImages;

        private List<ResourceDescriptor>[] _resourceDescriptors;
        private List<ResourceUsage>[] _resourceUsages;

        /// <summary>
        /// Creates a new shader info builder.
        /// </summary>
        /// <param name="context">GPU context that owns the shaders that will be added to the builder</param>
        /// <param name="tfEnabled">Indicates if the graphics shader is used with transform feedback enabled</param>
        /// <param name="vertexAsCompute">Indicates that the vertex shader will be emulated on a compute shader</param>
        /// <param name="computeLocalSize">Indicates the local thread size for a compute shader</param>
        public ShaderInfoBuilder(GpuContext context, bool tfEnabled, bool vertexAsCompute = false, ComputeSize computeLocalSize = default)
        {
            _context = context;
            _computeLocalSize = computeLocalSize;

            _fragmentOutputMap = -1;

            int uniformSetIndex = context.Capabilities.UniformBufferSetIndex;
            int storageSetIndex = context.Capabilities.StorageBufferSetIndex;
            int textureSetIndex = context.Capabilities.TextureSetIndex;
            int imageSetIndex = context.Capabilities.ImageSetIndex;

            int totalSets = Math.Max(uniformSetIndex, storageSetIndex);
            totalSets = Math.Max(totalSets, textureSetIndex);
            totalSets = Math.Max(totalSets, imageSetIndex);
            totalSets++;

            _resourceDescriptors = new List<ResourceDescriptor>[totalSets];
            _resourceUsages = new List<ResourceUsage>[totalSets];

            for (int index = 0; index < totalSets; index++)
            {
                _resourceDescriptors[index] = new();
                _resourceUsages[index] = new();
            }

            AddDescriptor(SupportBufferStages, ResourceType.UniformBuffer, uniformSetIndex, 0, 1);
            AddUsage(SupportBufferStages, ResourceType.UniformBuffer, uniformSetIndex, 0, 1);

            ResourceReservationCounts rrc = new(!context.Capabilities.SupportsTransformFeedback && tfEnabled, vertexAsCompute);

            _reservedConstantBuffers = rrc.ReservedConstantBuffers;
            _reservedStorageBuffers = rrc.ReservedStorageBuffers;
            _reservedTextures = rrc.ReservedTextures;
            _reservedImages = rrc.ReservedImages;

            // TODO: Handle that better? Maybe we should only set the binding that are really needed on each shader.
            ResourceStages stages = vertexAsCompute ? ResourceStages.Compute | ResourceStages.Vertex : VtgStages;

            PopulateDescriptorAndUsages(stages, ResourceType.UniformBuffer, uniformSetIndex, 1, rrc.ReservedConstantBuffers - 1);
            PopulateDescriptorAndUsages(stages, ResourceType.StorageBuffer, storageSetIndex, 0, rrc.ReservedStorageBuffers, true);
            PopulateDescriptorAndUsages(stages, ResourceType.BufferTexture, textureSetIndex, 0, rrc.ReservedTextures);
            PopulateDescriptorAndUsages(stages, ResourceType.BufferImage, imageSetIndex, 0, rrc.ReservedImages, true);
        }

        /// <summary>
        /// Populates descriptors and usages for vertex as compute and transform feedback emulation reserved resources.
        /// </summary>
        /// <param name="stages">Shader stages where the resources are used</param>
        /// <param name="type">Resource type</param>
        /// <param name="setIndex">Resource set index where the resources are used</param>
        /// <param name="start">First binding number</param>
        /// <param name="count">Amount of bindings</param>
        /// <param name="write">True if the binding is written from the shader, false otherwise</param>
        private void PopulateDescriptorAndUsages(ResourceStages stages, ResourceType type, int setIndex, int start, int count, bool write = false)
        {
            AddDescriptor(stages, type, setIndex, start, count);
            // AddUsage(stages, type, setIndex, start, count, write);
        }

        /// <summary>
        /// Adds information from a given shader stage.
        /// </summary>
        /// <param name="info">Shader stage information</param>
        /// <param name="vertexAsCompute">True if the shader stage has been converted into a compute shader</param>
        public void AddStageInfo(ShaderProgramInfo info, bool vertexAsCompute = false)
        {
            if (info.Stage == ShaderStage.Fragment)
            {
                _fragmentOutputMap = info.FragmentOutputMap;
            }

            int stageIndex = GpuAccessorBase.GetStageIndex(info.Stage switch
            {
                ShaderStage.TessellationControl => 1,
                ShaderStage.TessellationEvaluation => 2,
                ShaderStage.Geometry => 3,
                ShaderStage.Fragment => 4,
                _ => 0,
            });

            ResourceStages stages = vertexAsCompute ? ResourceStages.Compute : info.Stage switch
            {
                ShaderStage.Compute => ResourceStages.Compute,
                ShaderStage.Vertex => ResourceStages.Vertex,
                ShaderStage.TessellationControl => ResourceStages.TessellationControl,
                ShaderStage.TessellationEvaluation => ResourceStages.TessellationEvaluation,
                ShaderStage.Geometry => ResourceStages.Geometry,
                ShaderStage.Fragment => ResourceStages.Fragment,
                _ => ResourceStages.None,
            };

            int uniformsPerStage = (int)_context.Capabilities.MaximumUniformBuffersPerStage;
            int storagesPerStage = (int)_context.Capabilities.MaximumStorageBuffersPerStage;
            int texturesPerStage = (int)_context.Capabilities.MaximumTexturesPerStage;
            int imagesPerStage = (int)_context.Capabilities.MaximumImagesPerStage;

            int uniformBinding = _reservedConstantBuffers + stageIndex * uniformsPerStage;
            int storageBinding = _reservedStorageBuffers + stageIndex * storagesPerStage;
            int textureBinding = _reservedTextures + stageIndex * texturesPerStage * 2;
            int imageBinding = _reservedImages + stageIndex * imagesPerStage * 2;

            int uniformSetIndex = _context.Capabilities.UniformBufferSetIndex;
            int storageSetIndex = _context.Capabilities.StorageBufferSetIndex;
            int textureSetIndex = _context.Capabilities.TextureSetIndex;
            int imageSetIndex = _context.Capabilities.ImageSetIndex;

            AddDescriptor(stages, ResourceType.UniformBuffer, uniformSetIndex, uniformBinding, uniformsPerStage);
            AddDescriptor(stages, ResourceType.StorageBuffer, storageSetIndex, storageBinding, storagesPerStage);
            AddDualDescriptor(stages, ResourceType.TextureAndSampler, ResourceType.BufferTexture, textureSetIndex, textureBinding, texturesPerStage);
            AddDualDescriptor(stages, ResourceType.Image, ResourceType.BufferImage, imageSetIndex, imageBinding, imagesPerStage);

            AddArrayDescriptors(info.Textures, stages, isImage: false);
            AddArrayDescriptors(info.Images, stages, isImage: true);

            AddUsage(info.CBuffers, stages, isStorage: false);
            AddUsage(info.SBuffers, stages, isStorage: true);
            AddUsage(info.Textures, stages, isImage: false);
            AddUsage(info.Images, stages, isImage: true);
        }

        public void AddStageInfoVac(ShaderProgramInfo info)
        {
            ResourceStages stages = info.Stage switch
            {
                ShaderStage.Compute => ResourceStages.Compute,
                ShaderStage.Vertex => ResourceStages.Vertex,
                ShaderStage.TessellationControl => ResourceStages.TessellationControl,
                ShaderStage.TessellationEvaluation => ResourceStages.TessellationEvaluation,
                ShaderStage.Geometry => ResourceStages.Geometry,
                ShaderStage.Fragment => ResourceStages.Fragment,
                _ => ResourceStages.None,
            };

            AddUsage(info.CBuffers, stages, isStorage: false);
            AddUsage(info.SBuffers, stages, isStorage: true);
            AddUsage(info.Textures, stages, isImage: false);
            AddUsage(info.Images, stages, isImage: true);
        }

        /// <summary>
        /// Adds a resource descriptor to the list of descriptors.
        /// </summary>
        /// <param name="stages">Shader stages where the resource is used</param>
        /// <param name="type">Type of the resource</param>
        /// <param name="setIndex">Descriptor set number where the resource will be bound</param>
        /// <param name="binding">Binding number where the resource will be bound</param>
        /// <param name="count">Number of resources bound at the binding location</param>
        private void AddDescriptor(ResourceStages stages, ResourceType type, int setIndex, int binding, int count)
        {
            for (int index = 0; index < count; index++)
            {
                _resourceDescriptors[setIndex].Add(new ResourceDescriptor(binding + index, 1, type, stages));
            }
        }

        /// <summary>
        /// Adds two interleaved groups of resources to the list of descriptors.
        /// </summary>
        /// <param name="stages">Shader stages where the resource is used</param>
        /// <param name="type">Type of the first interleaved resource</param>
        /// <param name="type2">Type of the second interleaved resource</param>
        /// <param name="setIndex">Descriptor set number where the resource will be bound</param>
        /// <param name="binding">Binding number where the resource will be bound</param>
        /// <param name="count">Number of resources bound at the binding location</param>
        private void AddDualDescriptor(ResourceStages stages, ResourceType type, ResourceType type2, int setIndex, int binding, int count)
        {
            AddDescriptor(stages, type, setIndex, binding, count);
            AddDescriptor(stages, type2, setIndex, binding + count, count);
        }

        /// <summary>
        /// Adds all array descriptors (those with an array length greater than one).
        /// </summary>
        /// <param name="textures">Textures to be added</param>
        /// <param name="stages">Stages where the textures are used</param>
        /// <param name="isImage">True for images, false for textures</param>
        private void AddArrayDescriptors(IEnumerable<TextureDescriptor> textures, ResourceStages stages, bool isImage)
        {
            foreach (TextureDescriptor texture in textures)
            {
                if (texture.ArrayLength > 1)
                {
                    ResourceType type = GetTextureResourceType(texture, isImage);

                    GetDescriptors(texture.Set).Add(new ResourceDescriptor(texture.Binding, texture.ArrayLength, type, stages));
                }
            }
        }

        /// <summary>
        /// Adds buffer usage information to the list of usages.
        /// </summary>
        /// <param name="stages">Shader stages where the resource is used</param>
        /// <param name="type">Type of the resource</param>
        /// <param name="setIndex">Descriptor set number where the resource will be bound</param>
        /// <param name="binding">Binding number where the resource will be bound</param>
        /// <param name="count">Number of resources bound at the binding location</param>
        /// <param name="write">True if the binding is written from the shader, false otherwise</param>
        private void AddUsage(ResourceStages stages, ResourceType type, int setIndex, int binding, int count, bool write = false)
        {
            for (int index = 0; index < count; index++)
            {
                _resourceUsages[setIndex].Add(new ResourceUsage(binding + index, 1, type, stages, write));
            }
        }

        /// <summary>
        /// Adds buffer usage information to the list of usages.
        /// </summary>
        /// <param name="buffers">Buffers to be added</param>
        /// <param name="stages">Stages where the buffers are used</param>
        /// <param name="isStorage">True for storage buffers, false for uniform buffers</param>
        private void AddUsage(IEnumerable<BufferDescriptor> buffers, ResourceStages stages, bool isStorage)
        {
            foreach (BufferDescriptor buffer in buffers)
            {
                GetUsages(buffer.Set).Add(new ResourceUsage(
                    buffer.Binding,
                    1,
                    isStorage ? ResourceType.StorageBuffer : ResourceType.UniformBuffer,
                    stages,
                    buffer.Flags.HasFlag(BufferUsageFlags.Write)));
            }
        }

        /// <summary>
        /// Adds texture usage information to the list of usages.
        /// </summary>
        /// <param name="textures">Textures to be added</param>
        /// <param name="stages">Stages where the textures are used</param>
        /// <param name="isImage">True for images, false for textures</param>
        private void AddUsage(IEnumerable<TextureDescriptor> textures, ResourceStages stages, bool isImage)
        {
            foreach (TextureDescriptor texture in textures)
            {
                ResourceType type = GetTextureResourceType(texture, isImage);

                GetUsages(texture.Set).Add(new ResourceUsage(
                    texture.Binding,
                    texture.ArrayLength,
                    type,
                    stages,
                    texture.Flags.HasFlag(TextureUsageFlags.ImageStore)));
            }
        }

        /// <summary>
        /// Gets the list of resource descriptors for a given set index. A new list will be created if needed.
        /// </summary>
        /// <param name="setIndex">Resource set index</param>
        /// <returns>List of resource descriptors</returns>
        private List<ResourceDescriptor> GetDescriptors(int setIndex)
        {
            if (_resourceDescriptors.Length <= setIndex)
            {
                int oldLength = _resourceDescriptors.Length;
                Array.Resize(ref _resourceDescriptors, setIndex + 1);

                for (int index = oldLength; index <= setIndex; index++)
                {
                    _resourceDescriptors[index] = new();
                }
            }

            return _resourceDescriptors[setIndex];
        }

        /// <summary>
        /// Gets the list of resource usages for a given set index. A new list will be created if needed.
        /// </summary>
        /// <param name="setIndex">Resource set index</param>
        /// <returns>List of resource usages</returns>
        private List<ResourceUsage> GetUsages(int setIndex)
        {
            if (_resourceUsages.Length <= setIndex)
            {
                int oldLength = _resourceUsages.Length;
                Array.Resize(ref _resourceUsages, setIndex + 1);

                for (int index = oldLength; index <= setIndex; index++)
                {
                    _resourceUsages[index] = new();
                }
            }

            return _resourceUsages[setIndex];
        }

        /// <summary>
        /// Gets a resource type from a texture descriptor.
        /// </summary>
        /// <param name="texture">Texture descriptor</param>
        /// <param name="isImage">Whether the texture is a image texture (writable) or not (sampled)</param>
        /// <returns>Resource type</returns>
        private static ResourceType GetTextureResourceType(TextureDescriptor texture, bool isImage)
        {
            bool isBuffer = (texture.Type & SamplerType.Mask) == SamplerType.TextureBuffer;

            if (isBuffer)
            {
                return isImage ? ResourceType.BufferImage : ResourceType.BufferTexture;
            }
            else if (isImage)
            {
                return ResourceType.Image;
            }
            else if (texture.Type == SamplerType.None)
            {
                return ResourceType.Sampler;
            }
            else if (texture.Separate)
            {
                return ResourceType.Texture;
            }
            else
            {
                return ResourceType.TextureAndSampler;
            }
        }

        /// <summary>
        /// Creates a new shader information structure from the added information.
        /// </summary>
        /// <param name="pipeline">Optional pipeline state for background shader compilation</param>
        /// <param name="fromCache">Indicates if the shader comes from a disk cache</param>
        /// <returns>Shader information</returns>
        public ShaderInfo Build(ProgramPipelineState? pipeline, bool fromCache = false)
        {
            int totalSets = _resourceDescriptors.Length;

            var descriptors = new ResourceDescriptorCollection[totalSets];
            var usages = new ResourceUsageCollection[totalSets];

            for (int index = 0; index < totalSets; index++)
            {
                descriptors[index] = new ResourceDescriptorCollection(_resourceDescriptors[index].ToArray().AsReadOnly());
                usages[index] = new ResourceUsageCollection(_resourceUsages[index].ToArray().AsReadOnly());
            }

            ResourceLayout resourceLayout = new(descriptors.AsReadOnly(), usages.AsReadOnly());

            return new ShaderInfo(_fragmentOutputMap, resourceLayout, _computeLocalSize, pipeline, fromCache);
        }

        /// <summary>
        /// Builds shader information for shaders from the disk cache.
        /// </summary>
        /// <param name="context">GPU context that owns the shaders</param>
        /// <param name="programs">Shaders from the disk cache</param>
        /// <param name="pipeline">Optional pipeline for background compilation</param>
        /// <param name="tfEnabled">Indicates if the graphics shader is used with transform feedback enabled</param>
        /// <param name="computeLocalSize">Compute local thread size</param>
        /// <returns>Shader information</returns>
        public static ShaderInfo BuildForCache(
            GpuContext context,
            IEnumerable<CachedShaderStage> programs,
            ProgramPipelineState? pipeline,
            bool tfEnabled,
            ComputeSize computeLocalSize)
        {
            ShaderInfoBuilder builder = new(context, tfEnabled, computeLocalSize: computeLocalSize);

            foreach (CachedShaderStage program in programs)
            {
                if (program?.Info != null)
                {
                    builder.AddStageInfo(program.Info);
                }
            }

            return builder.Build(pipeline, fromCache: true);
        }

        /// <summary>
        /// Builds shader information for a compute shader.
        /// </summary>
        /// <param name="context">GPU context that owns the shader</param>
        /// <param name="info">Compute shader information</param>
        /// <param name="computeLocalSize">Compute local thread size</param>
        /// <param name="fromCache">True if the compute shader comes from a disk cache, false otherwise</param>
        /// <returns>Shader information</returns>
        public static ShaderInfo BuildForCompute(GpuContext context, ShaderProgramInfo info, ComputeSize computeLocalSize, bool fromCache = false)
        {
            ShaderInfoBuilder builder = new(context, tfEnabled: false, vertexAsCompute: false, computeLocalSize: computeLocalSize);

            builder.AddStageInfo(info);

            return builder.Build(null, fromCache);
        }

        /// <summary>
        /// Builds shader information for a vertex or geometry shader thas was converted to compute shader.
        /// </summary>
        /// <param name="context">GPU context that owns the shader</param>
        /// <param name="info">Compute shader information</param>
        /// <param name="tfEnabled">Indicates if the graphics shader is used with transform feedback enabled</param>
        /// <param name="fromCache">True if the compute shader comes from a disk cache, false otherwise</param>
        /// <returns>Shader information</returns>
        public static ShaderInfo BuildForVertexAsCompute(GpuContext context, ShaderProgramInfo info, ShaderProgramInfo info2, bool tfEnabled, bool fromCache = false)
        {
            ShaderInfoBuilder builder = new(context, tfEnabled, vertexAsCompute: true, computeLocalSize: ComputeSize.VtgAsCompute);

            builder.AddStageInfoVac(info2);
            builder.AddStageInfo(info, vertexAsCompute: true);

            return builder.Build(null, fromCache);
        }
    }
}
