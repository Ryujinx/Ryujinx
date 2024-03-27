using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.ViewModels.Input;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Views.Input
{
    public partial class RumbleInputView : UserControl
    {
        private readonly RumbleInputViewModel _viewModel;

        public RumbleInputView()
        {
            InitializeComponent();
        }

        public RumbleInputView(ControllerInputViewModel viewModel)
        {
            var config = viewModel.Config;

            _viewModel = new RumbleInputViewModel
            {
                StrongRumble = config.StrongRumble,
                WeakRumble = config.WeakRumble,
            };

            InitializeComponent();

            DataContext = _viewModel;
        }

        public static async Task Show(ControllerInputViewModel viewModel)
        {
            RumbleInputView content = new(viewModel);

            ContentDialog contentDialog = new()
            {
                Title = LocaleManager.Instance[LocaleKeys.ControllerRumbleTitle],
                PrimaryButtonText = LocaleManager.Instance[LocaleKeys.ControllerSettingsSave],
                SecondaryButtonText = "",
                CloseButtonText = LocaleManager.Instance[LocaleKeys.ControllerSettingsClose],
                Content = content,
            };

            contentDialog.PrimaryButtonClick += (sender, args) =>
            {
                var config = viewModel.Config;
                config.StrongRumble = content._viewModel.StrongRumble;
                config.WeakRumble = content._viewModel.WeakRumble;
            };

            await contentDialog.ShowAsync();
        }
    }
}
