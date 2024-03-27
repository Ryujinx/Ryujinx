using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.ViewModels.Input;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Views.Input
{
    public partial class MotionInputView : UserControl
    {
        private readonly MotionInputViewModel _viewModel;

        public MotionInputView()
        {
            InitializeComponent();
        }

        public MotionInputView(ControllerInputViewModel viewModel)
        {
            var config = viewModel.Config;

            _viewModel = new MotionInputViewModel
            {
                Slot = config.Slot,
                AltSlot = config.AltSlot,
                DsuServerHost = config.DsuServerHost,
                DsuServerPort = config.DsuServerPort,
                MirrorInput = config.MirrorInput,
                Sensitivity = config.Sensitivity,
                GyroDeadzone = config.GyroDeadzone,
                EnableCemuHookMotion = config.EnableCemuHookMotion,
            };

            InitializeComponent();
            DataContext = _viewModel;
        }

        public static async Task Show(ControllerInputViewModel viewModel)
        {
            MotionInputView content = new(viewModel);

            ContentDialog contentDialog = new()
            {
                Title = LocaleManager.Instance[LocaleKeys.ControllerMotionTitle],
                PrimaryButtonText = LocaleManager.Instance[LocaleKeys.ControllerSettingsSave],
                SecondaryButtonText = "",
                CloseButtonText = LocaleManager.Instance[LocaleKeys.ControllerSettingsClose],
                Content = content,
            };
            contentDialog.PrimaryButtonClick += (sender, args) =>
            {
                var config = viewModel.Config;
                config.Slot = content._viewModel.Slot;
                config.Sensitivity = content._viewModel.Sensitivity;
                config.GyroDeadzone = content._viewModel.GyroDeadzone;
                config.AltSlot = content._viewModel.AltSlot;
                config.DsuServerHost = content._viewModel.DsuServerHost;
                config.DsuServerPort = content._viewModel.DsuServerPort;
                config.EnableCemuHookMotion = content._viewModel.EnableCemuHookMotion;
                config.MirrorInput = content._viewModel.MirrorInput;
            };

            await contentDialog.ShowAsync();
        }
    }
}
