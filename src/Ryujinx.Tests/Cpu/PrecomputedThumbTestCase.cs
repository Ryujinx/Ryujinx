using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    public struct PrecomputedThumbTestCase : IXunitSerializable
    {
        public ushort[] Instructions;
        public uint[] StartRegs;
        public uint[] FinalRegs;

        public void Deserialize(IXunitSerializationInfo info)
        {
            Instructions = info.GetValue<ushort[]>(nameof(Instructions));
            StartRegs = info.GetValue<uint[]>(nameof(StartRegs));
            FinalRegs = info.GetValue<uint[]>(nameof(FinalRegs));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Instructions), Instructions, Instructions.GetType());
            info.AddValue(nameof(StartRegs), StartRegs, StartRegs.GetType());
            info.AddValue(nameof(FinalRegs), FinalRegs, FinalRegs.GetType());
        }
    }
}
