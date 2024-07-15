using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;

namespace Ryujinx.Ava.UI.Helpers
{
    public class OffscreenTextBox : TextBox
    {
        protected override Type StyleKeyOverride => typeof(TextBox);

        public static RoutedEvent<KeyEventArgs> GetKeyDownRoutedEvent()
        {
            return KeyDownEvent;
        }

        public static RoutedEvent<KeyEventArgs> GetKeyUpRoutedEvent()
        {
            return KeyUpEvent;
        }

        public void SendKeyDownEvent(KeyEventArgs keyEvent)
        {
            OnKeyDown(keyEvent);
        }

        public void SendKeyUpEvent(KeyEventArgs keyEvent)
        {
            OnKeyUp(keyEvent);
        }

        public void SendText(string text)
        {
            OnTextInput(new TextInputEventArgs
            {
                Text = text,
                Source = this,
                RoutedEvent = TextInputEvent,
            });
        }
    }
}
