#define AluRs32

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("AluRs32")]
    public sealed class CpuTestAluRs32 : CpuTest32
    {
        public CpuTestAluRs32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if AluRs32

        #region "ValueSource (Opcodes)"
        private static uint[] _Add_Adds_Rsb_Rsbs_()
        {
            return new[]
            {
                0xe0800000u, // ADD R0, R0, R0, LSL #0
                0xe0900000u, // ADDS R0, R0, R0, LSL #0
                0xe0600000u, // RSB R0, R0, R0, LSL #0
                0xe0700000u, // RSBS R0, R0, R0, LSL #0
            };
        }

        private static uint[] _Adc_Adcs_Rsc_Rscs_Sbc_Sbcs_()
        {
            return new[]
            {
                0xe0a00000u, // ADC R0, R0, R0
                0xe0b00000u, // ADCS R0, R0, R0
                0xe0e00000u, // RSC R0, R0, R0
                0xe0f00000u, // RSCS R0, R0, R0
                0xe0c00000u, // SBC R0, R0, R0
                0xe0d00000u, // SBCS R0, R0, R0
            };
        }
        #endregion


        [Theory]
        [PairwiseData]
        public void Adc_Adcs_Rsc_Rscs_Sbc_Sbcs([CombinatorialMemberData(nameof(_Adc_Adcs_Rsc_Rscs_Sbc_Sbcs_))] uint opcode,
                                               [CombinatorialValues(0u, 13u)] uint rd,
                                               [CombinatorialValues(1u, 13u)] uint rn,
                                               [CombinatorialValues(2u, 13u)] uint rm,
                                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                                               bool carryIn)
        {
            opcode |= ((rm & 15) << 0) | ((rn & 15) << 16) | ((rd & 15) << 12);

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r1: wn, r2: wm, sp: sp, carry: carryIn);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Add_Adds_Rsb_Rsbs([CombinatorialMemberData(nameof(_Add_Adds_Rsb_Rsbs_))] uint opcode,
                                      [CombinatorialValues(0u, 13u)] uint rd,
                                      [CombinatorialValues(1u, 13u)] uint rn,
                                      [CombinatorialValues(2u, 13u)] uint rm,
                                      [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                              0x80000000u, 0xFFFFFFFFu)] uint wn,
                                      [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                              0x80000000u, 0xFFFFFFFFu)] uint wm,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint shift, // <LSL, LSR, ASR, ROR>
                                      [CombinatorialValues(0u, 15u, 16u, 31u)] uint amount)
        {
            opcode |= ((rm & 15) << 0) | ((rn & 15) << 16) | ((rd & 15) << 12);
            opcode |= ((shift & 3) << 5) | ((amount & 31) << 7);

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r1: wn, r2: wm, sp: sp);

            CompareAgainstUnicorn();
        }
#endif
    }
}
