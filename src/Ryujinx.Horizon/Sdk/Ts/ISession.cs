using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Sf;

namespace Ryujinx.Horizon.Sdk.Ts
{
    interface ISession : IServiceObject
    {
        Result GetTemperatureRange(out int minimumTemperature, out int maximumTemperature);
        Result GetTemperature(out int temperature);
        Result SetMeasurementMode(byte measurementMode);
    }
}
