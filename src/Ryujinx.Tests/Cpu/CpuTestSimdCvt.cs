#define SimdCvt

using ARMeilleure.State;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdCvt")]
    public sealed class CpuTestSimdCvt : CpuTest
    {
        public CpuTestSimdCvt(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdCvt

        #region "ValueSource (Types)"
        private static uint[] _W_()
        {
            return new[] {
                0x00000000u, 0x7FFFFFFFu,
                0x80000000u, 0xFFFFFFFFu,
            };
        }

        private static ulong[] _X_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static IEnumerable<ulong> _1S_F_WX_()
        {
            // int
            yield return 0x00000000CF000001ul; // -2.1474839E9f  (-2147483904)
            yield return 0x00000000CF000000ul; // -2.14748365E9f (-2147483648)
            yield return 0x00000000CEFFFFFFul; // -2.14748352E9f (-2147483520)
            yield return 0x000000004F000001ul; //  2.1474839E9f  (2147483904)
            yield return 0x000000004F000000ul; //  2.14748365E9f (2147483648)
            yield return 0x000000004EFFFFFFul; //  2.14748352E9f (2147483520)

            // long
            yield return 0x00000000DF000001ul; // -9.223373E18f  (-9223373136366403584)
            yield return 0x00000000DF000000ul; // -9.223372E18f  (-9223372036854775808)
            yield return 0x00000000DEFFFFFFul; // -9.2233715E18f (-9223371487098961920)
            yield return 0x000000005F000001ul; //  9.223373E18f  (9223373136366403584)
            yield return 0x000000005F000000ul; //  9.223372E18f  (9223372036854775808)
            yield return 0x000000005EFFFFFFul; //  9.2233715E18f (9223371487098961920)

            // uint
            yield return 0x000000004F800001ul; // 4.2949678E9f  (4294967808)
            yield return 0x000000004F800000ul; // 4.2949673E9f  (4294967296)
            yield return 0x000000004F7FFFFFul; // 4.29496704E9f (4294967040)

            // ulong
            yield return 0x000000005F800001ul; // 1.8446746E19f (18446746272732807168)
            yield return 0x000000005F800000ul; // 1.8446744E19f (18446744073709551616)
            yield return 0x000000005F7FFFFFul; // 1.8446743E19f (18446742974197923840)

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

                ulong rnd1 = (uint)BitConverter.SingleToInt32Bits(
                    (int)Random.Shared.NextUInt());
                ulong rnd2 = (uint)BitConverter.SingleToInt32Bits(
                    (long)Random.Shared.NextULong());
                ulong rnd3 = (uint)BitConverter.SingleToInt32Bits(
                    Random.Shared.NextUInt());
                ulong rnd4 = (uint)BitConverter.SingleToInt32Bits(
                    Random.Shared.NextULong());

                ulong rnd5 = GenNormalS();
                ulong rnd6 = GenSubnormalS();

                yield return (grbg << 32) | rnd1;
                yield return (grbg << 32) | rnd2;
                yield return (grbg << 32) | rnd3;
                yield return (grbg << 32) | rnd4;

                yield return (grbg << 32) | rnd5;
                yield return (grbg << 32) | rnd6;
            }
        }

        private static IEnumerable<ulong> _1D_F_WX_()
        {
            // int
            yield return 0xC1E0000000200000ul; // -2147483649.0000000d (-2147483649)
            yield return 0xC1E0000000000000ul; // -2147483648.0000000d (-2147483648)
            yield return 0xC1DFFFFFFFC00000ul; // -2147483647.0000000d (-2147483647)
            yield return 0x41E0000000200000ul; //  2147483649.0000000d (2147483649)
            yield return 0x41E0000000000000ul; //  2147483648.0000000d (2147483648)
            yield return 0x41DFFFFFFFC00000ul; //  2147483647.0000000d (2147483647)

            // long
            yield return 0xC3E0000000000001ul; // -9.2233720368547780E18d (-9223372036854778000)
            yield return 0xC3E0000000000000ul; // -9.2233720368547760E18d (-9223372036854776000)
            yield return 0xC3DFFFFFFFFFFFFFul; // -9.2233720368547750E18d (-9223372036854775000)
            yield return 0x43E0000000000001ul; //  9.2233720368547780E18d (9223372036854778000)
            yield return 0x43E0000000000000ul; //  9.2233720368547760E18d (9223372036854776000)
            yield return 0x43DFFFFFFFFFFFFFul; //  9.2233720368547750E18d (9223372036854775000)

            // uint
            yield return 0x41F0000000100000ul; // 4294967297.0000000d (4294967297)
            yield return 0x41F0000000000000ul; // 4294967296.0000000d (4294967296)
            yield return 0x41EFFFFFFFE00000ul; // 4294967295.0000000d (4294967295)

            // ulong
            yield return 0x43F0000000000001ul; // 1.8446744073709556e19d (18446744073709556000)
            yield return 0x43F0000000000000ul; // 1.8446744073709552E19d (18446744073709552000)
            yield return 0x43EFFFFFFFFFFFFFul; // 1.8446744073709550e19d (18446744073709550000)

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
                ulong rnd1 = (ulong)BitConverter.DoubleToInt64Bits(
                    (int)Random.Shared.NextUInt());
                ulong rnd2 = (ulong)BitConverter.DoubleToInt64Bits(
                    (long)Random.Shared.NextULong());
                ulong rnd3 = (ulong)BitConverter.DoubleToInt64Bits(
                    Random.Shared.NextUInt());
                ulong rnd4 = (ulong)BitConverter.DoubleToInt64Bits(
                    Random.Shared.NextULong());

                ulong rnd5 = GenNormalD();
                ulong rnd6 = GenSubnormalD();

                yield return rnd1;
                yield return rnd2;
                yield return rnd3;
                yield return rnd4;

                yield return rnd5;
                yield return rnd6;
            }
        }
        #endregion

        #region "ValueSource (Opcodes)"
        private static uint[] _F_Cvt_AMPZ_SU_Gp_SW_()
        {
            return new[]
            {
                0x1E240000u, // FCVTAS W0, S0
                0x1E250000u, // FCVTAU W0, S0
                0x1E300000u, // FCVTMS W0, S0
                0x1E310000u, // FCVTMU W0, S0
                0x1E200000u, // FCVTNS W0, S0
                0x1E280000u, // FCVTPS W0, S0
                0x1E290000u, // FCVTPU W0, S0
                0x1E380000u, // FCVTZS W0, S0
                0x1E390000u, // FCVTZU W0, S0
            };
        }

        private static uint[] _F_Cvt_AMPZ_SU_Gp_SX_()
        {
            return new[]
            {
                0x9E240000u, // FCVTAS X0, S0
                0x9E250000u, // FCVTAU X0, S0
                0x9E300000u, // FCVTMS X0, S0
                0x9E310000u, // FCVTMU X0, S0
                0x9E200000u, // FCVTNS X0, S0
                0x9E280000u, // FCVTPS X0, S0
                0x9E290000u, // FCVTPU X0, S0
                0x9E380000u, // FCVTZS X0, S0
                0x9E390000u, // FCVTZU X0, S0
            };
        }

        private static uint[] _F_Cvt_AMPZ_SU_Gp_DW_()
        {
            return new[]
            {
                0x1E640000u, // FCVTAS W0, D0
                0x1E650000u, // FCVTAU W0, D0
                0x1E700000u, // FCVTMS W0, D0
                0x1E710000u, // FCVTMU W0, D0
                0x1E600000u, // FCVTNS W0, D0
                0x1E680000u, // FCVTPS W0, D0
                0x1E690000u, // FCVTPU W0, D0
                0x1E780000u, // FCVTZS W0, D0
                0x1E790000u, // FCVTZU W0, D0
            };
        }

        private static uint[] _F_Cvt_AMPZ_SU_Gp_DX_()
        {
            return new[]
            {
                0x9E640000u, // FCVTAS X0, D0
                0x9E650000u, // FCVTAU X0, D0
                0x9E700000u, // FCVTMS X0, D0
                0x9E710000u, // FCVTMU X0, D0
                0x9E600000u, // FCVTNS X0, D0
                0x9E680000u, // FCVTPS X0, D0
                0x9E690000u, // FCVTPU X0, D0
                0x9E780000u, // FCVTZS X0, D0
                0x9E790000u, // FCVTZU X0, D0
            };
        }

        private static uint[] _F_Cvt_Z_SU_Gp_Fixed_SW_()
        {
            return new[]
            {
                0x1E188000u, // FCVTZS W0, S0, #32
                0x1E198000u, // FCVTZU W0, S0, #32
            };
        }

        private static uint[] _F_Cvt_Z_SU_Gp_Fixed_SX_()
        {
            return new[]
            {
                0x9E180000u, // FCVTZS X0, S0, #64
                0x9E190000u, // FCVTZU X0, S0, #64
            };
        }

        private static uint[] _F_Cvt_Z_SU_Gp_Fixed_DW_()
        {
            return new[]
            {
                0x1E588000u, // FCVTZS W0, D0, #32
                0x1E598000u, // FCVTZU W0, D0, #32
            };
        }

        private static uint[] _F_Cvt_Z_SU_Gp_Fixed_DX_()
        {
            return new[]
            {
                0x9E580000u, // FCVTZS X0, D0, #64
                0x9E590000u, // FCVTZU X0, D0, #64
            };
        }

        private static uint[] _SU_Cvt_F_Gp_WS_()
        {
            return new[]
            {
                0x1E220000u, // SCVTF S0, W0
                0x1E230000u, // UCVTF S0, W0
            };
        }

        private static uint[] _SU_Cvt_F_Gp_WD_()
        {
            return new[]
            {
                0x1E620000u, // SCVTF D0, W0
                0x1E630000u, // UCVTF D0, W0
            };
        }

        private static uint[] _SU_Cvt_F_Gp_XS_()
        {
            return new[]
            {
                0x9E220000u, // SCVTF S0, X0
                0x9E230000u, // UCVTF S0, X0
            };
        }

        private static uint[] _SU_Cvt_F_Gp_XD_()
        {
            return new[]
            {
                0x9E620000u, // SCVTF D0, X0
                0x9E630000u, // UCVTF D0, X0
            };
        }

        private static uint[] _SU_Cvt_F_Gp_Fixed_WS_()
        {
            return new[]
            {
                0x1E028000u, // SCVTF S0, W0, #32
                0x1E038000u, // UCVTF S0, W0, #32
            };
        }

        private static uint[] _SU_Cvt_F_Gp_Fixed_WD_()
        {
            return new[]
            {
                0x1E428000u, // SCVTF D0, W0, #32
                0x1E438000u, // UCVTF D0, W0, #32
            };
        }

        private static uint[] _SU_Cvt_F_Gp_Fixed_XS_()
        {
            return new[]
            {
                0x9E020000u, // SCVTF S0, X0, #64
                0x9E030000u, // UCVTF S0, X0, #64
            };
        }

        private static uint[] _SU_Cvt_F_Gp_Fixed_XD_()
        {
            return new[]
            {
                0x9E420000u, // SCVTF D0, X0, #64
                0x9E430000u, // UCVTF D0, X0, #64
            };
        }
        #endregion

        private const int RndCnt = 2;

        private static readonly bool _noZeros = false;
        private static readonly bool _noInfs = false;
        private static readonly bool _noNaNs = false;

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_AMPZ_SU_Gp_SW([CombinatorialMemberData(nameof(_F_Cvt_AMPZ_SU_Gp_SW_))] uint opcodes,
                                        [CombinatorialValues(0u, 31u)] uint rd,
                                        [CombinatorialValues(1u)] uint rn,
                                        [CombinatorialMemberData(nameof(_1S_F_WX_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x0 = (ulong)Random.Shared.NextUInt() << 32;
            uint w31 = Random.Shared.NextUInt();
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, x0: x0, x31: w31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_AMPZ_SU_Gp_SX([CombinatorialMemberData(nameof(_F_Cvt_AMPZ_SU_Gp_SX_))] uint opcodes,
                                        [CombinatorialValues(0u, 31u)] uint rd,
                                        [CombinatorialValues(1u)] uint rn,
                                        [CombinatorialMemberData(nameof(_1S_F_WX_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, x31: x31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_AMPZ_SU_Gp_DW([CombinatorialMemberData(nameof(_F_Cvt_AMPZ_SU_Gp_DW_))] uint opcodes,
                                        [CombinatorialValues(0u, 31u)] uint rd,
                                        [CombinatorialValues(1u)] uint rn,
                                        [CombinatorialMemberData(nameof(_1D_F_WX_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x0 = (ulong)Random.Shared.NextUInt() << 32;
            uint w31 = Random.Shared.NextUInt();
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, x0: x0, x31: w31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_AMPZ_SU_Gp_DX([CombinatorialMemberData(nameof(_F_Cvt_AMPZ_SU_Gp_DX_))] uint opcodes,
                                        [CombinatorialValues(0u, 31u)] uint rd,
                                        [CombinatorialValues(1u)] uint rn,
                                        [CombinatorialMemberData(nameof(_1D_F_WX_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, x31: x31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_Z_SU_Gp_Fixed_SW([CombinatorialMemberData(nameof(_F_Cvt_Z_SU_Gp_Fixed_SW_))] uint opcodes,
                                           [CombinatorialValues(0u, 31u)] uint rd,
                                           [CombinatorialValues(1u)] uint rn,
                                           [CombinatorialMemberData(nameof(_1S_F_WX_))] ulong a,
                                           [CombinatorialValues(1u, 32u)] uint fBits)
        {
            uint scale = (64u - fBits) & 0x3Fu;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (scale << 10);

            ulong x0 = (ulong)Random.Shared.NextUInt() << 32;
            uint w31 = Random.Shared.NextUInt();
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, x0: x0, x31: w31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_Z_SU_Gp_Fixed_SX([CombinatorialMemberData(nameof(_F_Cvt_Z_SU_Gp_Fixed_SX_))] uint opcodes,
                                           [CombinatorialValues(0u, 31u)] uint rd,
                                           [CombinatorialValues(1u)] uint rn,
                                           [CombinatorialMemberData(nameof(_1S_F_WX_))] ulong a,
                                           [CombinatorialValues(1u, 64u)] uint fBits)
        {
            uint scale = (64u - fBits) & 0x3Fu;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (scale << 10);

            ulong x31 = Random.Shared.NextULong();
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, x31: x31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_Z_SU_Gp_Fixed_DW([CombinatorialMemberData(nameof(_F_Cvt_Z_SU_Gp_Fixed_DW_))] uint opcodes,
                                           [CombinatorialValues(0u, 31u)] uint rd,
                                           [CombinatorialValues(1u)] uint rn,
                                           [CombinatorialMemberData(nameof(_1D_F_WX_))] ulong a,
                                           [CombinatorialValues(1u, 32u)] uint fBits)
        {
            uint scale = (64u - fBits) & 0x3Fu;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (scale << 10);

            ulong x0 = (ulong)Random.Shared.NextUInt() << 32;
            uint w31 = Random.Shared.NextUInt();
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, x0: x0, x31: w31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_Z_SU_Gp_Fixed_DX([CombinatorialMemberData(nameof(_F_Cvt_Z_SU_Gp_Fixed_DX_))] uint opcodes,
                                           [CombinatorialValues(0u, 31u)] uint rd,
                                           [CombinatorialValues(1u)] uint rn,
                                           [CombinatorialMemberData(nameof(_1D_F_WX_))] ulong a,
                                           [CombinatorialValues(1u, 64u)] uint fBits)
        {
            uint scale = (64u - fBits) & 0x3Fu;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (scale << 10);

            ulong x31 = Random.Shared.NextULong();
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, x31: x31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_Gp_WS([CombinatorialMemberData(nameof(_SU_Cvt_F_Gp_WS_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 31u)] uint rn,
                                   [CombinatorialMemberData(nameof(_W_))] uint wn)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcodes, x1: wn, x31: w31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_Gp_WD([CombinatorialMemberData(nameof(_SU_Cvt_F_Gp_WD_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 31u)] uint rn,
                                   [CombinatorialMemberData(nameof(_W_))] uint wn)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);

            SingleOpcode(opcodes, x1: wn, x31: w31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_Gp_XS([CombinatorialMemberData(nameof(_SU_Cvt_F_Gp_XS_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 31u)] uint rn,
                                   [CombinatorialMemberData(nameof(_X_))] ulong xn)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcodes, x1: xn, x31: x31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_Gp_XD([CombinatorialMemberData(nameof(_SU_Cvt_F_Gp_XD_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 31u)] uint rn,
                                   [CombinatorialMemberData(nameof(_X_))] ulong xn)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);

            SingleOpcode(opcodes, x1: xn, x31: x31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_Gp_Fixed_WS([CombinatorialMemberData(nameof(_SU_Cvt_F_Gp_Fixed_WS_))] uint opcodes,
                                         [CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 31u)] uint rn,
                                         [CombinatorialMemberData(nameof(_W_))] uint wn,
                                         [CombinatorialValues(1u, 32u)] uint fBits)
        {
            uint scale = (64u - fBits) & 0x3Fu;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (scale << 10);

            uint w31 = Random.Shared.NextUInt();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcodes, x1: wn, x31: w31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_Gp_Fixed_WD([CombinatorialMemberData(nameof(_SU_Cvt_F_Gp_Fixed_WD_))] uint opcodes,
                                         [CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 31u)] uint rn,
                                         [CombinatorialMemberData(nameof(_W_))] uint wn,
                                         [CombinatorialValues(1u, 32u)] uint fBits)
        {
            uint scale = (64u - fBits) & 0x3Fu;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (scale << 10);

            uint w31 = Random.Shared.NextUInt();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);

            SingleOpcode(opcodes, x1: wn, x31: w31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_Gp_Fixed_XS([CombinatorialMemberData(nameof(_SU_Cvt_F_Gp_Fixed_XS_))] uint opcodes,
                                         [CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 31u)] uint rn,
                                         [CombinatorialMemberData(nameof(_X_))] ulong xn,
                                         [CombinatorialValues(1u, 64u)] uint fBits)
        {
            uint scale = (64u - fBits) & 0x3Fu;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (scale << 10);

            ulong x31 = Random.Shared.NextULong();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcodes, x1: xn, x31: x31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_Gp_Fixed_XD([CombinatorialMemberData(nameof(_SU_Cvt_F_Gp_Fixed_XD_))] uint opcodes,
                                         [CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 31u)] uint rn,
                                         [CombinatorialMemberData(nameof(_X_))] ulong xn,
                                         [CombinatorialValues(1u, 64u)] uint fBits)
        {
            uint scale = (64u - fBits) & 0x3Fu;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (scale << 10);

            ulong x31 = Random.Shared.NextULong();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);

            SingleOpcode(opcodes, x1: xn, x31: x31, v0: v0);

            CompareAgainstUnicorn();
        }
#endif
    }
}
