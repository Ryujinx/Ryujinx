using Gtk;

namespace Ryujinx.UI.Windows
{
    public partial class AmiiboWindow : Window
    {
        private Box _mainBox;
        private ButtonBox _buttonBox;
        private Button _scanButton;
        private Button _cancelButton;
        private CheckButton _randomUuidCheckBox;
        private Box _amiiboBox;
        private Box _amiiboHeadBox;
        private Box _amiiboSeriesBox;
        private Label _amiiboSeriesLabel;
        private ComboBoxText _amiiboSeriesComboBox;
        private Box _amiiboCharsBox;
        private Label _amiiboCharsLabel;
        private ComboBoxText _amiiboCharsComboBox;
        private CheckButton _showAllCheckBox;
        private Image _amiiboImage;
        private Label _gameUsageLabel;

        private void InitializeComponent()
        {
            //
            // AmiiboWindow
            //
            CanFocus = false;
            Resizable = false;
            Modal = true;
            WindowPosition = WindowPosition.Center;
            DefaultWidth = 600;
            DefaultHeight = 470;
            TypeHint = Gdk.WindowTypeHint.Dialog;

            //
            // _mainBox
            //
            _mainBox = new Box(Orientation.Vertical, 2);

            //
            // _buttonBox
            //
            _buttonBox = new ButtonBox(Orientation.Horizontal)
            {
                Margin = 20,
                LayoutStyle = ButtonBoxStyle.End,
            };

            //
            // _scanButton
            //
            _scanButton = new Button()
            {
                Label = "Scan It!",
                CanFocus = true,
                ReceivesDefault = true,
                MarginStart = 10,
            };
            _scanButton.Clicked += ScanButton_Pressed;

            //
            // _randomUuidCheckBox
            //
            _randomUuidCheckBox = new CheckButton()
            {
                Label = "Hack: Use Random Tag Uuid",
                TooltipText = "This allows multiple scans of a single Amiibo.\n(used in The Legend of Zelda: Breath of the Wild)",
            };

            //
            // _cancelButton
            //
            _cancelButton = new Button()
            {
                Label = "Cancel",
                CanFocus = true,
                ReceivesDefault = true,
                MarginStart = 10,
            };
            _cancelButton.Clicked += CancelButton_Pressed;

            //
            // _amiiboBox
            //
            _amiiboBox = new Box(Orientation.Vertical, 0);

            //
            // _amiiboHeadBox
            //
            _amiiboHeadBox = new Box(Orientation.Horizontal, 0)
            {
                Margin = 20,
                Hexpand = true,
            };

            //
            // _amiiboSeriesBox
            //
            _amiiboSeriesBox = new Box(Orientation.Horizontal, 0)
            {
                Hexpand = true,
            };

            //
            // _amiiboSeriesLabel
            //
            _amiiboSeriesLabel = new Label("Amiibo Series:");

            //
            // _amiiboSeriesComboBox
            //
            _amiiboSeriesComboBox = new ComboBoxText();

            //
            // _amiiboCharsBox
            //
            _amiiboCharsBox = new Box(Orientation.Horizontal, 0)
            {
                Hexpand = true,
            };

            //
            // _amiiboCharsLabel
            //
            _amiiboCharsLabel = new Label("Character:");

            //
            // _amiiboCharsComboBox
            //
            _amiiboCharsComboBox = new ComboBoxText();

            //
            // _showAllCheckBox
            //
            _showAllCheckBox = new CheckButton()
            {
                Label = "Show All Amiibo",
            };

            //
            // _amiiboImage
            //
            _amiiboImage = new Image()
            {
                HeightRequest = 350,
                WidthRequest = 350,
            };

            //
            // _gameUsageLabel
            //
            _gameUsageLabel = new Label("")
            {
                MarginTop = 20,
            };

            ShowComponent();
        }

        private void ShowComponent()
        {
            _buttonBox.Add(_showAllCheckBox);
            _buttonBox.Add(_randomUuidCheckBox);
            _buttonBox.Add(_scanButton);
            _buttonBox.Add(_cancelButton);

            _amiiboSeriesBox.Add(_amiiboSeriesLabel);
            _amiiboSeriesBox.Add(_amiiboSeriesComboBox);

            _amiiboCharsBox.Add(_amiiboCharsLabel);
            _amiiboCharsBox.Add(_amiiboCharsComboBox);

            _amiiboHeadBox.Add(_amiiboSeriesBox);
            _amiiboHeadBox.Add(_amiiboCharsBox);

            _amiiboBox.PackStart(_amiiboHeadBox, true, true, 0);
            _amiiboBox.PackEnd(_gameUsageLabel, false, false, 0);
            _amiiboBox.PackEnd(_amiiboImage, false, false, 0);

            _mainBox.Add(_amiiboBox);
            _mainBox.PackEnd(_buttonBox, false, false, 0);

            Add(_mainBox);

            ShowAll();
        }
    }
}
