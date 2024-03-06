using Gtk;
using Ryujinx.UI.Common;
using Ryujinx.UI.Common.Helper;

namespace Ryujinx.UI.Widgets
{
    internal class UserErrorDialog : MessageDialog
    {
        private const string SetupGuideUrl = "https://github.com/Ryujinx/Ryujinx/wiki/Ryujinx-Setup-&-Configuration-Guide";
        private const int OkResponseId = 0;
        private const int SetupGuideResponseId = 1;

        private readonly UserError _userError;

        private UserErrorDialog(UserError error) : base(null, DialogFlags.Modal, MessageType.Error, ButtonsType.None, null)
        {
            _userError = error;

            WindowPosition = WindowPosition.Center;
            SecondaryUseMarkup = true;

            Response += UserErrorDialog_Response;

            SetSizeRequest(120, 50);

            AddButton("OK", OkResponseId);

            bool isInSetupGuide = IsCoveredBySetupGuide(error);

            if (isInSetupGuide)
            {
                AddButton("Open the Setup Guide", SetupGuideResponseId);
            }

            string errorCode = GetErrorCode(error);

            SecondaryUseMarkup = true;

            Title = $"Ryujinx error ({errorCode})";
            Text = $"{errorCode}: {GetErrorTitle(error)}";
            SecondaryText = GetErrorDescription(error);

            if (isInSetupGuide)
            {
                SecondaryText += "\n<b>For more information on how to fix this error, follow our Setup Guide.</b>";
            }
        }

        private static string GetErrorCode(UserError error)
        {
            return $"RYU-{(uint)error:X4}";
        }

        private static string GetErrorTitle(UserError error)
        {
            return error switch
            {
                UserError.NoKeys => "Keys not found",
                UserError.NoFirmware => "Firmware not found",
                UserError.FirmwareParsingFailed => "Firmware parsing error",
                UserError.ApplicationNotFound => "Application not found",
                UserError.Unknown => "Unknown error",
                _ => "Undefined error",
            };
        }

        private static string GetErrorDescription(UserError error)
        {
            return error switch
            {
                UserError.NoKeys => "Ryujinx was unable to find your 'prod.keys' file",
                UserError.NoFirmware => "Ryujinx was unable to find any firmwares installed",
                UserError.FirmwareParsingFailed => "Ryujinx was unable to parse the provided firmware. This is usually caused by outdated keys.",
                UserError.ApplicationNotFound => "Ryujinx couldn't find a valid application at the given path.",
                UserError.Unknown => "An unknown error occured!",
                _ => "An undefined error occured! This shouldn't happen, please contact a dev!",
            };
        }

        private static bool IsCoveredBySetupGuide(UserError error)
        {
            return error switch
            {
                UserError.NoKeys or
                UserError.NoFirmware or
                UserError.FirmwareParsingFailed => true,
                _ => false,
            };
        }

        private static string GetSetupGuideUrl(UserError error)
        {
            if (!IsCoveredBySetupGuide(error))
            {
                return null;
            }

            return error switch
            {
                UserError.NoKeys => SetupGuideUrl + "#initial-setup---placement-of-prodkeys",
                UserError.NoFirmware => SetupGuideUrl + "#initial-setup-continued---installation-of-firmware",
                _ => SetupGuideUrl,
            };
        }

        private void UserErrorDialog_Response(object sender, ResponseArgs args)
        {
            int responseId = (int)args.ResponseId;

            if (responseId == SetupGuideResponseId)
            {
                OpenHelper.OpenUrl(GetSetupGuideUrl(_userError));
            }

            Dispose();
        }

        public static void CreateUserErrorDialog(UserError error)
        {
            new UserErrorDialog(error).Run();
        }
    }
}
