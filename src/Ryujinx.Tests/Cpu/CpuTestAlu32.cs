#define Alu32

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Alu32")]
    public sealed class CpuTestAlu32 : CpuTest32
    {
        public CpuTestAlu32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Alu32

        #region "ValueSource (Opcodes)"
        private static uint[] SuHAddSub8()
        {
            return new[]
            {
                0xe6100f90u, // SADD8  R0, R0, R0
                0xe6100ff0u, // SSUB8  R0, R0, R0
                0xe6300f90u, // SHADD8 R0, R0, R0
                0xe6300ff0u, // SHSUB8 R0, R0, R0
                0xe6500f90u, // UADD8  R0, R0, R0
                0xe6500ff0u, // USUB8  R0, R0, R0
                0xe6700f90u, // UHADD8 R0, R0, R0
                0xe6700ff0u, // UHSUB8 R0, R0, R0
            };
        }

        private static uint[] SsatUsat()
        {
            return new[]
            {
                0xe6a00010u, // SSAT R0, #1, R0, LSL #0
                0xe6a00050u, // SSAT R0, #1, R0, ASR #32
                0xe6e00010u, // USAT R0, #0, R0, LSL #0
                0xe6e00050u, // USAT R0, #0, R0, ASR #32
            };
        }

        private static uint[] Ssat16Usat16()
        {
            return new[]
            {
                0xe6a00f30u, // SSAT16 R0, #1, R0
                0xe6e00f30u, // USAT16 R0, #0, R0
            };
        }

        private static uint[] LsrLslAsrRor()
        {
            return new[]
            {
                0xe1b00030u, // LSRS R0, R0, R0
                0xe1b00010u, // LSLS R0, R0, R0
                0xe1b00050u, // ASRS R0, R0, R0
                0xe1b00070u, // RORS R0, R0, R0
            };
        }
        #endregion

        private const int RndCnt = 2;

        [Theory(DisplayName = "RBIT <Rd>, <Rn>")]
        [PairwiseData]
        public void Rbit_32bit([CombinatorialValues(0u, 0xdu)] uint rd,
                               [CombinatorialValues(1u, 0xdu)] uint rm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn)
        {
            uint opcode = 0xe6ff0f30u; // RBIT R0, R0
            opcode |= ((rm & 15) << 0) | ((rd & 15) << 12);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, r1: wn, sp: w31);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Lsr_Lsl_Asr_Ror([CombinatorialMemberData(nameof(LsrLslAsrRor))] uint opcode,
                                    [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                            0x80000000u, 0xFFFFFFFFu)] uint shiftValue,
                                    [CombinatorialRange(0, 31, 1)] int shiftAmount)
        {
            uint rd = 0;
            uint rm = 1;
            uint rs = 2;
            opcode |= ((rm & 15) << 0) | ((rd & 15) << 12) | ((rs & 15) << 8);

            SingleOpcode(opcode, r1: shiftValue, r2: (uint)shiftAmount);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Shadd8([CombinatorialValues(0u, 0xdu)] uint rd,
                           [CombinatorialValues(1u)] uint rm,
                           [CombinatorialValues(2u)] uint rn,
                           [CombinatorialRandomData(Count = RndCnt)] uint w0,
                           [CombinatorialRandomData(Count = RndCnt)] uint w1,
                           [CombinatorialRandomData(Count = RndCnt)] uint w2)
        {
            uint opcode = 0xE6300F90u; // SHADD8 R0, R0, R0

            opcode |= ((rm & 15) << 0) | ((rd & 15) << 12) | ((rn & 15) << 16);

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: w0, r1: w1, r2: w2, sp: sp);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Shsub8([CombinatorialValues(0u, 0xdu)] uint rd,
                           [CombinatorialValues(1u)] uint rm,
                           [CombinatorialValues(2u)] uint rn,
                           [CombinatorialRandomData(Count = RndCnt)] uint w0,
                           [CombinatorialRandomData(Count = RndCnt)] uint w1,
                           [CombinatorialRandomData(Count = RndCnt)] uint w2)
        {
            uint opcode = 0xE6300FF0u; // SHSUB8 R0, R0, R0

            opcode |= ((rm & 15) << 0) | ((rd & 15) << 12) | ((rn & 15) << 16);

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: w0, r1: w1, r2: w2, sp: sp);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Ssat_Usat([CombinatorialMemberData(nameof(SsatUsat))] uint opcode,
                              [CombinatorialValues(0u, 0xdu)] uint rd,
                              [CombinatorialValues(1u, 0xdu)] uint rn,
                              [CombinatorialValues(0u, 7u, 8u, 0xfu, 0x10u, 0x1fu)] uint sat,
                              [CombinatorialValues(0u, 7u, 8u, 0xfu, 0x10u, 0x1fu)] uint shift,
                              [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                      0x80000000u, 0xFFFFFFFFu)] uint wn)
        {
            opcode |= ((rn & 15) << 0) | ((shift & 31) << 7) | ((rd & 15) << 12) | ((sat & 31) << 16);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, r1: wn, sp: w31);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Ssat16_Usat16([CombinatorialMemberData(nameof(Ssat16Usat16))] uint opcode,
                                  [CombinatorialValues(0u, 0xdu)] uint rd,
                                  [CombinatorialValues(1u, 0xdu)] uint rn,
                                  [CombinatorialValues(0u, 7u, 8u, 0xfu)] uint sat,
                                  [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                          0x80000000u, 0xFFFFFFFFu)] uint wn)
        {
            opcode |= ((rn & 15) << 0) | ((rd & 15) << 12) | ((sat & 15) << 16);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, r1: wn, sp: w31);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void SU_H_AddSub_8([CombinatorialMemberData(nameof(SuHAddSub8))] uint opcode,
                                  [CombinatorialValues(0u, 0xdu)] uint rd,
                                  [CombinatorialValues(1u)] uint rm,
                                  [CombinatorialValues(2u)] uint rn,
                                  [CombinatorialRandomData(Count = RndCnt)] uint w0,
                                  [CombinatorialRandomData(Count = RndCnt)] uint w1,
                                  [CombinatorialRandomData(Count = RndCnt)] uint w2)
        {
            opcode |= ((rm & 15) << 0) | ((rd & 15) << 12) | ((rn & 15) << 16);

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: w0, r1: w1, r2: w2, sp: sp);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Uadd8_Sel([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u)] uint rm,
                              [CombinatorialValues(2u)] uint rn,
                              [CombinatorialRandomData(Count = RndCnt)] uint w0,
                              [CombinatorialRandomData(Count = RndCnt)] uint w1,
                              [CombinatorialRandomData(Count = RndCnt)] uint w2)
        {
            uint opUadd8 = 0xE6500F90; // UADD8 R0, R0, R0
            uint opSel = 0xE6800FB0; // SEL R0, R0, R0

            opUadd8 |= ((rm & 15) << 0) | ((rd & 15) << 12) | ((rn & 15) << 16);
            opSel |= ((rm & 15) << 0) | ((rd & 15) << 12) | ((rn & 15) << 16);

            SetContext(r0: w0, r1: w1, r2: w2);
            Opcode(opUadd8);
            Opcode(opSel);
            Opcode(0xE12FFF1E); // BX LR
            ExecuteOpcodes();

            CompareAgainstUnicorn();
        }
#endif
    }
}
