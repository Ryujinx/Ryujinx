using System;
using System.Collections.ObjectModel;

namespace Ryujinx.Graphics.GAL
{
    public enum ResourceType : byte
    {
        UniformBuffer,
        StorageBuffer,
        Texture,
        Sampler,
        TextureAndSampler,
        Image,
        BufferTexture,
        BufferImage,
    }

    [Flags]
    public enum ResourceStages : byte
    {
        None = 0,
        Compute = 1 << 0,
        Vertex = 1 << 1,
        TessellationControl = 1 << 2,
        TessellationEvaluation = 1 << 3,
        Geometry = 1 << 4,
        Fragment = 1 << 5,
    }

    public readonly struct ResourceDescriptor : IEquatable<ResourceDescriptor>
    {
        public int Binding { get; }
        public int Count { get; }
        public ResourceType Type { get; }
        public ResourceStages Stages { get; }

        public ResourceDescriptor(int binding, int count, ResourceType type, ResourceStages stages)
        {
            Binding = binding;
            Count = count;
            Type = type;
            Stages = stages;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Binding, Count, Type, Stages);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceDescriptor other && Equals(other);
        }

        public bool Equals(ResourceDescriptor other)
        {
            return Binding == other.Binding && Count == other.Count && Type == other.Type && Stages == other.Stages;
        }

        public static bool operator ==(ResourceDescriptor left, ResourceDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ResourceDescriptor left, ResourceDescriptor right)
        {
            return !(left == right);
        }
    }

    public readonly struct ResourceUsage : IEquatable<ResourceUsage>
    {
        public int Binding { get; }
        public int ArrayLength { get; }
        public ResourceType Type { get; }
        public ResourceStages Stages { get; }

        public ResourceUsage(int binding, int arrayLength, ResourceType type, ResourceStages stages)
        {
            Binding = binding;
            ArrayLength = arrayLength;
            Type = type;
            Stages = stages;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Binding, ArrayLength, Type, Stages);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceUsage other && Equals(other);
        }

        public bool Equals(ResourceUsage other)
        {
            return Binding == other.Binding && ArrayLength == other.ArrayLength && Type == other.Type && Stages == other.Stages;
        }

        public static bool operator ==(ResourceUsage left, ResourceUsage right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ResourceUsage left, ResourceUsage right)
        {
            return !(left == right);
        }
    }

    public readonly struct ResourceDescriptorCollection
    {
        public ReadOnlyCollection<ResourceDescriptor> Descriptors { get; }

        public ResourceDescriptorCollection(ReadOnlyCollection<ResourceDescriptor> descriptors)
        {
            Descriptors = descriptors;
        }

        public override int GetHashCode()
        {
            HashCode hasher = new();

            if (Descriptors != null)
            {
                foreach (var descriptor in Descriptors)
                {
                    hasher.Add(descriptor);
                }
            }

            return hasher.ToHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceDescriptorCollection other && Equals(other);
        }

        public bool Equals(ResourceDescriptorCollection other)
        {
            if ((Descriptors == null) != (other.Descriptors == null))
            {
                return false;
            }

            if (Descriptors != null)
            {
                if (Descriptors.Count != other.Descriptors.Count)
                {
                    return false;
                }

                for (int index = 0; index < Descriptors.Count; index++)
                {
                    if (!Descriptors[index].Equals(other.Descriptors[index]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool operator ==(ResourceDescriptorCollection left, ResourceDescriptorCollection right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ResourceDescriptorCollection left, ResourceDescriptorCollection right)
        {
            return !(left == right);
        }
    }

    public readonly struct ResourceUsageCollection
    {
        public ReadOnlyCollection<ResourceUsage> Usages { get; }

        public ResourceUsageCollection(ReadOnlyCollection<ResourceUsage> usages)
        {
            Usages = usages;
        }
    }

    public readonly struct ResourceLayout
    {
        public ReadOnlyCollection<ResourceDescriptorCollection> Sets { get; }
        public ReadOnlyCollection<ResourceUsageCollection> SetUsages { get; }

        public ResourceLayout(
            ReadOnlyCollection<ResourceDescriptorCollection> sets,
            ReadOnlyCollection<ResourceUsageCollection> setUsages)
        {
            Sets = sets;
            SetUsages = setUsages;
        }
    }
}
