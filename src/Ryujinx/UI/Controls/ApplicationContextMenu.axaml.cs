using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LibHac.Fs;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.HLE.HOS;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;

namespace Ryujinx.Ava.UI.Controls
{
    public class ApplicationContextMenu : MenuFlyout
    {
        public ApplicationContextMenu()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void ToggleFavorite_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                viewModel.SelectedApplication.Favorite = !viewModel.SelectedApplication.Favorite;

                ApplicationLibrary.LoadAndSaveMetaData(viewModel.SelectedApplication.IdString, appMetadata =>
                {
                    appMetadata.Favorite = viewModel.SelectedApplication.Favorite;
                });

                viewModel.RefreshView();
            }
        }

        public void OpenUserSaveDirectory_Click(object sender, RoutedEventArgs args)
        {
            if (sender is MenuItem { DataContext: MainWindowViewModel viewModel })
            {
                OpenSaveDirectory(viewModel, SaveDataType.Account, new UserId((ulong)viewModel.AccountManager.LastOpenedUser.UserId.High, (ulong)viewModel.AccountManager.LastOpenedUser.UserId.Low));
            }
        }

        public void OpenDeviceSaveDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            OpenSaveDirectory(viewModel, SaveDataType.Device, default);
        }

        public void OpenBcatSaveDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            OpenSaveDirectory(viewModel, SaveDataType.Bcat, default);
        }

        private static void OpenSaveDirectory(MainWindowViewModel viewModel, SaveDataType saveDataType, UserId userId)
        {
            if (viewModel?.SelectedApplication != null)
            {
                var saveDataFilter = SaveDataFilter.Make(viewModel.SelectedApplication.Id, saveDataType, userId, saveDataId: default, index: default);

                ApplicationHelper.OpenSaveDir(in saveDataFilter, viewModel.SelectedApplication.Id, viewModel.SelectedApplication.ControlHolder, viewModel.SelectedApplication.Name);
            }
        }

        public async void OpenTitleUpdateManager_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await TitleUpdateWindow.Show(viewModel.VirtualFileSystem, viewModel.SelectedApplication);
            }
        }

        public async void OpenDownloadableContentManager_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await DownloadableContentManagerWindow.Show(viewModel.VirtualFileSystem, viewModel.SelectedApplication);
            }
        }

        public async void OpenCheatManager_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await new CheatWindow(
                    viewModel.VirtualFileSystem,
                    viewModel.SelectedApplication.IdString,
                    viewModel.SelectedApplication.Name,
                    viewModel.SelectedApplication.Path).ShowDialog(viewModel.TopLevel as Window);
            }
        }

        public void OpenModsDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                string modsBasePath = ModLoader.GetModsBasePath();
                string titleModsPath = ModLoader.GetApplicationDir(modsBasePath, viewModel.SelectedApplication.IdString);

                OpenHelper.OpenFolder(titleModsPath);
            }
        }

        public void OpenSdModsDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                string sdModsBasePath = ModLoader.GetSdModsBasePath();
                string titleModsPath = ModLoader.GetApplicationDir(sdModsBasePath, viewModel.SelectedApplication.IdString);

                OpenHelper.OpenFolder(titleModsPath);
            }
        }

        public async void OpenModManager_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await ModManagerWindow.Show(viewModel.SelectedApplication.Id, viewModel.SelectedApplication.Name);
            }
        }

        public async void PurgePtcCache_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                    LocaleManager.Instance[LocaleKeys.DialogWarning],
                    LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogPPTCDeletionMessage, viewModel.SelectedApplication.Name),
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                if (result == UserResult.Yes)
                {
                    DirectoryInfo mainDir = new(Path.Combine(AppDataManager.GamesDirPath, viewModel.SelectedApplication.IdString, "cache", "cpu", "0"));
                    DirectoryInfo backupDir = new(Path.Combine(AppDataManager.GamesDirPath, viewModel.SelectedApplication.IdString, "cache", "cpu", "1"));

                    List<FileInfo> cacheFiles = new();

                    if (mainDir.Exists)
                    {
                        cacheFiles.AddRange(mainDir.EnumerateFiles("*.cache"));
                    }

                    if (backupDir.Exists)
                    {
                        cacheFiles.AddRange(backupDir.EnumerateFiles("*.cache"));
                    }

                    if (cacheFiles.Count > 0)
                    {
                        foreach (FileInfo file in cacheFiles)
                        {
                            try
                            {
                                file.Delete();
                            }
                            catch (Exception ex)
                            {
                                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogPPTCDeletionErrorMessage, file.Name, ex));
                            }
                        }
                    }
                }
            }
        }

        public async void PurgeShaderCache_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                    LocaleManager.Instance[LocaleKeys.DialogWarning],
                    LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogShaderDeletionMessage, viewModel.SelectedApplication.Name),
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                if (result == UserResult.Yes)
                {
                    DirectoryInfo shaderCacheDir = new(Path.Combine(AppDataManager.GamesDirPath, viewModel.SelectedApplication.IdString, "cache", "shader"));

                    List<DirectoryInfo> oldCacheDirectories = new();
                    List<FileInfo> newCacheFiles = new();

                    if (shaderCacheDir.Exists)
                    {
                        oldCacheDirectories.AddRange(shaderCacheDir.EnumerateDirectories("*"));
                        newCacheFiles.AddRange(shaderCacheDir.GetFiles("*.toc"));
                        newCacheFiles.AddRange(shaderCacheDir.GetFiles("*.data"));
                    }

                    if ((oldCacheDirectories.Count > 0 || newCacheFiles.Count > 0))
                    {
                        foreach (DirectoryInfo directory in oldCacheDirectories)
                        {
                            try
                            {
                                directory.Delete(true);
                            }
                            catch (Exception ex)
                            {
                                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogPPTCDeletionErrorMessage, directory.Name, ex));
                            }
                        }

                        foreach (FileInfo file in newCacheFiles)
                        {
                            try
                            {
                                file.Delete();
                            }
                            catch (Exception ex)
                            {
                                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.ShaderCachePurgeError, file.Name, ex));
                            }
                        }
                    }
                }
            }
        }

        public void OpenPtcDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                string ptcDir = Path.Combine(AppDataManager.GamesDirPath, viewModel.SelectedApplication.IdString, "cache", "cpu");
                string mainDir = Path.Combine(ptcDir, "0");
                string backupDir = Path.Combine(ptcDir, "1");

                if (!Directory.Exists(ptcDir))
                {
                    Directory.CreateDirectory(ptcDir);
                    Directory.CreateDirectory(mainDir);
                    Directory.CreateDirectory(backupDir);
                }

                OpenHelper.OpenFolder(ptcDir);
            }
        }

        public void OpenShaderCacheDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                string shaderCacheDir = Path.Combine(AppDataManager.GamesDirPath, viewModel.SelectedApplication.IdString, "cache", "shader");

                if (!Directory.Exists(shaderCacheDir))
                {
                    Directory.CreateDirectory(shaderCacheDir);
                }

                OpenHelper.OpenFolder(shaderCacheDir);
            }
        }

        public async void ExtractApplicationExeFs_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await ApplicationHelper.ExtractSection(
                    viewModel.StorageProvider,
                    NcaSectionType.Code,
                    viewModel.SelectedApplication.Path,
                    viewModel.SelectedApplication.Name);
            }
        }

        public async void ExtractApplicationRomFs_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await ApplicationHelper.ExtractSection(
                    viewModel.StorageProvider,
                    NcaSectionType.Data,
                    viewModel.SelectedApplication.Path,
                    viewModel.SelectedApplication.Name);
            }
        }

        public async void ExtractApplicationLogo_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await ApplicationHelper.ExtractSection(
                    viewModel.StorageProvider,
                    NcaSectionType.Logo,
                    viewModel.SelectedApplication.Path,
                    viewModel.SelectedApplication.Name);
            }
        }

        public void CreateApplicationShortcut_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                ApplicationData selectedApplication = viewModel.SelectedApplication;
                ShortcutHelper.CreateAppShortcut(selectedApplication.Path, selectedApplication.Name, selectedApplication.IdString, selectedApplication.Icon);
            }
        }

        public async void RunApplication_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await viewModel.LoadApplication(viewModel.SelectedApplication);
            }
        }
    }
}
