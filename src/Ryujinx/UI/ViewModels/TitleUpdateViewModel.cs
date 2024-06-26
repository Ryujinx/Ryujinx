using Avalonia.Collections;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using Ryujinx.HLE.Utilities;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Application = Avalonia.Application;
using ContentType = LibHac.Ncm.ContentType;
using Path = System.IO.Path;
using SpanHelpers = LibHac.Common.SpanHelpers;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class TitleUpdateViewModel : BaseModel
    {
        public TitleUpdateMetadata TitleUpdateWindowData;
        public readonly string TitleUpdateJsonPath;
        private VirtualFileSystem VirtualFileSystem { get; }
        private ApplicationData ApplicationData { get; }

        private AvaloniaList<TitleUpdateModel> _titleUpdates = new();
        private AvaloniaList<object> _views = new();
        private object _selectedUpdate;

        private static readonly TitleUpdateMetadataJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public AvaloniaList<TitleUpdateModel> TitleUpdates
        {
            get => _titleUpdates;
            set
            {
                _titleUpdates = value;
                OnPropertyChanged();
            }
        }

        public AvaloniaList<object> Views
        {
            get => _views;
            set
            {
                _views = value;
                OnPropertyChanged();
            }
        }

        public object SelectedUpdate
        {
            get => _selectedUpdate;
            set
            {
                _selectedUpdate = value;
                OnPropertyChanged();
            }
        }

        public IStorageProvider StorageProvider;

        public TitleUpdateViewModel(VirtualFileSystem virtualFileSystem, ApplicationData applicationData)
        {
            VirtualFileSystem = virtualFileSystem;

            ApplicationData = applicationData;

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                StorageProvider = desktop.MainWindow.StorageProvider;
            }

            TitleUpdateJsonPath = Path.Combine(AppDataManager.GamesDirPath, ApplicationData.IdString, "updates.json");

            try
            {
                TitleUpdateWindowData = JsonHelper.DeserializeFromFile(TitleUpdateJsonPath, _serializerContext.TitleUpdateMetadata);
            }
            catch
            {
                Logger.Warning?.Print(LogClass.Application, $"Failed to deserialize title update data for {ApplicationData.IdString} at {TitleUpdateJsonPath}");

                TitleUpdateWindowData = new TitleUpdateMetadata
                {
                    Selected = "",
                    Paths = new List<string>(),
                };

                Save();
            }

            LoadUpdates();
        }

        private void LoadUpdates()
        {
            // Try to load updates from PFS first
            AddUpdate(ApplicationData.Path, true);

            foreach (string path in TitleUpdateWindowData.Paths)
            {
                AddUpdate(path);
            }

            TitleUpdateModel selected = TitleUpdates.FirstOrDefault(x => x.Path == TitleUpdateWindowData.Selected, null);

            SelectedUpdate = selected;

            // NOTE: Save the list again to remove leftovers.
            Save();
            SortUpdates();
        }

        public void SortUpdates()
        {
            var list = TitleUpdates.ToList();

            list.Sort((first, second) =>
            {
                if (string.IsNullOrEmpty(first.Control.DisplayVersionString.ToString()))
                {
                    return -1;
                }

                if (string.IsNullOrEmpty(second.Control.DisplayVersionString.ToString()))
                {
                    return 1;
                }

                return Version.Parse(first.Control.DisplayVersionString.ToString()).CompareTo(Version.Parse(second.Control.DisplayVersionString.ToString())) * -1;
            });

            Views.Clear();
            Views.Add(new BaseModel());
            Views.AddRange(list);

            if (SelectedUpdate == null)
            {
                SelectedUpdate = Views[0];
            }
            else if (!TitleUpdates.Contains(SelectedUpdate))
            {
                if (Views.Count > 1)
                {
                    SelectedUpdate = Views[1];
                }
                else
                {
                    SelectedUpdate = Views[0];
                }
            }
        }

        private void AddUpdate(string path, bool ignoreNotFound = false)
        {
            if (!File.Exists(path) || TitleUpdates.Any(x => x.Path == path))
            {
                return;
            }

            IntegrityCheckLevel checkLevel = ConfigurationState.Instance.System.EnableFsIntegrityChecks
                ? IntegrityCheckLevel.ErrorOnInvalid
                : IntegrityCheckLevel.None;

            try
            {
                using IFileSystem pfs = PartitionFileSystemUtils.OpenApplicationFileSystem(path, VirtualFileSystem);

                Dictionary<ulong, ContentMetaData> updates = pfs.GetContentData(ContentMetaType.Patch, VirtualFileSystem, checkLevel);

                Nca patchNca = null;
                Nca controlNca = null;

                if (updates.TryGetValue(ApplicationData.Id, out ContentMetaData content))
                {
                    patchNca = content.GetNcaByType(VirtualFileSystem.KeySet, ContentType.Program);
                    controlNca = content.GetNcaByType(VirtualFileSystem.KeySet, ContentType.Control);
                }

                if (controlNca != null && patchNca != null)
                {
                    ApplicationControlProperty controlData = new();

                    using UniqueRef<IFile> nacpFile = new();

                    controlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None).OpenFile(ref nacpFile.Ref, "/control.nacp".ToU8Span(), OpenMode.Read).ThrowIfFailure();
                    nacpFile.Get.Read(out _, 0, SpanHelpers.AsByteSpan(ref controlData), ReadOption.None).ThrowIfFailure();

                    TitleUpdates.Add(new TitleUpdateModel(controlData, path));
                }
                else
                {
                    if (!ignoreNotFound)
                    {
                        Dispatcher.UIThread.InvokeAsync(() => ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogUpdateAddUpdateErrorMessage]));
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.InvokeAsync(() => ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogLoadFileErrorMessage, ex.Message, path)));
            }
        }

        public void RemoveUpdate(TitleUpdateModel update)
        {
            TitleUpdates.Remove(update);

            SortUpdates();
        }

        public async Task Add()
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(LocaleManager.Instance[LocaleKeys.AllSupportedFormats])
                    {
                        Patterns = new[] { "*.nsp" },
                        AppleUniformTypeIdentifiers = new[] { "com.ryujinx.nsp" },
                        MimeTypes = new[] { "application/x-nx-nsp" },
                    },
                },
            });

            foreach (var file in result)
            {
                AddUpdate(file.Path.LocalPath);
            }

            SortUpdates();
        }

        public void Save()
        {
            TitleUpdateWindowData.Paths.Clear();
            TitleUpdateWindowData.Selected = "";

            foreach (TitleUpdateModel update in TitleUpdates)
            {
                TitleUpdateWindowData.Paths.Add(update.Path);

                if (update == SelectedUpdate)
                {
                    TitleUpdateWindowData.Selected = update.Path;
                }
            }

            JsonHelper.SerializeToFile(TitleUpdateJsonPath, TitleUpdateWindowData, _serializerContext.TitleUpdateMetadata);
        }
    }
}
