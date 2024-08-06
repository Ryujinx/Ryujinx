using Ryujinx.Horizon.Common;

namespace Ryujinx.Horizon.Hid
{
    class HidResult
    {
        private const int ModuleId = 202;

        public static Result InvalidNpadDeviceType => new Result(ModuleId, 122);
        public static Result InvalidNpadIdType => new Result(ModuleId, 123);
        public static Result InvalidDeviceIndex => new Result(ModuleId, 124);
        public static Result InvalidBufferSize => new Result(ModuleId, 131);
    }
}
