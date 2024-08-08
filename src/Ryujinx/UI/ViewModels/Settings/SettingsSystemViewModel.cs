using Avalonia.Collections;
using Avalonia.Threading;
using LibHac.Tools.FsSystem;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Configuration.System;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TimeZone = Ryujinx.Ava.UI.Models.TimeZone;

namespace Ryujinx.Ava.UI.ViewModels.Settings
{
    public class SettingsSystemViewModel : BaseModel
    {
        public event Action DirtyEvent;

        private readonly List<string> _validTzRegions = new();
        private readonly VirtualFileSystem _virtualFileSystem;
        private readonly ContentManager _contentManager;
        private TimeZoneContentManager _timeZoneContentManager;

        private int _region;
        public int Region
        {
            get => _region;
            set
            {
                _region = value;
                DirtyEvent?.Invoke();
            }
        }

        private int _language;
        public int Language
        {
            get => _language;
            set
            {
                _language = value;
                DirtyEvent?.Invoke();
            }
        }

        private string _timeZone;
        public string TimeZone
        {
            get => _timeZone;
            set
            {
                _timeZone = value;
                OnPropertyChanged();
                DirtyEvent?.Invoke();
            }
        }

        private DateTimeOffset _currentDate;
        public DateTimeOffset CurrentDate
        {
            get => _currentDate;
            set
            {
                _currentDate = value;
                DirtyEvent?.Invoke();
            }
        }

        private TimeSpan _currentTime;
        public TimeSpan CurrentTime
        {
            get => _currentTime;
            set
            {
                _currentTime = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableVsync;
        public bool EnableVsync
        {
            get => _enableVsync;
            set
            {
                _enableVsync = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableFsIntegrityChecks;
        public bool EnableFsIntegrityChecks
        {
            get => _enableFsIntegrityChecks;
            set
            {
                _enableFsIntegrityChecks = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _ignoreMissingServices;
        public bool IgnoreMissingServices
        {
            get => _ignoreMissingServices;
            set
            {
                _ignoreMissingServices = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _expandedDramSize;
        public bool ExpandDramSize
        {
            get => _expandedDramSize;
            set
            {
                _expandedDramSize = value;
                DirtyEvent?.Invoke();
            }
        }

        internal AvaloniaList<TimeZone> TimeZones { get; set; }

        public SettingsSystemViewModel(VirtualFileSystem virtualFileSystem, ContentManager contentManager)
        {
            _virtualFileSystem = virtualFileSystem;
            _contentManager = contentManager;

            ConfigurationState config = ConfigurationState.Instance;

            TimeZones = new();

            if (Program.PreviewerDetached)
            {
                Task.Run(LoadTimeZones);
            }

            Region = (int)config.System.Region.Value;
            Language = (int)config.System.Language.Value;
            TimeZone = config.System.TimeZone;

            DateTime currentHostDateTime = DateTime.Now;
            TimeSpan systemDateTimeOffset = TimeSpan.FromSeconds(config.System.SystemTimeOffset);
            DateTime currentDateTime = currentHostDateTime.Add(systemDateTimeOffset);

            CurrentDate = currentDateTime.Date;
            CurrentTime = currentDateTime.TimeOfDay;

            EnableVsync = config.Graphics.EnableVsync;
            EnableFsIntegrityChecks = config.System.EnableFsIntegrityChecks;
            ExpandDramSize = config.System.ExpandRam;
            IgnoreMissingServices = config.System.IgnoreMissingServices;
        }

        public async Task LoadTimeZones()
        {
            _timeZoneContentManager = new TimeZoneContentManager();

            _timeZoneContentManager.InitializeInstance(_virtualFileSystem, _contentManager, IntegrityCheckLevel.None);

            foreach ((int offset, string location, string abbr) in _timeZoneContentManager.ParseTzOffsets())
            {
                int hours = Math.DivRem(offset, 3600, out int seconds);
                int minutes = Math.Abs(seconds) / 60;

                string abbr2 = abbr.StartsWith('+') || abbr.StartsWith('-') ? string.Empty : abbr;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TimeZones.Add(new TimeZone($"UTC{hours:+0#;-0#;+00}:{minutes:D2}", location, abbr2));

                    _validTzRegions.Add(location);
                });
            }
        }

        public void ValidateAndSetTimeZone(string location)
        {
            if (_validTzRegions.Contains(location))
            {
                TimeZone = location;
            }
        }


        public bool CheckIfModified(ConfigurationState config)
        {
            bool isDirty = false;

            isDirty |= config.System.Region.Value != (Region)Region;
            isDirty |= config.System.Language.Value != (Language)Language;

            if (_validTzRegions.Contains(TimeZone))
            {
                isDirty |= config.System.TimeZone.Value != TimeZone;
            }

            // SystemTimeOffset will always have changed, so we don't check it here

            isDirty |= config.Graphics.EnableVsync.Value != EnableVsync;
            isDirty |= config.System.EnableFsIntegrityChecks.Value != EnableFsIntegrityChecks;
            isDirty |= config.System.ExpandRam.Value != ExpandDramSize;
            isDirty |= config.System.IgnoreMissingServices.Value != IgnoreMissingServices;

            return isDirty;
        }

        public void Save(ConfigurationState config)
        {
            config.System.Region.Value = (Region)Region;
            config.System.Language.Value = (Language)Language;

            if (_validTzRegions.Contains(TimeZone))
            {
                config.System.TimeZone.Value = TimeZone;
            }

            config.System.SystemTimeOffset.Value = Convert.ToInt64((CurrentDate.ToUnixTimeSeconds() + CurrentTime.TotalSeconds) - DateTimeOffset.Now.ToUnixTimeSeconds());

            config.Graphics.EnableVsync.Value = EnableVsync;
            config.System.EnableFsIntegrityChecks.Value = EnableFsIntegrityChecks;
            config.System.ExpandRam.Value = ExpandDramSize;
            config.System.IgnoreMissingServices.Value = IgnoreMissingServices;
        }
    }
}
