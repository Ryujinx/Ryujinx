using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.HLE.HOS.Applets;
using Ryujinx.HLE.HOS.Applets.SoftwareKeyboard;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Controls
{
    internal partial class SwkbdAppletDialog : UserControl
    {
        private Predicate<int> _checkLength = _ => true;
        private Predicate<string> _checkInput = _ => true;
        private int _inputMax;
        private int _inputMin;
        private readonly string _placeholder;

        private ContentDialog _host;

        public SwkbdAppletDialog(string mainText, string secondaryText, string placeholder, string message)
        {
            MainText = mainText;
            SecondaryText = secondaryText;
            Message = message ?? "";
            DataContext = this;
            _placeholder = placeholder;
            InitializeComponent();

            Input.Watermark = _placeholder;

            Input.AddHandler(TextInputEvent, Message_TextInput, RoutingStrategies.Tunnel, true);
        }

        public SwkbdAppletDialog()
        {
            DataContext = this;
            InitializeComponent();
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            // FIXME: This does not work. Might be a bug in Avalonia with DialogHost
            //        Currently focus will be redirected to the overlay window instead.
            Input.Focus();
        }

        public string Message { get; set; } = "";
        public string MainText { get; set; } = "";
        public string SecondaryText { get; set; } = "";

        public static async Task<(UserResult Result, string Input)> ShowInputDialog(string title, SoftwareKeyboardUIArgs args)
        {
            ContentDialog contentDialog = new();

            UserResult result = UserResult.Cancel;

            SwkbdAppletDialog content = new(args.HeaderText, args.SubtitleText, args.GuideText, args.InitialText);

            string input = string.Empty;

            content.SetInputLengthValidation(args.StringLengthMin, args.StringLengthMax);
            content.SetInputValidation(args.KeyboardMode);

            content._host = contentDialog;
            contentDialog.Title = title;
            contentDialog.PrimaryButtonText = args.SubmitText;
            contentDialog.IsPrimaryButtonEnabled = content._checkLength(content.Message.Length);
            contentDialog.SecondaryButtonText = "";
            contentDialog.CloseButtonText = LocaleManager.Instance[LocaleKeys.InputDialogCancel];
            contentDialog.Content = content;

            void Handler(ContentDialog sender, ContentDialogClosedEventArgs eventArgs)
            {
                if (eventArgs.Result == ContentDialogResult.Primary)
                {
                    result = UserResult.Ok;
                    input = content.Input.Text;
                }
            }

            contentDialog.Closed += Handler;

            await ContentDialogHelper.ShowAsync(contentDialog);

            return (result, input);
        }

        private void ApplyValidationInfo(string text)
        {
            Error.IsVisible = !string.IsNullOrEmpty(text);
            Error.Text = text;
        }

        public void SetInputLengthValidation(int min, int max)
        {
            _inputMin = Math.Min(min, max);
            _inputMax = Math.Max(min, max);

            Error.IsVisible = false;
            Error.FontStyle = FontStyle.Italic;

            string validationInfoText = "";

            if (_inputMin <= 0 && _inputMax == int.MaxValue) // Disable.
            {
                Error.IsVisible = false;

                _checkLength = length => true;
            }
            else if (_inputMin > 0 && _inputMax == int.MaxValue)
            {
                validationInfoText = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.SwkbdMinCharacters, _inputMin);

                _checkLength = length => _inputMin <= length;
            }
            else
            {
                validationInfoText = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.SwkbdMinRangeCharacters, _inputMin, _inputMax);

                _checkLength = length => _inputMin <= length && length <= _inputMax;
            }

            ApplyValidationInfo(validationInfoText);
            Message_TextInput(this, new TextInputEventArgs());
        }

        private void SetInputValidation(KeyboardMode mode)
        {
            string validationInfoText = Error.Text;
            string localeText;
            switch (mode)
            {
                case KeyboardMode.Numeric:
                    localeText = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.SoftwareKeyboardModeNumeric);
                    validationInfoText = string.IsNullOrEmpty(validationInfoText) ? localeText : string.Join("\n", validationInfoText, localeText);
                    _checkInput = text => text.All(NumericCharacterValidation.IsNumeric);
                    break;
                case KeyboardMode.Alphabet:
                    localeText = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.SoftwareKeyboardModeAlphabet);
                    validationInfoText = string.IsNullOrEmpty(validationInfoText) ? localeText : string.Join("\n", validationInfoText, localeText);
                    _checkInput = text => text.All(value => !CJKCharacterValidation.IsCJK(value));
                    break;
                case KeyboardMode.ASCII:
                    localeText = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.SoftwareKeyboardModeASCII);
                    validationInfoText = string.IsNullOrEmpty(validationInfoText) ? localeText : string.Join("\n", validationInfoText, localeText);
                    _checkInput = text => text.All(char.IsAscii);
                    break;
                default:
                    _checkInput = _ => true;
                    break;
            }

            ApplyValidationInfo(validationInfoText);
            Message_TextInput(this, new TextInputEventArgs());
        }

        private void Message_TextInput(object sender, TextInputEventArgs e)
        {
            if (_host != null)
            {
                _host.IsPrimaryButtonEnabled = _checkLength(Message.Length) && _checkInput(Message);
            }
        }

        private void Message_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _host.IsPrimaryButtonEnabled)
            {
                _host.Hide(ContentDialogResult.Primary);
            }
            else
            {
                _host.IsPrimaryButtonEnabled = _checkLength(Message.Length) && _checkInput(Message);
            }
        }
    }
}
