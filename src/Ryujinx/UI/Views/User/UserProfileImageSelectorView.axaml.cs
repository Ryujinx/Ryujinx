using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.HLE.FileSystem;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;

namespace Ryujinx.Ava.UI.Views.User
{
    public partial class UserProfileImageSelectorView : UserControl
    {
        private ContentManager _contentManager;
        private NavigationDialogHost _parent;
        private TempProfile _profile;

        internal UserProfileImageSelectorViewModel ViewModel { get; private set; }

        public UserProfileImageSelectorView()
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
                        (_parent, _profile) = ((NavigationDialogHost, TempProfile))arg.Parameter;
                        _contentManager = _parent.ContentManager;

                        ((ContentDialog)_parent.Parent).Title = $"{LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle]} - {LocaleManager.Instance[LocaleKeys.ProfileImageSelectionHeader]}";

                        if (Program.PreviewerDetached)
                        {
                            DataContext = ViewModel = new UserProfileImageSelectorViewModel();
                            ViewModel.FirmwareFound = _contentManager.GetCurrentFirmwareVersion() != null;
                        }

                        break;
                    case NavigationMode.Back:
                        if (_profile.Image != null)
                        {
                            _parent.GoBack();
                        }
                        break;
                }
            }
        }

        private async void Import_OnClick(object sender, RoutedEventArgs e)
        {
            var window = this.GetVisualRoot() as Window;
            var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(LocaleManager.Instance[LocaleKeys.AllSupportedFormats])
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp" },
                        AppleUniformTypeIdentifiers = new[] { "public.jpeg", "public.png", "com.microsoft.bmp" },
                        MimeTypes = new[] { "image/jpeg", "image/png", "image/bmp" },
                    },
                },
            });

            if (result.Count > 0)
            {
                _profile.Image = ProcessProfileImage(File.ReadAllBytes(result[0].Path.LocalPath));
                _parent.GoBack();
            }
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            _parent.GoBack();
        }

        private void SelectFirmwareImage_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.FirmwareFound)
            {
                _parent.Navigate(typeof(UserFirmwareAvatarSelectorView), (_parent, _profile));
            }
        }

        private static byte[] ProcessProfileImage(byte[] buffer)
        {
            using var bitmap = SKBitmap.Decode(buffer);

            var resizedBitmap = bitmap.Resize(new SKImageInfo(256, 256), SKFilterQuality.High);

            using var streamJpg = new MemoryStream();

            if (resizedBitmap != null)
            {
                using var image = SKImage.FromBitmap(resizedBitmap);
                using var dataJpeg = image.Encode(SKEncodedImageFormat.Jpeg, 100);

                dataJpeg.SaveTo(streamJpg);
            }

            return streamJpg.ToArray();
        }
    }
}
