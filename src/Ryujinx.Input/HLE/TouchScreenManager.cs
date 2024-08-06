using Ryujinx.HLE;
using Ryujinx.Horizon;
using Ryujinx.Horizon.Sdk.Hid;
using System;

namespace Ryujinx.Input.HLE
{
    public class TouchScreenManager : IDisposable
    {
        private readonly IMouse _mouse;
        private bool _wasClicking;

        public TouchScreenManager(IMouse mouse)
        {
            _mouse = mouse;
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

                    HorizonStatic.Hid.Touchscreen.Update(currentPoint);
                }

                _wasClicking = false;

                HorizonStatic.Hid.Touchscreen.Update();

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

                HorizonStatic.Hid.Touchscreen.Update(currentPoint);

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
