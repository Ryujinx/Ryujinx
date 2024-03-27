#define Bf32

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Bf32")]
    public sealed class CpuTestBf32 : CpuTest32
    {
        public CpuTestBf32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Bf32
        private const int RndCnt = 2;

        [Theory(DisplayName = "BFC <Rd>, #<lsb>, #<width>")]
        [PairwiseData]
        public void Bfc([CombinatorialValues(0u, 0xdu)] uint rd,
                        [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                0x80000000u, 0xFFFFFFFFu)] uint wd,
                        [CombinatorialValues(0u, 15u, 16u, 31u)] uint lsb,
                        [CombinatorialValues(0u, 15u, 16u, 31u)] uint msb)
        {
            msb = Math.Max(lsb, msb); // Don't test unpredictable for now.
            uint opcode = 0xe7c0001fu; // BFC R0, #0, #1
            opcode |= ((rd & 0xf) << 12);
            opcode |= ((msb & 31) << 16) | ((lsb & 31) << 7);

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: wd, sp: sp);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "BFI <Rd>, <Rn>, #<lsb>, #<width>")]
        [PairwiseData]
        public void Bfi([CombinatorialValues(0u, 0xdu)] uint rd,
                        [CombinatorialValues(1u, 0xdu)] uint rn,
                        [CombinatorialRandomData(Count = RndCnt)] uint wd,
                        [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                0x80000000u, 0xFFFFFFFFu)] uint wn,
                        [CombinatorialValues(0u, 15u, 16u, 31u)] uint lsb,
                        [CombinatorialValues(0u, 15u, 16u, 31u)] uint msb)
        {
            msb = Math.Max(lsb, msb); // Don't test unpredictable for now.
            uint opcode = 0xe7c00010u; // BFI R0, R0, #0, #1
            opcode |= ((rd & 0xf) << 12);
            opcode |= ((rn & 0xf) << 0);
            opcode |= ((msb & 31) << 16) | ((lsb & 31) << 7);

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: wd, r1: wn, sp: sp);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "UBFX <Rd>, <Rn>, #<lsb>, #<width>")]
        [PairwiseData]
        public void Ubfx([CombinatorialValues(0u, 0xdu)] uint rd,
                         [CombinatorialValues(1u, 0xdu)] uint rn,
                         [CombinatorialRandomData(Count = RndCnt)] uint wd,
                         [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                 0x80000000u, 0xFFFFFFFFu)] uint wn,
                         [CombinatorialValues(0u, 15u, 16u, 31u)] uint lsb,
                         [CombinatorialValues(0u, 15u, 16u, 31u)] uint widthm1)
        {
            if (lsb + widthm1 > 31)
            {
                widthm1 -= (lsb + widthm1) - 31;
            }
            uint opcode = 0xe7e00050u; // UBFX R0, R0, #0, #1
            opcode |= ((rd & 0xf) << 12);
            opcode |= ((rn & 0xf) << 0);
            opcode |= ((widthm1 & 31) << 16) | ((lsb & 31) << 7);

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: wd, r1: wn, sp: sp);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "SBFX <Rd>, <Rn>, #<lsb>, #<width>")]
        [PairwiseData]
        public void Sbfx([CombinatorialValues(0u, 0xdu)] uint rd,
                         [CombinatorialValues(1u, 0xdu)] uint rn,
                         [CombinatorialRandomData(Count = RndCnt)] uint wd,
                         [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                 0x80000000u, 0xFFFFFFFFu)] uint wn,
                         [CombinatorialValues(0u, 15u, 16u, 31u)] uint lsb,
                         [CombinatorialValues(0u, 15u, 16u, 31u)] uint widthm1)
        {
            if (lsb + widthm1 > 31)
            {
                widthm1 -= (lsb + widthm1) - 31;
            }
            uint opcode = 0xe7a00050u; // SBFX R0, R0, #0, #1
            opcode |= ((rd & 0xf) << 12);
            opcode |= ((rn & 0xf) << 0);
            opcode |= ((widthm1 & 31) << 16) | ((lsb & 31) << 7);

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: wd, r1: wn, sp: sp);

            CompareAgainstUnicorn();
        }
#endif
    }
}
