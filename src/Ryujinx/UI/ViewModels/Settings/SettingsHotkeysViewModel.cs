using Ryujinx.Ava.UI.Models.Input;
using Ryujinx.UI.Common.Configuration;
using System;

namespace Ryujinx.Ava.UI.ViewModels.Settings
{
    public class SettingsHotkeysViewModel : BaseModel
    {
        public event Action DirtyEvent;

        public HotkeyConfig KeyboardHotkey { get; set; }

        public SettingsHotkeysViewModel()
        {
            ConfigurationState config = ConfigurationState.Instance;

            KeyboardHotkey = new HotkeyConfig(config.Hid.Hotkeys.Value);
            KeyboardHotkey.PropertyChanged += (_, _) => DirtyEvent?.Invoke();
        }

        public bool CheckIfModified(ConfigurationState config)
        {
            bool isDirty = false;

            var hotkeys = KeyboardHotkey.GetConfig();

            isDirty |= config.Hid.Hotkeys.Value.ToggleVsync != hotkeys.ToggleVsync;
            isDirty |= config.Hid.Hotkeys.Value.Screenshot != hotkeys.Screenshot;
            isDirty |= config.Hid.Hotkeys.Value.ShowUI != hotkeys.ShowUI;
            isDirty |= config.Hid.Hotkeys.Value.Pause != hotkeys.Pause;
            isDirty |= config.Hid.Hotkeys.Value.ToggleMute != hotkeys.ToggleMute;
            isDirty |= config.Hid.Hotkeys.Value.ResScaleUp != hotkeys.ResScaleUp;
            isDirty |= config.Hid.Hotkeys.Value.ResScaleDown != hotkeys.ResScaleDown;
            isDirty |= config.Hid.Hotkeys.Value.VolumeUp != hotkeys.VolumeUp;
            isDirty |= config.Hid.Hotkeys.Value.VolumeDown != hotkeys.VolumeDown;

            return isDirty;
        }

        public void Save(ConfigurationState config)
        {
            config.Hid.Hotkeys.Value = KeyboardHotkey.GetConfig();
        }
    }
}
