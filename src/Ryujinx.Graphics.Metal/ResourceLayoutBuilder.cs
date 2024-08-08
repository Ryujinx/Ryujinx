using Ryujinx.Graphics.GAL;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class ResourceLayoutBuilder
    {
        private const int TotalSets = MetalRenderer.TotalSets;

        private readonly List<ResourceDescriptor>[] _resourceDescriptors;
        private readonly List<ResourceUsage>[] _resourceUsages;

        public ResourceLayoutBuilder()
        {
            _resourceDescriptors = new List<ResourceDescriptor>[TotalSets];
            _resourceUsages = new List<ResourceUsage>[TotalSets];

            for (int index = 0; index < TotalSets; index++)
            {
                _resourceDescriptors[index] = new();
                _resourceUsages[index] = new();
            }
        }

        public ResourceLayoutBuilder Add(ResourceStages stages, ResourceType type, int binding, bool write = false)
        {
            uint setIndex = type switch
            {
                ResourceType.UniformBuffer => Constants.ConstantBuffersSetIndex,
                ResourceType.StorageBuffer => Constants.StorageBuffersSetIndex,
                ResourceType.TextureAndSampler or ResourceType.BufferTexture => Constants.TexturesSetIndex,
                ResourceType.Image or ResourceType.BufferImage => Constants.ImagesSetIndex,
                _ => throw new ArgumentException($"Invalid resource type \"{type}\"."),
            };

            _resourceDescriptors[setIndex].Add(new ResourceDescriptor(binding, 1, type, stages));
            _resourceUsages[setIndex].Add(new ResourceUsage(binding, 1, type, stages, write));

            return this;
        }

        public ResourceLayout Build()
        {
            var descriptors = new ResourceDescriptorCollection[TotalSets];
            var usages = new ResourceUsageCollection[TotalSets];

            for (int index = 0; index < TotalSets; index++)
            {
                descriptors[index] = new ResourceDescriptorCollection(_resourceDescriptors[index].ToArray().AsReadOnly());
                usages[index] = new ResourceUsageCollection(_resourceUsages[index].ToArray().AsReadOnly());
            }

            return new ResourceLayout(descriptors.AsReadOnly(), usages.AsReadOnly());
        }
    }
}
