namespace Ryujinx.Ava.UI.ViewModels.Input
{
    public class MotionInputViewModel : BaseModel
    {
        private int _slot;
        public int Slot
        {
            get => _slot;
            set
            {
                _slot = value;
                OnPropertyChanged();
            }
        }

        private int _altSlot;
        public int AltSlot
        {
            get => _altSlot;
            set
            {
                _altSlot = value;
                OnPropertyChanged();
            }
        }

        private string _dsuServerHost;
        public string DsuServerHost
        {
            get => _dsuServerHost;
            set
            {
                _dsuServerHost = value;
                OnPropertyChanged();
            }
        }

        private int _dsuServerPort;
        public int DsuServerPort
        {
            get => _dsuServerPort;
            set
            {
                _dsuServerPort = value;
                OnPropertyChanged();
            }
        }

        private bool _mirrorInput;
        public bool MirrorInput
        {
            get => _mirrorInput;
            set
            {
                _mirrorInput = value;
                OnPropertyChanged();
            }
        }

        private int _sensitivity;
        public int Sensitivity
        {
            get => _sensitivity;
            set
            {
                _sensitivity = value;
                OnPropertyChanged();
            }
        }

        private double _gryoDeadzone;
        public double GyroDeadzone
        {
            get => _gryoDeadzone;
            set
            {
                _gryoDeadzone = value;
                OnPropertyChanged();
            }
        }

        private bool _enableCemuHookMotion;
        public bool EnableCemuHookMotion
        {
            get => _enableCemuHookMotion;
            set
            {
                _enableCemuHookMotion = value;
                OnPropertyChanged();
            }
        }
    }
}
