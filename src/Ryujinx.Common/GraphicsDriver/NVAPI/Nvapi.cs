namespace Ryujinx.Common.GraphicsDriver.NVAPI
{
    public enum NvapiSettingId : uint
    {
        OglThreadControlId = 0x20C1221E,
        OglCplPreferDxPresentId = 0x20D690F8,
    }

    enum OglThreadControl : uint
    {
        Default = 0,
        Enabled = 1,
        Disabled = 2,
    }

    // Only present in driver versions >= 526.47
    enum OglCplDxPresent : uint
    {
        Disabled = 0,
        Enabled = 1,
        Default = 2,
    }
}
