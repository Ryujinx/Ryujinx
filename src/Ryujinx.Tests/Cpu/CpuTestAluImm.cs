#define AluImm

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("AluImm")]
    public sealed class CpuTestAluImm : CpuTest
    {
        public CpuTestAluImm(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if AluImm

        [SkippableTheory(DisplayName = "ADD <Xd|SP>, <Xn|SP>, #<imm>{, <shift>}")]
        [PairwiseData]
        public void Add_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                              [CombinatorialValues(0u, 4095u)] uint imm,
                              [CombinatorialValues(0b00u, 0b01u)] uint shift) // <LSL #0, LSL #12>
        {
            uint opcode = 0x91000000; // ADD X0, X0, #0, LSL #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((imm & 4095) << 10);

            if (rn != 31)
            {
                SingleOpcode(opcode, x1: xnSp);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Wd|WSP>, <Wn|WSP>, #<imm>{, <shift>}")]
        [PairwiseData]
        public void Add_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                              [CombinatorialValues(0u, 4095u)] uint imm,
                              [CombinatorialValues(0b00u, 0b01u)] uint shift) // <LSL #0, LSL #12>
        {
            uint opcode = 0x11000000; // ADD W0, W0, #0, LSL #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((imm & 4095) << 10);

            if (rn != 31)
            {
                SingleOpcode(opcode, x1: wnWsp);
            }
            else
            {
                SingleOpcode(opcode, x31: wnWsp);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Xd>, <Xn|SP>, #<imm>{, <shift>}")]
        [PairwiseData]
        public void Adds_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                               [CombinatorialValues(0u, 4095u)] uint imm,
                               [CombinatorialValues(0b00u, 0b01u)] uint shift) // <LSL #0, LSL #12>
        {
            uint opcode = 0xB1000000; // ADDS X0, X0, #0, LSL #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((imm & 4095) << 10);

            if (rn != 31)
            {
                SingleOpcode(opcode, x1: xnSp);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Wd>, <Wn|WSP>, #<imm>{, <shift>}")]
        [PairwiseData]
        public void Adds_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                               [CombinatorialValues(0u, 4095u)] uint imm,
                               [CombinatorialValues(0b00u, 0b01u)] uint shift) // <LSL #0, LSL #12>
        {
            uint opcode = 0x31000000; // ADDS W0, W0, #0, LSL #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((imm & 4095) << 10);

            if (rn != 31)
            {
                SingleOpcode(opcode, x1: wnWsp);
            }
            else
            {
                SingleOpcode(opcode, x31: wnWsp);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "AND <Xd|SP>, <Xn>, #<imm>")]
        [PairwiseData]
        public void And_N1_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                 [CombinatorialValues(0u, 31u, 32u, 62u)] uint imms, // <imm>
                                 [CombinatorialValues(0u, 31u, 32u, 63u)] uint immr) // <imm>
        {
            uint opcode = 0x92400000; // AND X0, X0, #0x1
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "AND <Xd|SP>, <Xn>, #<imm>")]
        [PairwiseData]
        public void And_N0_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                 [CombinatorialValues(0u, 15u, 16u, 30u)] uint imms, // <imm>
                                 [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr) // <imm>
        {
            uint opcode = 0x92000000; // AND X0, X0, #0x100000001
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "AND <Wd|WSP>, <Wn>, #<imm>")]
        [PairwiseData]
        public void And_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0u, 15u, 16u, 30u)] uint imms, // <imm>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr) // <imm>
        {
            uint opcode = 0x12000000; // AND W0, W0, #0x1
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ANDS <Xd>, <Xn>, #<imm>")]
        [PairwiseData]
        public void Ands_N1_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                  [CombinatorialValues(1u, 31u)] uint rn,
                                  [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                          0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                  [CombinatorialValues(0u, 31u, 32u, 62u)] uint imms, // <imm>
                                  [CombinatorialValues(0u, 31u, 32u, 63u)] uint immr) // <imm>
        {
            uint opcode = 0xF2400000; // ANDS X0, X0, #0x1
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ANDS <Xd>, <Xn>, #<imm>")]
        [PairwiseData]
        public void Ands_N0_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                  [CombinatorialValues(1u, 31u)] uint rn,
                                  [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                          0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                  [CombinatorialValues(0u, 15u, 16u, 30u)] uint imms, // <imm>
                                  [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr) // <imm>
        {
            uint opcode = 0xF2000000; // ANDS X0, X0, #0x100000001
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ANDS <Wd>, <Wn>, #<imm>")]
        [PairwiseData]
        public void Ands_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0u, 15u, 16u, 30u)] uint imms, // <imm>
                               [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr) // <imm>
        {
            uint opcode = 0x72000000; // ANDS W0, W0, #0x1
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EOR <Xd|SP>, <Xn>, #<imm>")]
        [PairwiseData]
        public void Eor_N1_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                 [CombinatorialValues(0u, 31u, 32u, 62u)] uint imms, // <imm>
                                 [CombinatorialValues(0u, 31u, 32u, 63u)] uint immr) // <imm>
        {
            uint opcode = 0xD2400000; // EOR X0, X0, #0x1
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EOR <Xd|SP>, <Xn>, #<imm>")]
        [PairwiseData]
        public void Eor_N0_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                 [CombinatorialValues(0u, 15u, 16u, 30u)] uint imms, // <imm>
                                 [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr) // <imm>
        {
            uint opcode = 0xD2000000; // EOR X0, X0, #0x100000001
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EOR <Wd>, <Wn>, #<imm>")]
        [PairwiseData]
        public void Eor_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0u, 15u, 16u, 30u)] uint imms, // <imm>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr) // <imm>
        {
            uint opcode = 0x52000000; // EOR W0, W0, #0x1
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORR <Xd|SP>, <Xn>, #<imm>")]
        [PairwiseData]
        public void Orr_N1_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                 [CombinatorialValues(0u, 31u, 32u, 62u)] uint imms, // <imm>
                                 [CombinatorialValues(0u, 31u, 32u, 63u)] uint immr) // <imm>
        {
            uint opcode = 0xB2400000; // ORR X0, X0, #0x1
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORR <Xd|SP>, <Xn>, #<imm>")]
        [PairwiseData]
        public void Orr_N0_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                 [CombinatorialValues(0u, 15u, 16u, 30u)] uint imms, // <imm>
                                 [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr) // <imm>
        {
            uint opcode = 0xB2000000; // ORR X0, X0, #0x100000001
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORR <Wd|WSP>, <Wn>, #<imm>")]
        [PairwiseData]
        public void Orr_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0u, 15u, 16u, 30u)] uint imms, // <imm>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint immr) // <imm>
        {
            uint opcode = 0x32000000; // ORR W0, W0, #0x1
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((immr & 63) << 16) | ((imms & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Xd|SP>, <Xn|SP>, #<imm>{, <shift>}")]
        [PairwiseData]
        public void Sub_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                              [CombinatorialValues(0u, 4095u)] uint imm,
                              [CombinatorialValues(0b00u, 0b01u)] uint shift) // <LSL #0, LSL #12>
        {
            uint opcode = 0xD1000000; // SUB X0, X0, #0, LSL #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((imm & 4095) << 10);

            if (rn != 31)
            {
                SingleOpcode(opcode, x1: xnSp);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Wd|WSP>, <Wn|WSP>, #<imm>{, <shift>}")]
        [PairwiseData]
        public void Sub_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                              [CombinatorialValues(0u, 4095u)] uint imm,
                              [CombinatorialValues(0b00u, 0b01u)] uint shift) // <LSL #0, LSL #12>
        {
            uint opcode = 0x51000000; // SUB W0, W0, #0, LSL #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((imm & 4095) << 10);

            if (rn != 31)
            {
                SingleOpcode(opcode, x1: wnWsp);
            }
            else
            {
                SingleOpcode(opcode, x31: wnWsp);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Xd>, <Xn|SP>, #<imm>{, <shift>}")]
        [PairwiseData]
        public void Subs_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                               [CombinatorialValues(0u, 4095u)] uint imm,
                               [CombinatorialValues(0b00u, 0b01u)] uint shift) // <LSL #0, LSL #12>
        {
            uint opcode = 0xF1000000; // SUBS X0, X0, #0, LSL #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((imm & 4095) << 10);

            if (rn != 31)
            {
                SingleOpcode(opcode, x1: xnSp);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Wd>, <Wn|WSP>, #<imm>{, <shift>}")]
        [PairwiseData]
        public void Subs_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                               [CombinatorialValues(0u, 4095u)] uint imm,
                               [CombinatorialValues(0b00u, 0b01u)] uint shift) // <LSL #0, LSL #12>
        {
            uint opcode = 0x71000000; // SUBS W0, W0, #0, LSL #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((imm & 4095) << 10);

            if (rn != 31)
            {
                SingleOpcode(opcode, x1: wnWsp);
            }
            else
            {
                SingleOpcode(opcode, x31: wnWsp);
            }

            CompareAgainstUnicorn();
        }
#endif
    }
}
