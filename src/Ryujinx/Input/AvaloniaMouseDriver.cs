using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Ryujinx.Input;
using System;
using System.Numerics;
using MouseButton = Ryujinx.Input.MouseButton;
using Size = System.Drawing.Size;

namespace Ryujinx.Ava.Input
{
    internal class AvaloniaMouseDriver : IGamepadDriver
    {
        private Control _widget;
        private bool _isDisposed;
        private Size _size;
        private readonly TopLevel _window;

        public bool[] PressedButtons { get; }
        public Vector2 CurrentPosition { get; private set; }
        public Vector2 Scroll { get; private set; }

        public string DriverName => "AvaloniaMouseDriver";
        public ReadOnlySpan<string> GamepadsIds => new[] { "0" };

        public AvaloniaMouseDriver(TopLevel window, Control parent)
        {
            _widget = parent;
            _window = window;

            _widget.PointerMoved += Parent_PointerMovedEvent;
            _widget.PointerPressed += Parent_PointerPressedEvent;
            _widget.PointerReleased += Parent_PointerReleasedEvent;
            _widget.PointerWheelChanged += Parent_PointerWheelChanged;

            _window.PointerMoved += Parent_PointerMovedEvent;
            _window.PointerPressed += Parent_PointerPressedEvent;
            _window.PointerReleased += Parent_PointerReleasedEvent;
            _window.PointerWheelChanged += Parent_PointerWheelChanged;

            PressedButtons = new bool[(int)MouseButton.Count];

            _size = new Size((int)parent.Bounds.Width, (int)parent.Bounds.Height);

            parent.GetObservable(Visual.BoundsProperty).Subscribe(Resized);
        }

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

        private void Resized(Rect rect)
        {
            _size = new Size((int)rect.Width, (int)rect.Height);
        }

        private void Parent_PointerWheelChanged(object o, PointerWheelEventArgs args)
        {
            Scroll = new Vector2((float)args.Delta.X, (float)args.Delta.Y);
        }

        private void Parent_PointerReleasedEvent(object o, PointerReleasedEventArgs args)
        {
            uint button = (uint)args.InitialPressMouseButton - 1;

            if ((uint)PressedButtons.Length > button)
            {
                PressedButtons[button] = false;
            }
        }
        private void Parent_PointerPressedEvent(object o, PointerPressedEventArgs args)
        {
            uint button = (uint)args.GetCurrentPoint(_widget).Properties.PointerUpdateKind;

            if ((uint)PressedButtons.Length > button)
            {
                PressedButtons[button] = true;
            }
        }

        private void Parent_PointerMovedEvent(object o, PointerEventArgs args)
        {
            Point position = args.GetPosition(_widget);

            CurrentPosition = new Vector2((float)position.X, (float)position.Y);
        }

        public void SetMousePressed(MouseButton button)
        {
            if ((uint)PressedButtons.Length > (uint)button)
            {
                PressedButtons[(uint)button] = true;
            }
        }

        public void SetMouseReleased(MouseButton button)
        {
            if ((uint)PressedButtons.Length > (uint)button)
            {
                PressedButtons[(uint)button] = false;
            }
        }

        public void SetPosition(double x, double y)
        {
            CurrentPosition = new Vector2((float)x, (float)y);
        }

        public bool IsButtonPressed(MouseButton button)
        {
            if ((uint)PressedButtons.Length > (uint)button)
            {
                return PressedButtons[(uint)button];
            }

            return false;
        }

        public Size GetClientSize()
        {
            return _size;
        }

        public IGamepad GetGamepad(string id)
        {
            return new AvaloniaMouse(this);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _widget.PointerMoved -= Parent_PointerMovedEvent;
            _widget.PointerPressed -= Parent_PointerPressedEvent;
            _widget.PointerReleased -= Parent_PointerReleasedEvent;
            _widget.PointerWheelChanged -= Parent_PointerWheelChanged;

            _window.PointerMoved -= Parent_PointerMovedEvent;
            _window.PointerPressed -= Parent_PointerPressedEvent;
            _window.PointerReleased -= Parent_PointerReleasedEvent;
            _window.PointerWheelChanged -= Parent_PointerWheelChanged;

            _widget = null;
        }
    }
}
