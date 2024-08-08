namespace Ryujinx.Graphics.GAL
{
    public readonly struct ComputeSize
    {
        public readonly static ComputeSize VtgAsCompute = new ComputeSize(32, 32, 1);

        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public ComputeSize(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
