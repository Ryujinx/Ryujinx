#define SimdFmov

using ARMeilleure.State;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdFmov")]
    public sealed class CpuTestSimdFmov : CpuTest
    {
        public CpuTestSimdFmov(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdFmov

        #region "ValueSource"
        private static uint[] _F_Mov_Si_S_()
        {
            return new[]
            {
                0x1E201000u, // FMOV S0, #2.0
            };
        }

        private static uint[] _F_Mov_Si_D_()
        {
            return new[]
            {
                0x1E601000u, // FMOV D0, #2.0
            };
        }
        #endregion

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Si_S([CombinatorialMemberData(nameof(_F_Mov_Si_S_))] uint opcodes,
                               [CombinatorialRange(0u, 255u, 1u)] uint imm8)
        {
            opcodes |= ((imm8 & 0xFFu) << 13);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcodes, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Si_D([CombinatorialMemberData(nameof(_F_Mov_Si_D_))] uint opcodes,
                               [CombinatorialRange(0u, 255u, 1u)] uint imm8)
        {
            opcodes |= ((imm8 & 0xFFu) << 13);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);

            SingleOpcode(opcodes, v0: v0);

            CompareAgainstUnicorn();
        }
#endif
    }
}
