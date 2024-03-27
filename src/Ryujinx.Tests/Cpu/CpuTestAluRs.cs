#define AluRs

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("AluRs")]
    public sealed class CpuTestAluRs : CpuTest
    {
        public CpuTestAluRs(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if AluRs

        [SkippableTheory(DisplayName = "ADC <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Adc_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                              bool carryIn)
        {
            uint opcode = 0x9A000000; // ADC X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31, carry: carryIn);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADC <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Adc_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wm,
                              bool carryIn)
        {
            uint opcode = 0x1A000000; // ADC W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31, carry: carryIn);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADCS <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Adcs_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               bool carryIn)
        {
            uint opcode = 0xBA000000; // ADCS X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31, carry: carryIn);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADCS <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Adcs_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               bool carryIn)
        {
            uint opcode = 0x3A000000; // ADCS W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31, carry: carryIn);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Add_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint shift, // <LSL, LSR, ASR>
                              [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0x8B000000; // ADD X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Add_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint shift, // <LSL, LSR, ASR>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x0B000000; // ADD W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Adds_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint shift, // <LSL, LSR, ASR>
                               [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0xAB000000; // ADDS X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Adds_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint shift, // <LSL, LSR, ASR>
                               [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x2B000000; // ADDS W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "AND <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void And_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0x8A000000; // AND X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "AND <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void And_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x0A000000; // AND W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ANDS <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Ands_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                               [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0xEA000000; // ANDS X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ANDS <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Ands_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                               [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x6A000000; // ANDS W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ASRV <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Asrv_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0ul, 31ul, 32ul, 63ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm)
        {
            uint opcode = 0x9AC02800; // ASRV X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ASRV <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Asrv_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0u, 15u, 16u, 31u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm)
        {
            uint opcode = 0x1AC02800; // ASRV W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BIC <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Bic_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0x8A200000; // BIC X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BIC <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Bic_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x0A200000; // BIC W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BICS <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Bics_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                               [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0xEA200000; // BICS X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BICS <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Bics_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                               [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x6A200000; // BICS W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EON <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Eon_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0xCA200000; // EON X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EON <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Eon_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x4A200000; // EON W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EOR <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Eor_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0xCA000000; // EOR X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EOR <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Eor_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x4A000000; // EOR W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EXTR <Xd>, <Xn>, <Xm>, #<lsb>")]
        [PairwiseData]
        public void Extr_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               [CombinatorialValues(0u, 31u, 32u, 63u)] uint lsb)
        {
            uint opcode = 0x93C00000; // EXTR X0, X0, X0, #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((lsb & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EXTR <Wd>, <Wn>, <Wm>, #<lsb>")]
        [PairwiseData]
        public void Extr_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               [CombinatorialValues(0u, 15u, 16u, 31u)] uint lsb)
        {
            uint opcode = 0x13800000; // EXTR W0, W0, W0, #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((lsb & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "LSLV <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Lslv_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0ul, 31ul, 32ul, 63ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm)
        {
            uint opcode = 0x9AC02000; // LSLV X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "LSLV <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Lslv_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0u, 15u, 16u, 31u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm)
        {
            uint opcode = 0x1AC02000; // LSLV W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "LSRV <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Lsrv_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0ul, 31ul, 32ul, 63ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm)
        {
            uint opcode = 0x9AC02400; // LSRV X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "LSRV <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Lsrv_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0u, 15u, 16u, 31u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm)
        {
            uint opcode = 0x1AC02400; // LSRV W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORN <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Orn_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0xAA200000; // ORN X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORN <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Orn_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x2A200000; // ORN W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORR <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Orr_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0xAA000000; // ORR X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORR <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Orr_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x2A000000; // ORR W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "RORV <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Rorv_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0ul, 31ul, 32ul, 63ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm)
        {
            uint opcode = 0x9AC02C00; // RORV X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "RORV <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Rorv_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0u, 15u, 16u, 31u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm)
        {
            uint opcode = 0x1AC02C00; // RORV W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SBC <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Sbc_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                              bool carryIn)
        {
            uint opcode = 0xDA000000; // SBC X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31, carry: carryIn);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SBC <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Sbc_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wm,
                              bool carryIn)
        {
            uint opcode = 0x5A000000; // SBC W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31, carry: carryIn);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SBCS <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Sbcs_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               bool carryIn)
        {
            uint opcode = 0xFA000000; // SBCS X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31, carry: carryIn);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SBCS <Wd>, <Wn>, <Wm>")]
        [PairwiseData]
        public void Sbcs_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               bool carryIn)
        {
            uint opcode = 0x7A000000; // SBCS W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31, carry: carryIn);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Sub_64bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                              [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                      0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint shift, // <LSL, LSR, ASR>
                              [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0xCB000000; // SUB X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Sub_32bit([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialValues(2u, 31u)] uint rm,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wm,
                              [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint shift, // <LSL, LSR, ASR>
                              [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x4B000000; // SUB W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Xd>, <Xn>, <Xm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Subs_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint shift, // <LSL, LSR, ASR>
                               [CombinatorialValues(0u, 31u, 32u, 63u)] uint amount)
        {
            uint opcode = 0xEB000000; // SUBS X0, X0, X0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Wd>, <Wn>, <Wm>{, <shift> #<amount>}")]
        [PairwiseData]
        public void Subs_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint shift, // <LSL, LSR, ASR>
                               [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            uint opcode = 0x6B000000; // SUBS W0, W0, W0, LSL #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((shift & 3) << 22) | ((amount & 63) << 10);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x31: w31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
