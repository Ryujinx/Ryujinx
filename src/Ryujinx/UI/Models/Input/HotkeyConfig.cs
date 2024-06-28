using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Common.Configuration.Hid;

namespace Ryujinx.Ava.UI.Models.Input
{
    public class HotkeyConfig : BaseModel
    {
        private Key _toggleVSyncMode;
        public Key ToggleVSyncMode
        {
            get => _toggleVSyncMode;
            set
            {
                _toggleVSyncMode = value;
                OnPropertyChanged();
            }
        }

        private Key _screenshot;
        public Key Screenshot
        {
            get => _screenshot;
            set
            {
                _screenshot = value;
                OnPropertyChanged();
            }
        }

        private Key _showUI;
        public Key ShowUI
        {
            get => _showUI;
            set
            {
                _showUI = value;
                OnPropertyChanged();
            }
        }

        private Key _pause;
        public Key Pause
        {
            get => _pause;
            set
            {
                _pause = value;
                OnPropertyChanged();
            }
        }

        private Key _toggleMute;
        public Key ToggleMute
        {
            get => _toggleMute;
            set
            {
                _toggleMute = value;
                OnPropertyChanged();
            }
        }

        private Key _resScaleUp;
        public Key ResScaleUp
        {
            get => _resScaleUp;
            set
            {
                _resScaleUp = value;
                OnPropertyChanged();
            }
        }

        private Key _resScaleDown;
        public Key ResScaleDown
        {
            get => _resScaleDown;
            set
            {
                _resScaleDown = value;
                OnPropertyChanged();
            }
        }

        private Key _volumeUp;
        public Key VolumeUp
        {
            get => _volumeUp;
            set
            {
                _volumeUp = value;
                OnPropertyChanged();
            }
        }

        private Key _volumeDown;
        public Key VolumeDown
        {
            get => _volumeDown;
            set
            {
                _volumeDown = value;
                OnPropertyChanged();
            }
        }

        private Key _customVSyncIntervalIncrement;
        public Key CustomVSyncIntervalIncrement
        {
            get => _customVSyncIntervalIncrement;
            set
            {
                _customVSyncIntervalIncrement = value;
                OnPropertyChanged();
            }
        }

        private Key _customVSyncIntervalDecrement;
        public Key CustomVSyncIntervalDecrement
        {
            get => _customVSyncIntervalDecrement;
            set
            {
                _customVSyncIntervalDecrement = value;
                OnPropertyChanged();
            }
        }

        public HotkeyConfig(KeyboardHotkeys config)
        {
            if (config != null)
            {
                ToggleVSyncMode = config.ToggleVSyncMode;
                Screenshot = config.Screenshot;
                ShowUI = config.ShowUI;
                Pause = config.Pause;
                ToggleMute = config.ToggleMute;
                ResScaleUp = config.ResScaleUp;
                ResScaleDown = config.ResScaleDown;
                VolumeUp = config.VolumeUp;
                VolumeDown = config.VolumeDown;
                CustomVSyncIntervalIncrement = config.CustomVSyncIntervalIncrement;
                CustomVSyncIntervalDecrement = config.CustomVSyncIntervalDecrement;
            }
        }

        public KeyboardHotkeys GetConfig()
        {
            var config = new KeyboardHotkeys
            {
                ToggleVSyncMode = ToggleVSyncMode,
                Screenshot = Screenshot,
                ShowUI = ShowUI,
                Pause = Pause,
                ToggleMute = ToggleMute,
                ResScaleUp = ResScaleUp,
                ResScaleDown = ResScaleDown,
                VolumeUp = VolumeUp,
                VolumeDown = VolumeDown,
                CustomVSyncIntervalIncrement = CustomVSyncIntervalIncrement,
                CustomVSyncIntervalDecrement = CustomVSyncIntervalDecrement,
            };

            return config;
        }
    }
}
