#define CcmpImm

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("CcmpImm")]
    public sealed class CpuTestCcmpImm : CpuTest
    {
        public CpuTestCcmpImm(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if CcmpImm

        public static readonly uint[] RandomNzcv =
        {
            Random.Shared.NextUInt(15u),
            Random.Shared.NextUInt(15u),
        };

        [SkippableTheory(DisplayName = "CCMN <Xn>, #<imm>, #<nzcv>, <cond>")]
        [PairwiseData]
        public void Ccmn_64bit([CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0u, 31u)] uint imm,
                               [CombinatorialMemberData(nameof(RandomNzcv))] uint nzcv,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0xBA400800; // CCMN X0, #0, #0, EQ
            opcode |= ((rn & 31) << 5);
            opcode |= ((imm & 31) << 16) | ((cond & 15) << 12) | ((nzcv & 15) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CCMN <Wn>, #<imm>, #<nzcv>, <cond>")]
        [PairwiseData]
        public void Ccmn_32bit([CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0u, 31u)] uint imm,
                               [CombinatorialMemberData(nameof(RandomNzcv))] uint nzcv,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0x3A400800; // CCMN W0, #0, #0, EQ
            opcode |= ((rn & 31) << 5);
            opcode |= ((imm & 31) << 16) | ((cond & 15) << 12) | ((nzcv & 15) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CCMP <Xn>, #<imm>, #<nzcv>, <cond>")]
        [PairwiseData]
        public void Ccmp_64bit([CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0u, 31u)] uint imm,
                               [CombinatorialMemberData(nameof(RandomNzcv))] uint nzcv,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0xFA400800; // CCMP X0, #0, #0, EQ
            opcode |= ((rn & 31) << 5);
            opcode |= ((imm & 31) << 16) | ((cond & 15) << 12) | ((nzcv & 15) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CCMP <Wn>, #<imm>, #<nzcv>, <cond>")]
        [PairwiseData]
        public void Ccmp_32bit([CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0u, 31u)] uint imm,
                               [CombinatorialMemberData(nameof(RandomNzcv))] uint nzcv,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0x7A400800; // CCMP W0, #0, #0, EQ
            opcode |= ((rn & 31) << 5);
            opcode |= ((imm & 31) << 16) | ((cond & 15) << 12) | ((nzcv & 15) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
