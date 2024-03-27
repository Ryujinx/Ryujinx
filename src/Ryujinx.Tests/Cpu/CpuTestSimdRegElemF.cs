#define SimdRegElemF

using ARMeilleure.State;
using System;
using Xunit;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdRegElemF")]
    public sealed class CpuTestSimdRegElemF : CpuTest
    {
        public CpuTestSimdRegElemF(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdRegElemF

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

        private static IEnumerable<ulong> _2S_F_()
        {
            yield return 0xFF7FFFFFFF7FFFFFul; // -Max Normal    (float.MinValue)
            yield return 0x8080000080800000ul; // -Min Normal
            yield return 0x807FFFFF807FFFFFul; // -Max Subnormal
            yield return 0x8000000180000001ul; // -Min Subnormal (-float.Epsilon)
            yield return 0x7F7FFFFF7F7FFFFFul; // +Max Normal    (float.MaxValue)
            yield return 0x0080000000800000ul; // +Min Normal
            yield return 0x007FFFFF007FFFFFul; // +Max Subnormal
            yield return 0x0000000100000001ul; // +Min Subnormal (float.Epsilon)

            if (!_noZeros)
            {
                yield return 0x8000000080000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0xFF800000FF800000ul; // -Infinity
                yield return 0x7F8000007F800000ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0xFFC00000FFC00000ul; // -QNaN (all zeros payload) (float.NaN)
                yield return 0xFFBFFFFFFFBFFFFFul; // -SNaN (all ones  payload)
                yield return 0x7FC000007FC00000ul; // +QNaN (all zeros payload) (-float.NaN) (DefaultNaN)
                yield return 0x7FBFFFFF7FBFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong rnd1 = GenNormalS();
                ulong rnd2 = GenSubnormalS();

                yield return (rnd1 << 32) | rnd1;
                yield return (rnd2 << 32) | rnd2;
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
        private static uint[] _F_Mla_Mls_Se_S_()
        {
            return new[]
            {
                0x5F821020u, // FMLA S0, S1, V2.S[0]
                0x5F825020u, // FMLS S0, S1, V2.S[0]
            };
        }

        private static uint[] _F_Mla_Mls_Se_D_()
        {
            return new[]
            {
                0x5FC21020u, // FMLA D0, D1, V2.D[0]
                0x5FC25020u, // FMLS D0, D1, V2.D[0]
            };
        }

        private static uint[] _F_Mla_Mls_Ve_2S_4S_()
        {
            return new[]
            {
                0x0F801000u, // FMLA V0.2S, V0.2S, V0.S[0]
                0x0F805000u, // FMLS V0.2S, V0.2S, V0.S[0]
            };
        }

        private static uint[] _F_Mla_Mls_Ve_2D_()
        {
            return new[]
            {
                0x4FC01000u, // FMLA V0.2D, V0.2D, V0.D[0]
                0x4FC05000u, // FMLS V0.2D, V0.2D, V0.D[0]
            };
        }

        private static uint[] _F_Mul_Mulx_Se_S_()
        {
            return new[]
            {
                0x5F829020u, // FMUL  S0, S1, V2.S[0]
                0x7F829020u, // FMULX S0, S1, V2.S[0]
            };
        }

        private static uint[] _F_Mul_Mulx_Se_D_()
        {
            return new[]
            {
                0x5FC29020u, // FMUL  D0, D1, V2.D[0]
                0x7FC29020u, // FMULX D0, D1, V2.D[0]
            };
        }

        private static uint[] _F_Mul_Mulx_Ve_2S_4S_()
        {
            return new[]
            {
                0x0F809000u, // FMUL  V0.2S, V0.2S, V0.S[0]
                0x2F809000u, // FMULX V0.2S, V0.2S, V0.S[0]
            };
        }

        private static uint[] _F_Mul_Mulx_Ve_2D_()
        {
            return new[]
            {
                0x4FC09000u, // FMUL  V0.2D, V0.2D, V0.D[0]
                0x6FC09000u, // FMULX V0.2D, V0.2D, V0.D[0]
            };
        }
        #endregion

        private const int RndCnt = 2;

        private static readonly bool _noZeros = false;
        private static readonly bool _noInfs = false;
        private static readonly bool _noNaNs = false;

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Mla_Mls_Se_S([CombinatorialMemberData(nameof(_F_Mla_Mls_Se_S_))] uint opcodes,
                                   [CombinatorialMemberData(nameof(_1S_F_))] ulong z,
                                   [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                   [CombinatorialMemberData(nameof(_2S_F_))] ulong b,
                                   [CombinatorialValues(0u, 1u, 2u, 3u)] uint index)
        {
            uint h = (index >> 1) & 1;
            uint l = index & 1;

            opcodes |= (l << 21) | (h << 11);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0E1(b, b * h);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsS);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Mla_Mls_Se_D([CombinatorialMemberData(nameof(_F_Mla_Mls_Se_D_))] uint opcodes,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong b,
                                   [CombinatorialValues(0u, 1u)] uint index)
        {
            uint h = index & 1;

            opcodes |= h << 11;

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0E1(b, b * h);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsD);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Mla_Mls_Ve_2S_4S([CombinatorialMemberData(nameof(_F_Mla_Mls_Ve_2S_4S_))] uint opcodes,
                                       [CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialValues(2u, 0u)] uint rm,
                                       [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                       [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                       [CombinatorialMemberData(nameof(_2S_F_))] ulong b,
                                       [CombinatorialValues(0u, 1u, 2u, 3u)] uint index,
                                       [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            uint h = (index >> 1) & 1;
            uint l = index & 1;

            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (l << 21) | (h << 11);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);
            V128 v2 = MakeVectorE0E1(b, b * h);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsS);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Mla_Mls_Ve_2D([CombinatorialMemberData(nameof(_F_Mla_Mls_Ve_2D_))] uint opcodes,
                                    [CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                    [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                    [CombinatorialMemberData(nameof(_1D_F_))] ulong b,
                                    [CombinatorialValues(0u, 1u)] uint index)
        {
            uint h = index & 1;

            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= h << 11;

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b * h);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsD);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mul_Mulx_Se_S([CombinatorialMemberData(nameof(_F_Mul_Mulx_Se_S_))] uint opcodes,
                                    [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                    [CombinatorialMemberData(nameof(_2S_F_))] ulong b,
                                    [CombinatorialValues(0u, 1u, 2u, 3u)] uint index)
        {
            uint h = (index >> 1) & 1;
            uint l = index & 1;

            opcodes |= (l << 21) | (h << 11);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0E1(b, b * h);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mul_Mulx_Se_D([CombinatorialMemberData(nameof(_F_Mul_Mulx_Se_D_))] uint opcodes,
                                    [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                    [CombinatorialMemberData(nameof(_1D_F_))] ulong b,
                                    [CombinatorialValues(0u, 1u)] uint index)
        {
            uint h = index & 1;

            opcodes |= h << 11;

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0E1(b, b * h);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mul_Mulx_Ve_2S_4S([CombinatorialMemberData(nameof(_F_Mul_Mulx_Ve_2S_4S_))] uint opcodes,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                        [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                        [CombinatorialMemberData(nameof(_2S_F_))] ulong b,
                                        [CombinatorialValues(0u, 1u, 2u, 3u)] uint index,
                                        [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            uint h = (index >> 1) & 1;
            uint l = index & 1;

            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (l << 21) | (h << 11);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);
            V128 v2 = MakeVectorE0E1(b, b * h);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mul_Mulx_Ve_2D([CombinatorialMemberData(nameof(_F_Mul_Mulx_Ve_2D_))] uint opcodes,
                                     [CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                     [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                     [CombinatorialMemberData(nameof(_1D_F_))] ulong b,
                                     [CombinatorialValues(0u, 1u)] uint index)
        {
            uint h = index & 1;

            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= h << 11;

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b * h);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }
#endif
    }
}
