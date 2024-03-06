using Gtk;
using Ryujinx.HLE.HOS.Applets;
using Ryujinx.HLE.HOS.Services.Am.AppletOE.ApplicationProxyService.ApplicationProxy.Types;
using Ryujinx.HLE.UI;
using Ryujinx.UI.Widgets;
using System;
using System.Threading;

namespace Ryujinx.UI.Applet
{
    internal class GtkHostUIHandler : IHostUIHandler
    {
        private readonly Window _parent;

        public IHostUITheme HostUITheme { get; }

        public GtkHostUIHandler(Window parent)
        {
            _parent = parent;

            HostUITheme = new GtkHostUITheme(parent);
        }

        public bool DisplayMessageDialog(ControllerAppletUIArgs args)
        {
            string playerCount = args.PlayerCountMin == args.PlayerCountMax ? $"exactly {args.PlayerCountMin}" : $"{args.PlayerCountMin}-{args.PlayerCountMax}";

            string message = $"Application requests <b>{playerCount}</b> player(s) with:\n\n"
                           + $"<tt><b>TYPES:</b> {args.SupportedStyles}</tt>\n\n"
                           + $"<tt><b>PLAYERS:</b> {string.Join(", ", args.SupportedPlayers)}</tt>\n\n"
                           + (args.IsDocked ? "Docked mode set. <tt>Handheld</tt> is also invalid.\n\n" : "")
                           + "<i>Please reconfigure Input now and then press OK.</i>";

            return DisplayMessageDialog("Controller Applet", message);
        }

        public bool DisplayMessageDialog(string title, string message)
        {
            ManualResetEvent dialogCloseEvent = new(false);

            bool okPressed = false;

            Application.Invoke(delegate
            {
                MessageDialog msgDialog = null;

                try
                {
                    msgDialog = new MessageDialog(_parent, DialogFlags.DestroyWithParent, MessageType.Info, ButtonsType.Ok, null)
                    {
                        Title = title,
                        Text = message,
                        UseMarkup = true,
                    };

                    msgDialog.SetDefaultSize(400, 0);

                    msgDialog.Response += (object o, ResponseArgs args) =>
                    {
                        if (args.ResponseId == ResponseType.Ok)
                        {
                            okPressed = true;
                        }

                        dialogCloseEvent.Set();
                        msgDialog?.Dispose();
                    };

                    msgDialog.Show();
                }
                catch (Exception ex)
                {
                    GtkDialog.CreateErrorDialog($"Error displaying Message Dialog: {ex}");

                    dialogCloseEvent.Set();
                }
            });

            dialogCloseEvent.WaitOne();

            return okPressed;
        }

        public bool DisplayInputDialog(SoftwareKeyboardUIArgs args, out string userText)
        {
            ManualResetEvent dialogCloseEvent = new(false);

            bool okPressed = false;
            bool error = false;
            string inputText = args.InitialText ?? "";

            Application.Invoke(delegate
            {
                try
                {
                    var swkbdDialog = new SwkbdAppletDialog(_parent)
                    {
                        Title = "Software Keyboard",
                        Text = args.HeaderText,
                        SecondaryText = args.SubtitleText,
                    };

                    swkbdDialog.InputEntry.Text = inputText;
                    swkbdDialog.InputEntry.PlaceholderText = args.GuideText;
                    swkbdDialog.OkButton.Label = args.SubmitText;

                    swkbdDialog.SetInputLengthValidation(args.StringLengthMin, args.StringLengthMax);
                    swkbdDialog.SetInputValidation(args.KeyboardMode);

                    if (swkbdDialog.Run() == (int)ResponseType.Ok)
                    {
                        inputText = swkbdDialog.InputEntry.Text;
                        okPressed = true;
                    }

                    swkbdDialog.Dispose();
                }
                catch (Exception ex)
                {
                    error = true;

                    GtkDialog.CreateErrorDialog($"Error displaying Software Keyboard: {ex}");
                }
                finally
                {
                    dialogCloseEvent.Set();
                }
            });

            dialogCloseEvent.WaitOne();

            userText = error ? null : inputText;

            return error || okPressed;
        }

        public void ExecuteProgram(HLE.Switch device, ProgramSpecifyKind kind, ulong value)
        {
            device.Configuration.UserChannelPersistence.ExecuteProgram(kind, value);
            ((MainWindow)_parent).RendererWidget?.Exit();
        }

        public bool DisplayErrorAppletDialog(string title, string message, string[] buttons)
        {
            ManualResetEvent dialogCloseEvent = new(false);

            bool showDetails = false;

            Application.Invoke(delegate
            {
                try
                {
                    ErrorAppletDialog msgDialog = new(_parent, DialogFlags.DestroyWithParent, MessageType.Error, buttons)
                    {
                        Title = title,
                        Text = message,
                        UseMarkup = true,
                        WindowPosition = WindowPosition.CenterAlways,
                    };

                    msgDialog.SetDefaultSize(400, 0);

                    msgDialog.Response += (object o, ResponseArgs args) =>
                    {
                        if (buttons != null)
                        {
                            if (buttons.Length > 1)
                            {
                                if (args.ResponseId != (ResponseType)(buttons.Length - 1))
                                {
                                    showDetails = true;
                                }
                            }
                        }

                        dialogCloseEvent.Set();
                        msgDialog?.Dispose();
                    };

                    msgDialog.Show();
                }
                catch (Exception ex)
                {
                    GtkDialog.CreateErrorDialog($"Error displaying ErrorApplet Dialog: {ex}");

                    dialogCloseEvent.Set();
                }
            });

            dialogCloseEvent.WaitOne();

            return showDetails;
        }

        public IDynamicTextInputHandler CreateDynamicTextInputHandler()
        {
            return new GtkDynamicTextInputHandler(_parent);
        }
    }
}
