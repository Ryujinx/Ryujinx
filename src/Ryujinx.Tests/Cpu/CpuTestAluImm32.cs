#define AluImm32

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("AluImm32")]
    public sealed class CpuTestAluImm32 : CpuTest32
    {
        public CpuTestAluImm32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if AluImm32

        #region "ValueSource (Opcodes)"
        private static uint[] Opcodes()
        {
            return new[]
            {
                0xe2a00000u, // ADC R0, R0, #0
                0xe2b00000u, // ADCS R0, R0, #0
                0xe2800000u, // ADD R0, R0, #0
                0xe2900000u, // ADDS R0, R0, #0
                0xe3c00000u, // BIC R0, R0, #0
                0xe3d00000u, // BICS R0, R0, #0
                0xe2600000u, // RSB R0, R0, #0
                0xe2700000u, // RSBS R0, R0, #0
                0xe2e00000u, // RSC R0, R0, #0
                0xe2f00000u, // RSCS R0, R0, #0
                0xe2c00000u, // SBC R0, R0, #0
                0xe2d00000u, // SBCS R0, R0, #0
                0xe2400000u, // SUB R0, R0, #0
                0xe2500000u, // SUBS R0, R0, #0
            };
        }
        #endregion

        private const int RndCnt = 2;

        [Theory]
        [PairwiseData]
        public void TestCpuTestAluImm32([CombinatorialMemberData(nameof(Opcodes))] uint opcode,
                                        [CombinatorialValues(0u, 13u)] uint rd,
                                        [CombinatorialValues(1u, 13u)] uint rn,
                                        [CombinatorialRandomData(Count = RndCnt)] uint imm,
                                        [CombinatorialRandomData(Count = RndCnt)] uint wn,
                                        [CombinatorialValues(true, false)] bool carryIn)
        {
            opcode |= ((imm & 0xfff) << 0) | ((rn & 15) << 16) | ((rd & 15) << 12);

            uint sp = Random.Shared.NextUInt();

            SingleOpcode(opcode, r1: wn, sp: sp, carry: carryIn);

            CompareAgainstUnicorn();
        }
#endif
    }
}
