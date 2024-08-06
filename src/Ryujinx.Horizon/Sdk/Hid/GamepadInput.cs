using Ryujinx.Horizon.Sdk.Hid.Npad;

namespace Ryujinx.Horizon.Sdk.Hid
{
    public struct GamepadInput
    {
        public PlayerIndex PlayerId;
        public ControllerKeys Buttons;
        public JoystickPosition LStick;
        public JoystickPosition RStick;
    }
}
