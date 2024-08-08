using Avalonia.Threading;
using Ryujinx.Audio.Backends.OpenAL;
using Ryujinx.Audio.Backends.SDL2;
using Ryujinx.Audio.Backends.SoundIo;
using Ryujinx.Common.Logging;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.ViewModels.Settings
{
    public class SettingsAudioViewModel : BaseModel
    {
        public event Action DirtyEvent;

        private int _audioBackend;
        public int AudioBackend
        {
            get => _audioBackend;
            set
            {
                _audioBackend = value;
                DirtyEvent?.Invoke();
            }
        }

        private float _volume;
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                DirtyEvent?.Invoke();
            }
        }

        public bool IsOpenAlEnabled { get; set; }
        public bool IsSoundIoEnabled { get; set; }
        public bool IsSDL2Enabled { get; set; }

        public SettingsAudioViewModel()
        {
            ConfigurationState config = ConfigurationState.Instance;

            Task.Run(CheckSoundBackends);

            AudioBackend = (int)config.System.AudioBackend.Value;
            Volume = config.System.AudioVolume * 100;
        }

        public async Task CheckSoundBackends()
        {
            IsOpenAlEnabled = OpenALHardwareDeviceDriver.IsSupported;
            IsSoundIoEnabled = SoundIoHardwareDeviceDriver.IsSupported;
            IsSDL2Enabled = SDL2HardwareDeviceDriver.IsSupported;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(IsOpenAlEnabled));
                OnPropertyChanged(nameof(IsSoundIoEnabled));
                OnPropertyChanged(nameof(IsSDL2Enabled));
            });
        }

        public bool CheckIfModified(ConfigurationState config)
        {
            bool isDirty = false;

            isDirty |= config.System.AudioBackend.Value != (AudioBackend)AudioBackend;
            isDirty |= config.System.AudioVolume.Value != Volume / 100;

            return isDirty;
        }

        public void Save(ConfigurationState config)
        {
            AudioBackend audioBackend = (AudioBackend)AudioBackend;
            if (audioBackend != config.System.AudioBackend.Value)
            {
                config.System.AudioBackend.Value = audioBackend;

                Logger.Info?.Print(LogClass.Application, $"AudioBackend toggled to: {audioBackend}");
            }

            config.System.AudioVolume.Value = Volume / 100;
        }
    }
}
