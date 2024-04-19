using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Ryujinx.Ava.Input;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels.Settings;
using Ryujinx.Input;
using Ryujinx.Input.Assigner;
using Key = Ryujinx.Common.Configuration.Hid.Key;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsHotkeysView : UserControl
    {
        private readonly SettingsViewModel _viewModel;
        private ButtonKeyAssigner _currentAssigner;
        private readonly IGamepadDriver _avaloniaKeyboardDriver;

        public SettingsHotkeysView()
        {

        }

        public SettingsHotkeysView(SettingsViewModel viewModel)
        {
            _viewModel = viewModel;

            InitializeComponent();

            foreach (ILogical visual in SettingButtons.GetLogicalDescendants())
            {
                if (visual is ToggleButton button and not CheckBox)
                {
                    button.IsCheckedChanged += Button_IsCheckedChanged;
                }
            }

            _avaloniaKeyboardDriver = new AvaloniaKeyboardDriver(this);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (!_currentAssigner?.ToggledButton?.IsPointerOver ?? false)
            {
                _currentAssigner.Cancel();
            }
        }

        private void MouseClick(object sender, PointerPressedEventArgs e)
        {
            bool shouldUnbind = e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed;

            _currentAssigner?.Cancel(shouldUnbind);

            PointerPressed -= MouseClick;
        }

        private void Button_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                if ((bool)button.IsChecked)
                {
                    if (_currentAssigner != null && button == _currentAssigner.ToggledButton)
                    {
                        return;
                    }

                    if (_currentAssigner == null)
                    {
                        _currentAssigner = new ButtonKeyAssigner(button);

                        this.Focus(NavigationMethod.Pointer);

                        PointerPressed += MouseClick;

                        var keyboard = (IKeyboard)_avaloniaKeyboardDriver.GetGamepad("0");
                        IButtonAssigner assigner = new KeyboardKeyAssigner(keyboard);

                        _currentAssigner.ButtonAssigned += (sender, e) =>
                        {
                            if (e.ButtonValue.HasValue)
                            {
                                var buttonValue = e.ButtonValue.Value;

                                switch (button.Name)
                                {
                                    case "ToggleVsync":
                                        _viewModel.KeyboardHotkey.ToggleVsync = buttonValue.AsHidType<Key>();
                                        break;
                                    case "Screenshot":
                                        _viewModel.KeyboardHotkey.Screenshot = buttonValue.AsHidType<Key>();
                                        break;
                                    case "ShowUI":
                                        _viewModel.KeyboardHotkey.ShowUI = buttonValue.AsHidType<Key>();
                                        break;
                                    case "Pause":
                                        _viewModel.KeyboardHotkey.Pause = buttonValue.AsHidType<Key>();
                                        break;
                                    case "ToggleMute":
                                        _viewModel.KeyboardHotkey.ToggleMute = buttonValue.AsHidType<Key>();
                                        break;
                                    case "ResScaleUp":
                                        _viewModel.KeyboardHotkey.ResScaleUp = buttonValue.AsHidType<Key>();
                                        break;
                                    case "ResScaleDown":
                                        _viewModel.KeyboardHotkey.ResScaleDown = buttonValue.AsHidType<Key>();
                                        break;
                                    case "VolumeUp":
                                        _viewModel.KeyboardHotkey.VolumeUp = buttonValue.AsHidType<Key>();
                                        break;
                                    case "VolumeDown":
                                        _viewModel.KeyboardHotkey.VolumeDown = buttonValue.AsHidType<Key>();
                                        break;
                                }
                            }
                        };

                        _currentAssigner.GetInputAndAssign(assigner, keyboard);
                    }
                    else
                    {
                        if (_currentAssigner != null)
                        {
                            _currentAssigner.Cancel();
                            _currentAssigner = null;
                            button.IsChecked = false;
                        }
                    }
                }
                else
                {
                    _currentAssigner?.Cancel();
                    _currentAssigner = null;
                }
            }
        }

        public void Dispose()
        {
            _currentAssigner?.Cancel();
            _currentAssigner = null;

            _avaloniaKeyboardDriver.Dispose();
        }
    }
}
