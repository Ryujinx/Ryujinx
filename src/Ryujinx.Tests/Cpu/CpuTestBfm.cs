#define Bfm

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Bfm")]
    public sealed class CpuTestBfm : CpuTest
    {
        public CpuTestBfm(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Bfm
        private const int RndCnt = 2;

        [SkippableTheory(DisplayName = "BFM <Xd>, <Xn>, #<immr>, #<imms>")]
        [PairwiseData]
        public void Bfm_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialRandomData(Count = RndCnt)] ulong xd,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0u, 31u, 32u, 63u)] uint immr,
                              [CombinatorialValues(0u, 31u, 32u, 63u)] uint imms)
        {
            uint opcode = 0xB3400000; // BFM X0, X0, #0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x0: xd, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BFM <Wd>, <Wn>, #<immr>, #<imms>")]
        [PairwiseData]
        public void Bfm_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialRandomData(Count = RndCnt)] uint wd,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr,
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint imms)
        {
            uint opcode = 0x33000000; // BFM W0, W0, #0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x0: wd, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SBFM <Xd>, <Xn>, #<immr>, #<imms>")]
        [PairwiseData]
        public void Sbfm_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0u, 31u, 32u, 63u)] uint immr,
                               [CombinatorialValues(0u, 31u, 32u, 63u)] uint imms)
        {
            uint opcode = 0x93400000; // SBFM X0, X0, #0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SBFM <Wd>, <Wn>, #<immr>, #<imms>")]
        [PairwiseData]
        public void Sbfm_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr,
                               [CombinatorialValues(0u, 15u, 16u, 31u)] uint imms)
        {
            uint opcode = 0x13000000; // SBFM W0, W0, #0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UBFM <Xd>, <Xn>, #<immr>, #<imms>")]
        [PairwiseData]
        public void Ubfm_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0u, 31u, 32u, 63u)] uint immr,
                               [CombinatorialValues(0u, 31u, 32u, 63u)] uint imms)
        {
            uint opcode = 0xD3400000; // UBFM X0, X0, #0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UBFM <Wd>, <Wn>, #<immr>, #<imms>")]
        [PairwiseData]
        public void Ubfm_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr,
                               [CombinatorialValues(0u, 15u, 16u, 31u)] uint imms)
        {
            uint opcode = 0x53000000; // UBFM W0, W0, #0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
