using Avalonia.Svg.Skia;
using Ryujinx.Ava.UI.Models.Input;
using Ryujinx.Ava.UI.Views.Input;

namespace Ryujinx.Ava.UI.ViewModels.Input
{
    public class ControllerInputViewModel : BaseModel
    {
        private GamepadInputConfig _config;
        public GamepadInputConfig Config
        {
            get => _config;
            set
            {
                _config = value;

                OnPropertyChanged();
            }
        }

        private StickVisualizer _visualizer;
        public StickVisualizer Visualizer
        {
            get => _visualizer;
            set
            {
                _visualizer = value;

                OnPropertyChanged();
            }
        }

        private bool _isLeft;
        public bool IsLeft
        {
            get => _isLeft;
            set
            {
                _isLeft = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSides));
            }
        }

        private bool _isRight;
        public bool IsRight
        {
            get => _isRight;
            set
            {
                _isRight = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSides));
            }
        }

        public bool HasSides => IsLeft ^ IsRight;

        private SvgImage _image;
        public SvgImage Image
        {
            get => _image;
            set
            {
                _image = value;
                OnPropertyChanged();
            }
        }

        public readonly InputViewModel ParentModel;

        public ControllerInputViewModel(InputViewModel model, GamepadInputConfig config, StickVisualizer visualizer)
        {
            ParentModel = model;
            Visualizer = visualizer;
            model.NotifyChangesEvent += OnParentModelChanged;
            OnParentModelChanged();
            Config = config;
        }

        public async void ShowMotionConfig()
        {
            await MotionInputView.Show(this);
        }

        public async void ShowRumbleConfig()
        {
            await RumbleInputView.Show(this);
        }

        public void OnParentModelChanged()
        {
            IsLeft = ParentModel.IsLeft;
            IsRight = ParentModel.IsRight;
            Image = ParentModel.Image;
        }
    }
}
