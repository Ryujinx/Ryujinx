using Avalonia.Controls;
using Avalonia.Threading;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.GraphicsDriver;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.ViewModels.Settings
{
    public class SettingsGraphicsViewModel : BaseModel
    {
        public event Action DirtyEvent;

        public ObservableCollection<ComboBoxItem> AvailableGpus { get; set; }
        public bool ColorSpacePassthroughAvailable => OperatingSystem.IsMacOS();
        public bool IsOpenGLAvailable => !OperatingSystem.IsMacOS();
        public bool IsCustomResolutionScaleActive => _resolutionScale == 4;
        public bool IsScalingFilterActive => _scalingFilter == (int)Ryujinx.Common.Configuration.ScalingFilter.Fsr;
        public bool IsVulkanSelected => GraphicsBackendIndex == 0;
        public string ScalingFilterLevelText => ScalingFilterLevel.ToString("0");
        private readonly List<string> _gpuIds = new();

        private int _graphicsBackendIndex;
        public int GraphicsBackendIndex
        {
            get => _graphicsBackendIndex;
            set
            {
                _graphicsBackendIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsVulkanSelected));
                DirtyEvent?.Invoke();
            }
        }

        private int _preferredGpuIndex;
        public int PreferredGpuIndex
        {
            get => _preferredGpuIndex;
            set
            {
                _preferredGpuIndex = value;
                OnPropertyChanged();
                DirtyEvent?.Invoke();
            }
        }


        private bool _isVulkanAvailable = true;
        public bool IsVulkanAvailable
        {
            get => _isVulkanAvailable;
            set
            {
                _isVulkanAvailable = value;
                OnPropertyChanged();
            }
        }

        private bool _enableShaderCache;
        public bool EnableShaderCache
        {
            get => _enableShaderCache;
            set
            {
                _enableShaderCache = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableTextureRecompression;
        public bool EnableTextureRecompression
        {
            get => _enableTextureRecompression;
            set
            {
                _enableTextureRecompression = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableMacroHLE;
        public bool EnableMacroHLE
        {
            get => _enableMacroHLE;
            set
            {
                _enableMacroHLE = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableColorSpacePassthrough;
        public bool EnableColorSpacePassthrough
        {
            get => _enableColorSpacePassthrough;
            set
            {
                _enableColorSpacePassthrough = value;
                DirtyEvent?.Invoke();
            }
        }

        private int _resolutionScale;
        public int ResolutionScale
        {
            get => _resolutionScale;
            set
            {
                _resolutionScale = value;

                OnPropertyChanged(nameof(CustomResolutionScale));
                OnPropertyChanged(nameof(IsCustomResolutionScaleActive));
                DirtyEvent?.Invoke();
            }
        }

        private float _customResolutionScale;
        public float CustomResolutionScale
        {
            get => _customResolutionScale;
            set
            {
                _customResolutionScale = MathF.Round(value, 1);
                OnPropertyChanged();
                DirtyEvent?.Invoke();
            }
        }

        private int _maxAnisotropy;
        public int MaxAnisotropy
        {
            get => _maxAnisotropy;
            set
            {
                _maxAnisotropy = value;
                DirtyEvent?.Invoke();
            }
        }

        private int _aspectRatio;
        public int AspectRatio
        {
            get => _aspectRatio;
            set
            {
                _aspectRatio = value;
                DirtyEvent?.Invoke();
            }
        }

        private int _graphicsBackendMultithreadingIndex;
        public int GraphicsBackendMultithreadingIndex
        {
            get => _graphicsBackendMultithreadingIndex;
            set
            {
                _graphicsBackendMultithreadingIndex = value;
                OnPropertyChanged();
                DirtyEvent?.Invoke();
            }
        }

        private string _shaderDumpPath;
        public string ShaderDumpPath
        {
            get => _shaderDumpPath;
            set
            {
                _shaderDumpPath = value;
                DirtyEvent?.Invoke();
            }
        }

        private int _antiAliasingEffect;
        public int AntiAliasingEffect
        {
            get => _antiAliasingEffect;
            set
            {
                _antiAliasingEffect = value;
                DirtyEvent?.Invoke();
            }
        }

        private int _scalingFilter;
        public int ScalingFilter
        {
            get => _scalingFilter;
            set
            {
                _scalingFilter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsScalingFilterActive));
                DirtyEvent?.Invoke();
            }
        }

        private int _scalingFilterLevel;
        public int ScalingFilterLevel
        {
            get => _scalingFilterLevel;
            set
            {
                _scalingFilterLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScalingFilterLevelText));
                DirtyEvent?.Invoke();
            }
        }

        public SettingsGraphicsViewModel()
        {
            AvailableGpus = new ObservableCollection<ComboBoxItem>();

            ConfigurationState config = ConfigurationState.Instance;

            GraphicsBackendIndex = (int)config.Graphics.GraphicsBackend.Value;
            // Physical devices are queried asynchronously hence the preferred index config value is loaded in LoadAvailableGpus().
            EnableShaderCache = config.Graphics.EnableShaderCache;
            EnableTextureRecompression = config.Graphics.EnableTextureRecompression;
            EnableMacroHLE = config.Graphics.EnableMacroHLE;
            EnableColorSpacePassthrough = config.Graphics.EnableColorSpacePassthrough;
            ResolutionScale = config.Graphics.ResScale == -1 ? 4 : config.Graphics.ResScale - 1;
            CustomResolutionScale = config.Graphics.ResScaleCustom;
            MaxAnisotropy = config.Graphics.MaxAnisotropy == -1 ? 0 : (int)(MathF.Log2(config.Graphics.MaxAnisotropy));
            AspectRatio = (int)config.Graphics.AspectRatio.Value;
            AntiAliasingEffect = (int)config.Graphics.AntiAliasing.Value;
            ScalingFilter = (int)config.Graphics.ScalingFilter.Value;
            ScalingFilterLevel = config.Graphics.ScalingFilterLevel.Value;
            GraphicsBackendMultithreadingIndex = (int)config.Graphics.BackendThreading.Value;
            ShaderDumpPath = config.Graphics.ShadersDumpPath;

            if (Program.PreviewerDetached)
            {
                Task.Run(LoadAvailableGpus);
            }
        }

        private async Task LoadAvailableGpus()
        {
            AvailableGpus.Clear();

            var devices = VulkanRenderer.GetPhysicalDevices();

            if (devices.Length == 0)
            {
                IsVulkanAvailable = false;
                GraphicsBackendIndex = 1;
            }
            else
            {
                foreach (var device in devices)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _gpuIds.Add(device.Id);

                        AvailableGpus.Add(new ComboBoxItem { Content = $"{device.Name} {(device.IsDiscrete ? "(dGPU)" : "")}" });
                    });
                }
            }

            // GPU configuration needs to be loaded during the async method or it will always return 0.
            PreferredGpuIndex = _gpuIds.Contains(ConfigurationState.Instance.Graphics.PreferredGpu) ?
                _gpuIds.IndexOf(ConfigurationState.Instance.Graphics.PreferredGpu) : 0;
        }

        public bool CheckIfModified(ConfigurationState config)
        {
            bool isDirty = false;

            isDirty |= config.Graphics.GraphicsBackend.Value != (GraphicsBackend)GraphicsBackendIndex;
            isDirty |= config.Graphics.PreferredGpu.Value != _gpuIds.ElementAtOrDefault(PreferredGpuIndex);
            isDirty |= config.Graphics.EnableShaderCache.Value != EnableShaderCache;
            isDirty |= config.Graphics.EnableTextureRecompression.Value != EnableTextureRecompression;
            isDirty |= config.Graphics.EnableMacroHLE.Value != EnableMacroHLE;
            isDirty |= config.Graphics.EnableColorSpacePassthrough.Value != EnableColorSpacePassthrough;
            isDirty |= config.Graphics.ResScale.Value != (ResolutionScale == 4 ? -1 : ResolutionScale + 1);
            isDirty |= config.Graphics.ResScaleCustom.Value != CustomResolutionScale;
            isDirty |= config.Graphics.MaxAnisotropy.Value != (MaxAnisotropy == 0 ? -1 : MathF.Pow(2, MaxAnisotropy));
            isDirty |= config.Graphics.AspectRatio.Value != (AspectRatio)AspectRatio;
            isDirty |= config.Graphics.AntiAliasing.Value != (AntiAliasing)AntiAliasingEffect;
            isDirty |= config.Graphics.ScalingFilter.Value != (ScalingFilter)ScalingFilter;
            isDirty |= config.Graphics.ScalingFilterLevel.Value != ScalingFilterLevel;
            isDirty |= config.Graphics.BackendThreading.Value != (BackendThreading)GraphicsBackendMultithreadingIndex;
            isDirty |= config.Graphics.ShadersDumpPath.Value != ShaderDumpPath;

            return isDirty;
        }

        public void Save(ConfigurationState config)
        {
            config.Graphics.GraphicsBackend.Value = (GraphicsBackend)GraphicsBackendIndex;
            config.Graphics.PreferredGpu.Value = _gpuIds.ElementAtOrDefault(PreferredGpuIndex);
            config.Graphics.EnableShaderCache.Value = EnableShaderCache;
            config.Graphics.EnableTextureRecompression.Value = EnableTextureRecompression;
            config.Graphics.EnableMacroHLE.Value = EnableMacroHLE;
            config.Graphics.EnableColorSpacePassthrough.Value = EnableColorSpacePassthrough;
            config.Graphics.ResScale.Value = ResolutionScale == 4 ? -1 : ResolutionScale + 1;
            config.Graphics.ResScaleCustom.Value = CustomResolutionScale;
            config.Graphics.MaxAnisotropy.Value = MaxAnisotropy == 0 ? -1 : MathF.Pow(2, MaxAnisotropy);
            config.Graphics.AspectRatio.Value = (AspectRatio)AspectRatio;
            config.Graphics.AntiAliasing.Value = (AntiAliasing)AntiAliasingEffect;
            config.Graphics.ScalingFilter.Value = (ScalingFilter)ScalingFilter;
            config.Graphics.ScalingFilterLevel.Value = ScalingFilterLevel;
            config.Graphics.BackendThreading.Value = (BackendThreading)GraphicsBackendMultithreadingIndex;
            config.Graphics.ShadersDumpPath.Value = ShaderDumpPath;

            if (config.Graphics.BackendThreading != (BackendThreading)GraphicsBackendMultithreadingIndex)
            {
                DriverUtilities.ToggleOGLThreading(GraphicsBackendMultithreadingIndex == (int)BackendThreading.Off);
            }
        }
    }
}
