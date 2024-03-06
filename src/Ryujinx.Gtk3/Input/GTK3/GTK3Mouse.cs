using Ryujinx.Common.Configuration.Hid;
using System;
using System.Drawing;
using System.Numerics;

namespace Ryujinx.Input.GTK3
{
    public class GTK3Mouse : IMouse
    {
        private GTK3MouseDriver _driver;

        public GamepadFeaturesFlag Features => throw new NotImplementedException();

        public string Id => "0";

        public string Name => "GTKMouse";

        public bool IsConnected => true;

        public bool[] Buttons => _driver.PressedButtons;

        public GTK3Mouse(GTK3MouseDriver driver)
        {
            _driver = driver;
        }

        public Size ClientSize => _driver.GetClientSize();

        public Vector2 GetPosition()
        {
            return _driver.CurrentPosition;
        }

        public Vector2 GetScroll()
        {
            return _driver.Scroll;
        }

        public GamepadStateSnapshot GetMappedStateSnapshot()
        {
            throw new NotImplementedException();
        }

        public Vector3 GetMotionData(MotionInputId inputId)
        {
            throw new NotImplementedException();
        }

        public GamepadStateSnapshot GetStateSnapshot()
        {
            throw new NotImplementedException();
        }

        public (float, float) GetStick(StickInputId inputId)
        {
            throw new NotImplementedException();
        }

        public bool IsButtonPressed(MouseButton button)
        {
            return _driver.IsButtonPressed(button);
        }

        public bool IsPressed(GamepadButtonInputId inputId)
        {
            throw new NotImplementedException();
        }

        public void Rumble(float lowFrequency, float highFrequency, uint durationMs)
        {
            throw new NotImplementedException();
        }

        public void SetConfiguration(InputConfig configuration)
        {
            throw new NotImplementedException();
        }

        public void SetTriggerThreshold(float triggerThreshold)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _driver = null;
        }
    }
}
