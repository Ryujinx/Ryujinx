using Ryujinx.HLE.HOS.Applets.Browser;
using Ryujinx.HLE.HOS.Applets.Error;
using Ryujinx.HLE.HOS.Services.Am.AppletAE;
using System;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Applets
{
    static class AppletManager
    {
        private static readonly Dictionary<AppletId, Type> _appletMapping;

        static AppletManager()
        {
            _appletMapping = new Dictionary<AppletId, Type>
            {
                { AppletId.Error,            typeof(ErrorApplet)            },
                { AppletId.PlayerSelect,     typeof(PlayerSelectApplet)     },
                { AppletId.Controller,       typeof(ControllerApplet)       },
                { AppletId.SoftwareKeyboard, typeof(SoftwareKeyboardApplet) },
                { AppletId.Cabinet,          typeof(CabinetApplet)          },
                { AppletId.LibAppletWeb,     typeof(BrowserApplet)          },
                { AppletId.LibAppletShop,    typeof(BrowserApplet)          },
                { AppletId.LibAppletOff,     typeof(BrowserApplet)          },
            };
        }

        public static IApplet Create(AppletId applet, Horizon system)
        {
            if (_appletMapping.TryGetValue(applet, out Type appletClass))
            {
                return (IApplet)Activator.CreateInstance(appletClass, system);
            }

            throw new NotImplementedException($"{applet} applet is not implemented.");
        }
    }
}
