using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using System;
using UserProfile = Ryujinx.Ava.UI.Models.UserProfile;

namespace Ryujinx.Ava.UI.Views.User
{
    public partial class UserEditorView : UserControl
    {
        private NavigationDialogHost _parent;
        private UserProfile _profile;
        private bool _isNewUser;

        public TempProfile TempProfile { get; set; }
        public static uint MaxProfileNameLength => 0x20;
        public bool IsDeletable => _profile.UserId != AccountManager.DefaultUserId;

        public UserEditorView()
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
                        var (parent, profile, isNewUser) = ((NavigationDialogHost parent, UserProfile profile, bool isNewUser))arg.Parameter;
                        _isNewUser = isNewUser;
                        _profile = profile;
                        TempProfile = new TempProfile(_profile);

                        _parent = parent;
                        break;
                }

                ((ContentDialog)_parent.Parent).Title = $"{LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle]} - " +
                                                        $"{(_isNewUser ? LocaleManager.Instance[LocaleKeys.UserEditorTitleCreate] : LocaleManager.Instance[LocaleKeys.UserEditorTitle])}";

                DataContext = TempProfile;

                AddPictureButton.IsVisible = _isNewUser;
                ChangePictureButton.IsVisible = !_isNewUser;
                IdLabel.IsVisible = _profile != null;
                IdText.IsVisible = _profile != null;
                if (!_isNewUser && IsDeletable)
                {
                    DeleteButton.IsVisible = true;
                }
                else
                {
                    DeleteButton.IsVisible = false;
                }
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isNewUser)
            {
                if (TempProfile.Name != String.Empty || TempProfile.Image != null)
                {
                    if (await ContentDialogHelper.CreateChoiceDialog(
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesTitle],
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesMessage],
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesSubMessage]))
                    {
                        _parent?.GoBack();
                    }
                }
                else
                {
                    _parent?.GoBack();
                }
            }
            else
            {
                if (_profile.Name != TempProfile.Name || _profile.Image != TempProfile.Image)
                {
                    if (await ContentDialogHelper.CreateChoiceDialog(
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesTitle],
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesMessage],
                            LocaleManager.Instance[LocaleKeys.DialogUserProfileUnsavedChangesSubMessage]))
                    {
                        _parent?.GoBack();
                    }
                }
                else
                {
                    _parent?.GoBack();
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            _parent.DeleteUser(_profile);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DataValidationErrors.ClearErrors(NameBox);

            if (string.IsNullOrWhiteSpace(TempProfile.Name))
            {
                DataValidationErrors.SetError(NameBox, new DataValidationException(LocaleManager.Instance[LocaleKeys.UserProfileEmptyNameError]));

                return;
            }

            if (TempProfile.Image == null)
            {
                _parent.Navigate(typeof(UserProfileImageSelectorView), (_parent, TempProfile));

                return;
            }

            if (_profile != null && !_isNewUser)
            {
                _profile.Name = TempProfile.Name;
                _profile.Image = TempProfile.Image;
                _profile.UpdateState();
                _parent.AccountManager.SetUserName(_profile.UserId, _profile.Name);
                _parent.AccountManager.SetUserImage(_profile.UserId, _profile.Image);
            }
            else if (_isNewUser)
            {
                _parent.AccountManager.AddUser(TempProfile.Name, TempProfile.Image, TempProfile.UserId);
            }
            else
            {
                return;
            }

            _parent?.GoBack();
        }

        public void SelectProfileImage()
        {
            _parent.Navigate(typeof(UserProfileImageSelectorView), (_parent, TempProfile));
        }

        private void ChangePictureButton_Click(object sender, RoutedEventArgs e)
        {
            if (_profile != null || _isNewUser)
            {
                SelectProfileImage();
            }
        }
    }
}
