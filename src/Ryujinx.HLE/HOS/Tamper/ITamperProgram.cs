using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.Horizon.Sdk.Hid.Npad;

namespace Ryujinx.HLE.HOS.Tamper
{
    interface ITamperProgram
    {
        bool IsEnabled { get; set; }
        string Name { get; }
        bool TampersCodeMemory { get; set; }
        ITamperedProcess Process { get; }
        void Execute(ControllerKeys pressedKeys);
    }
}
