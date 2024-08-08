using Avalonia.Collections;
using Avalonia.Threading;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.ViewModels.Settings
{
    public class SettingsNetworkViewModel : BaseModel
    {
        public event Action DirtyEvent;

        private readonly Dictionary<string, string> _networkInterfaces = new();
        public AvaloniaList<string> NetworkInterfaceList
        {
            get => new(_networkInterfaces.Keys);
        }

        private bool _enableInternetAccess;
        public bool EnableInternetAccess
        {
            get => _enableInternetAccess;
            set
            {
                _enableInternetAccess = value;
                DirtyEvent?.Invoke();
            }
        }

        private int _networkInterfaceIndex;
        public int NetworkInterfaceIndex
        {
            get => _networkInterfaceIndex;
            set
            {
                _networkInterfaceIndex = value != -1 ? value : 0;
                OnPropertyChanged();
                DirtyEvent?.Invoke();
            }
        }

        private int _multiplayerModeIndex;
        public int MultiplayerModeIndex
        {
            get => _multiplayerModeIndex;
            set
            {
                _multiplayerModeIndex = value;
                DirtyEvent?.Invoke();
            }
        }

        public SettingsNetworkViewModel()
        {
            ConfigurationState config = ConfigurationState.Instance;

            Task.Run(PopulateNetworkInterfaces);

            // LAN interface index is loaded asynchronously in PopulateNetworkInterfaces()
            EnableInternetAccess = config.System.EnableInternetAccess;
            MultiplayerModeIndex = (int)config.Multiplayer.Mode.Value;
        }

        private async Task PopulateNetworkInterfaces()
        {
            _networkInterfaces.Clear();
            _networkInterfaces.Add(LocaleManager.Instance[LocaleKeys.NetworkInterfaceDefault], "0");

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _networkInterfaces.Add(networkInterface.Name, networkInterface.Id);
                });
            }

            // Network interface index needs to be loaded during the async method, or it will always return 0.
            NetworkInterfaceIndex = _networkInterfaces.Values.ToList().IndexOf(ConfigurationState.Instance.Multiplayer.LanInterfaceId.Value);
        }

        public bool CheckIfModified(ConfigurationState config)
        {
            bool isDirty = false;

            isDirty |= config.System.EnableInternetAccess.Value != EnableInternetAccess;
            isDirty |= config.Multiplayer.LanInterfaceId.Value != _networkInterfaces[NetworkInterfaceList[NetworkInterfaceIndex]];
            isDirty |= config.Multiplayer.Mode.Value != (MultiplayerMode)MultiplayerModeIndex;

            return isDirty;
        }

        public void Save(ConfigurationState config)
        {
            config.System.EnableInternetAccess.Value = EnableInternetAccess;
            config.Multiplayer.LanInterfaceId.Value = _networkInterfaces[NetworkInterfaceList[NetworkInterfaceIndex]];
            config.Multiplayer.Mode.Value = (MultiplayerMode)MultiplayerModeIndex;
        }
    }
}
