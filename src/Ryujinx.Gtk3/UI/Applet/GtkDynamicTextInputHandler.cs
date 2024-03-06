using Gtk;
using Ryujinx.HLE.UI;
using Ryujinx.Input.GTK3;
using Ryujinx.UI.Widgets;
using System.Threading;

namespace Ryujinx.UI.Applet
{
    /// <summary>
    /// Class that forwards key events to a GTK Entry so they can be processed into text.
    /// </summary>
    internal class GtkDynamicTextInputHandler : IDynamicTextInputHandler
    {
        private readonly Window _parent;
        private readonly OffscreenWindow _inputToTextWindow = new();
        private readonly RawInputToTextEntry _inputToTextEntry = new();

        private bool _canProcessInput;

        public event DynamicTextChangedHandler TextChangedEvent;
        public event KeyPressedHandler KeyPressedEvent;
        public event KeyReleasedHandler KeyReleasedEvent;

        public bool TextProcessingEnabled
        {
            get
            {
                return Volatile.Read(ref _canProcessInput);
            }

            set
            {
                Volatile.Write(ref _canProcessInput, value);
            }
        }

        public GtkDynamicTextInputHandler(Window parent)
        {
            _parent = parent;
            _parent.KeyPressEvent += HandleKeyPressEvent;
            _parent.KeyReleaseEvent += HandleKeyReleaseEvent;

            _inputToTextWindow.Add(_inputToTextEntry);

            _inputToTextEntry.TruncateMultiline = true;

            // Start with input processing turned off so the text box won't accumulate text 
            // if the user is playing on the keyboard.
            _canProcessInput = false;
        }

        [GLib.ConnectBefore()]
        private void HandleKeyPressEvent(object o, KeyPressEventArgs args)
        {
            var key = (Ryujinx.Common.Configuration.Hid.Key)GTK3MappingHelper.ToInputKey(args.Event.Key);

            if (!(KeyPressedEvent?.Invoke(key)).GetValueOrDefault(true))
            {
                return;
            }

            if (_canProcessInput)
            {
                _inputToTextEntry.SendKeyPressEvent(o, args);
                _inputToTextEntry.GetSelectionBounds(out int selectionStart, out int selectionEnd);
                TextChangedEvent?.Invoke(_inputToTextEntry.Text, selectionStart, selectionEnd, _inputToTextEntry.OverwriteMode);
            }
        }

        [GLib.ConnectBefore()]
        private void HandleKeyReleaseEvent(object o, KeyReleaseEventArgs args)
        {
            var key = (Ryujinx.Common.Configuration.Hid.Key)GTK3MappingHelper.ToInputKey(args.Event.Key);

            if (!(KeyReleasedEvent?.Invoke(key)).GetValueOrDefault(true))
            {
                return;
            }

            if (_canProcessInput)
            {
                // TODO (caian): This solution may have problems if the pause is sent after a key press
                // and before a key release. But for now GTK Entry does not seem to use release events.
                _inputToTextEntry.SendKeyReleaseEvent(o, args);
                _inputToTextEntry.GetSelectionBounds(out int selectionStart, out int selectionEnd);
                TextChangedEvent?.Invoke(_inputToTextEntry.Text, selectionStart, selectionEnd, _inputToTextEntry.OverwriteMode);
            }
        }

        public void SetText(string text, int cursorBegin)
        {
            _inputToTextEntry.Text = text;
            _inputToTextEntry.Position = cursorBegin;
        }

        public void SetText(string text, int cursorBegin, int cursorEnd)
        {
            _inputToTextEntry.Text = text;
            _inputToTextEntry.SelectRegion(cursorBegin, cursorEnd);
        }

        public void Dispose()
        {
            _parent.KeyPressEvent -= HandleKeyPressEvent;
            _parent.KeyReleaseEvent -= HandleKeyReleaseEvent;
        }
    }
}
