using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DynamicData;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS;
using System;
using System.IO;
using System.Linq;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class ModManagerViewModel : BaseModel
    {
        private readonly string _modJsonPath;

        private AvaloniaList<ModModel> _mods = new();
        private AvaloniaList<ModModel> _views = new();
        private AvaloniaList<ModModel> _selectedMods = new();

        private string _search;
        private readonly ulong _applicationId;
        private readonly IStorageProvider _storageProvider;

        private static readonly ModMetadataJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public AvaloniaList<ModModel> Mods
        {
            get => _mods;
            set
            {
                _mods = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModCount));
                Sort();
            }
        }

        public AvaloniaList<ModModel> Views
        {
            get => _views;
            set
            {
                _views = value;
                OnPropertyChanged();
            }
        }

        public AvaloniaList<ModModel> SelectedMods
        {
            get => _selectedMods;
            set
            {
                _selectedMods = value;
                OnPropertyChanged();
            }
        }

        public string Search
        {
            get => _search;
            set
            {
                _search = value;
                OnPropertyChanged();
                Sort();
            }
        }

        public string ModCount
        {
            get => string.Format(LocaleManager.Instance[LocaleKeys.ModWindowHeading], Mods.Count);
        }

        public ModManagerViewModel(ulong applicationId)
        {
            _applicationId = applicationId;

            _modJsonPath = Path.Combine(AppDataManager.GamesDirPath, applicationId.ToString("x16"), "mods.json");

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _storageProvider = desktop.MainWindow.StorageProvider;
            }

            LoadMods(applicationId);
        }

        private void LoadMods(ulong applicationId)
        {
            Mods.Clear();
            SelectedMods.Clear();

            string[] modsBasePaths = [ModLoader.GetSdModsBasePath(), ModLoader.GetModsBasePath()];

            foreach (var path in modsBasePaths)
            {
                var inSd = path == ModLoader.GetSdModsBasePath();
                var modCache = new ModLoader.ModCache();

                ModLoader.QueryContentsDir(modCache, new DirectoryInfo(Path.Combine(path, "contents")), applicationId);

                foreach (var mod in modCache.RomfsDirs)
                {
                    var modModel = new ModModel(mod.Path.Parent.FullName, mod.Name, mod.Enabled, inSd);
                    if (Mods.All(x => x.Path != mod.Path.Parent.FullName))
                    {
                        Mods.Add(modModel);
                    }
                }

                foreach (var mod in modCache.RomfsContainers)
                {
                    Mods.Add(new ModModel(mod.Path.FullName, mod.Name, mod.Enabled, inSd));
                }

                foreach (var mod in modCache.ExefsDirs)
                {
                    var modModel = new ModModel(mod.Path.Parent.FullName, mod.Name, mod.Enabled, inSd);
                    if (Mods.All(x => x.Path != mod.Path.Parent.FullName))
                    {
                        Mods.Add(modModel);
                    }
                }

                foreach (var mod in modCache.ExefsContainers)
                {
                    Mods.Add(new ModModel(mod.Path.FullName, mod.Name, mod.Enabled, inSd));
                }
            }

            Sort();
        }

        public void Sort()
        {
            Mods.AsObservableChangeSet()
                .Filter(Filter)
                .Bind(out var view).AsObservableList();

            _views.Clear();
            _views.AddRange(view);

            SelectedMods = new(Views.Where(x => x.Enabled));

            OnPropertyChanged(nameof(ModCount));
            OnPropertyChanged(nameof(Views));
            OnPropertyChanged(nameof(SelectedMods));
        }

        private bool Filter(object arg)
        {
            if (arg is ModModel content)
            {
                return string.IsNullOrWhiteSpace(_search) || content.Name.ToLower().Contains(_search.ToLower());
            }

            return false;
        }

        public void Save()
        {
            ModMetadata modData = new();

            foreach (ModModel mod in Mods)
            {
                modData.Mods.Add(new Mod
                {
                    Name = mod.Name,
                    Path = mod.Path,
                    Enabled = SelectedMods.Contains(mod),
                });
            }

            JsonHelper.SerializeToFile(_modJsonPath, modData, _serializerContext.ModMetadata);
        }

        public void Delete(ModModel model)
        {
            var isSubdir = true;
            var pathToDelete = model.Path;
            var basePath = model.InSd ? ModLoader.GetSdModsBasePath() : ModLoader.GetModsBasePath();
            var modsDir = ModLoader.GetApplicationDir(basePath, _applicationId.ToString("x16"));

            if (new DirectoryInfo(model.Path).Parent?.FullName == modsDir)
            {
                isSubdir = false;
            }

            if (isSubdir)
            {
                var parentDir = String.Empty;

                foreach (var dir in Directory.GetDirectories(modsDir, "*", SearchOption.TopDirectoryOnly))
                {
                    if (Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).Contains(model.Path))
                    {
                        parentDir = dir;
                        break;
                    }
                }

                if (parentDir == String.Empty)
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(
                            LocaleKeys.DialogModDeleteNoParentMessage,
                            model.Path));
                    });
                    return;
                }
            }

            Logger.Info?.Print(LogClass.Application, $"Deleting mod at \"{pathToDelete}\"");
            Directory.Delete(pathToDelete, true);

            Mods.Remove(model);
            OnPropertyChanged(nameof(ModCount));
            Sort();
        }

        private void AddMod(DirectoryInfo directory)
        {
            string[] directories;

            try
            {
                directories = Directory.GetDirectories(directory.ToString(), "*", SearchOption.AllDirectories);
            }
            catch (Exception exception)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(
                        LocaleKeys.DialogLoadFileErrorMessage,
                        exception.ToString(),
                        directory));
                });
                return;
            }

            var destinationDir = ModLoader.GetApplicationDir(ModLoader.GetSdModsBasePath(), _applicationId.ToString("x16"));

            // TODO: More robust checking for valid mod folders
            var isDirectoryValid = true;

            if (directories.Length == 0)
            {
                isDirectoryValid = false;
            }

            if (!isDirectoryValid)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogModInvalidMessage]);
                });
                return;
            }

            foreach (var dir in directories)
            {
                string dirToCreate = dir.Replace(directory.Parent.ToString(), destinationDir);

                // Mod already exists
                if (Directory.Exists(dirToCreate))
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(
                            LocaleKeys.DialogLoadFileErrorMessage,
                            LocaleManager.Instance[LocaleKeys.DialogModAlreadyExistsMessage],
                            dirToCreate));
                    });

                    return;
                }

                Directory.CreateDirectory(dirToCreate);
            }

            var files = Directory.GetFiles(directory.ToString(), "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                File.Copy(file, file.Replace(directory.Parent.ToString(), destinationDir), true);
            }

            LoadMods(_applicationId);
        }

        public async void Add()
        {
            var result = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocaleManager.Instance[LocaleKeys.SelectModDialogTitle],
                AllowMultiple = true,
            });

            foreach (var folder in result)
            {
                AddMod(new DirectoryInfo(folder.Path.LocalPath));
            }
        }

        public void DeleteAll()
        {
            foreach (var mod in Mods)
            {
                Delete(mod);
            }

            Mods.Clear();
            OnPropertyChanged(nameof(ModCount));
            Sort();
        }

        public void EnableAll()
        {
            SelectedMods = new(Mods);
        }

        public void DisableAll()
        {
            SelectedMods.Clear();
        }
    }
}
