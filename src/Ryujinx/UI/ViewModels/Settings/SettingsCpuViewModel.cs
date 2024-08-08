using Ryujinx.Common.Configuration;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Ava.UI.ViewModels.Settings
{
    public class SettingsCpuViewModel : BaseModel
    {
        public event Action DirtyEvent;

        public bool IsHypervisorAvailable => OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        private bool _enablePptc;
        public bool EnablePptc
        {
            get => _enablePptc;
            set
            {
                _enablePptc = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _useHypervisor;
        public bool UseHypervisor
        {
            get => _useHypervisor;
            set
            {
                _useHypervisor = value;
                DirtyEvent?.Invoke();
            }
        }

        private int _memoryMode;
        public int MemoryMode
        {
            get => _memoryMode;
            set
            {
                _memoryMode = value;
                DirtyEvent?.Invoke();
            }
        }

        public SettingsCpuViewModel()
        {
            ConfigurationState config = ConfigurationState.Instance;

            EnablePptc = config.System.EnablePtc;
            MemoryMode = (int)config.System.MemoryManagerMode.Value;
            UseHypervisor = config.System.UseHypervisor;
        }

        public bool CheckIfModified(ConfigurationState config)
        {
            bool isDirty = false;

            isDirty |= config.System.EnablePtc.Value != EnablePptc;
            isDirty |= config.System.MemoryManagerMode.Value != (MemoryManagerMode)MemoryMode;
            isDirty |= config.System.UseHypervisor.Value != UseHypervisor;

            return isDirty;
        }

        public void Save(ConfigurationState config)
        {
            config.System.EnablePtc.Value = EnablePptc;
            config.System.MemoryManagerMode.Value = (MemoryManagerMode)MemoryMode;
            config.System.UseHypervisor.Value = UseHypervisor;
        }
    }
}
