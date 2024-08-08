using Ryujinx.Graphics.GAL;

namespace Ryujinx.Graphics.Metal
{
    readonly struct ResourceBindingSegment
    {
        public readonly int Binding;
        public readonly int Count;
        public readonly ResourceType Type;
        public readonly ResourceStages Stages;
        public readonly bool IsArray;

        public ResourceBindingSegment(int binding, int count, ResourceType type, ResourceStages stages, bool isArray)
        {
            Binding = binding;
            Count = count;
            Type = type;
            Stages = stages;
            IsArray = isArray;
        }
    }
}
