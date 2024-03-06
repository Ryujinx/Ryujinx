using Gdk;
using Gtk;
using System;
using System.Numerics;
using Size = System.Drawing.Size;

namespace Ryujinx.Input.GTK3
{
    public class GTK3MouseDriver : IGamepadDriver
    {
        private Widget _widget;
        private bool _isDisposed;

        public bool[] PressedButtons { get; }

        public Vector2 CurrentPosition { get; private set; }
        public Vector2 Scroll { get; private set; }

        public GTK3MouseDriver(Widget parent)
        {
            _widget = parent;

            _widget.MotionNotifyEvent += Parent_MotionNotifyEvent;
            _widget.ButtonPressEvent += Parent_ButtonPressEvent;
            _widget.ButtonReleaseEvent += Parent_ButtonReleaseEvent;
            _widget.ScrollEvent += Parent_ScrollEvent;

            PressedButtons = new bool[(int)MouseButton.Count];
        }


        [GLib.ConnectBefore]
        private void Parent_ScrollEvent(object o, ScrollEventArgs args)
        {
            Scroll = new Vector2((float)args.Event.X, (float)args.Event.Y);
        }

        [GLib.ConnectBefore]
        private void Parent_ButtonReleaseEvent(object o, ButtonReleaseEventArgs args)
        {
            PressedButtons[args.Event.Button - 1] = false;
        }

        [GLib.ConnectBefore]
        private void Parent_ButtonPressEvent(object o, ButtonPressEventArgs args)
        {
            PressedButtons[args.Event.Button - 1] = true;
        }

        [GLib.ConnectBefore]
        private void Parent_MotionNotifyEvent(object o, MotionNotifyEventArgs args)
        {
            if (args.Event.Device.InputSource == InputSource.Mouse)
            {
                CurrentPosition = new Vector2((float)args.Event.X, (float)args.Event.Y);
            }
        }

        public bool IsButtonPressed(MouseButton button)
        {
            return PressedButtons[(int)button];
        }

        public Size GetClientSize()
        {
            return new Size(_widget.AllocatedWidth, _widget.AllocatedHeight);
        }

        public string DriverName => "GTK3";

        public event Action<string> OnGamepadConnected
        {
            add { }
            remove { }
        }

        public event Action<string> OnGamepadDisconnected
        {
            add { }
            remove { }
        }

        public ReadOnlySpan<string> GamepadsIds => new[] { "0" };

        public IGamepad GetGamepad(string id)
        {
            return new GTK3Mouse(this);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            GC.SuppressFinalize(this);

            _isDisposed = true;

            _widget.MotionNotifyEvent -= Parent_MotionNotifyEvent;
            _widget.ButtonPressEvent -= Parent_ButtonPressEvent;
            _widget.ButtonReleaseEvent -= Parent_ButtonReleaseEvent;

            _widget = null;
        }
    }
}
