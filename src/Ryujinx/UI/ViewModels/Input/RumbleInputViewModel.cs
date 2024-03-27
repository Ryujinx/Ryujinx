namespace Ryujinx.Ava.UI.ViewModels.Input
{
    public class RumbleInputViewModel : BaseModel
    {
        private float _strongRumble;
        public float StrongRumble
        {
            get => _strongRumble;
            set
            {
                _strongRumble = value;
                OnPropertyChanged();
            }
        }

        private float _weakRumble;
        public float WeakRumble
        {
            get => _weakRumble;
            set
            {
                _weakRumble = value;
                OnPropertyChanged();
            }
        }
    }
}
