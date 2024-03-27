#define Alu

using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Alu")]
    public sealed class CpuTestAlu : CpuTest
    {
        public CpuTestAlu(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Alu

        #region "Helper methods"
        private static uint GenLeadingSignsMinus32(int cnt) // 0 <= cnt <= 31
        {
            return ~GenLeadingZeros32(cnt + 1);
        }

        private static ulong GenLeadingSignsMinus64(int cnt) // 0 <= cnt <= 63
        {
            return ~GenLeadingZeros64(cnt + 1);
        }

        private static uint GenLeadingSignsPlus32(int cnt) // 0 <= cnt <= 31
        {
            return GenLeadingZeros32(cnt + 1);
        }

        private static ulong GenLeadingSignsPlus64(int cnt) // 0 <= cnt <= 63
        {
            return GenLeadingZeros64(cnt + 1);
        }

        private static uint GenLeadingZeros32(int cnt) // 0 <= cnt <= 32
        {
            if (cnt == 32)
            {
                return 0u;
            }

            if (cnt == 31)
            {
                return 1u;
            }

            uint rnd = Random.Shared.NextUInt();
            int mask = int.MinValue;

            return (rnd >> (cnt + 1)) | ((uint)mask >> cnt);
        }

        private static ulong GenLeadingZeros64(int cnt) // 0 <= cnt <= 64
        {
            if (cnt == 64)
            {
                return 0ul;
            }

            if (cnt == 63)
            {
                return 1ul;
            }

            ulong rnd = Random.Shared.NextULong();
            long mask = long.MinValue;

            return (rnd >> (cnt + 1)) | ((ulong)mask >> cnt);
        }
        #endregion

        #region "ValueSource (Types)"
        private static IEnumerable<ulong> GenLeadingSignsX()
        {
            for (int cnt = 0; cnt <= 63; cnt++)
            {
                yield return GenLeadingSignsMinus64(cnt);
                yield return GenLeadingSignsPlus64(cnt);
            }
        }

        private static IEnumerable<uint> GenLeadingSignsW()
        {
            for (int cnt = 0; cnt <= 31; cnt++)
            {
                yield return GenLeadingSignsMinus32(cnt);
                yield return GenLeadingSignsPlus32(cnt);
            }
        }

        private static IEnumerable<ulong> GenLeadingZerosX()
        {
            for (int cnt = 0; cnt <= 64; cnt++)
            {
                yield return GenLeadingZeros64(cnt);
            }
        }

        private static IEnumerable<uint> GenLeadingZerosW()
        {
            for (int cnt = 0; cnt <= 32; cnt++)
            {
                yield return GenLeadingZeros32(cnt);
            }
        }
        #endregion

        [SkippableTheory(DisplayName = "CLS <Xd>, <Xn>")]
        [PairwiseData]
        public void Cls_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialMemberData(nameof(GenLeadingSignsX))] ulong xn)
        {
            uint opcode = 0xDAC01400; // CLS X0, X0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CLS <Wd>, <Wn>")]
        [PairwiseData]
        public void Cls_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialMemberData(nameof(GenLeadingSignsW))] uint wn)
        {
            uint opcode = 0x5AC01400; // CLS W0, W0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CLZ <Xd>, <Xn>")]
        [PairwiseData]
        public void Clz_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialMemberData(nameof(GenLeadingZerosX))] ulong xn)
        {
            uint opcode = 0xDAC01000; // CLZ X0, X0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CLZ <Wd>, <Wn>")]
        [PairwiseData]
        public void Clz_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialMemberData(nameof(GenLeadingZerosW))] uint wn)
        {
            uint opcode = 0x5AC01000; // CLZ W0, W0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "RBIT <Xd>, <Xn>")]
        [PairwiseData]
        public void Rbit_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn)
        {
            uint opcode = 0xDAC00000; // RBIT X0, X0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "RBIT <Wd>, <Wn>")]
        [PairwiseData]
        public void Rbit_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn)
        {
            uint opcode = 0x5AC00000; // RBIT W0, W0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV16 <Xd>, <Xn>")]
        [PairwiseData]
        public void Rev16_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn)
        {
            uint opcode = 0xDAC00400; // REV16 X0, X0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV16 <Wd>, <Wn>")]
        [PairwiseData]
        public void Rev16_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wn)
        {
            uint opcode = 0x5AC00400; // REV16 W0, W0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV32 <Xd>, <Xn>")]
        [PairwiseData]
        public void Rev32_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn)
        {
            uint opcode = 0xDAC00800; // REV32 X0, X0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV <Wd>, <Wn>")]
        [PairwiseData]
        public void Rev32_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wn)
        {
            uint opcode = 0x5AC00800; // REV W0, W0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV64 <Xd>, <Xn>")]
        [PairwiseData]
        public void Rev64_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn)
        {
            uint opcode = 0xDAC00C00; // REV64 X0, X0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
