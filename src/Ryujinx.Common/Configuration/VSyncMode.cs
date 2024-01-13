using Ryujinx.Common.Utilities;
using System;
using System.Text.Json.Serialization;

namespace Ryujinx.Common.Configuration
{
    [JsonConverter(typeof(TypedStringEnumConverter<VSyncMode>))]
    [Flags]
    public enum VSyncMode
    {
        Switch = 0,
        Unbounded = 1 << 0,
        Custom = 1 << 1
    }
}
