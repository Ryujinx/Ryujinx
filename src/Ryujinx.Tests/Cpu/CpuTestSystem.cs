#define System

using ARMeilleure.State;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("System")]
    public sealed class CpuTestSystem : CpuTest
    {
        public CpuTestSystem(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if System

        #region "ValueSource (Types)"
        private static IEnumerable<ulong> _GenNzcv_()
        {
            yield return 0x0000000000000000ul;
            yield return 0x7FFFFFFFFFFFFFFFul;
            yield return 0x8000000000000000ul;
            yield return 0xFFFFFFFFFFFFFFFFul;

            bool v = Random.Shared.NextBool();
            bool c = Random.Shared.NextBool();
            bool z = Random.Shared.NextBool();
            bool n = Random.Shared.NextBool();

            ulong rnd = 0UL;

            rnd |= (v ? 1UL : 0UL) << (int)PState.VFlag;
            rnd |= (c ? 1UL : 0UL) << (int)PState.CFlag;
            rnd |= (z ? 1UL : 0UL) << (int)PState.ZFlag;
            rnd |= (n ? 1UL : 0UL) << (int)PState.NFlag;

            yield return rnd;
        }
        #endregion

        #region "ValueSource (Opcodes)"
        private static uint[] _MrsMsr_Nzcv_()
        {
            return new[]
            {
                0xD53B4200u, // MRS X0, NZCV
                0xD51B4200u, // MSR NZCV, X0
            };
        }
        #endregion

        [SkippableTheory]
        [PairwiseData]
        public void MrsMsr_Nzcv([CombinatorialMemberData(nameof(_MrsMsr_Nzcv_))] uint opcodes,
                                [CombinatorialValues(0u, 1u, 31u)] uint rt,
                                [CombinatorialMemberData(nameof(_GenNzcv_))] ulong xt)
        {
            opcodes |= (rt & 31) << 0;

            bool v = Random.Shared.NextBool();
            bool c = Random.Shared.NextBool();
            bool z = Random.Shared.NextBool();
            bool n = Random.Shared.NextBool();

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcodes, x0: xt, x1: xt, x31: x31, overflow: v, carry: c, zero: z, negative: n);

            CompareAgainstUnicorn();
        }
#endif
    }
}
