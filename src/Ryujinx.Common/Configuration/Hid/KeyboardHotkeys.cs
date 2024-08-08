using System;

namespace Ryujinx.Common.Configuration.Hid
{
    public class KeyboardHotkeys : IEquatable<KeyboardHotkeys>
    {
        public Key ToggleVsync { get; set; }
        public Key Screenshot { get; set; }
        public Key ShowUI { get; set; }
        public Key Pause { get; set; }
        public Key ToggleMute { get; set; }
        public Key ResScaleUp { get; set; }
        public Key ResScaleDown { get; set; }
        public Key VolumeUp { get; set; }
        public Key VolumeDown { get; set; }

        public bool Equals(KeyboardHotkeys other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return ToggleVsync == other.ToggleVsync &&
                   Screenshot == other.Screenshot &&
                   ShowUI == other.ShowUI &&
                   Pause == other.Pause &&
                   ToggleMute == other.ToggleMute &&
                   ResScaleUp == other.ResScaleUp &&
                   ResScaleDown == other.ResScaleDown &&
                   VolumeUp == other.VolumeUp &&
                   VolumeDown == other.VolumeDown;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((KeyboardHotkeys)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)ToggleVsync;
                hashCode = (hashCode * 397) ^ (int)Screenshot;
                hashCode = (hashCode * 397) ^ (int)ShowUI;
                hashCode = (hashCode * 397) ^ (int)Pause;
                hashCode = (hashCode * 397) ^ (int)ToggleMute;
                hashCode = (hashCode * 397) ^ (int)ResScaleUp;
                hashCode = (hashCode * 397) ^ (int)ResScaleDown;
                hashCode = (hashCode * 397) ^ (int)VolumeUp;
                hashCode = (hashCode * 397) ^ (int)VolumeDown;
                return hashCode;
            }
        }
    }
}
