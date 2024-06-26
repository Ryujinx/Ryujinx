using Gtk;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using Ryujinx.HLE.Utilities;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Widgets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GUI = Gtk.Builder.ObjectAttribute;

namespace Ryujinx.UI.Windows
{
    public class DlcWindow : Window
    {
        private readonly VirtualFileSystem _virtualFileSystem;
        private readonly string _applicationId;
        private readonly string _dlcJsonPath;
        private readonly List<DownloadableContentContainer> _dlcContainerList;

        private static readonly DownloadableContentJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());

#pragma warning disable CS0649, IDE0044 // Field is never assigned to, Add readonly modifier
        [GUI] Label _baseTitleInfoLabel;
        [GUI] TreeView _dlcTreeView;
        [GUI] TreeSelection _dlcTreeSelection;
#pragma warning restore CS0649, IDE0044

        public DlcWindow(VirtualFileSystem virtualFileSystem, string titleId, ApplicationData applicationData) : this(new Builder("Ryujinx.Gtk3.UI.Windows.DlcWindow.glade"), virtualFileSystem, titleId, applicationData) { }

        private DlcWindow(Builder builder, VirtualFileSystem virtualFileSystem, string applicationId, ApplicationData applicationData) : base(builder.GetRawOwnedObject("_dlcWindow"))
        {
            builder.Autoconnect(this);

            _applicationId = applicationId;
            _virtualFileSystem = virtualFileSystem;
            _dlcJsonPath = System.IO.Path.Combine(AppDataManager.GamesDirPath, _applicationId, "dlc.json");
            _baseTitleInfoLabel.Text = $"DLC Available for {applicationData.Name} [{applicationId.ToUpper()}]";

            try
            {
                _dlcContainerList = JsonHelper.DeserializeFromFile(_dlcJsonPath, _serializerContext.ListDownloadableContentContainer);
            }
            catch
            {
                _dlcContainerList = new List<DownloadableContentContainer>();
            }

            _dlcTreeView.Model = new TreeStore(typeof(bool), typeof(string), typeof(string));

            CellRendererToggle enableToggle = new();
            enableToggle.Toggled += (sender, args) =>
            {
                _dlcTreeView.Model.GetIter(out TreeIter treeIter, new TreePath(args.Path));
                bool newValue = !(bool)_dlcTreeView.Model.GetValue(treeIter, 0);
                _dlcTreeView.Model.SetValue(treeIter, 0, newValue);

                if (_dlcTreeView.Model.IterChildren(out TreeIter childIter, treeIter))
                {
                    do
                    {
                        _dlcTreeView.Model.SetValue(childIter, 0, newValue);
                    }
                    while (_dlcTreeView.Model.IterNext(ref childIter));
                }
            };

            _dlcTreeView.AppendColumn("Enabled", enableToggle, "active", 0);
            _dlcTreeView.AppendColumn("ApplicationId", new CellRendererText(), "text", 1);
            _dlcTreeView.AppendColumn("Path", new CellRendererText(), "text", 2);

            foreach (DownloadableContentContainer dlcContainer in _dlcContainerList)
            {
                if (File.Exists(dlcContainer.ContainerPath))
                {
                    // The parent tree item has its own "enabled" check box, but it's the actual
                    // nca entries that store the enabled / disabled state. A bit of a UI inconsistency.
                    // Maybe a tri-state check box would be better, but for now we check the parent
                    // "enabled" box if all child NCAs are enabled. Usually fine since each nsp has only one nca.
                    bool areAllContentPacksEnabled = dlcContainer.DownloadableContentNcaList.TrueForAll((nca) => nca.Enabled);
                    TreeIter parentIter = ((TreeStore)_dlcTreeView.Model).AppendValues(areAllContentPacksEnabled, "", dlcContainer.ContainerPath);

                    using IFileSystem partitionFileSystem = PartitionFileSystemUtils.OpenApplicationFileSystem(dlcContainer.ContainerPath, _virtualFileSystem, false);

                    if (partitionFileSystem == null)
                    {
                        continue;
                    }

                    foreach (DownloadableContentNca dlcNca in dlcContainer.DownloadableContentNcaList)
                    {
                        using var ncaFile = new UniqueRef<IFile>();

                        partitionFileSystem.OpenFile(ref ncaFile.Ref, dlcNca.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                        Nca nca = TryCreateNca(ncaFile.Get.AsStorage(), dlcContainer.ContainerPath);

                        if (nca != null)
                        {
                            ((TreeStore)_dlcTreeView.Model).AppendValues(parentIter, dlcNca.Enabled, nca.Header.TitleId.ToString("X16"), dlcNca.FullPath);
                        }
                    }
                }
                else
                {
                    // DLC file moved or renamed. Allow the user to remove it without crashing the whole dialog.
                    TreeIter parentIter = ((TreeStore)_dlcTreeView.Model).AppendValues(false, "", $"(MISSING) {dlcContainer.ContainerPath}");
                }
            }

            // NOTE: Try to load downloadable contents from PFS last to preserve enabled state.
            AddDlc(applicationData.Path, true);
        }

        private Nca TryCreateNca(IStorage ncaStorage, string containerPath)
        {
            try
            {
                return new Nca(_virtualFileSystem.KeySet, ncaStorage);
            }
            catch (Exception exception)
            {
                GtkDialog.CreateErrorDialog($"{exception.Message}. Errored File: {containerPath}");
            }

            return null;
        }

        private void AddDlc(string path, bool ignoreNotFound = false)
        {
            if (!File.Exists(path) || _dlcContainerList.Any(x => x.ContainerPath == path))
            {
                return;
            }

            using IFileSystem partitionFileSystem = PartitionFileSystemUtils.OpenApplicationFileSystem(path, _virtualFileSystem);

            bool containsDlc = false;

            TreeIter? parentIter = null;

            foreach (DirectoryEntryEx fileEntry in partitionFileSystem.EnumerateEntries("/", "*.nca"))
            {
                using var ncaFile = new UniqueRef<IFile>();

                partitionFileSystem.OpenFile(ref ncaFile.Ref, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                Nca nca = TryCreateNca(ncaFile.Get.AsStorage(), path);

                if (nca == null)
                {
                    continue;
                }

                if (nca.Header.ContentType == NcaContentType.PublicData)
                {
                    if (nca.GetProgramIdBase() != (ulong.Parse(_applicationId, NumberStyles.HexNumber) & ~0x1FFFUL))
                    {
                        continue;
                    }

                    parentIter ??= ((TreeStore)_dlcTreeView.Model).AppendValues(true, "", path);

                    ((TreeStore)_dlcTreeView.Model).AppendValues(parentIter.Value, true, nca.Header.TitleId.ToString("X16"), fileEntry.FullPath);
                    containsDlc = true;
                }
            }

            if (!containsDlc && !ignoreNotFound)
            {
                GtkDialog.CreateErrorDialog("The specified file does not contain DLC for the selected title!");
            }
        }

        private void AddButton_Clicked(object sender, EventArgs args)
        {
            FileChooserNative fileChooser = new("Select DLC files", this, FileChooserAction.Open, "Add", "Cancel")
            {
                SelectMultiple = true,
            };

            FileFilter filter = new()
            {
                Name = "Switch Game DLCs",
            };
            filter.AddPattern("*.nsp");

            fileChooser.AddFilter(filter);

            if (fileChooser.Run() == (int)ResponseType.Accept)
            {
                foreach (string containerPath in fileChooser.Filenames)
                {
                    AddDlc(containerPath);
                }
            }

            fileChooser.Dispose();
        }

        private void RemoveButton_Clicked(object sender, EventArgs args)
        {
            if (_dlcTreeSelection.GetSelected(out ITreeModel treeModel, out TreeIter treeIter))
            {
                if (_dlcTreeView.Model.IterParent(out TreeIter parentIter, treeIter) && _dlcTreeView.Model.IterNChildren(parentIter) <= 1)
                {
                    ((TreeStore)treeModel).Remove(ref parentIter);
                }
                else
                {
                    ((TreeStore)treeModel).Remove(ref treeIter);
                }
            }
        }

        private void RemoveAllButton_Clicked(object sender, EventArgs args)
        {
            List<TreeIter> toRemove = new();

            if (_dlcTreeView.Model.GetIterFirst(out TreeIter iter))
            {
                do
                {
                    toRemove.Add(iter);
                }
                while (_dlcTreeView.Model.IterNext(ref iter));
            }

            foreach (TreeIter i in toRemove)
            {
                TreeIter j = i;
                ((TreeStore)_dlcTreeView.Model).Remove(ref j);
            }
        }

        private void SaveButton_Clicked(object sender, EventArgs args)
        {
            _dlcContainerList.Clear();

            if (_dlcTreeView.Model.GetIterFirst(out TreeIter parentIter))
            {
                do
                {
                    if (_dlcTreeView.Model.IterChildren(out TreeIter childIter, parentIter))
                    {
                        DownloadableContentContainer dlcContainer = new()
                        {
                            ContainerPath = (string)_dlcTreeView.Model.GetValue(parentIter, 2),
                            DownloadableContentNcaList = new List<DownloadableContentNca>(),
                        };

                        do
                        {
                            dlcContainer.DownloadableContentNcaList.Add(new DownloadableContentNca
                            {
                                Enabled = (bool)_dlcTreeView.Model.GetValue(childIter, 0),
                                TitleId = Convert.ToUInt64(_dlcTreeView.Model.GetValue(childIter, 1).ToString(), 16),
                                FullPath = (string)_dlcTreeView.Model.GetValue(childIter, 2),
                            });
                        }
                        while (_dlcTreeView.Model.IterNext(ref childIter));

                        _dlcContainerList.Add(dlcContainer);
                    }
                }
                while (_dlcTreeView.Model.IterNext(ref parentIter));
            }

            JsonHelper.SerializeToFile(_dlcJsonPath, _dlcContainerList, _serializerContext.ListDownloadableContentContainer);

            Dispose();
        }

        private void CancelButton_Clicked(object sender, EventArgs args)
        {
            Dispose();
        }
    }
}
