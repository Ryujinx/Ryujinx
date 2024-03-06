using Gtk;

namespace Ryujinx.UI.Widgets
{
    public class RawInputToTextEntry : Entry
    {
        public void SendKeyPressEvent(object o, KeyPressEventArgs args)
        {
            base.OnKeyPressEvent(args.Event);
        }

        public void SendKeyReleaseEvent(object o, KeyReleaseEventArgs args)
        {
            base.OnKeyReleaseEvent(args.Event);
        }

        public void SendButtonPressEvent(object o, ButtonPressEventArgs args)
        {
            base.OnButtonPressEvent(args.Event);
        }

        public void SendButtonReleaseEvent(object o, ButtonReleaseEventArgs args)
        {
            base.OnButtonReleaseEvent(args.Event);
        }
    }
}
