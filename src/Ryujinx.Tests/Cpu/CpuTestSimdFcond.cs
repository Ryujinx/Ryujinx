#define SimdFcond

using ARMeilleure.State;
using System;
using Xunit;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdFcond")]
    public sealed class CpuTestSimdFcond : CpuTest
    {
        public CpuTestSimdFcond(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdFcond

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

        private static IEnumerable<ulong> _1D_F_()
        {
            yield return 0xFFEFFFFFFFFFFFFFul; // -Max Normal    (double.MinValue)
            yield return 0x8010000000000000ul; // -Min Normal
            yield return 0x800FFFFFFFFFFFFFul; // -Max Subnormal
            yield return 0x8000000000000001ul; // -Min Subnormal (-double.Epsilon)
            yield return 0x7FEFFFFFFFFFFFFFul; // +Max Normal    (double.MaxValue)
            yield return 0x0010000000000000ul; // +Min Normal
            yield return 0x000FFFFFFFFFFFFFul; // +Max Subnormal
            yield return 0x0000000000000001ul; // +Min Subnormal (double.Epsilon)

            if (!_noZeros)
            {
                yield return 0x8000000000000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0xFFF0000000000000ul; // -Infinity
                yield return 0x7FF0000000000000ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0xFFF8000000000000ul; // -QNaN (all zeros payload) (double.NaN)
                yield return 0xFFF7FFFFFFFFFFFFul; // -SNaN (all ones  payload)
                yield return 0x7FF8000000000000ul; // +QNaN (all zeros payload) (-double.NaN) (DefaultNaN)
                yield return 0x7FF7FFFFFFFFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong rnd1 = GenNormalD();
                ulong rnd2 = GenSubnormalD();

                yield return rnd1;
                yield return rnd2;
            }
        }
        #endregion

        #region "ValueSource (Opcodes)"
        private static uint[] _F_Ccmp_Ccmpe_S_S_()
        {
            return new[]
            {
                0x1E220420u, // FCCMP  S1, S2, #0, EQ
                0x1E220430u, // FCCMPE S1, S2, #0, EQ
            };
        }

        private static uint[] _F_Ccmp_Ccmpe_S_D_()
        {
            return new[]
            {
                0x1E620420u, // FCCMP  D1, D2, #0, EQ
                0x1E620430u, // FCCMPE D1, D2, #0, EQ
            };
        }

        private static uint[] _F_Csel_S_S_()
        {
            return new[]
            {
                0x1E220C20u, // FCSEL S0, S1, S2, EQ
            };
        }

        private static uint[] _F_Csel_S_D_()
        {
            return new[]
            {
                0x1E620C20u, // FCSEL D0, D1, D2, EQ
            };
        }
        #endregion

        private const int RndCnt = 2;
        private const int RndCntNzcv = 2;

        private static readonly bool _noZeros = false;
        private static readonly bool _noInfs = false;
        private static readonly bool _noNaNs = false;

        public static readonly uint[] RandomNzcv =
        {
            Random.Shared.NextUInt(15u),
            Random.Shared.NextUInt(15u),
        };

        [SkippableTheory]
        [PairwiseData]
        public void F_Ccmp_Ccmpe_S_S([CombinatorialMemberData(nameof(_F_Ccmp_Ccmpe_S_S_))] uint opcodes,
                                     [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                     [CombinatorialMemberData(nameof(_1S_F_))] ulong b,
                                     [CombinatorialMemberData(nameof(RandomNzcv))] uint nzcv,
                                     [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u, // <EQ, NE, CS/HS, CC/LO,
                                             0b0100u, 0b0101u, 0b0110u, 0b0111u,              //  MI, PL, VS, VC,
                                             0b1000u, 0b1001u, 0b1010u, 0b1011u,              //  HI, LS, GE, LT,
                                             0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond)  //  GT, LE, AL, NV>
        {
            opcodes |= ((cond & 15) << 12) | ((nzcv & 15) << 0);

            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            bool v = Random.Shared.NextBool();
            bool c = Random.Shared.NextBool();
            bool z = Random.Shared.NextBool();
            bool n = Random.Shared.NextBool();

            SingleOpcode(opcodes, v1: v1, v2: v2, overflow: v, carry: c, zero: z, negative: n);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Ccmp_Ccmpe_S_D([CombinatorialMemberData(nameof(_F_Ccmp_Ccmpe_S_D_))] uint opcodes,
                                     [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                     [CombinatorialMemberData(nameof(_1D_F_))] ulong b,
                                     [CombinatorialMemberData(nameof(RandomNzcv))] uint nzcv,
                                     [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u, // <EQ, NE, CS/HS, CC/LO,
                                             0b0100u, 0b0101u, 0b0110u, 0b0111u,              //  MI, PL, VS, VC,
                                             0b1000u, 0b1001u, 0b1010u, 0b1011u,              //  HI, LS, GE, LT,
                                             0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond)  //  GT, LE, AL, NV>
        {
            opcodes |= ((cond & 15) << 12) | ((nzcv & 15) << 0);

            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            bool v = Random.Shared.NextBool();
            bool c = Random.Shared.NextBool();
            bool z = Random.Shared.NextBool();
            bool n = Random.Shared.NextBool();

            SingleOpcode(opcodes, v1: v1, v2: v2, overflow: v, carry: c, zero: z, negative: n);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Csel_S_S([CombinatorialMemberData(nameof(_F_Csel_S_S_))] uint opcodes,
                               [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                               [CombinatorialMemberData(nameof(_1S_F_))] ulong b,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u, // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,              //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,              //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond)  //  GT, LE, AL, NV>
        {
            opcodes |= ((cond & 15) << 12);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Csel_S_D([CombinatorialMemberData(nameof(_F_Csel_S_D_))] uint opcodes,
                               [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                               [CombinatorialMemberData(nameof(_1D_F_))] ulong b,
                               [CombinatorialValues(0b0000u, 0b0001u, 0b0010u, 0b0011u, // <EQ, NE, CS/HS, CC/LO,
                                       0b0100u, 0b0101u, 0b0110u, 0b0111u,              //  MI, PL, VS, VC,
                                       0b1000u, 0b1001u, 0b1010u, 0b1011u,              //  HI, LS, GE, LT,
                                       0b1100u, 0b1101u, 0b1110u, 0b1111u)] uint cond)  //  GT, LE, AL, NV>
        {
            opcodes |= ((cond & 15) << 12);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }
#endif
    }
}
