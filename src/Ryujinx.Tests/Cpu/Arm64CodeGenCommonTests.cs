using ARMeilleure.CodeGen.Arm64;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    public class Arm64CodeGenCommonTests
    {
        public struct TestCase : IXunitSerializable
        {
            public ulong Value;
            public bool Valid;
            public int ImmN;
            public int ImmS;
            public int ImmR;

            public void Deserialize(IXunitSerializationInfo info)
            {
                Value = info.GetValue<ulong>(nameof(Value));
                Valid = info.GetValue<bool>(nameof(Valid));
                ImmN = info.GetValue<int>(nameof(ImmN));
                ImmS = info.GetValue<int>(nameof(ImmS));
                ImmR = info.GetValue<int>(nameof(ImmR));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(Value), Value, Value.GetType());
                info.AddValue(nameof(Valid), Valid, Valid.GetType());
                info.AddValue(nameof(ImmN), ImmN, ImmN.GetType());
                info.AddValue(nameof(ImmS), ImmS, ImmS.GetType());
                info.AddValue(nameof(ImmR), ImmR, ImmR.GetType());
            }
        }

        private static readonly TestCase[] _testCases =
        {
            new() { Value = 0, Valid = false, ImmN = 0, ImmS = 0, ImmR = 0 },
            new() { Value = 0x970977f35f848714, Valid = false, ImmN = 0, ImmS = 0, ImmR = 0 },
            new() { Value = 0xffffffffffffffff, Valid = false, ImmN = 0, ImmS = 0, ImmR = 0 },
            new() { Value = 0x5555555555555555, Valid = true, ImmN = 0, ImmS = 0x3c, ImmR = 0 },
            new() { Value = 0xaaaaaaaaaaaaaaaa, Valid = true, ImmN = 0, ImmS = 0x3c, ImmR = 1 },
            new() { Value = 0x6666666666666666, Valid = true, ImmN = 0, ImmS = 0x39, ImmR = 3 },
            new() { Value = 0x1c1c1c1c1c1c1c1c, Valid = true, ImmN = 0, ImmS = 0x32, ImmR = 6 },
            new() { Value = 0x0f0f0f0f0f0f0f0f, Valid = true, ImmN = 0, ImmS = 0x33, ImmR = 0 },
            new() { Value = 0xf1f1f1f1f1f1f1f1, Valid = true, ImmN = 0, ImmS = 0x34, ImmR = 4 },
            new() { Value = 0xe7e7e7e7e7e7e7e7, Valid = true, ImmN = 0, ImmS = 0x35, ImmR = 3 },
            new() { Value = 0xc001c001c001c001, Valid = true, ImmN = 0, ImmS = 0x22, ImmR = 2 },
            new() { Value = 0x0000038000000380, Valid = true, ImmN = 0, ImmS = 0x02, ImmR = 25 },
            new() { Value = 0xffff8fffffff8fff, Valid = true, ImmN = 0, ImmS = 0x1c, ImmR = 17 },
            new() { Value = 0x000000000ffff800, Valid = true, ImmN = 1, ImmS = 0x10, ImmR = 53 },
        };

        public static readonly EnumerableTheoryData<TestCase> TestData = new(_testCases);

        [Theory]
        [MemberData(nameof(TestData))]
        public void BitImmTests(TestCase test)
        {
            bool valid = CodeGenCommon.TryEncodeBitMask(test.Value, out int immN, out int immS, out int immR);

            Assert.Equal(test.Valid, valid);
            Assert.Equal(test.ImmN, immN);
            Assert.Equal(test.ImmS, immS);
            Assert.Equal(test.ImmR, immR);
        }
    }
}
