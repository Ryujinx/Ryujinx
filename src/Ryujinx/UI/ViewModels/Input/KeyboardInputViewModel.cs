using Avalonia.Svg.Skia;
using Ryujinx.Ava.UI.Models.Input;

namespace Ryujinx.Ava.UI.ViewModels.Input
{
    public class KeyboardInputViewModel : BaseModel
    {
        private KeyboardInputConfig _config;
        public KeyboardInputConfig Config
        {
            get => _config;
            set
            {
                _config = value;
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

        public InputViewModel parentModel;

        public KeyboardInputViewModel(InputViewModel model, KeyboardInputConfig config)
        {
            parentModel = model;
            model.NotifyChangesEvent += OnParentModelChanged;
            OnParentModelChanged();
            Config = config;
        }

        public void OnParentModelChanged()
        {
            IsLeft = parentModel.IsLeft;
            IsRight = parentModel.IsRight;
            Image = parentModel.Image;
        }
    }
}
