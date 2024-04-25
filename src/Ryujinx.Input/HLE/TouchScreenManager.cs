using Ryujinx.HLE;
using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.Horizon.Sdk.Hid;
using Ryujinx.Horizon.Sdk.Hid.SharedMemory.TouchScreen;
using System;

namespace Ryujinx.Input.HLE
{
    public class TouchScreenManager : IDisposable
    {
        private readonly IMouse _mouse;
        private Switch _device;
        private bool _wasClicking;

        public TouchScreenManager(IMouse mouse)
        {
            _mouse = mouse;
        }

        public void Initialize(Switch device)
        {
            _device = device;
        }

        public bool Update(bool isFocused, bool isClicking = false, float aspectRatio = 0)
        {
            if (!isFocused || (!_wasClicking && !isClicking))
            {
                // In case we lost focus, send the end touch.
                if (_wasClicking && !isClicking)
                {
                    MouseStateSnapshot snapshot = IMouse.GetMouseStateSnapshot(_mouse);
                    var touchPosition = IMouse.GetScreenPosition(snapshot.Position, _mouse.ClientSize, aspectRatio);

                    TouchPoint currentPoint = new()
                    {
                        Attribute = TouchAttribute.End,

                        X = (uint)touchPosition.X,
                        Y = (uint)touchPosition.Y,

                        // Placeholder values till more data is acquired
                        DiameterX = 10,
                        DiameterY = 10,
                        Angle = 90,
                    };

                    _device.Hid.Touchscreen.Update(currentPoint);

                }

                _wasClicking = false;

                _device.Hid.Touchscreen.Update();

                return false;
            }

            if (aspectRatio > 0)
            {
                MouseStateSnapshot snapshot = IMouse.GetMouseStateSnapshot(_mouse);
                var touchPosition = IMouse.GetScreenPosition(snapshot.Position, _mouse.ClientSize, aspectRatio);

                TouchAttribute attribute = TouchAttribute.None;

                if (!_wasClicking && isClicking)
                {
                    attribute = TouchAttribute.Start;
                }
                else if (_wasClicking && !isClicking)
                {
                    attribute = TouchAttribute.End;
                }

                TouchPoint currentPoint = new()
                {
                    Attribute = attribute,

                    X = (uint)touchPosition.X,
                    Y = (uint)touchPosition.Y,

                    // Placeholder values till more data is acquired
                    DiameterX = 10,
                    DiameterY = 10,
                    Angle = 90,
                };

                _device.Hid.Touchscreen.Update(currentPoint);

                _wasClicking = isClicking;

                return true;
            }

            return false;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
