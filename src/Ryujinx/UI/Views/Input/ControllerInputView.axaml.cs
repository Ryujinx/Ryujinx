using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Input;
using Ryujinx.Input.Assigner;
using System;

namespace Ryujinx.Ava.UI.Views.Input
{
    public partial class ControllerInputView : UserControl
    {
        private bool _dialogOpen;

        private ButtonKeyAssigner _currentAssigner;
        internal ControllerInputViewModel ViewModel { get; set; }

        public ControllerInputView()
        {
            DataContext = ViewModel = new ControllerInputViewModel(this);

            InitializeComponent();

            foreach (ILogical visual in SettingButtons.GetLogicalDescendants())
            {
                if (visual is ToggleButton button && visual is not CheckBox)
                {
                    button.IsCheckedChanged += Button_IsCheckedChanged;
                }
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_currentAssigner != null && _currentAssigner.ToggledButton != null && !_currentAssigner.ToggledButton.IsPointerOver)
            {
                _currentAssigner.Cancel();
            }
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

                    bool isStick = button.Tag != null && button.Tag.ToString() == "stick";

                    if (_currentAssigner == null)
                    {
                        _currentAssigner = new ButtonKeyAssigner(button);

                        this.Focus(NavigationMethod.Pointer);

                        PointerPressed += MouseClick;

                        IKeyboard keyboard = (IKeyboard)ViewModel.AvaloniaKeyboardDriver.GetGamepad("0"); // Open Avalonia keyboard for cancel operations.
                        IButtonAssigner assigner = CreateButtonAssigner(isStick);

                        _currentAssigner.ButtonAssigned += (sender, e) =>
                        {
                            if (e.IsAssigned)
                            {
                                ViewModel.IsModified = true;
                            }
                        };

                        _currentAssigner.GetInputAndAssign(assigner, keyboard);
                    }
                    else
                    {
                        if (_currentAssigner != null)
                        {
                            ToggleButton oldButton = _currentAssigner.ToggledButton;

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

        public void SaveCurrentProfile()
        {
            ViewModel.Save();
        }

        private IButtonAssigner CreateButtonAssigner(bool forStick)
        {
            IButtonAssigner assigner;

            var device = ViewModel.Devices[ViewModel.Device];

            if (device.Type == DeviceType.Keyboard)
            {
                assigner = new KeyboardKeyAssigner((IKeyboard)ViewModel.SelectedGamepad);
            }
            else if (device.Type == DeviceType.Controller)
            {
                assigner = new GamepadButtonAssigner(ViewModel.SelectedGamepad, (ViewModel.Config as StandardControllerInputConfig).TriggerThreshold, forStick);
            }
            else
            {
                throw new Exception("Controller not supported");
            }

            return assigner;
        }

        private void MouseClick(object sender, PointerPressedEventArgs e)
        {
            bool shouldUnbind = false;

            if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                shouldUnbind = true;
            }

            _currentAssigner?.Cancel(shouldUnbind);

            PointerPressed -= MouseClick;
        }

        private async void PlayerIndexBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.IsModified && !_dialogOpen)
            {
                _dialogOpen = true;

                var result = await ContentDialogHelper.CreateConfirmationDialog(
                    LocaleManager.Instance[LocaleKeys.DialogControllerSettingsModifiedConfirmMessage],
                    LocaleManager.Instance[LocaleKeys.DialogControllerSettingsModifiedConfirmSubMessage],
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                if (result == UserResult.Yes)
                {
                    ViewModel.Save();
                }

                _dialogOpen = false;

                ViewModel.IsModified = false;

                if (e.AddedItems.Count > 0)
                {
                    var player = (PlayerModel)e.AddedItems[0];
                    ViewModel.PlayerId = player.Id;
                }
            }
        }

        public void Dispose()
        {
            _currentAssigner?.Cancel();
            _currentAssigner = null;
            ViewModel.Dispose();
        }
    }
}
