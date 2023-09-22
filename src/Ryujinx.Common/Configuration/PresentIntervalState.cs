using Ryujinx.Common.Utilities;
using System;
using System.Text.Json.Serialization;

namespace Ryujinx.Common.Configuration
{
    [JsonConverter(typeof(TypedStringEnumConverter<PresentIntervalState>))]
    [Flags]
    public enum PresentIntervalState
    {
        Switch = 0,
        Unbounded = 1 << 0,
        Custom = 1 << 1
    }
}
