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

            isDirty |= !config.Hid.Hotkeys.Value.Equals(KeyboardHotkey.GetConfig());

            return isDirty;
        }

        public void Save(ConfigurationState config)
        {
            config.Hid.Hotkeys.Value = KeyboardHotkey.GetConfig();
        }
    }
}
