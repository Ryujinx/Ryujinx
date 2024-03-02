using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Ryujinx.Input;
using Ryujinx.Input.Assigner;
using System;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Helpers
{
    internal class ButtonKeyAssigner
    {
        internal class ButtonAssignedEventArgs : EventArgs
        {
            public ToggleButton Button { get; }
            public ButtonValue? ButtonValue { get; }

            public ButtonAssignedEventArgs(ToggleButton button, ButtonValue? buttonValue)
            {
                Button = button;
                ButtonValue = buttonValue;
            }
        }

        public ToggleButton ToggledButton { get; set; }

        private bool _isWaitingForInput;
        private bool _shouldUnbind;
        public event EventHandler<ButtonAssignedEventArgs> ButtonAssigned;

        public ButtonKeyAssigner(ToggleButton toggleButton)
        {
            ToggledButton = toggleButton;
        }

        public async void GetInputAndAssign(IButtonAssigner assigner, IKeyboard keyboard = null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ToggledButton.IsChecked = true;
            });

            if (_isWaitingForInput)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Cancel();
                });

                return;
            }

            _isWaitingForInput = true;

            assigner.Initialize();

            await Task.Run(async () =>
            {
                while (true)
                {
                    if (!_isWaitingForInput)
                    {
                        return;
                    }

                    await Task.Delay(10);

                    assigner.ReadInput();

                    if (assigner.HasAnyButtonPressed() || assigner.ShouldCancel() || (keyboard != null && keyboard.IsPressed(Key.Escape)))
                    {
                        break;
                    }
                }
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ButtonValue? pressedButton = assigner.GetPressedButton();

                if (_shouldUnbind)
                {
                    pressedButton = null;
                }

                _shouldUnbind = false;
                _isWaitingForInput = false;

                ToggledButton.IsChecked = false;

                ButtonAssigned?.Invoke(this, new ButtonAssignedEventArgs(ToggledButton, pressedButton));

            });
        }

        public void Cancel(bool shouldUnbind = false)
        {
            _isWaitingForInput = false;
            ToggledButton.IsChecked = false;
            _shouldUnbind = shouldUnbind;
        }
    }
}
