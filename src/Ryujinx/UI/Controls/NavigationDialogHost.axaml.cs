using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.Views.User;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserId = Ryujinx.HLE.HOS.Services.Account.Acc.UserId;
using UserProfile = Ryujinx.Ava.UI.Models.UserProfile;

namespace Ryujinx.Ava.UI.Controls
{
    public partial class NavigationDialogHost : UserControl
    {
        public AccountManager AccountManager { get; }
        public ContentManager ContentManager { get; }
        public VirtualFileSystem VirtualFileSystem { get; }
        public HorizonClient HorizonClient { get; }
        public UserProfileViewModel ViewModel { get; set; }

        public NavigationDialogHost()
        {
            InitializeComponent();
        }

        public NavigationDialogHost(AccountManager accountManager, ContentManager contentManager,
            VirtualFileSystem virtualFileSystem, HorizonClient horizonClient)
        {
            AccountManager = accountManager;
            ContentManager = contentManager;
            VirtualFileSystem = virtualFileSystem;
            HorizonClient = horizonClient;
            ViewModel = new UserProfileViewModel();
            LoadProfiles();

            if (contentManager.GetCurrentFirmwareVersion() != null)
            {
                Task.Run(() =>
                {
                    UserFirmwareAvatarSelectorViewModel.PreloadAvatars(contentManager, virtualFileSystem);
                });
            }
            InitializeComponent();
        }

        public void GoBack()
        {
            if (ContentFrame.BackStack.Count > 0)
            {
                ContentFrame.GoBack();
            }

            LoadProfiles();
        }

        public void Navigate(Type sourcePageType, object parameter)
        {
            ContentFrame.Navigate(sourcePageType, parameter);
        }

        public static async Task Show(AccountManager ownerAccountManager, ContentManager ownerContentManager,
            VirtualFileSystem ownerVirtualFileSystem, HorizonClient ownerHorizonClient)
        {
            var content = new NavigationDialogHost(ownerAccountManager, ownerContentManager, ownerVirtualFileSystem, ownerHorizonClient);
            ContentDialog contentDialog = new()
            {
                Title = LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle],
                PrimaryButtonText = "",
                SecondaryButtonText = "",
                CloseButtonText = "",
                Content = content,
                Padding = new Thickness(0),
            };

            contentDialog.Closed += (sender, args) =>
            {
                content.ViewModel.Dispose();
            };

            Style footer = new(x => x.Name("DialogSpace").Child().OfType<Border>());
            footer.Setters.Add(new Setter(IsVisibleProperty, false));

            contentDialog.Styles.Add(footer);

            await contentDialog.ShowAsync();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            Navigate(typeof(UserSelectorViews), this);
        }

        public void LoadProfiles()
        {
            ViewModel.Profiles.Clear();
            ViewModel.LostProfiles.Clear();

            var profiles = AccountManager.GetAllUsers().OrderBy(x => x.Name);

            foreach (var profile in profiles)
            {
                ViewModel.Profiles.Add(new UserProfile(profile, this));
            }

            var saveDataFilter = SaveDataFilter.Make(programId: default, saveType: SaveDataType.Account, default, saveDataId: default, index: default);

            using var saveDataIterator = new UniqueRef<SaveDataIterator>();

            HorizonClient.Fs.OpenSaveDataIterator(ref saveDataIterator.Ref, SaveDataSpaceId.User, in saveDataFilter).ThrowIfFailure();

            Span<SaveDataInfo> saveDataInfo = stackalloc SaveDataInfo[10];

            HashSet<UserId> lostAccounts = new();

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
                    var id = new UserId((long)save.UserId.Id.Low, (long)save.UserId.Id.High);
                    if (ViewModel.Profiles.Cast<UserProfile>().FirstOrDefault(x => x.UserId == id) == null)
                    {
                        lostAccounts.Add(id);
                    }
                }
            }

            foreach (var account in lostAccounts)
            {
                ViewModel.LostProfiles.Add(new UserProfile(new HLE.HOS.Services.Account.Acc.UserProfile(account, "", null), this));
            }

            ViewModel.Profiles.Add(new BaseModel());
        }

        public async void DeleteUser(UserProfile userProfile)
        {
            var lastUserId = AccountManager.LastOpenedUser.UserId;

            if (userProfile.UserId == lastUserId)
            {
                // If we are deleting the currently open profile, then we must open something else before deleting.
                var profile = ViewModel.Profiles.Cast<UserProfile>().FirstOrDefault(x => x.UserId != lastUserId);

                if (profile == null)
                {
                    static async void Action()
                    {
                        await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogUserProfileDeletionWarningMessage]);
                    }

                    Dispatcher.UIThread.Post(Action);

                    return;
                }

                AccountManager.OpenUser(profile.UserId);
            }

            var result = await ContentDialogHelper.CreateConfirmationDialog(
                LocaleManager.Instance[LocaleKeys.DialogUserProfileDeletionConfirmMessage],
                "",
                LocaleManager.Instance[LocaleKeys.InputDialogYes],
                LocaleManager.Instance[LocaleKeys.InputDialogNo],
                "");

            if (result == UserResult.Yes)
            {
                GoBack();
                AccountManager.DeleteUser(userProfile.UserId);
            }

            LoadProfiles();
        }

        public void AddUser()
        {
            Navigate(typeof(UserEditorView), (this, (UserProfile)null, true));
        }

        public void EditUser(UserProfile userProfile)
        {
            Navigate(typeof(UserEditorView), (this, userProfile, false));
        }

        public void RecoverLostAccounts()
        {
            Navigate(typeof(UserRecovererView), this);
        }

        public void ManageSaves()
        {
            Navigate(typeof(UserSaveManagerView), (this, AccountManager, HorizonClient, VirtualFileSystem));
        }
    }
}
