using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using LibHac.Common;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Input;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.Models.Generic;
using Ryujinx.Ava.UI.Renderer;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Cpu;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.UI;
using Ryujinx.Input.HLE;
using Ryujinx.Modules;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Key = Ryujinx.Input.Key;
using MissingKeyException = LibHac.Common.Keys.MissingKeyException;
using ShaderCacheLoadingState = Ryujinx.Graphics.Gpu.Shader.ShaderCacheState;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class MainWindowViewModel : BaseModel
    {
        private const int HotKeyPressDelayMs = 500;

        private ObservableCollection<ApplicationData> _applications;
        private string _aspectStatusText;

        private string _loadHeading;
        private string _cacheLoadStatus;
        private string _searchText;
        private Timer _searchTimer;
        private string _dockedStatusText;
        private string _fifoStatusText;
        private string _gameStatusText;
        private string _volumeStatusText;
        private string _gpuStatusText;
        private bool _isAmiiboRequested;
        private bool _isGameRunning;
        private bool _isFullScreen;
        private int _progressMaximum;
        private int _progressValue;
        private long _lastFullscreenToggle = Environment.TickCount64;
        private bool _showLoadProgress;
        private bool _showMenuAndStatusBar = true;
        private bool _showStatusSeparator;
        private Brush _progressBarForegroundColor;
        private Brush _progressBarBackgroundColor;
        private Brush _vsyncColor;
        private byte[] _selectedIcon;
        private bool _isAppletMenuActive;
        private int _statusBarProgressMaximum;
        private int _statusBarProgressValue;
        private bool _isPaused;
        private bool _showContent = true;
        private bool _isLoadingIndeterminate = true;
        private bool _showAll;
        private string _lastScannedAmiiboId;
        private bool _statusBarVisible;
        private ReadOnlyObservableCollection<ApplicationData> _appsObservableList;

        private string _showUiKey = "F4";
        private string _pauseKey = "F5";
        private string _screenshotKey = "F8";
        private float _volume;
        private float _volumeBeforeMute;
        private string _backendText;

        private bool _canUpdate = true;
        private Cursor _cursor;
        private string _title;
        private ApplicationData _currentApplicationData;
        private readonly AutoResetEvent _rendererWaitEvent;
        private WindowState _windowState;
        private double _windowWidth;
        private double _windowHeight;

        private bool _isActive;
        private bool _isSubMenuOpen;

        public ApplicationData ListSelectedApplication;
        public ApplicationData GridSelectedApplication;

        internal AppHost AppHost { get; set; }

        public MainWindowViewModel()
        {
            Applications = new ObservableCollection<ApplicationData>();

            Applications.ToObservableChangeSet()
                .Filter(Filter)
                .Sort(GetComparer())
                .Bind(out _appsObservableList).AsObservableList();

            _rendererWaitEvent = new AutoResetEvent(false);

            if (Program.PreviewerDetached)
            {
                LoadConfigurableHotKeys();

                Volume = ConfigurationState.Instance.System.AudioVolume;
            }
        }

        public void Initialize(
            ContentManager contentManager,
            IStorageProvider storageProvider,
            ApplicationLibrary applicationLibrary,
            VirtualFileSystem virtualFileSystem,
            AccountManager accountManager,
            InputManager inputManager,
            UserChannelPersistence userChannelPersistence,
            LibHacHorizonManager libHacHorizonManager,
            IHostUIHandler uiHandler,
            Action<bool> showLoading,
            Action<bool> switchToGameControl,
            Action<Control> setMainContent,
            TopLevel topLevel)
        {
            ContentManager = contentManager;
            StorageProvider = storageProvider;
            ApplicationLibrary = applicationLibrary;
            VirtualFileSystem = virtualFileSystem;
            AccountManager = accountManager;
            InputManager = inputManager;
            UserChannelPersistence = userChannelPersistence;
            LibHacHorizonManager = libHacHorizonManager;
            UiHandler = uiHandler;

            ShowLoading = showLoading;
            SwitchToGameControl = switchToGameControl;
            SetMainContent = setMainContent;
            TopLevel = topLevel;
        }

        #region Properties

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;

                _searchTimer?.Dispose();

                _searchTimer = new Timer(TimerCallback, null, 1000, 0);
            }
        }

        private void TimerCallback(object obj)
        {
            RefreshView();

            _searchTimer.Dispose();
            _searchTimer = null;
        }

        public bool CanUpdate
        {
            get => _canUpdate && EnableNonGameRunningControls && Updater.CanUpdate(false);
            set
            {
                _canUpdate = value;
                OnPropertyChanged();
            }
        }

        public Cursor Cursor
        {
            get => _cursor;
            set
            {
                _cursor = value;
                OnPropertyChanged();
            }
        }

        public ReadOnlyObservableCollection<ApplicationData> AppsObservableList
        {
            get => _appsObservableList;
            set
            {
                _appsObservableList = value;

                OnPropertyChanged();
            }
        }

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                _isPaused = value;

                OnPropertyChanged();
            }
        }

        public long LastFullscreenToggle
        {
            get => _lastFullscreenToggle;
            set
            {
                _lastFullscreenToggle = value;

                OnPropertyChanged();
            }
        }

        public bool StatusBarVisible
        {
            get => _statusBarVisible && EnableNonGameRunningControls;
            set
            {
                _statusBarVisible = value;

                OnPropertyChanged();
            }
        }

        public bool EnableNonGameRunningControls => !IsGameRunning;

        public bool ShowFirmwareStatus => !ShowLoadProgress;

        public bool IsGameRunning
        {
            get => _isGameRunning;
            set
            {
                _isGameRunning = value;

                if (!value)
                {
                    ShowMenuAndStatusBar = false;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(EnableNonGameRunningControls));
                OnPropertyChanged(nameof(IsAppletMenuActive));
                OnPropertyChanged(nameof(StatusBarVisible));
                OnPropertyChanged(nameof(ShowFirmwareStatus));
            }
        }

        public bool IsAmiiboRequested
        {
            get => _isAmiiboRequested && _isGameRunning;
            set
            {
                _isAmiiboRequested = value;

                OnPropertyChanged();
            }
        }

        public bool ShowLoadProgress
        {
            get => _showLoadProgress;
            set
            {
                _showLoadProgress = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowFirmwareStatus));
            }
        }

        public string GameStatusText
        {
            get => _gameStatusText;
            set
            {
                _gameStatusText = value;

                OnPropertyChanged();
            }
        }

        public bool IsFullScreen
        {
            get => _isFullScreen;
            set
            {
                _isFullScreen = value;

                OnPropertyChanged();
            }
        }

        public bool IsSubMenuOpen
        {
            get => _isSubMenuOpen;
            set
            {
                _isSubMenuOpen = value;

                OnPropertyChanged();
            }
        }

        public bool ShowAll
        {
            get => _showAll;
            set
            {
                _showAll = value;

                OnPropertyChanged();
            }
        }

        public string LastScannedAmiiboId
        {
            get => _lastScannedAmiiboId;
            set
            {
                _lastScannedAmiiboId = value;

                OnPropertyChanged();
            }
        }

        public ApplicationData SelectedApplication
        {
            get
            {
                return Glyph switch
                {
                    Glyph.List => ListSelectedApplication,
                    Glyph.Grid => GridSelectedApplication,
                    _ => null,
                };
            }
        }

        public bool OpenUserSaveDirectoryEnabled => !SelectedApplication.ControlHolder.ByteSpan.IsZeros() && SelectedApplication.ControlHolder.Value.UserAccountSaveDataSize > 0;

        public bool OpenDeviceSaveDirectoryEnabled => !SelectedApplication.ControlHolder.ByteSpan.IsZeros() && SelectedApplication.ControlHolder.Value.DeviceSaveDataSize > 0;

        public bool OpenBcatSaveDirectoryEnabled => !SelectedApplication.ControlHolder.ByteSpan.IsZeros() && SelectedApplication.ControlHolder.Value.BcatDeliveryCacheStorageSize > 0;

        public bool CreateShortcutEnabled => !ReleaseInformation.IsFlatHubBuild;

        public string LoadHeading
        {
            get => _loadHeading;
            set
            {
                _loadHeading = value;

                OnPropertyChanged();
            }
        }

        public string CacheLoadStatus
        {
            get => _cacheLoadStatus;
            set
            {
                _cacheLoadStatus = value;

                OnPropertyChanged();
            }
        }

        public Brush ProgressBarBackgroundColor
        {
            get => _progressBarBackgroundColor;
            set
            {
                _progressBarBackgroundColor = value;

                OnPropertyChanged();
            }
        }

        public Brush ProgressBarForegroundColor
        {
            get => _progressBarForegroundColor;
            set
            {
                _progressBarForegroundColor = value;

                OnPropertyChanged();
            }
        }

        public Brush VsyncColor
        {
            get => _vsyncColor;
            set
            {
                _vsyncColor = value;

                OnPropertyChanged();
            }
        }

        public byte[] SelectedIcon
        {
            get => _selectedIcon;
            set
            {
                _selectedIcon = value;

                OnPropertyChanged();
            }
        }

        public int ProgressMaximum
        {
            get => _progressMaximum;
            set
            {
                _progressMaximum = value;

                OnPropertyChanged();
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;

                OnPropertyChanged();
            }
        }

        public int StatusBarProgressMaximum
        {
            get => _statusBarProgressMaximum;
            set
            {
                _statusBarProgressMaximum = value;

                OnPropertyChanged();
            }
        }

        public int StatusBarProgressValue
        {
            get => _statusBarProgressValue;
            set
            {
                _statusBarProgressValue = value;

                OnPropertyChanged();
            }
        }

        public string FifoStatusText
        {
            get => _fifoStatusText;
            set
            {
                _fifoStatusText = value;

                OnPropertyChanged();
            }
        }

        public string GpuNameText
        {
            get => _gpuStatusText;
            set
            {
                _gpuStatusText = value;

                OnPropertyChanged();
            }
        }

        public string BackendText
        {
            get => _backendText;
            set
            {
                _backendText = value;

                OnPropertyChanged();
            }
        }

        public string DockedStatusText
        {
            get => _dockedStatusText;
            set
            {
                _dockedStatusText = value;

                OnPropertyChanged();
            }
        }

        public string AspectRatioStatusText
        {
            get => _aspectStatusText;
            set
            {
                _aspectStatusText = value;

                OnPropertyChanged();
            }
        }

        public string VolumeStatusText
        {
            get => _volumeStatusText;
            set
            {
                _volumeStatusText = value;

                OnPropertyChanged();
            }
        }

        public bool VolumeMuted => _volume == 0;

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;

                if (_isGameRunning)
                {
                    AppHost.Device.SetVolume(_volume);
                }

                OnPropertyChanged(nameof(VolumeStatusText));
                OnPropertyChanged(nameof(VolumeMuted));
                OnPropertyChanged();
            }
        }

        public float VolumeBeforeMute
        {
            get => _volumeBeforeMute;
            set
            {
                _volumeBeforeMute = value;

                OnPropertyChanged();
            }
        }

        public bool ShowStatusSeparator
        {
            get => _showStatusSeparator;
            set
            {
                _showStatusSeparator = value;

                OnPropertyChanged();
            }
        }

        public bool ShowMenuAndStatusBar
        {
            get => _showMenuAndStatusBar;
            set
            {
                _showMenuAndStatusBar = value;

                OnPropertyChanged();
            }
        }

        public bool IsLoadingIndeterminate
        {
            get => _isLoadingIndeterminate;
            set
            {
                _isLoadingIndeterminate = value;

                OnPropertyChanged();
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;

                OnPropertyChanged();
            }
        }


        public bool ShowContent
        {
            get => _showContent;
            set
            {
                _showContent = value;

                OnPropertyChanged();
            }
        }

        public bool IsAppletMenuActive
        {
            get => _isAppletMenuActive && EnableNonGameRunningControls;
            set
            {
                _isAppletMenuActive = value;

                OnPropertyChanged();
            }
        }

        public WindowState WindowState
        {
            get => _windowState;
            internal set
            {
                _windowState = value;

                OnPropertyChanged();
            }
        }

        public double WindowWidth
        {
            get => _windowWidth;
            set
            {
                _windowWidth = value;

                OnPropertyChanged();
            }
        }

        public double WindowHeight
        {
            get => _windowHeight;
            set
            {
                _windowHeight = value;

                OnPropertyChanged();
            }
        }

        public bool IsGrid => Glyph == Glyph.Grid;
        public bool IsList => Glyph == Glyph.List;

        internal void Sort(bool isAscending)
        {
            IsAscending = isAscending;

            RefreshView();
        }

        internal void Sort(ApplicationSort sort)
        {
            SortMode = sort;

            RefreshView();
        }

        public bool StartGamesInFullscreen
        {
            get => ConfigurationState.Instance.UI.StartFullscreen;
            set
            {
                ConfigurationState.Instance.UI.StartFullscreen.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                OnPropertyChanged();
            }
        }

        public bool ShowConsole
        {
            get => ConfigurationState.Instance.UI.ShowConsole;
            set
            {
                ConfigurationState.Instance.UI.ShowConsole.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                OnPropertyChanged();
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;

                OnPropertyChanged();
            }
        }

        public bool ShowConsoleVisible
        {
            get => ConsoleHelper.SetConsoleWindowStateSupported;
        }

        public bool ManageFileTypesVisible
        {
            get => FileAssociationHelper.IsTypeAssociationSupported;
        }

        public ObservableCollection<ApplicationData> Applications
        {
            get => _applications;
            set
            {
                _applications = value;

                OnPropertyChanged();
            }
        }

        public Glyph Glyph
        {
            get => (Glyph)ConfigurationState.Instance.UI.GameListViewMode.Value;
            set
            {
                ConfigurationState.Instance.UI.GameListViewMode.Value = (int)value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGrid));
                OnPropertyChanged(nameof(IsList));

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            }
        }

        public bool ShowNames
        {
            get => ConfigurationState.Instance.UI.ShowNames && ConfigurationState.Instance.UI.GridSize > 1; set
            {
                ConfigurationState.Instance.UI.ShowNames.Value = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(GridSizeScale));
                OnPropertyChanged(nameof(GridItemSelectorSize));

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            }
        }

        internal ApplicationSort SortMode
        {
            get => (ApplicationSort)ConfigurationState.Instance.UI.ApplicationSort.Value;
            private set
            {
                ConfigurationState.Instance.UI.ApplicationSort.Value = (int)value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SortName));

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            }
        }

        public int ListItemSelectorSize
        {
            get
            {
                return ConfigurationState.Instance.UI.GridSize.Value switch
                {
                    1 => 78,
                    2 => 100,
                    3 => 120,
                    4 => 140,
                    _ => 16,
                };
            }
        }

        public int GridItemSelectorSize
        {
            get
            {
                return ConfigurationState.Instance.UI.GridSize.Value switch
                {
                    1 => 120,
                    2 => ShowNames ? 210 : 150,
                    3 => ShowNames ? 240 : 180,
                    4 => ShowNames ? 280 : 220,
                    _ => 16,
                };
            }
        }

        public int GridSizeScale
        {
            get => ConfigurationState.Instance.UI.GridSize;
            set
            {
                ConfigurationState.Instance.UI.GridSize.Value = value;

                if (value < 2)
                {
                    ShowNames = false;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGridSmall));
                OnPropertyChanged(nameof(IsGridMedium));
                OnPropertyChanged(nameof(IsGridLarge));
                OnPropertyChanged(nameof(IsGridHuge));
                OnPropertyChanged(nameof(ListItemSelectorSize));
                OnPropertyChanged(nameof(GridItemSelectorSize));
                OnPropertyChanged(nameof(ShowNames));

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            }
        }

        public string SortName
        {
            get
            {
                return SortMode switch
                {
                    ApplicationSort.Title => LocaleManager.Instance[LocaleKeys.GameListHeaderApplication],
                    ApplicationSort.Developer => LocaleManager.Instance[LocaleKeys.GameListHeaderDeveloper],
                    ApplicationSort.LastPlayed => LocaleManager.Instance[LocaleKeys.GameListHeaderLastPlayed],
                    ApplicationSort.TotalTimePlayed => LocaleManager.Instance[LocaleKeys.GameListHeaderTimePlayed],
                    ApplicationSort.FileType => LocaleManager.Instance[LocaleKeys.GameListHeaderFileExtension],
                    ApplicationSort.FileSize => LocaleManager.Instance[LocaleKeys.GameListHeaderFileSize],
                    ApplicationSort.Path => LocaleManager.Instance[LocaleKeys.GameListHeaderPath],
                    ApplicationSort.Favorite => LocaleManager.Instance[LocaleKeys.CommonFavorite],
                    _ => string.Empty,
                };
            }
        }

        public bool IsAscending
        {
            get => ConfigurationState.Instance.UI.IsAscendingOrder;
            private set
            {
                ConfigurationState.Instance.UI.IsAscendingOrder.Value = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SortMode));
                OnPropertyChanged(nameof(SortName));

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            }
        }

        public KeyGesture ShowUiKey
        {
            get => KeyGesture.Parse(_showUiKey);
            set
            {
                _showUiKey = value.ToString();

                OnPropertyChanged();
            }
        }

        public KeyGesture ScreenshotKey
        {
            get => KeyGesture.Parse(_screenshotKey);
            set
            {
                _screenshotKey = value.ToString();

                OnPropertyChanged();
            }
        }

        public KeyGesture PauseKey
        {
            get => KeyGesture.Parse(_pauseKey); set
            {
                _pauseKey = value.ToString();

                OnPropertyChanged();
            }
        }

        public ContentManager ContentManager { get; private set; }
        public IStorageProvider StorageProvider { get; private set; }
        public ApplicationLibrary ApplicationLibrary { get; private set; }
        public VirtualFileSystem VirtualFileSystem { get; private set; }
        public AccountManager AccountManager { get; private set; }
        public InputManager InputManager { get; private set; }
        public UserChannelPersistence UserChannelPersistence { get; private set; }
        public Action<bool> ShowLoading { get; private set; }
        public Action<bool> SwitchToGameControl { get; private set; }
        public Action<Control> SetMainContent { get; private set; }
        public TopLevel TopLevel { get; private set; }
        public RendererHost RendererHostControl { get; private set; }
        public bool IsClosing { get; set; }
        public LibHacHorizonManager LibHacHorizonManager { get; internal set; }
        public IHostUIHandler UiHandler { get; internal set; }
        public bool IsSortedByFavorite => SortMode == ApplicationSort.Favorite;
        public bool IsSortedByTitle => SortMode == ApplicationSort.Title;
        public bool IsSortedByDeveloper => SortMode == ApplicationSort.Developer;
        public bool IsSortedByLastPlayed => SortMode == ApplicationSort.LastPlayed;
        public bool IsSortedByTimePlayed => SortMode == ApplicationSort.TotalTimePlayed;
        public bool IsSortedByType => SortMode == ApplicationSort.FileType;
        public bool IsSortedBySize => SortMode == ApplicationSort.FileSize;
        public bool IsSortedByPath => SortMode == ApplicationSort.Path;
        public bool IsGridSmall => ConfigurationState.Instance.UI.GridSize == 1;
        public bool IsGridMedium => ConfigurationState.Instance.UI.GridSize == 2;
        public bool IsGridLarge => ConfigurationState.Instance.UI.GridSize == 3;
        public bool IsGridHuge => ConfigurationState.Instance.UI.GridSize == 4;

        #endregion

        #region PrivateMethods

        private IComparer<ApplicationData> GetComparer()
        {
            return SortMode switch
            {
#pragma warning disable IDE0055 // Disable formatting
                ApplicationSort.Title           => IsAscending ? SortExpressionComparer<ApplicationData>.Ascending(app => app.Name)
                                                               : SortExpressionComparer<ApplicationData>.Descending(app => app.Name),
                ApplicationSort.Developer       => IsAscending ? SortExpressionComparer<ApplicationData>.Ascending(app => app.Developer)
                                                               : SortExpressionComparer<ApplicationData>.Descending(app => app.Developer),
                ApplicationSort.LastPlayed      => new LastPlayedSortComparer(IsAscending),
                ApplicationSort.TotalTimePlayed => new TimePlayedSortComparer(IsAscending),
                ApplicationSort.FileType        => IsAscending ? SortExpressionComparer<ApplicationData>.Ascending(app => app.FileExtension)
                                                               : SortExpressionComparer<ApplicationData>.Descending(app => app.FileExtension),
                ApplicationSort.FileSize        => IsAscending ? SortExpressionComparer<ApplicationData>.Ascending(app => app.FileSize)
                                                               : SortExpressionComparer<ApplicationData>.Descending(app => app.FileSize),
                ApplicationSort.Path            => IsAscending ? SortExpressionComparer<ApplicationData>.Ascending(app => app.Path)
                                                               : SortExpressionComparer<ApplicationData>.Descending(app => app.Path),
                ApplicationSort.Favorite        => IsAscending ? SortExpressionComparer<ApplicationData>.Ascending(app => new AppListFavoriteComparable(app))
                                                                : SortExpressionComparer<ApplicationData>.Descending(app => new AppListFavoriteComparable(app)),
                _ => null,
#pragma warning restore IDE0055
            };
        }

        public void RefreshView()
        {
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            Applications.ToObservableChangeSet()
                .Filter(Filter)
                .Sort(GetComparer())
                .Bind(out _appsObservableList).AsObservableList();

            OnPropertyChanged(nameof(AppsObservableList));
        }

        private bool Filter(object arg)
        {
            if (arg is ApplicationData app)
            {
                if (string.IsNullOrWhiteSpace(_searchText))
                {
                    return true;
                }

                CompareInfo compareInfo = CultureInfo.CurrentCulture.CompareInfo;

                return compareInfo.IndexOf(app.Name, _searchText, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
            }

            return false;
        }

        private async Task HandleFirmwareInstallation(string filename)
        {
            try
            {
                SystemVersion firmwareVersion = ContentManager.VerifyFirmwarePackage(filename);

                if (firmwareVersion == null)
                {
                    await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogFirmwareInstallerFirmwareNotFoundErrorMessage, filename));

                    return;
                }

                string dialogTitle = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogFirmwareInstallerFirmwareInstallTitle, firmwareVersion.VersionString);
                string dialogMessage = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogFirmwareInstallerFirmwareInstallMessage, firmwareVersion.VersionString);

                SystemVersion currentVersion = ContentManager.GetCurrentFirmwareVersion();
                if (currentVersion != null)
                {
                    dialogMessage += LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogFirmwareInstallerFirmwareInstallSubMessage, currentVersion.VersionString);
                }

                dialogMessage += LocaleManager.Instance[LocaleKeys.DialogFirmwareInstallerFirmwareInstallConfirmMessage];

                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                    dialogTitle,
                    dialogMessage,
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                UpdateWaitWindow waitingDialog = new(dialogTitle, LocaleManager.Instance[LocaleKeys.DialogFirmwareInstallerFirmwareInstallWaitMessage]);

                if (result == UserResult.Yes)
                {
                    Logger.Info?.Print(LogClass.Application, $"Installing firmware {firmwareVersion.VersionString}");

                    Thread thread = new(() =>
                    {
                        Dispatcher.UIThread.InvokeAsync(delegate
                        {
                            waitingDialog.Show();
                        });

                        try
                        {
                            ContentManager.InstallFirmware(filename);

                            Dispatcher.UIThread.InvokeAsync(async delegate
                            {
                                waitingDialog.Close();

                                string message = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogFirmwareInstallerFirmwareInstallSuccessMessage, firmwareVersion.VersionString);

                                await ContentDialogHelper.CreateInfoDialog(dialogTitle, message, LocaleManager.Instance[LocaleKeys.InputDialogOk], "", LocaleManager.Instance[LocaleKeys.RyujinxInfo]);

                                Logger.Info?.Print(LogClass.Application, message);

                                // Purge Applet Cache.

                                DirectoryInfo miiEditorCacheFolder = new(Path.Combine(AppDataManager.GamesDirPath, "0100000000001009", "cache"));

                                if (miiEditorCacheFolder.Exists)
                                {
                                    miiEditorCacheFolder.Delete(true);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                waitingDialog.Close();

                                await ContentDialogHelper.CreateErrorDialog(ex.Message);
                            });
                        }
                        finally
                        {
                            RefreshFirmwareStatus();
                        }
                    })
                    {
                        Name = "GUI.FirmwareInstallerThread",
                    };

                    thread.Start();
                }
            }
            catch (MissingKeyException ex)
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    Logger.Error?.Print(LogClass.Application, ex.ToString());

                    await UserErrorDialog.ShowUserErrorDialog(UserError.NoKeys);
                }
            }
            catch (Exception ex)
            {
                await ContentDialogHelper.CreateErrorDialog(ex.Message);
            }
        }

        private void ProgressHandler<T>(T state, int current, int total) where T : Enum
        {
            Dispatcher.UIThread.Post((() =>
            {
                ProgressMaximum = total;
                ProgressValue = current;

                switch (state)
                {
                    case LoadState ptcState:
                        CacheLoadStatus = $"{current} / {total}";
                        switch (ptcState)
                        {
                            case LoadState.Unloaded:
                            case LoadState.Loading:
                                LoadHeading = LocaleManager.Instance[LocaleKeys.CompilingPPTC];
                                IsLoadingIndeterminate = false;
                                break;
                            case LoadState.Loaded:
                                LoadHeading = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.LoadingHeading, _currentApplicationData.Name);
                                IsLoadingIndeterminate = true;
                                CacheLoadStatus = "";
                                break;
                        }
                        break;
                    case ShaderCacheLoadingState shaderCacheState:
                        CacheLoadStatus = $"{current} / {total}";
                        switch (shaderCacheState)
                        {
                            case ShaderCacheLoadingState.Start:
                            case ShaderCacheLoadingState.Loading:
                                LoadHeading = LocaleManager.Instance[LocaleKeys.CompilingShaders];
                                IsLoadingIndeterminate = false;
                                break;
                            case ShaderCacheLoadingState.Packaging:
                                LoadHeading = LocaleManager.Instance[LocaleKeys.PackagingShaders];
                                IsLoadingIndeterminate = false;
                                break;
                            case ShaderCacheLoadingState.Loaded:
                                LoadHeading = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.LoadingHeading, _currentApplicationData.Name);
                                IsLoadingIndeterminate = true;
                                CacheLoadStatus = "";
                                break;
                        }
                        break;
                    default:
                        throw new ArgumentException($"Unknown Progress Handler type {typeof(T)}");
                }
            }));
        }

        private void PrepareLoadScreen()
        {
            using MemoryStream stream = new(SelectedIcon);
            using var gameIconBmp = SKBitmap.Decode(stream);

            var dominantColor = IconColorPicker.GetFilteredColor(gameIconBmp);

            const float ColorMultiple = 0.5f;

            Color progressFgColor = Color.FromRgb(dominantColor.Red, dominantColor.Green, dominantColor.Blue);
            Color progressBgColor = Color.FromRgb(
                (byte)(dominantColor.Red * ColorMultiple),
                (byte)(dominantColor.Green * ColorMultiple),
                (byte)(dominantColor.Blue * ColorMultiple));

            ProgressBarForegroundColor = new SolidColorBrush(progressFgColor);
            ProgressBarBackgroundColor = new SolidColorBrush(progressBgColor);
        }

        private void InitializeGame()
        {
            RendererHostControl.WindowCreated += RendererHost_Created;

            AppHost.StatusInitEvent += Init_StatusBar;
            AppHost.StatusUpdatedEvent += Update_StatusBar;
            AppHost.AppExit += AppHost_AppExit;

            _rendererWaitEvent.WaitOne();

            AppHost?.Start();

            AppHost?.DisposeContext();
        }

        private async Task HandleRelaunch()
        {
            if (UserChannelPersistence.PreviousIndex != -1 && UserChannelPersistence.ShouldRestart)
            {
                UserChannelPersistence.ShouldRestart = false;

                await LoadApplication(_currentApplicationData);
            }
            else
            {
                // Otherwise, clear state.
                UserChannelPersistence = new UserChannelPersistence();
                _currentApplicationData = null;
            }
        }

        private void Init_StatusBar(object sender, StatusInitEventArgs args)
        {
            if (ShowMenuAndStatusBar && !ShowLoadProgress)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    GpuNameText = args.GpuName;
                    BackendText = args.GpuBackend;
                });
            }
        }

        private void Update_StatusBar(object sender, StatusUpdatedEventArgs args)
        {
            if (ShowMenuAndStatusBar && !ShowLoadProgress)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Application.Current.Styles.TryGetResource(args.VSyncEnabled
                        ? "VsyncEnabled"
                        : "VsyncDisabled",
                        Application.Current.ActualThemeVariant,
                        out object color);

                    if (color is not null)
                    {
                        VsyncColor = new SolidColorBrush((Color)color);
                    }

                    DockedStatusText = args.DockedMode;
                    AspectRatioStatusText = args.AspectRatio;
                    GameStatusText = args.GameStatus;
                    VolumeStatusText = args.VolumeStatus;
                    FifoStatusText = args.FifoStatus;

                    ShowStatusSeparator = true;
                });
            }
        }

        private void RendererHost_Created(object sender, EventArgs e)
        {
            ShowLoading(false);

            _rendererWaitEvent.Set();
        }

        #endregion

        #region PublicMethods

        public void SetUiProgressHandlers(Switch emulationContext)
        {
            if (emulationContext.Processes.ActiveApplication.DiskCacheLoadState != null)
            {
                emulationContext.Processes.ActiveApplication.DiskCacheLoadState.StateChanged -= ProgressHandler;
                emulationContext.Processes.ActiveApplication.DiskCacheLoadState.StateChanged += ProgressHandler;
            }

            emulationContext.Gpu.ShaderCacheStateChanged -= ProgressHandler;
            emulationContext.Gpu.ShaderCacheStateChanged += ProgressHandler;
        }

        public void LoadConfigurableHotKeys()
        {
            if (AvaloniaKeyboardMappingHelper.TryGetAvaKey((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ShowUI, out var showUiKey))
            {
                ShowUiKey = new KeyGesture(showUiKey);
            }

            if (AvaloniaKeyboardMappingHelper.TryGetAvaKey((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.Screenshot, out var screenshotKey))
            {
                ScreenshotKey = new KeyGesture(screenshotKey);
            }

            if (AvaloniaKeyboardMappingHelper.TryGetAvaKey((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.Pause, out var pauseKey))
            {
                PauseKey = new KeyGesture(pauseKey);
            }
        }

        public void TakeScreenshot()
        {
            AppHost.ScreenshotRequested = true;
        }

        public void HideUi()
        {
            ShowMenuAndStatusBar = false;
        }

        public void ToggleStartGamesInFullscreen()
        {
            StartGamesInFullscreen = !StartGamesInFullscreen;
        }

        public void ToggleShowConsole()
        {
            ShowConsole = !ShowConsole;
        }

        public void SetListMode()
        {
            Glyph = Glyph.List;
        }

        public void SetGridMode()
        {
            Glyph = Glyph.Grid;
        }

        public void SetAspectRatio(AspectRatio aspectRatio)
        {
            ConfigurationState.Instance.Graphics.AspectRatio.Value = aspectRatio;
        }

        public async Task InstallFirmwareFromFile()
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(LocaleManager.Instance[LocaleKeys.FileDialogAllTypes])
                    {
                        Patterns = new[] { "*.xci", "*.zip" },
                        AppleUniformTypeIdentifiers = new[] { "com.ryujinx.xci", "public.zip-archive" },
                        MimeTypes = new[] { "application/x-nx-xci", "application/zip" },
                    },
                    new("XCI")
                    {
                        Patterns = new[] { "*.xci" },
                        AppleUniformTypeIdentifiers = new[] { "com.ryujinx.xci" },
                        MimeTypes = new[] { "application/x-nx-xci" },
                    },
                    new("ZIP")
                    {
                        Patterns = new[] { "*.zip" },
                        AppleUniformTypeIdentifiers = new[] { "public.zip-archive" },
                        MimeTypes = new[] { "application/zip" },
                    },
                },
            });

            if (result.Count > 0)
            {
                await HandleFirmwareInstallation(result[0].Path.LocalPath);
            }
        }

        public async Task InstallFirmwareFromFolder()
        {
            var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
            });

            if (result.Count > 0)
            {
                await HandleFirmwareInstallation(result[0].Path.LocalPath);
            }
        }

        public void OpenRyujinxFolder()
        {
            OpenHelper.OpenFolder(AppDataManager.BaseDirPath);
        }

        public void OpenLogsFolder()
        {
            string logPath = AppDataManager.GetOrCreateLogsDir();
            if (!string.IsNullOrEmpty(logPath))
            {
                OpenHelper.OpenFolder(logPath);
            }
        }

        public void ToggleDockMode()
        {
            if (IsGameRunning)
            {
                ConfigurationState.Instance.System.EnableDockedMode.Value = !ConfigurationState.Instance.System.EnableDockedMode.Value;
            }
        }

        public async Task ExitCurrentState()
        {
            if (WindowState == WindowState.FullScreen)
            {
                ToggleFullscreen();
            }
            else if (IsGameRunning)
            {
                await Task.Delay(100);

                AppHost?.ShowExitPrompt();
            }
        }

        public static void ChangeLanguage(object languageCode)
        {
            LocaleManager.Instance.LoadLanguage((string)languageCode);

            if (Program.PreviewerDetached)
            {
                ConfigurationState.Instance.UI.LanguageCode.Value = (string)languageCode;
                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            }
        }

        public async Task ManageProfiles()
        {
            await NavigationDialogHost.Show(AccountManager, ContentManager, VirtualFileSystem, LibHacHorizonManager.RyujinxClient);
        }

        public void SimulateWakeUpMessage()
        {
            AppHost.Device.System.SimulateWakeUpMessage();
        }

        public async Task OpenFile()
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = LocaleManager.Instance[LocaleKeys.OpenFileDialogTitle],
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(LocaleManager.Instance[LocaleKeys.AllSupportedFormats])
                    {
                        Patterns = new[] { "*.nsp", "*.xci", "*.nca", "*.nro", "*.nso" },
                        AppleUniformTypeIdentifiers = new[]
                        {
                            "com.ryujinx.nsp",
                            "com.ryujinx.xci",
                            "com.ryujinx.nca",
                            "com.ryujinx.nro",
                            "com.ryujinx.nso",
                        },
                        MimeTypes = new[]
                        {
                            "application/x-nx-nsp",
                            "application/x-nx-xci",
                            "application/x-nx-nca",
                            "application/x-nx-nro",
                            "application/x-nx-nso",
                        },
                    },
                    new("NSP")
                    {
                        Patterns = new[] { "*.nsp" },
                        AppleUniformTypeIdentifiers = new[] { "com.ryujinx.nsp" },
                        MimeTypes = new[] { "application/x-nx-nsp" },
                    },
                    new("XCI")
                    {
                        Patterns = new[] { "*.xci" },
                        AppleUniformTypeIdentifiers = new[] { "com.ryujinx.xci" },
                        MimeTypes = new[] { "application/x-nx-xci" },
                    },
                    new("NCA")
                    {
                        Patterns = new[] { "*.nca" },
                        AppleUniformTypeIdentifiers = new[] { "com.ryujinx.nca" },
                        MimeTypes = new[] { "application/x-nx-nca" },
                    },
                    new("NRO")
                    {
                        Patterns = new[] { "*.nro" },
                        AppleUniformTypeIdentifiers = new[] { "com.ryujinx.nro" },
                        MimeTypes = new[] { "application/x-nx-nro" },
                    },
                    new("NSO")
                    {
                        Patterns = new[] { "*.nso" },
                        AppleUniformTypeIdentifiers = new[] { "com.ryujinx.nso" },
                        MimeTypes = new[] { "application/x-nx-nso" },
                    },
                },
            });

            if (result.Count > 0)
            {
                if (ApplicationLibrary.TryGetApplicationsFromFile(result[0].Path.LocalPath,
                        out List<ApplicationData> applications))
                {
                    await LoadApplication(applications[0]);
                }
                else
                {
                    await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.MenuBarFileOpenFromFileError]);
                }
            }
        }

        public async Task OpenFolder()
        {
            var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocaleManager.Instance[LocaleKeys.OpenFolderDialogTitle],
                AllowMultiple = false,
            });

            if (result.Count > 0)
            {
                ApplicationData applicationData = new()
                {
                    Name = Path.GetFileNameWithoutExtension(result[0].Path.LocalPath),
                    Path = result[0].Path.LocalPath,
                };

                await LoadApplication(applicationData);
            }
        }

        public async Task LoadApplication(ApplicationData application, bool startFullscreen = false)
        {
            if (AppHost != null)
            {
                await ContentDialogHelper.CreateInfoDialog(
                    LocaleManager.Instance[LocaleKeys.DialogLoadAppGameAlreadyLoadedMessage],
                    LocaleManager.Instance[LocaleKeys.DialogLoadAppGameAlreadyLoadedSubMessage],
                    LocaleManager.Instance[LocaleKeys.InputDialogOk],
                    "",
                    LocaleManager.Instance[LocaleKeys.RyujinxInfo]);

                return;
            }

#if RELEASE
            await PerformanceCheck();
#endif

            Logger.RestartTime();

            SelectedIcon ??= ApplicationLibrary.GetApplicationIcon(application.Path, ConfigurationState.Instance.System.Language, application.Id);

            PrepareLoadScreen();

            RendererHostControl = new RendererHost();

            AppHost = new AppHost(
                RendererHostControl,
                InputManager,
                application.Path,
                application.Id,
                VirtualFileSystem,
                ContentManager,
                AccountManager,
                UserChannelPersistence,
                this,
                TopLevel);

            if (!await AppHost.LoadGuestApplication())
            {
                AppHost.DisposeContext();
                AppHost = null;

                return;
            }

            CanUpdate = false;

            LoadHeading = application.Name;

            if (string.IsNullOrWhiteSpace(application.Name))
            {
                LoadHeading = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.LoadingHeading, AppHost.Device.Processes.ActiveApplication.Name);
                application.Name = AppHost.Device.Processes.ActiveApplication.Name;
            }

            SwitchToRenderer(startFullscreen);

            _currentApplicationData = application;

            Thread gameThread = new(InitializeGame) { Name = "GUI.WindowThread" };
            gameThread.Start();
        }

        public void SwitchToRenderer(bool startFullscreen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                SwitchToGameControl(startFullscreen);

                SetMainContent(RendererHostControl);

                RendererHostControl.Focus();
            });
        }

        public static void UpdateGameMetadata(string titleId)
        {
            ApplicationLibrary.LoadAndSaveMetaData(titleId, appMetadata =>
            {
                appMetadata.UpdatePostGame();
            });
        }

        public void RefreshFirmwareStatus()
        {
            SystemVersion version = null;
            try
            {
                version = ContentManager.GetCurrentFirmwareVersion();
            }
            catch (Exception) { }

            bool hasApplet = false;

            if (version != null)
            {
                LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.StatusBarSystemVersion, version.VersionString);

                hasApplet = version.Major > 3;
            }
            else
            {
                LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.StatusBarSystemVersion, "0.0");
            }

            IsAppletMenuActive = hasApplet;
        }

        public void AppHost_AppExit(object sender, EventArgs e)
        {
            if (IsClosing)
            {
                return;
            }

            IsGameRunning = false;

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                ShowMenuAndStatusBar = true;
                ShowContent = true;
                ShowLoadProgress = false;
                IsLoadingIndeterminate = false;
                CanUpdate = true;
                Cursor = Cursor.Default;

                SetMainContent(null);

                AppHost = null;

                await HandleRelaunch();
            });

            RendererHostControl.WindowCreated -= RendererHost_Created;
            RendererHostControl = null;

            SelectedIcon = null;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Title = $"Ryujinx {Program.Version}";
            });
        }

        public void ToggleFullscreen()
        {
            if (Environment.TickCount64 - LastFullscreenToggle < HotKeyPressDelayMs)
            {
                return;
            }

            LastFullscreenToggle = Environment.TickCount64;

            if (WindowState == WindowState.FullScreen)
            {
                WindowState = WindowState.Normal;

                if (IsGameRunning)
                {
                    ShowMenuAndStatusBar = true;
                }
            }
            else
            {
                WindowState = WindowState.FullScreen;

                if (IsGameRunning)
                {
                    ShowMenuAndStatusBar = false;
                }
            }

            IsFullScreen = WindowState == WindowState.FullScreen;
        }

        public static void SaveConfig()
        {
            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
        }

        public static async Task PerformanceCheck()
        {
            if (ConfigurationState.Instance.Logger.EnableTrace.Value)
            {
                string mainMessage = LocaleManager.Instance[LocaleKeys.DialogPerformanceCheckLoggingEnabledMessage];
                string secondaryMessage = LocaleManager.Instance[LocaleKeys.DialogPerformanceCheckLoggingEnabledConfirmMessage];

                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                    mainMessage,
                    secondaryMessage,
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                if (result == UserResult.Yes)
                {
                    ConfigurationState.Instance.Logger.EnableTrace.Value = false;

                    SaveConfig();
                }
            }

            if (!string.IsNullOrWhiteSpace(ConfigurationState.Instance.Graphics.ShadersDumpPath.Value))
            {
                string mainMessage = LocaleManager.Instance[LocaleKeys.DialogPerformanceCheckShaderDumpEnabledMessage];
                string secondaryMessage = LocaleManager.Instance[LocaleKeys.DialogPerformanceCheckShaderDumpEnabledConfirmMessage];

                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                    mainMessage,
                    secondaryMessage,
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                if (result == UserResult.Yes)
                {
                    ConfigurationState.Instance.Graphics.ShadersDumpPath.Value = "";

                    SaveConfig();
                }
            }
        }
        #endregion
    }
}
