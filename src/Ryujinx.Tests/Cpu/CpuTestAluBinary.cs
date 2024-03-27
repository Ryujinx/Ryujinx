#define AluBinary

using ARMeilleure.State;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("AluBinary")]
    public sealed class CpuTestAluBinary : CpuTest
    {
        public CpuTestAluBinary(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if AluBinary
        public struct CrcTest : IXunitSerializable
        {
            public uint Crc;
            public ulong Value;
            public bool C;

            public uint[] Results; // One result for each CRC variant (8, 16, 32)

            public CrcTest(uint crc, ulong value, bool c, params uint[] results)
            {
                Crc = crc;
                Value = value;
                C = c;
                Results = results;
            }

            public void Deserialize(IXunitSerializationInfo info)
            {
                Crc = info.GetValue<uint>(nameof(Crc));
                Value = info.GetValue<ulong>(nameof(Value));
                C = info.GetValue<bool>(nameof(C));
                Results = info.GetValue<uint[]>(nameof(Results));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(Crc), Crc, Crc.GetType());
                info.AddValue(nameof(Value), Value, Value.GetType());
                info.AddValue(nameof(C), C, C.GetType());
                info.AddValue(nameof(Results), Results, Results.GetType());
            }
        }

        #region "ValueSource (CRC32)"
        private static CrcTest[] _CRC32_Test_Values_()
        {
            // Created with http://www.sunshine2k.de/coding/javascript/crc/crc_js.html, with:
            //  - non-reflected polynomials
            //  - input reflected, result reflected
            //  - bytes in order of increasing significance
            //  - xor 0
            // Only includes non-C variant, as the other can be tested with unicorn.

            return new[]
            {
                new CrcTest(0x00000000u, 0x00_00_00_00_00_00_00_00u, false, 0x00000000, 0x00000000, 0x00000000, 0x00000000),
                new CrcTest(0x00000000u, 0x7f_ff_ff_ff_ff_ff_ff_ffu, false, 0x2d02ef8d, 0xbe2612ff, 0xdebb20e3, 0xa9de8355),
                new CrcTest(0x00000000u, 0x80_00_00_00_00_00_00_00u, false, 0x00000000, 0x00000000, 0x00000000, 0xedb88320),
                new CrcTest(0x00000000u, 0xff_ff_ff_ff_ff_ff_ff_ffu, false, 0x2d02ef8d, 0xbe2612ff, 0xdebb20e3, 0x44660075),
                new CrcTest(0x00000000u, 0xa0_02_f1_ca_52_78_8c_1cu, false, 0x14015c4f, 0x02799256, 0x9063c9e5, 0x8816610a),

                new CrcTest(0xffffffffu, 0x00_00_00_00_00_00_00_00u, false, 0x2dfd1072, 0xbe26ed00, 0xdebb20e3, 0x9add2096),
                new CrcTest(0xffffffffu, 0x7f_ff_ff_ff_ff_ff_ff_ffu, false, 0x00ffffff, 0x0000ffff, 0x00000000, 0x3303a3c3),
                new CrcTest(0xffffffffu, 0x80_00_00_00_00_00_00_00u, false, 0x2dfd1072, 0xbe26ed00, 0xdebb20e3, 0x7765a3b6),
                new CrcTest(0xffffffffu, 0xff_ff_ff_ff_ff_ff_ff_ffu, false, 0x00ffffff, 0x0000ffff, 0x00000000, 0xdebb20e3),
                new CrcTest(0xffffffffu, 0xa0_02_f1_ca_52_78_8c_1cu, false, 0x39fc4c3d, 0xbc5f7f56, 0x4ed8e906, 0x12cb419c),
            };
        }
        #endregion

        [Theory]
        [CombinatorialData]
        public void Crc32_b_h_w_x([CombinatorialValues(0u)] uint rd,
                                  [CombinatorialValues(1u)] uint rn,
                                  [CombinatorialValues(2u)] uint rm,
                                  [CombinatorialRange(0u, 3u, 1u)] uint size,
                                  [CombinatorialMemberData(nameof(_CRC32_Test_Values_))] CrcTest test)
        {
            uint opcode = 0x1AC04000; // CRC32B W0, W0, W0

            opcode |= size << 10;
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            if (size == 3)
            {
                opcode |= 0x80000000;
            }

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: test.Crc, x2: test.Value, x31: w31, runUnicorn: false);

            ExecutionContext context = GetContext();
            ulong result = context.GetX((int)rd);
            Assert.Equal(test.Results[size], result);
        }

        [SkippableTheory(DisplayName = "CRC32X <Wd>, <Wn>, <Xm>", Skip = "Unicorn fails.")]
        [PairwiseData]
        public void Crc32x([CombinatorialValues(0u, 31u)] uint rd,
                           [CombinatorialValues(1u, 31u)] uint rn,
                           [CombinatorialValues(2u, 31u)] uint rm,
                           [CombinatorialValues(0x00000000u, 0xFFFFFFFFu)] uint wn,
                           [CombinatorialValues((ulong)0x00_00_00_00_00_00_00_00,
                                   (ulong)0x7F_FF_FF_FF_FF_FF_FF_FF,
                                   0x80_00_00_00_00_00_00_00,
                                   0xFF_FF_FF_FF_FF_FF_FF_FF)] ulong xm)
        {
            uint opcode = 0x9AC04C00; // CRC32X W0, W0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: xm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CRC32W <Wd>, <Wn>, <Wm>", Skip = "Unicorn fails.")]
        [PairwiseData]
        public void Crc32w([CombinatorialValues(0u, 31u)] uint rd,
                           [CombinatorialValues(1u, 31u)] uint rn,
                           [CombinatorialValues(2u, 31u)] uint rm,
                           [CombinatorialValues(0x00000000u, 0xFFFFFFFFu)] uint wn,
                           [CombinatorialValues((uint)0x00_00_00_00, (uint)0x7F_FF_FF_FF,
                                   0x80_00_00_00, 0xFF_FF_FF_FF)] uint wm)
        {
            uint opcode = 0x1AC04800; // CRC32W W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CRC32H <Wd>, <Wn>, <Wm>", Skip = "Unicorn fails.")]
        [PairwiseData]
        public void Crc32h([CombinatorialValues(0u, 31u)] uint rd,
                           [CombinatorialValues(1u, 31u)] uint rn,
                           [CombinatorialValues(2u, 31u)] uint rm,
                           [CombinatorialValues(0x00000000u, 0xFFFFFFFFu)] uint wn,
                           [CombinatorialValues((ushort)0x00_00, (ushort)0x7F_FF,
                                   (ushort)0x80_00, (ushort)0xFF_FF)] ushort wm)
        {
            uint opcode = 0x1AC04400; // CRC32H W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CRC32B <Wd>, <Wn>, <Wm>", Skip = "Unicorn fails.")]
        [PairwiseData]
        public void Crc32b([CombinatorialValues(0u, 31u)] uint rd,
                           [CombinatorialValues(1u, 31u)] uint rn,
                           [CombinatorialValues(2u, 31u)] uint rm,
                           [CombinatorialValues(0x00000000u, 0xFFFFFFFFu)] uint wn,
                           [CombinatorialValues((byte)0x00, (byte)0x7F,
                                   (byte)0x80, (byte)0xFF)] byte wm)
        {
            uint opcode = 0x1AC04000; // CRC32B W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CRC32CX <Wd>, <Wn>, <Xm>")]
        [PairwiseData]
        public void Crc32cx([CombinatorialValues(0u, 31u)] uint rd,
                            [CombinatorialValues(1u, 31u)] uint rn,
                            [CombinatorialValues(2u, 31u)] uint rm,
                            [CombinatorialValues(0x00000000u, 0xFFFFFFFFu)] uint wn,
                            [CombinatorialValues((ulong)0x00_00_00_00_00_00_00_00,
                                    (ulong)0x7F_FF_FF_FF_FF_FF_FF_FF,
                                    0x80_00_00_00_00_00_00_00,
                                    0xFF_FF_FF_FF_FF_FF_FF_FF)] ulong xm)
        {
            uint opcode = 0x9AC05C00; // CRC32CX W0, W0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: xm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CRC32CW <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Crc32cw([CombinatorialValues(0u, 31u)] uint rd,
                            [CombinatorialValues(1u, 31u)] uint rn,
                            [CombinatorialValues(2u, 31u)] uint rm,
                            [CombinatorialValues(0x00000000u, 0xFFFFFFFFu)] uint wn,
                            [CombinatorialValues((uint)0x00_00_00_00, (uint)0x7F_FF_FF_FF,
                                    0x80_00_00_00, 0xFF_FF_FF_FF)] uint wm)
        {
            uint opcode = 0x1AC05800; // CRC32CW W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CRC32CH <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Crc32ch([CombinatorialValues(0u, 31u)] uint rd,
                            [CombinatorialValues(1u, 31u)] uint rn,
                            [CombinatorialValues(2u, 31u)] uint rm,
                            [CombinatorialValues(0x00000000u, 0xFFFFFFFFu)] uint wn,
                            [CombinatorialValues((ushort)0x00_00, (ushort)0x7F_FF,
                                    (ushort)0x80_00, (ushort)0xFF_FF)] ushort wm)
        {
            uint opcode = 0x1AC05400; // CRC32CH W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CRC32CB <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Crc32cb([CombinatorialValues(0u, 31u)] uint rd,
                            [CombinatorialValues(1u, 31u)] uint rn,
                            [CombinatorialValues(2u, 31u)] uint rm,
                            [CombinatorialValues(0x00000000u, 0xFFFFFFFFu)] uint wn,
                            [CombinatorialValues((byte)0x00, (byte)0x7F,
                                    (byte)0x80, (byte)0xFF)] byte wm)
        {
            uint opcode = 0x1AC05000; // CRC32CB W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SDIV <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Sdiv_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm)
        {
            uint opcode = 0x9AC00C00; // SDIV X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SDIV <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Sdiv_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm)
        {
            uint opcode = 0x1AC00C00; // SDIV W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UDIV <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Udiv_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm)
        {
            uint opcode = 0x9AC00800; // UDIV X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UDIV <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Udiv_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm)
        {
            uint opcode = 0x1AC00800; // UDIV W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
