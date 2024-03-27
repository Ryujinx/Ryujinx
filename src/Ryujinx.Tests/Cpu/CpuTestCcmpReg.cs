#define CcmpReg

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("CcmpReg")]
    public sealed class CpuTestCcmpReg : CpuTest
    {
        public CpuTestCcmpReg(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if CcmpReg
        public static readonly uint[] RandomNzcv =
        {
            Random.Shared.NextUInt(15u),
            Random.Shared.NextUInt(15u),
        };

        [SkippableTheory(DisplayName = "CCMN <Xn>, <Xm>, #<nzcv>, <cond>")]
        [PairwiseData]
        public void Ccmn_64bit([CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               [CombinatorialMemberData(nameof(RandomNzcv))] uint nzcv,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0xBA400000; // CCMN X0, X0, #0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5);
            opcode |= ((cond & 15) << 12) | ((nzcv & 15) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CCMN <Wn>, <Wm>, #<nzcv>, <cond>")]
        [PairwiseData]
        public void Ccmn_32bit([CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               [CombinatorialMemberData(nameof(RandomNzcv))] uint nzcv,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0x3A400000; // CCMN W0, W0, #0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5);
            opcode |= ((cond & 15) << 12) | ((nzcv & 15) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CCMP <Xn>, <Xm>, #<nzcv>, <cond>")]
        [PairwiseData]
        public void Ccmp_64bit([CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               [CombinatorialMemberData(nameof(RandomNzcv))] uint nzcv,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0xFA400000; // CCMP X0, X0, #0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5);
            opcode |= ((cond & 15) << 12) | ((nzcv & 15) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CCMP <Wn>, <Wm>, #<nzcv>, <cond>")]
        [PairwiseData]
        public void Ccmp_32bit([CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               [CombinatorialMemberData(nameof(RandomNzcv))] uint nzcv,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0x7A400000; // CCMP W0, W0, #0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5);
            opcode |= ((cond & 15) << 12) | ((nzcv & 15) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
