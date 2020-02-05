namespace Ryujinx.Graphics.Gpu.State
{
    /// <summary>
    /// Texture to texture (with optional resizing) copy parameters.
    /// </summary>
    struct CopyTexture
    {
        public RtFormat     Format;
        public MemoryLayout MemoryLayout;
#pragma warning disable CS0649
        public Boolean32    LinearLayout;
        public int          Depth;
        public int          Layer;
        public int          Stride;
        public int          Width;
        public int          Height;
#pragma warning restore CS0649
        public GpuVa        Address;
    }
}