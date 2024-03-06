using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Button = Avalonia.Controls.Button;
using UserId = LibHac.Fs.UserId;

namespace Ryujinx.Ava.UI.Views.User
{
    public partial class UserSaveManagerView : UserControl
    {
        internal UserSaveManagerViewModel ViewModel { get; private set; }

        private AccountManager _accountManager;
        private HorizonClient _horizonClient;
        private VirtualFileSystem _virtualFileSystem;
        private NavigationDialogHost _parent;

        public UserSaveManagerView()
        {
            InitializeComponent();
            AddHandler(Frame.NavigatedToEvent, (s, e) =>
            {
                NavigatedTo(e);
            }, RoutingStrategies.Direct);
        }

        private void NavigatedTo(NavigationEventArgs arg)
        {
            if (Program.PreviewerDetached)
            {
                switch (arg.NavigationMode)
                {
                    case NavigationMode.New:
                        var (parent, accountManager, client, virtualFileSystem) = ((NavigationDialogHost parent, AccountManager accountManager, HorizonClient client, VirtualFileSystem virtualFileSystem))arg.Parameter;
                        _accountManager = accountManager;
                        _horizonClient = client;
                        _virtualFileSystem = virtualFileSystem;

                        _parent = parent;
                        break;
                }

                DataContext = ViewModel = new UserSaveManagerViewModel(_accountManager);
                ((ContentDialog)_parent.Parent).Title = $"{LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle]} - {ViewModel.SaveManagerHeading}";

                Task.Run(LoadSaves);
            }
        }

        public void LoadSaves()
        {
            ViewModel.Saves.Clear();
            var saves = new ObservableCollection<SaveModel>();
            var saveDataFilter = SaveDataFilter.Make(
                programId: default,
                saveType: SaveDataType.Account,
                new UserId((ulong)_accountManager.LastOpenedUser.UserId.High, (ulong)_accountManager.LastOpenedUser.UserId.Low),
                saveDataId: default,
                index: default);

            using var saveDataIterator = new UniqueRef<SaveDataIterator>();

            _horizonClient.Fs.OpenSaveDataIterator(ref saveDataIterator.Ref, SaveDataSpaceId.User, in saveDataFilter).ThrowIfFailure();

            Span<SaveDataInfo> saveDataInfo = stackalloc SaveDataInfo[10];

            while (true)
            {
                saveDataIterator.Get.ReadSaveDataInfo(out long readCount, saveDataInfo).ThrowIfFailure();

                if (readCount == 0)
                {
                    break;
                }

                for (int i = 0; i < readCount; i++)
                {
                    var save = saveDataInfo[i];
                    if (save.ProgramId.Value != 0)
                    {
                        var saveModel = new SaveModel(save);
                        saves.Add(saveModel);
                    }
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.Saves = saves;
                ViewModel.Sort();
            });
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            _parent?.GoBack();
        }

        private void OpenLocation(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is SaveModel saveModel)
                {
                    ApplicationHelper.OpenSaveDir(saveModel.SaveId);
                }
            }
        }

        private async void Delete(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is SaveModel saveModel)
                {
                    var result = await ContentDialogHelper.CreateConfirmationDialog(LocaleManager.Instance[LocaleKeys.DeleteUserSave],
                        LocaleManager.Instance[LocaleKeys.IrreversibleActionNote],
                        LocaleManager.Instance[LocaleKeys.InputDialogYes],
                        LocaleManager.Instance[LocaleKeys.InputDialogNo], "");

                    if (result == UserResult.Yes)
                    {
                        _horizonClient.Fs.DeleteSaveData(SaveDataSpaceId.User, saveModel.SaveId);
                        ViewModel.Saves.Remove(saveModel);
                        ViewModel.Sort();
                    }
                }
            }
        }
    }
}
