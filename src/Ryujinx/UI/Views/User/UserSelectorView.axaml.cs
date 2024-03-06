using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels;
using Button = Avalonia.Controls.Button;

namespace Ryujinx.Ava.UI.Views.User
{
    public partial class UserSelectorViews : UserControl
    {
        private NavigationDialogHost _parent;

        public UserProfileViewModel ViewModel { get; set; }

        public UserSelectorViews()
        {
            InitializeComponent();

            if (Program.PreviewerDetached)
            {
                AddHandler(Frame.NavigatedToEvent, (s, e) =>
                {
                    NavigatedTo(e);
                }, RoutingStrategies.Direct);
            }
        }

        private void NavigatedTo(NavigationEventArgs arg)
        {
            if (Program.PreviewerDetached)
            {
                if (arg.NavigationMode == NavigationMode.New)
                {
                    _parent = (NavigationDialogHost)arg.Parameter;
                    ViewModel = _parent.ViewModel;
                }

                if (arg.NavigationMode == NavigationMode.Back)
                {
                    ((ContentDialog)_parent.Parent).Title = LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle];
                }

                DataContext = ViewModel;
            }
        }

        private void Grid_PointerEntered(object sender, PointerEventArgs e)
        {
            if (sender is Grid grid)
            {
                if (grid.DataContext is UserProfile profile)
                {
                    profile.IsPointerOver = true;
                }
            }
        }

        private void Grid_OnPointerExited(object sender, PointerEventArgs e)
        {
            if (sender is Grid grid)
            {
                if (grid.DataContext is UserProfile profile)
                {
                    profile.IsPointerOver = false;
                }
            }
        }

        private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                int selectedIndex = listBox.SelectedIndex;

                if (selectedIndex >= 0 && selectedIndex < ViewModel.Profiles.Count)
                {
                    if (ViewModel.Profiles[selectedIndex] is UserProfile userProfile)
                    {
                        _parent?.AccountManager?.OpenUser(userProfile.UserId);

                        foreach (BaseModel profile in ViewModel.Profiles)
                        {
                            if (profile is UserProfile uProfile)
                            {
                                uProfile.UpdateState();
                            }
                        }
                    }
                }
            }
        }

        private void AddUser(object sender, RoutedEventArgs e)
        {
            _parent.AddUser();
        }

        private void EditUser(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is UserProfile userProfile)
                {
                    _parent.EditUser(userProfile);
                }
            }
        }

        private void ManageSaves(object sender, RoutedEventArgs e)
        {
            _parent.ManageSaves();
        }

        private void RecoverLostAccounts(object sender, RoutedEventArgs e)
        {
            _parent.RecoverLostAccounts();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            ((ContentDialog)_parent.Parent).Hide();
        }
    }
}
