using Gtk;
using Pango;
using System;

namespace Ryujinx.UI.Windows
{
    public partial class UserProfilesManagerWindow : Window
    {
        private Box _mainBox;
        private Label _selectedLabel;
        private Box _selectedUserBox;
        private Image _selectedUserImage;
        private Box _selectedUserInfoBox;
        private Entry _selectedUserNameEntry;
        private Label _selectedUserIdLabel;
        private Box _selectedUserButtonsBox;
        private Button _saveProfileNameButton;
        private Button _changeProfileImageButton;
        private Box _usersTreeViewBox;
        private Label _availableUsersLabel;
        private ScrolledWindow _usersTreeViewWindow;
        private ListStore _tableStore;
        private TreeView _usersTreeView;
        private Box _bottomBox;
        private Button _addButton;
        private Button _deleteButton;
        private Button _closeButton;

        private void InitializeComponent()
        {
            //
            // UserProfilesManagerWindow
            //
            CanFocus = false;
            Resizable = false;
            Modal = true;
            WindowPosition = WindowPosition.Center;
            DefaultWidth = 620;
            DefaultHeight = 548;
            TypeHint = Gdk.WindowTypeHint.Dialog;

            //
            // _mainBox
            //
            _mainBox = new Box(Orientation.Vertical, 0);

            //
            // _selectedLabel
            //
            _selectedLabel = new Label("Selected User Profile:")
            {
                Margin = 15,
                Attributes = new AttrList(),
                Halign = Align.Start,
            };
            _selectedLabel.Attributes.Insert(new Pango.AttrWeight(Weight.Bold));

            //
            // _viewBox
            //
            _usersTreeViewBox = new Box(Orientation.Vertical, 0);

            //
            // _SelectedUserBox
            //
            _selectedUserBox = new Box(Orientation.Horizontal, 0)
            {
                MarginStart = 30,
            };

            //
            // _selectedUserImage
            //
            _selectedUserImage = new Image();

            //
            // _selectedUserInfoBox
            //
            _selectedUserInfoBox = new Box(Orientation.Vertical, 0)
            {
                Homogeneous = true,
            };

            //
            // _selectedUserNameEntry
            //
            _selectedUserNameEntry = new Entry("")
            {
                MarginStart = 15,
                MaxLength = (int)MaxProfileNameLength,
            };
            _selectedUserNameEntry.KeyReleaseEvent += SelectedUserNameEntry_KeyReleaseEvent;

            //
            // _selectedUserIdLabel
            //
            _selectedUserIdLabel = new Label("")
            {
                MarginTop = 15,
                MarginStart = 15,
            };

            //
            // _selectedUserButtonsBox
            //
            _selectedUserButtonsBox = new Box(Orientation.Vertical, 0)
            {
                MarginEnd = 30,
            };

            //
            // _saveProfileNameButton
            //
            _saveProfileNameButton = new Button()
            {
                Label = "Save Profile Name",
                CanFocus = true,
                ReceivesDefault = true,
                Sensitive = false,
            };
            _saveProfileNameButton.Clicked += EditProfileNameButton_Pressed;

            //
            // _changeProfileImageButton
            //
            _changeProfileImageButton = new Button()
            {
                Label = "Change Profile Image",
                CanFocus = true,
                ReceivesDefault = true,
                MarginTop = 10,
            };
            _changeProfileImageButton.Clicked += ChangeProfileImageButton_Pressed;

            //
            // _availableUsersLabel
            //
            _availableUsersLabel = new Label("Available User Profiles:")
            {
                Margin = 15,
                Attributes = new AttrList(),
                Halign = Align.Start,
            };
            _availableUsersLabel.Attributes.Insert(new Pango.AttrWeight(Weight.Bold));

            //
            // _usersTreeViewWindow
            //
            _usersTreeViewWindow = new ScrolledWindow()
            {
                ShadowType = ShadowType.In,
                CanFocus = true,
                Expand = true,
                MarginStart = 30,
                MarginEnd = 30,
                MarginBottom = 15,
            };

            //
            // _tableStore
            //
            _tableStore = new ListStore(typeof(bool), typeof(Gdk.Pixbuf), typeof(string), typeof(Gdk.RGBA));

            //
            // _usersTreeView
            //
            _usersTreeView = new TreeView(_tableStore)
            {
                HoverSelection = true,
                HeadersVisible = false,
            };
            _usersTreeView.RowActivated += UsersTreeView_Activated;

            //
            // _bottomBox
            //
            _bottomBox = new Box(Orientation.Horizontal, 0)
            {
                MarginStart = 30,
                MarginEnd = 30,
                MarginBottom = 15,
            };

            //
            // _addButton
            //
            _addButton = new Button()
            {
                Label = "Add New Profile",
                CanFocus = true,
                ReceivesDefault = true,
                HeightRequest = 35,
            };
            _addButton.Clicked += AddButton_Pressed;

            //
            // _deleteButton
            //
            _deleteButton = new Button()
            {
                Label = "Delete Selected Profile",
                CanFocus = true,
                ReceivesDefault = true,
                HeightRequest = 35,
                MarginStart = 10,
            };
            _deleteButton.Clicked += DeleteButton_Pressed;

            //
            // _closeButton
            //
            _closeButton = new Button()
            {
                Label = "Close",
                CanFocus = true,
                ReceivesDefault = true,
                HeightRequest = 35,
                WidthRequest = 80,
            };
            _closeButton.Clicked += CloseButton_Pressed;

            ShowComponent();
        }

        private void ShowComponent()
        {
            _usersTreeViewWindow.Add(_usersTreeView);

            _usersTreeViewBox.Add(_usersTreeViewWindow);
            _bottomBox.PackStart(_addButton, false, false, 0);
            _bottomBox.PackStart(_deleteButton, false, false, 0);
            _bottomBox.PackEnd(_closeButton, false, false, 0);

            _selectedUserInfoBox.Add(_selectedUserNameEntry);
            _selectedUserInfoBox.Add(_selectedUserIdLabel);

            _selectedUserButtonsBox.Add(_saveProfileNameButton);
            _selectedUserButtonsBox.Add(_changeProfileImageButton);

            _selectedUserBox.Add(_selectedUserImage);
            _selectedUserBox.PackStart(_selectedUserInfoBox, false, false, 0);
            _selectedUserBox.PackEnd(_selectedUserButtonsBox, false, false, 0);

            _mainBox.PackStart(_selectedLabel, false, false, 0);
            _mainBox.PackStart(_selectedUserBox, false, true, 0);
            _mainBox.PackStart(_availableUsersLabel, false, false, 0);
            _mainBox.Add(_usersTreeViewBox);
            _mainBox.Add(_bottomBox);

            Add(_mainBox);

            ShowAll();
        }
    }
}
