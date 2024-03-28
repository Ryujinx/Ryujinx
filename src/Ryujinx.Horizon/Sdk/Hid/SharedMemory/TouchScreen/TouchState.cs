namespace Ryujinx.Horizon.Sdk.Hid
{
    struct TouchState
    {
        public ulong DeltaTime;
#pragma warning disable CS0649 // Field is never assigned to
        public TouchAttribute Attribute;
#pragma warning restore CS0649
        public uint FingerId;
        public uint X;
        public uint Y;
        public uint DiameterX;
        public uint DiameterY;
        public uint RotationAngle;
#pragma warning disable CS0169, IDE0051 // Remove unused private member
        private readonly uint _reserved;
#pragma warning restore CS0169, IDE0051
    }
}
