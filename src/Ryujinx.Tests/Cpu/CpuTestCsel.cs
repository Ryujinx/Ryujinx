#define Csel

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Csel")]
    public sealed class CpuTestCsel : CpuTest
    {
        public CpuTestCsel(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Csel

        [SkippableTheory(DisplayName = "CSEL <Xd>, <Xn>, <Xm>, <cond>")]
        [PairwiseData]
        public void Csel_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0x9A800000; // CSEL X0, X0, X0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((cond & 15) << 12);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CSEL <Wd>, <Wn>, <Wm>, <cond>")]
        [PairwiseData]
        public void Csel_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0x1A800000; // CSEL W0, W0, W0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((cond & 15) << 12);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CSINC <Xd>, <Xn>, <Xm>, <cond>")]
        [PairwiseData]
        public void Csinc_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                                [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                        0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                        0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                        0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0x9A800400; // CSINC X0, X0, X0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((cond & 15) << 12);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CSINC <Wd>, <Wn>, <Wm>, <cond>")]
        [PairwiseData]
        public void Csinc_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wn,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wm,
                                [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                        0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                        0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                        0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0x1A800400; // CSINC W0, W0, W0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((cond & 15) << 12);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CSINV <Xd>, <Xn>, <Xm>, <cond>")]
        [PairwiseData]
        public void Csinv_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                                [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                        0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                        0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                        0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0xDA800000; // CSINV X0, X0, X0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((cond & 15) << 12);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CSINV <Wd>, <Wn>, <Wm>, <cond>")]
        [PairwiseData]
        public void Csinv_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wn,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wm,
                                [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                        0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                        0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                        0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0x5A800000; // CSINV W0, W0, W0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((cond & 15) << 12);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CSNEG <Xd>, <Xn>, <Xm>, <cond>")]
        [PairwiseData]
        public void Csneg_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                                [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                        0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                        0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                        0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0xDA800400; // CSNEG X0, X0, X0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((cond & 15) << 12);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CSNEG <Wd>, <Wn>, <Wm>, <cond>")]
        [PairwiseData]
        public void Csneg_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wn,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wm,
                                [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u,             // <EQ, NE, CS/HS, CC/LO,
                                        0b0100u, 0b0101u, 0b0110u, 0b0111u,             //  MI, PL, VS, VC,
                                        0b1000u, 0b1001u, 0b1010u, 0b1011u,             //  HI, LS, GE, LT,
                                        0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond) //  GT, LE, AL, NV>
        {
            uint opcode = 0x5A800400; // CSNEG W0, W0, W0, EQ
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((cond & 15) << 12);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
