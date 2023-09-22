namespace Ryujinx.Graphics.GAL
{
    public enum PresentIntervalState
    {
        Switch = 0,
        Unbounded = 1 << 0,
        Custom = 1 << 1
    }
}
