#define Misc32

using ARMeilleure.State;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Misc32")]
    public sealed class CpuTestMisc32 : CpuTest32
    {
        public CpuTestMisc32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Misc32

        #region "ValueSource (Types)"
        private static IEnumerable<ulong> _1S_F_()
        {
            yield return 0x00000000FF7FFFFFul; // -Max Normal    (float.MinValue)
            yield return 0x0000000080800000ul; // -Min Normal
            yield return 0x00000000807FFFFFul; // -Max Subnormal
            yield return 0x0000000080000001ul; // -Min Subnormal (-float.Epsilon)
            yield return 0x000000007F7FFFFFul; // +Max Normal    (float.MaxValue)
            yield return 0x0000000000800000ul; // +Min Normal
            yield return 0x00000000007FFFFFul; // +Max Subnormal
            yield return 0x0000000000000001ul; // +Min Subnormal (float.Epsilon)

            if (!_noZeros)
            {
                yield return 0x0000000080000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0x00000000FF800000ul; // -Infinity
                yield return 0x000000007F800000ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0x00000000FFC00000ul; // -QNaN (all zeros payload) (float.NaN)
                yield return 0x00000000FFBFFFFFul; // -SNaN (all ones  payload)
                yield return 0x000000007FC00000ul; // +QNaN (all zeros payload) (-float.NaN) (DefaultNaN)
                yield return 0x000000007FBFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong grbg = Random.Shared.NextUInt();
                ulong rnd1 = GenNormalS();
                ulong rnd2 = GenSubnormalS();

                yield return (grbg << 32) | rnd1;
                yield return (grbg << 32) | rnd2;
            }
        }
        #endregion

        private const int RndCnt = 2;

        private static readonly bool _noZeros = false;
        private static readonly bool _noInfs = false;
        private static readonly bool _noNaNs = false;

        [Theory]
        [PairwiseData]
        public void Vmsr_Vcmp_Vmrs([CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                   [CombinatorialMemberData(nameof(_1S_F_))] ulong b,
                                   bool mode1,
                                   bool mode2,
                                   bool mode3)
        {
            V128 v4 = MakeVectorE0(a);
            V128 v5 = MakeVectorE0(b);

            uint r0 = mode1
                ? Random.Shared.NextUInt(0xf) << 28
                : Random.Shared.NextUInt();

            bool v = mode3 && Random.Shared.NextBool();
            bool c = mode3 && Random.Shared.NextBool();
            bool z = mode3 && Random.Shared.NextBool();
            bool n = mode3 && Random.Shared.NextBool();

            int fpscr = mode1
                ? (int)Random.Shared.NextUInt()
                : (int)Random.Shared.NextUInt(0xf) << 28;

            SetContext(r0: r0, v4: v4, v5: v5, overflow: v, carry: c, zero: z, negative: n, fpscr: fpscr);

            if (mode1)
            {
                Opcode(0xEEE10A10); // VMSR FPSCR, R0
            }
            Opcode(0xEEB48A4A); // VCMP.F32 S16, S20
            if (mode2)
            {
                Opcode(0xEEF10A10); // VMRS R0, FPSCR
                Opcode(0xE200020F); // AND R0, #0xF0000000 // R0 &= "Fpsr.Nzcv".
            }
            if (mode3)
            {
                Opcode(0xEEF1FA10); // VMRS APSR_NZCV, FPSCR
            }
            Opcode(0xE12FFF1E); // BX LR

            ExecuteOpcodes();

            CompareAgainstUnicorn(fpsrMask: Fpsr.Nzcv);
        }
#endif
    }
}
