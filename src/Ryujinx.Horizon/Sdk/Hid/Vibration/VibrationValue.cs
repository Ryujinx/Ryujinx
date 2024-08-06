using System;

namespace Ryujinx.Horizon.Sdk.Hid.Vibration
{
    public struct VibrationValue
    {
        public float AmplitudeLow;
        public float FrequencyLow;
        public float AmplitudeHigh;
        public float FrequencyHigh;

        public readonly override bool Equals(object obj)
        {
            return obj is VibrationValue value &&
                   AmplitudeLow == value.AmplitudeLow &&
                   AmplitudeHigh == value.AmplitudeHigh;
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(AmplitudeLow, AmplitudeHigh);
        }

        public static bool operator ==(VibrationValue left, VibrationValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VibrationValue left, VibrationValue right)
        {
            return !(left == right);
        }
    }
}
