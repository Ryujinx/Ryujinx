using Gtk;
using Ryujinx.UI.Common.Configuration;
using System.Reflection;

namespace Ryujinx.UI.Applet
{
    internal class ErrorAppletDialog : MessageDialog
    {
        public ErrorAppletDialog(Window parentWindow, DialogFlags dialogFlags, MessageType messageType, string[] buttons) : base(parentWindow, dialogFlags, messageType, ButtonsType.None, null)
        {
            Icon = new Gdk.Pixbuf(Assembly.GetAssembly(typeof(ConfigurationState)), "Ryujinx.Gtk3.UI.Common.Resources.Logo_Ryujinx.png");

            int responseId = 0;

            if (buttons != null)
            {
                foreach (string buttonText in buttons)
                {
                    AddButton(buttonText, responseId);
                    responseId++;
                }
            }
            else
            {
                AddButton("OK", 0);
            }

            ShowAll();
        }
    }
}
