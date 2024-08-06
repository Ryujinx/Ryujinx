using System.Runtime.InteropServices;

namespace Ryujinx.Common.GraphicsDriver.NVAPI
{
    enum NvdrsSettingType : uint
    {
        NvdrsDwordType,
        NvdrsBinaryType,
        NvdrsStringType,
        NvdrsWstringType,
    }

    enum NvdrsSettingLocation : uint
    {
        NvdrsCurrentProfileLocation,
        NvdrsGlobalProfileLocation,
        NvdrsBaseProfileLocation,
        NvdrsDefaultProfileLocation,
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x3020)]
    struct NvdrsSetting
    {
        [FieldOffset(0x0)]
        public uint Version;
        [FieldOffset(0x4)]
        public NvapiUnicodeString SettingName;
        [FieldOffset(0x1004)]
        public NvapiSettingId SettingId;
        [FieldOffset(0x1008)]
        public NvdrsSettingType SettingType;
        [FieldOffset(0x100C)]
        public NvdrsSettingLocation SettingLocation;
        [FieldOffset(0x1010)]
        public uint IsCurrentPredefined;
        [FieldOffset(0x1014)]
        public uint IsPredefinedValid;

        [FieldOffset(0x1018)]
        public uint PredefinedValue;
        [FieldOffset(0x1018)]
        public NvapiUnicodeString PredefinedString;

        [FieldOffset(0x201C)]
        public uint CurrentValue;
        [FieldOffset(0x201C)]
        public NvapiUnicodeString CurrentString;
    }
}
