using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.HLE.HOS.Tamper.Operations;
using Ryujinx.Horizon.Sdk.Hid.Npad;

namespace Ryujinx.HLE.HOS.Tamper
{
    class AtmosphereProgram : ITamperProgram
    {
        private readonly Parameter<long> _pressedKeys;
        private readonly IOperation _entryPoint;

        public string Name { get; }
        public bool TampersCodeMemory { get; set; } = false;
        public ITamperedProcess Process { get; }
        public bool IsEnabled { get; set; }

        public AtmosphereProgram(string name, ITamperedProcess process, Parameter<long> pressedKeys, IOperation entryPoint)
        {
            Name = name;
            Process = process;
            _pressedKeys = pressedKeys;
            _entryPoint = entryPoint;
        }

        public void Execute(ControllerKeys pressedKeys)
        {
            if (IsEnabled)
            {
                _pressedKeys.Value = (long)pressedKeys;
                _entryPoint.Execute();
            }
        }
    }
}
