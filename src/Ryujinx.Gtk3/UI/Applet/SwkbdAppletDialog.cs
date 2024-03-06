using Gtk;
using Ryujinx.HLE.HOS.Applets.SoftwareKeyboard;
using System;
using System.Linq;

namespace Ryujinx.UI.Applet
{
    public class SwkbdAppletDialog : MessageDialog
    {
        private int _inputMin;
        private int _inputMax;
#pragma warning disable IDE0052 // Remove unread private member
        private KeyboardMode _mode;
#pragma warning restore IDE0052

        private string _validationInfoText = "";

        private Predicate<int> _checkLength = _ => true;
        private Predicate<string> _checkInput = _ => true;

        private readonly Label _validationInfo;

        public Entry InputEntry { get; }
        public Button OkButton { get; }
        public Button CancelButton { get; }

        public SwkbdAppletDialog(Window parent) : base(parent, DialogFlags.Modal | DialogFlags.DestroyWithParent, MessageType.Question, ButtonsType.None, null)
        {
            SetDefaultSize(300, 0);

            _validationInfo = new Label()
            {
                Visible = false,
            };

            InputEntry = new Entry()
            {
                Visible = true,
            };

            InputEntry.Activated += OnInputActivated;
            InputEntry.Changed += OnInputChanged;

            OkButton = (Button)AddButton("OK", ResponseType.Ok);
            CancelButton = (Button)AddButton("Cancel", ResponseType.Cancel);

            ((Box)MessageArea).PackEnd(_validationInfo, true, true, 0);
            ((Box)MessageArea).PackEnd(InputEntry, true, true, 4);
        }

        private void ApplyValidationInfo()
        {
            _validationInfo.Visible = !string.IsNullOrEmpty(_validationInfoText);
            _validationInfo.Markup = _validationInfoText;
        }

        public void SetInputLengthValidation(int min, int max)
        {
            _inputMin = Math.Min(min, max);
            _inputMax = Math.Max(min, max);

            _validationInfo.Visible = false;

            if (_inputMin <= 0 && _inputMax == int.MaxValue) // Disable.
            {
                _validationInfo.Visible = false;

                _checkLength = _ => true;
            }
            else if (_inputMin > 0 && _inputMax == int.MaxValue)
            {
                _validationInfoText = $"<i>Must be at least {_inputMin} characters long.</i> ";

                _checkLength = length => _inputMin <= length;
            }
            else
            {
                _validationInfoText = $"<i>Must be {_inputMin}-{_inputMax} characters long.</i> ";

                _checkLength = length => _inputMin <= length && length <= _inputMax;
            }

            ApplyValidationInfo();
            OnInputChanged(this, EventArgs.Empty);
        }

        public void SetInputValidation(KeyboardMode mode)
        {
            _mode = mode;

            switch (mode)
            {
                case KeyboardMode.Numeric:
                    _validationInfoText += "<i>Must be 0-9 or '.' only.</i>";
                    _checkInput = text => text.All(NumericCharacterValidation.IsNumeric);
                    break;
                case KeyboardMode.Alphabet:
                    _validationInfoText += "<i>Must be non CJK-characters only.</i>";
                    _checkInput = text => text.All(value => !CJKCharacterValidation.IsCJK(value));
                    break;
                case KeyboardMode.ASCII:
                    _validationInfoText += "<i>Must be ASCII text only.</i>";
                    _checkInput = text => text.All(char.IsAscii);
                    break;
                default:
                    _checkInput = _ => true;
                    break;
            }

            ApplyValidationInfo();
            OnInputChanged(this, EventArgs.Empty);
        }

        private void OnInputActivated(object sender, EventArgs e)
        {
            if (OkButton.IsSensitive)
            {
                Respond(ResponseType.Ok);
            }
        }

        private void OnInputChanged(object sender, EventArgs e)
        {
            OkButton.Sensitive = _checkLength(InputEntry.Text.Length) && _checkInput(InputEntry.Text);
        }
    }
}
