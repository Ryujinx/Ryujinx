using Ryujinx.Common.GraphicsDriver.NVAPI;
using Ryujinx.Common.Logging;
using System;

namespace Ryujinx.Common.GraphicsDriver
{
    public static class DriverUtilities
    {
        public static void ToggleOglThreading(bool enabled)
        {
            Environment.SetEnvironmentVariable("mesa_glthread", enabled.ToString().ToLower());
            Environment.SetEnvironmentVariable("__GL_THREADED_OPTIMIZATIONS", enabled ? "1" : "0");

            ToggleNvDriverSetting(NvapiSettingId.OglThreadControlId, enabled);
        }

        public static void ToggleNvDriverSetting(NvapiSettingId id, bool enabled)
        {
            try
            {
                NVDriverHelper.SetControlOption(id, enabled);
            }
            catch
            {
                Logger.Info?.Print(LogClass.Application, "NVAPI was unreachable, no changes were made.");
            }
        }
    }
}
