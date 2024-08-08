namespace Ryujinx.Graphics.GAL
{
    public struct ShaderInfo
    {
        public int FragmentOutputMap { get; }
        public ResourceLayout ResourceLayout { get; }
        public ComputeSize ComputeLocalSize { get; }
        public ProgramPipelineState? State { get; }
        public bool FromCache { get; set; }

        public ShaderInfo(
            int fragmentOutputMap,
            ResourceLayout resourceLayout,
            ComputeSize computeLocalSize,
            ProgramPipelineState? state,
            bool fromCache = false)
        {
            FragmentOutputMap = fragmentOutputMap;
            ResourceLayout = resourceLayout;
            ComputeLocalSize = computeLocalSize;
            State = state;
            FromCache = fromCache;
        }
    }
}
