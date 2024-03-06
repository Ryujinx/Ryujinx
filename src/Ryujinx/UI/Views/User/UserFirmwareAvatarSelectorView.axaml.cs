using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.HLE.FileSystem;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using Image = SixLabors.ImageSharp.Image;

namespace Ryujinx.Ava.UI.Views.User
{
    public partial class UserFirmwareAvatarSelectorView : UserControl
    {
        private NavigationDialogHost _parent;
        private TempProfile _profile;

        public UserFirmwareAvatarSelectorView(ContentManager contentManager)
        {
            ContentManager = contentManager;

            DataContext = ViewModel;

            InitializeComponent();
        }

        public UserFirmwareAvatarSelectorView()
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
                if (arg.NavigationMode == NavigationMode.New)
                {
                    (_parent, _profile) = ((NavigationDialogHost, TempProfile))arg.Parameter;
                    ContentManager = _parent.ContentManager;
                    if (Program.PreviewerDetached)
                    {
                        ViewModel = new UserFirmwareAvatarSelectorViewModel();
                    }

                    DataContext = ViewModel;
                }
            }
        }

        public ContentManager ContentManager { get; private set; }

        internal UserFirmwareAvatarSelectorViewModel ViewModel { get; set; }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            _parent.GoBack();
        }

        private void ChooseButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedImage != null)
            {
                MemoryStream streamJpg = new();
                Image avatarImage = Image.Load(ViewModel.SelectedImage, new PngDecoder());

                avatarImage.Mutate(x => x.BackgroundColor(new Rgba32(
                    ViewModel.BackgroundColor.R,
                    ViewModel.BackgroundColor.G,
                    ViewModel.BackgroundColor.B,
                    ViewModel.BackgroundColor.A)));
                avatarImage.SaveAsJpeg(streamJpg);

                _profile.Image = streamJpg.ToArray();

                _parent.GoBack();
            }
        }
    }
}
