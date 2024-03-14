using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Gpu.Shader
{
    /// <summary>
    /// Shader info structure builder.
    /// </summary>
    class ShaderInfoBuilder
    {
        private const int TotalSets = 4;

        private const int UniformSetIndex = 0;
        private const int StorageSetIndex = 1;
        private const int TextureSetIndex = 2;
        private const int ImageSetIndex = 3;

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

        private int _fragmentOutputMap;

        private readonly int _reservedConstantBuffers;
        private readonly int _reservedStorageBuffers;
        private readonly int _reservedTextures;
        private readonly int _reservedImages;

        private readonly List<ResourceDescriptor>[] _resourceDescriptors;
        private readonly List<ResourceUsage>[] _resourceUsages;

        /// <summary>
        /// Creates a new shader info builder.
        /// </summary>
        /// <param name="context">GPU context that owns the shaders that will be added to the builder</param>
        /// <param name="tfEnabled">Indicates if the graphics shader is used with transform feedback enabled</param>
        /// <param name="vertexAsCompute">Indicates that the vertex shader will be emulated on a compute shader</param>
        public ShaderInfoBuilder(GpuContext context, bool tfEnabled, bool vertexAsCompute = false)
        {
            _context = context;

            _fragmentOutputMap = -1;

            _resourceDescriptors = new List<ResourceDescriptor>[TotalSets];
            _resourceUsages = new List<ResourceUsage>[TotalSets];

            for (int index = 0; index < TotalSets; index++)
            {
                _resourceDescriptors[index] = new();
                _resourceUsages[index] = new();
            }

            AddDescriptor(SupportBufferStages, ResourceType.UniformBuffer, UniformSetIndex, 0, 1);
            AddUsage(SupportBufferStages, ResourceType.UniformBuffer, UniformSetIndex, 0, 1);

            ResourceReservationCounts rrc = new(!context.Capabilities.SupportsTransformFeedback && tfEnabled, vertexAsCompute);

            _reservedConstantBuffers = rrc.ReservedConstantBuffers;
            _reservedStorageBuffers = rrc.ReservedStorageBuffers;
            _reservedTextures = rrc.ReservedTextures;
            _reservedImages = rrc.ReservedImages;

            // TODO: Handle that better? Maybe we should only set the binding that are really needed on each shader.
            ResourceStages stages = vertexAsCompute ? ResourceStages.Compute | ResourceStages.Vertex : VtgStages;

            PopulateDescriptorAndUsages(stages, ResourceType.UniformBuffer, UniformSetIndex, 1, rrc.ReservedConstantBuffers - 1);
            PopulateDescriptorAndUsages(stages, ResourceType.StorageBuffer, StorageSetIndex, 0, rrc.ReservedStorageBuffers);
            PopulateDescriptorAndUsages(stages, ResourceType.BufferTexture, TextureSetIndex, 0, rrc.ReservedTextures);
            PopulateDescriptorAndUsages(stages, ResourceType.BufferImage, ImageSetIndex, 0, rrc.ReservedImages);
        }

        private void PopulateDescriptorAndUsages(ResourceStages stages, ResourceType type, int setIndex, int start, int count)
        {
            AddDescriptor(stages, type, setIndex, start, count);
            AddUsage(stages, type, setIndex, start, count);
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

            AddDescriptor(stages, ResourceType.UniformBuffer, UniformSetIndex, uniformBinding, uniformsPerStage);
            AddDescriptor(stages, ResourceType.StorageBuffer, StorageSetIndex, storageBinding, storagesPerStage);
            AddDualDescriptor(stages, ResourceType.TextureAndSampler, ResourceType.BufferTexture, TextureSetIndex, textureBinding, texturesPerStage);
            AddDualDescriptor(stages, ResourceType.Image, ResourceType.BufferImage, ImageSetIndex, imageBinding, imagesPerStage);

            AddArrayDescriptors(info.Textures, stages, TextureSetIndex, isImage: false);
            AddArrayDescriptors(info.Images, stages, TextureSetIndex, isImage: true);

            AddUsage(info.CBuffers, stages, UniformSetIndex, isStorage: false);
            AddUsage(info.SBuffers, stages, StorageSetIndex, isStorage: true);
            AddUsage(info.Textures, stages, TextureSetIndex, isImage: false);
            AddUsage(info.Images, stages, ImageSetIndex, isImage: true);
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
        /// <param name="setIndex">Descriptor set index where the textures will be bound</param>
        /// <param name="isImage">True for images, false for textures</param>
        private void AddArrayDescriptors(IEnumerable<TextureDescriptor> textures, ResourceStages stages, int setIndex, bool isImage)
        {
            foreach (TextureDescriptor texture in textures)
            {
                if (texture.ArrayLength > 1)
                {
                    bool isBuffer = (texture.Type & SamplerType.Mask) == SamplerType.TextureBuffer;

                    ResourceType type = isBuffer
                        ? (isImage ? ResourceType.BufferImage : ResourceType.BufferTexture)
                        : (isImage ? ResourceType.Image : ResourceType.TextureAndSampler);

                    _resourceDescriptors[setIndex].Add(new ResourceDescriptor(texture.Binding, texture.ArrayLength, type, stages));
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
        private void AddUsage(ResourceStages stages, ResourceType type, int setIndex, int binding, int count)
        {
            for (int index = 0; index < count; index++)
            {
                _resourceUsages[setIndex].Add(new ResourceUsage(binding + index, 1, type, stages));
            }
        }

        /// <summary>
        /// Adds buffer usage information to the list of usages.
        /// </summary>
        /// <param name="buffers">Buffers to be added</param>
        /// <param name="stages">Stages where the buffers are used</param>
        /// <param name="setIndex">Descriptor set index where the buffers will be bound</param>
        /// <param name="isStorage">True for storage buffers, false for uniform buffers</param>
        private void AddUsage(IEnumerable<BufferDescriptor> buffers, ResourceStages stages, int setIndex, bool isStorage)
        {
            foreach (BufferDescriptor buffer in buffers)
            {
                _resourceUsages[setIndex].Add(new ResourceUsage(
                    buffer.Binding,
                    1,
                    isStorage ? ResourceType.StorageBuffer : ResourceType.UniformBuffer,
                    stages));
            }
        }

        /// <summary>
        /// Adds texture usage information to the list of usages.
        /// </summary>
        /// <param name="textures">Textures to be added</param>
        /// <param name="stages">Stages where the textures are used</param>
        /// <param name="setIndex">Descriptor set index where the textures will be bound</param>
        /// <param name="isImage">True for images, false for textures</param>
        private void AddUsage(IEnumerable<TextureDescriptor> textures, ResourceStages stages, int setIndex, bool isImage)
        {
            foreach (TextureDescriptor texture in textures)
            {
                bool isBuffer = (texture.Type & SamplerType.Mask) == SamplerType.TextureBuffer;

                ResourceType type = isBuffer
                    ? (isImage ? ResourceType.BufferImage : ResourceType.BufferTexture)
                    : (isImage ? ResourceType.Image : ResourceType.TextureAndSampler);

                _resourceUsages[setIndex].Add(new ResourceUsage(texture.Binding, texture.ArrayLength, type, stages));
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
            var descriptors = new ResourceDescriptorCollection[TotalSets];
            var usages = new ResourceUsageCollection[TotalSets];

            for (int index = 0; index < TotalSets; index++)
            {
                descriptors[index] = new ResourceDescriptorCollection(_resourceDescriptors[index].ToArray().AsReadOnly());
                usages[index] = new ResourceUsageCollection(_resourceUsages[index].ToArray().AsReadOnly());
            }

            ResourceLayout resourceLayout = new(descriptors.AsReadOnly(), usages.AsReadOnly());

            if (pipeline.HasValue)
            {
                return new ShaderInfo(_fragmentOutputMap, resourceLayout, pipeline.Value, fromCache);
            }
            else
            {
                return new ShaderInfo(_fragmentOutputMap, resourceLayout, fromCache);
            }
        }

        /// <summary>
        /// Builds shader information for shaders from the disk cache.
        /// </summary>
        /// <param name="context">GPU context that owns the shaders</param>
        /// <param name="programs">Shaders from the disk cache</param>
        /// <param name="pipeline">Optional pipeline for background compilation</param>
        /// <param name="tfEnabled">Indicates if the graphics shader is used with transform feedback enabled</param>
        /// <returns>Shader information</returns>
        public static ShaderInfo BuildForCache(
            GpuContext context,
            IEnumerable<CachedShaderStage> programs,
            ProgramPipelineState? pipeline,
            bool tfEnabled)
        {
            ShaderInfoBuilder builder = new(context, tfEnabled);

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
        /// <param name="fromCache">True if the compute shader comes from a disk cache, false otherwise</param>
        /// <returns>Shader information</returns>
        public static ShaderInfo BuildForCompute(GpuContext context, ShaderProgramInfo info, bool fromCache = false)
        {
            ShaderInfoBuilder builder = new(context, tfEnabled: false, vertexAsCompute: false);

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
        public static ShaderInfo BuildForVertexAsCompute(GpuContext context, ShaderProgramInfo info, bool tfEnabled, bool fromCache = false)
        {
            ShaderInfoBuilder builder = new(context, tfEnabled, vertexAsCompute: true);

            builder.AddStageInfo(info, vertexAsCompute: true);

            return builder.Build(null, fromCache);
        }
    }
}
