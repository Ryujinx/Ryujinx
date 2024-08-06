using Ryujinx.Horizon.Sdk.Hid.Npad;
using System.Numerics;

namespace Ryujinx.Horizon.Sdk.Hid.SixAxis
{
    public struct SixAxisInput
    {
        public PlayerIndex PlayerId;
        public Vector3 Accelerometer;
        public Vector3 Gyroscope;
        public Vector3 Rotation;
        public float[] Orientation;
    }
}
