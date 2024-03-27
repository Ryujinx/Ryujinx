#define SimdShImm

using ARMeilleure.State;
using Xunit;
using System;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdShImm")]
    public sealed class CpuTestSimdShImm : CpuTest
    {
        public CpuTestSimdShImm(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdShImm

        #region "ValueSource (Types)"
        private static ulong[] _1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _1H_()
        {
            return new[] {
                0x0000000000000000ul, 0x0000000000007FFFul,
                0x0000000000008000ul, 0x000000000000FFFFul,
            };
        }

        private static ulong[] _1S_()
        {
            return new[] {
                0x0000000000000000ul, 0x000000007FFFFFFFul,
                0x0000000080000000ul, 0x00000000FFFFFFFFul,
            };
        }

        private static ulong[] _2S_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _4H_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _8B_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static IEnumerable<ulong> _2S_F_W_()
        {
            // int
            yield return 0xCF000001CF000001ul; // -2.1474839E9f  (-2147483904)
            yield return 0xCF000000CF000000ul; // -2.14748365E9f (-2147483648)
            yield return 0xCEFFFFFFCEFFFFFFul; // -2.14748352E9f (-2147483520)
            yield return 0x4F0000014F000001ul; //  2.1474839E9f  (2147483904)
            yield return 0x4F0000004F000000ul; //  2.14748365E9f (2147483648)
            yield return 0x4EFFFFFF4EFFFFFFul; //  2.14748352E9f (2147483520)

            // uint
            yield return 0x4F8000014F800001ul; // 4.2949678E9f  (4294967808)
            yield return 0x4F8000004F800000ul; // 4.2949673E9f  (4294967296)
            yield return 0x4F7FFFFF4F7FFFFFul; // 4.29496704E9f (4294967040)

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
                ulong rnd1 = (uint)BitConverter.SingleToInt32Bits((int)Random.Shared.NextUInt());
                ulong rnd2 = (uint)BitConverter.SingleToInt32Bits(Random.Shared.NextUInt());

                ulong rnd3 = GenNormalS();
                ulong rnd4 = GenSubnormalS();

                yield return (rnd1 << 32) | rnd1;
                yield return (rnd2 << 32) | rnd2;

                yield return (rnd3 << 32) | rnd3;
                yield return (rnd4 << 32) | rnd4;
            }
        }

        private static IEnumerable<ulong> _1D_F_X_()
        {
            // long
            yield return 0xC3E0000000000001ul; // -9.2233720368547780E18d (-9223372036854778000)
            yield return 0xC3E0000000000000ul; // -9.2233720368547760E18d (-9223372036854776000)
            yield return 0xC3DFFFFFFFFFFFFFul; // -9.2233720368547750E18d (-9223372036854775000)
            yield return 0x43E0000000000001ul; //  9.2233720368547780E18d (9223372036854778000)
            yield return 0x43E0000000000000ul; //  9.2233720368547760E18d (9223372036854776000)
            yield return 0x43DFFFFFFFFFFFFFul; //  9.2233720368547750E18d (9223372036854775000)

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
                    (long)Random.Shared.NextULong());
                ulong rnd2 = (ulong)BitConverter.DoubleToInt64Bits(
                    Random.Shared.NextULong());

                ulong rnd3 = GenNormalD();
                ulong rnd4 = GenSubnormalD();

                yield return rnd1;
                yield return rnd2;

                yield return rnd3;
                yield return rnd4;
            }
        }
        #endregion

        #region "ValueSource (Opcodes)"
        private static uint[] _F_Cvt_Z_SU_V_Fixed_2S_4S_()
        {
            return new[]
            {
                0x0F20FC00u, // FCVTZS V0.2S, V0.2S, #32
                0x2F20FC00u, // FCVTZU V0.2S, V0.2S, #32
            };
        }

        private static uint[] _F_Cvt_Z_SU_V_Fixed_2D_()
        {
            return new[]
            {
                0x4F40FC00u, // FCVTZS V0.2D, V0.2D, #64
                0x6F40FC00u, // FCVTZU V0.2D, V0.2D, #64
            };
        }

        private static uint[] _SU_Cvt_F_S_Fixed_S_()
        {
            return new[]
            {
                0x5F20E420u, // SCVTF S0, S1, #32
                0x7F20E420u, // UCVTF S0, S1, #32
            };
        }

        private static uint[] _SU_Cvt_F_S_Fixed_D_()
        {
            return new[]
            {
                0x5F40E420u, // SCVTF D0, D1, #64
                0x7F40E420u, // UCVTF D0, D1, #64
            };
        }

        private static uint[] _SU_Cvt_F_V_Fixed_2S_4S_()
        {
            return new[]
            {
                0x0F20E400u, // SCVTF V0.2S, V0.2S, #32
                0x2F20E400u, // UCVTF V0.2S, V0.2S, #32
            };
        }

        private static uint[] _SU_Cvt_F_V_Fixed_2D_()
        {
            return new[]
            {
                0x4F40E400u, // SCVTF V0.2D, V0.2D, #64
                0x6F40E400u, // UCVTF V0.2D, V0.2D, #64
            };
        }

        private static uint[] _Shl_Sli_S_D_()
        {
            return new[]
            {
                0x5F405400u, // SHL D0, D0, #0
                0x7F405400u, // SLI D0, D0, #0
            };
        }

        private static uint[] _Shl_Sli_V_8B_16B_()
        {
            return new[]
            {
                0x0F085400u, // SHL V0.8B, V0.8B, #0
                0x2F085400u, // SLI V0.8B, V0.8B, #0
            };
        }

        private static uint[] _Shl_Sli_V_4H_8H_()
        {
            return new[]
            {
                0x0F105400u, // SHL V0.4H, V0.4H, #0
                0x2F105400u, // SLI V0.4H, V0.4H, #0
            };
        }

        private static uint[] _Shl_Sli_V_2S_4S_()
        {
            return new[]
            {
                0x0F205400u, // SHL V0.2S, V0.2S, #0
                0x2F205400u, // SLI V0.2S, V0.2S, #0
            };
        }

        private static uint[] _Shl_Sli_V_2D_()
        {
            return new[]
            {
                0x4F405400u, // SHL V0.2D, V0.2D, #0
                0x6F405400u, // SLI V0.2D, V0.2D, #0
            };
        }

        private static uint[] _SU_Shll_V_8B8H_16B8H_()
        {
            return new[]
            {
                0x0F08A400u, // SSHLL V0.8H, V0.8B, #0
                0x2F08A400u, // USHLL V0.8H, V0.8B, #0
            };
        }

        private static uint[] _SU_Shll_V_4H4S_8H4S_()
        {
            return new[]
            {
                0x0F10A400u, // SSHLL V0.4S, V0.4H, #0
                0x2F10A400u, // USHLL V0.4S, V0.4H, #0
            };
        }

        private static uint[] _SU_Shll_V_2S2D_4S2D_()
        {
            return new[]
            {
                0x0F20A400u, // SSHLL V0.2D, V0.2S, #0
                0x2F20A400u, // USHLL V0.2D, V0.2S, #0
            };
        }

        private static uint[] _ShlImm_S_D_()
        {
            return new[]
            {
                0x5F407400u, // SQSHL D0, D0, #0
            };
        }

        private static uint[] _ShlImm_V_8B_16B_()
        {
            return new[]
            {
                0x0F087400u, // SQSHL V0.8B, V0.8B, #0
            };
        }

        private static uint[] _ShlImm_V_4H_8H_()
        {
            return new[]
            {
                0x0F107400u, // SQSHL V0.4H, V0.4H, #0
            };
        }

        private static uint[] _ShlImm_V_2S_4S_()
        {
            return new[]
            {
                0x0F207400u, // SQSHL V0.2S, V0.2S, #0
            };
        }

        private static uint[] _ShlImm_V_2D_()
        {
            return new[]
            {
                0x4F407400u, // SQSHL V0.2D, V0.2D, #0
            };
        }

        private static uint[] _ShrImm_Sri_S_D_()
        {
            return new[]
            {
                0x7F404400u, // SRI   D0, D0, #64
                0x5F402400u, // SRSHR D0, D0, #64
                0x5F403400u, // SRSRA D0, D0, #64
                0x5F400400u, // SSHR  D0, D0, #64
                0x5F401400u, // SSRA  D0, D0, #64
                0x7F402400u, // URSHR D0, D0, #64
                0x7F403400u, // URSRA D0, D0, #64
                0x7F400400u, // USHR  D0, D0, #64
                0x7F401400u, // USRA  D0, D0, #64
            };
        }

        private static uint[] _ShrImm_Sri_V_8B_16B_()
        {
            return new[]
            {
                0x2F084400u, // SRI   V0.8B, V0.8B, #8
                0x0F082400u, // SRSHR V0.8B, V0.8B, #8
                0x0F083400u, // SRSRA V0.8B, V0.8B, #8
                0x0F080400u, // SSHR  V0.8B, V0.8B, #8
                0x0F081400u, // SSRA  V0.8B, V0.8B, #8
                0x2F082400u, // URSHR V0.8B, V0.8B, #8
                0x2F083400u, // URSRA V0.8B, V0.8B, #8
                0x2F080400u, // USHR  V0.8B, V0.8B, #8
                0x2F081400u, // USRA  V0.8B, V0.8B, #8
            };
        }

        private static uint[] _ShrImm_Sri_V_4H_8H_()
        {
            return new[]
            {
                0x2F104400u, // SRI   V0.4H, V0.4H, #16
                0x0F102400u, // SRSHR V0.4H, V0.4H, #16
                0x0F103400u, // SRSRA V0.4H, V0.4H, #16
                0x0F100400u, // SSHR  V0.4H, V0.4H, #16
                0x0F101400u, // SSRA  V0.4H, V0.4H, #16
                0x2F102400u, // URSHR V0.4H, V0.4H, #16
                0x2F103400u, // URSRA V0.4H, V0.4H, #16
                0x2F100400u, // USHR  V0.4H, V0.4H, #16
                0x2F101400u, // USRA  V0.4H, V0.4H, #16
            };
        }

        private static uint[] _ShrImm_Sri_V_2S_4S_()
        {
            return new[]
            {
                0x2F204400u, // SRI   V0.2S, V0.2S, #32
                0x0F202400u, // SRSHR V0.2S, V0.2S, #32
                0x0F203400u, // SRSRA V0.2S, V0.2S, #32
                0x0F200400u, // SSHR  V0.2S, V0.2S, #32
                0x0F201400u, // SSRA  V0.2S, V0.2S, #32
                0x2F202400u, // URSHR V0.2S, V0.2S, #32
                0x2F203400u, // URSRA V0.2S, V0.2S, #32
                0x2F200400u, // USHR  V0.2S, V0.2S, #32
                0x2F201400u, // USRA  V0.2S, V0.2S, #32
            };
        }

        private static uint[] _ShrImm_Sri_V_2D_()
        {
            return new[]
            {
                0x6F404400u, // SRI   V0.2D, V0.2D, #64
                0x4F402400u, // SRSHR V0.2D, V0.2D, #64
                0x4F403400u, // SRSRA V0.2D, V0.2D, #64
                0x4F400400u, // SSHR  V0.2D, V0.2D, #64
                0x4F401400u, // SSRA  V0.2D, V0.2D, #64
                0x6F402400u, // URSHR V0.2D, V0.2D, #64
                0x6F403400u, // URSRA V0.2D, V0.2D, #64
                0x6F400400u, // USHR  V0.2D, V0.2D, #64
                0x6F401400u, // USRA  V0.2D, V0.2D, #64
            };
        }

        private static uint[] _ShrImmNarrow_V_8H8B_8H16B_()
        {
            return new[]
            {
                0x0F088C00u, // RSHRN V0.8B, V0.8H, #8
                0x0F088400u, // SHRN  V0.8B, V0.8H, #8
            };
        }

        private static uint[] _ShrImmNarrow_V_4S4H_4S8H_()
        {
            return new[]
            {
                0x0F108C00u, // RSHRN V0.4H, V0.4S, #16
                0x0F108400u, // SHRN  V0.4H, V0.4S, #16
            };
        }

        private static uint[] _ShrImmNarrow_V_2D2S_2D4S_()
        {
            return new[]
            {
                0x0F208C00u, // RSHRN V0.2S, V0.2D, #32
                0x0F208400u, // SHRN  V0.2S, V0.2D, #32
            };
        }

        private static uint[] _ShrImmSaturatingNarrow_S_HB_()
        {
            return new[]
            {
                0x5F089C00u, // SQRSHRN  B0, H0, #8
                0x7F089C00u, // UQRSHRN  B0, H0, #8
                0x7F088C00u, // SQRSHRUN B0, H0, #8
                0x5F089400u, // SQSHRN   B0, H0, #8
                0x7F089400u, // UQSHRN   B0, H0, #8
                0x7F088400u, // SQSHRUN  B0, H0, #8
            };
        }

        private static uint[] _ShrImmSaturatingNarrow_S_SH_()
        {
            return new[]
            {
                0x5F109C00u, // SQRSHRN  H0, S0, #16
                0x7F109C00u, // UQRSHRN  H0, S0, #16
                0x7F108C00u, // SQRSHRUN H0, S0, #16
                0x5F109400u, // SQSHRN   H0, S0, #16
                0x7F109400u, // UQSHRN   H0, S0, #16
                0x7F108400u, // SQSHRUN  H0, S0, #16
            };
        }

        private static uint[] _ShrImmSaturatingNarrow_S_DS_()
        {
            return new[]
            {
                0x5F209C00u, // SQRSHRN  S0, D0, #32
                0x7F209C00u, // UQRSHRN  S0, D0, #32
                0x7F208C00u, // SQRSHRUN S0, D0, #32
                0x5F209400u, // SQSHRN   S0, D0, #32
                0x7F209400u, // UQSHRN   S0, D0, #32
                0x7F208400u, // SQSHRUN  S0, D0, #32
            };
        }

        private static uint[] _ShrImmSaturatingNarrow_V_8H8B_8H16B_()
        {
            return new[]
            {
                0x0F089C00u, // SQRSHRN  V0.8B, V0.8H, #8
                0x2F089C00u, // UQRSHRN  V0.8B, V0.8H, #8
                0x2F088C00u, // SQRSHRUN V0.8B, V0.8H, #8
                0x0F089400u, // SQSHRN   V0.8B, V0.8H, #8
                0x2F089400u, // UQSHRN   V0.8B, V0.8H, #8
                0x2F088400u, // SQSHRUN  V0.8B, V0.8H, #8
            };
        }

        private static uint[] _ShrImmSaturatingNarrow_V_4S4H_4S8H_()
        {
            return new[]
            {
                0x0F109C00u, // SQRSHRN  V0.4H, V0.4S, #16
                0x2F109C00u, // UQRSHRN  V0.4H, V0.4S, #16
                0x2F108C00u, // SQRSHRUN V0.4H, V0.4S, #16
                0x0F109400u, // SQSHRN   V0.4H, V0.4S, #16
                0x2F109400u, // UQSHRN   V0.4H, V0.4S, #16
                0x2F108400u, // SQSHRUN  V0.4H, V0.4S, #16
            };
        }

        private static uint[] _ShrImmSaturatingNarrow_V_2D2S_2D4S_()
        {
            return new[]
            {
                0x0F209C00u, // SQRSHRN  V0.2S, V0.2D, #32
                0x2F209C00u, // UQRSHRN  V0.2S, V0.2D, #32
                0x2F208C00u, // SQRSHRUN V0.2S, V0.2D, #32
                0x0F209400u, // SQSHRN   V0.2S, V0.2D, #32
                0x2F209400u, // UQSHRN   V0.2S, V0.2D, #32
                0x2F208400u, // SQSHRUN  V0.2S, V0.2D, #32
            };
        }
        #endregion

        private const int RndCnt = 2;

        private static readonly bool _noZeros = false;
        private static readonly bool _noInfs = false;
        private static readonly bool _noNaNs = false;

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_Z_SU_V_Fixed_2S_4S([CombinatorialMemberData(nameof(_F_Cvt_Z_SU_V_Fixed_2S_4S_))] uint opcodes,
                                             [CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialMemberData(nameof(_2S_F_W_))] ulong z,
                                             [CombinatorialMemberData(nameof(_2S_F_W_))] ulong a,
                                             [CombinatorialValues(1u, 32u)] uint fBits,
                                             [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            uint immHb = (64 - fBits) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_Z_SU_V_Fixed_2D([CombinatorialMemberData(nameof(_F_Cvt_Z_SU_V_Fixed_2D_))] uint opcodes,
                                          [CombinatorialValues(0u)] uint rd,
                                          [CombinatorialValues(1u, 0u)] uint rn,
                                          [CombinatorialMemberData(nameof(_1D_F_X_))] ulong z,
                                          [CombinatorialMemberData(nameof(_1D_F_X_))] ulong a,
                                          [CombinatorialValues(1u, 64u)] uint fBits)
        {
            uint immHb = (128 - fBits) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_S_Fixed_S([CombinatorialMemberData(nameof(_SU_Cvt_F_S_Fixed_S_))] uint opcodes,
                                       [CombinatorialMemberData(nameof(_1S_))] ulong a,
                                       [CombinatorialValues(1u, 32u)] uint fBits)
        {
            uint immHb = (64 - fBits) & 0x7F;

            opcodes |= (immHb << 16);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_S_Fixed_D([CombinatorialMemberData(nameof(_SU_Cvt_F_S_Fixed_D_))] uint opcodes,
                                       [CombinatorialMemberData(nameof(_1D_))] ulong a,
                                       [CombinatorialValues(1u, 64u)] uint fBits)
        {
            uint immHb = (128 - fBits) & 0x7F;

            opcodes |= (immHb << 16);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_V_Fixed_2S_4S([CombinatorialMemberData(nameof(_SU_Cvt_F_V_Fixed_2S_4S_))] uint opcodes,
                                           [CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                           [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                           [CombinatorialValues(1u, 32u)] uint fBits,
                                           [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            uint immHb = (64 - fBits) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_V_Fixed_2D([CombinatorialMemberData(nameof(_SU_Cvt_F_V_Fixed_2D_))] uint opcodes,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_1D_))] ulong a,
                                        [CombinatorialValues(1u, 64u)] uint fBits)
        {
            uint immHb = (128 - fBits) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Shl_Sli_S_D([CombinatorialMemberData(nameof(_Shl_Sli_S_D_))] uint opcodes,
                                [CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(1u, 0u)] uint rn,
                                [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                [CombinatorialMemberData(nameof(_1D_))] ulong a,
                                [CombinatorialValues(0u, 63u)] uint shift)
        {
            uint immHb = (64 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Shl_Sli_V_8B_16B([CombinatorialMemberData(nameof(_Shl_Sli_V_8B_16B_))] uint opcodes,
                                     [CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B_))] ulong a,
                                     [CombinatorialValues(0u, 7u)] uint shift,
                                     [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            uint immHb = (8 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Shl_Sli_V_4H_8H([CombinatorialMemberData(nameof(_Shl_Sli_V_4H_8H_))] uint opcodes,
                                    [CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                    [CombinatorialMemberData(nameof(_4H_))] ulong a,
                                    [CombinatorialValues(0u, 15u)] uint shift,
                                    [CombinatorialValues(0b0u, 0b1u)] uint q) // <4H, 8H>
        {
            uint immHb = (16 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Shl_Sli_V_2S_4S([CombinatorialMemberData(nameof(_Shl_Sli_V_2S_4S_))] uint opcodes,
                                    [CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                    [CombinatorialValues(0u, 31u)] uint shift,
                                    [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            uint immHb = (32 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Shl_Sli_V_2D([CombinatorialMemberData(nameof(_Shl_Sli_V_2D_))] uint opcodes,
                                 [CombinatorialValues(0u)] uint rd,
                                 [CombinatorialValues(1u, 0u)] uint rn,
                                 [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                 [CombinatorialMemberData(nameof(_1D_))] ulong a,
                                 [CombinatorialValues(0u, 63u)] uint shift)
        {
            uint immHb = (64 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Shll_V_8B8H_16B8H([CombinatorialMemberData(nameof(_SU_Shll_V_8B8H_16B8H_))] uint opcodes,
                                         [CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                         [CombinatorialMemberData(nameof(_8B_))] ulong a,
                                         [CombinatorialValues(0u, 7u)] uint shift,
                                         [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B8H, 16B8H>
        {
            uint immHb = (8 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(q == 0u ? a : 0ul, q == 1u ? a : 0ul);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Shll_V_4H4S_8H4S([CombinatorialMemberData(nameof(_SU_Shll_V_4H4S_8H4S_))] uint opcodes,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                        [CombinatorialMemberData(nameof(_4H_))] ulong a,
                                        [CombinatorialValues(0u, 15u)] uint shift,
                                        [CombinatorialValues(0b0u, 0b1u)] uint q) // <4H4S, 8H4S>
        {
            uint immHb = (16 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(q == 0u ? a : 0ul, q == 1u ? a : 0ul);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Shll_V_2S2D_4S2D([CombinatorialMemberData(nameof(_SU_Shll_V_2S2D_4S2D_))] uint opcodes,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                        [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                        [CombinatorialValues(0u, 31u)] uint shift,
                                        [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S2D, 4S2D>
        {
            uint immHb = (32 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(q == 0u ? a : 0ul, q == 1u ? a : 0ul);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShlImm_S_D([CombinatorialMemberData(nameof(_ShlImm_S_D_))] uint opcodes,
                               [CombinatorialValues(0u)] uint rd,
                               [CombinatorialValues(1u, 0u)] uint rn,
                               [CombinatorialMemberData(nameof(_1D_))] ulong z,
                               [CombinatorialMemberData(nameof(_1D_))] ulong a,
                               [CombinatorialValues(1u, 64u)] uint shift)
        {
            uint immHb = (64 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShlImm_V_8B_16B([CombinatorialMemberData(nameof(_ShlImm_V_8B_16B_))] uint opcodes,
                                    [CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B_))] ulong a,
                                    [CombinatorialValues(1u, 8u)] uint shift,
                                    [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            uint immHb = (8 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShlImm_V_4H_8H([CombinatorialMemberData(nameof(_ShlImm_V_4H_8H_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                   [CombinatorialMemberData(nameof(_4H_))] ulong a,
                                   [CombinatorialValues(1u, 16u)] uint shift,
                                   [CombinatorialValues(0b0u, 0b1u)] uint q) // <4H, 8H>
        {
            uint immHb = (16 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShlImm_V_2S_4S([CombinatorialMemberData(nameof(_ShlImm_V_2S_4S_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                   [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                   [CombinatorialValues(1u, 32u)] uint shift,
                                   [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            uint immHb = (32 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= (((q | (immHb >> 6)) & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShlImm_V_2D([CombinatorialMemberData(nameof(_ShlImm_V_2D_))] uint opcodes,
                                [CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(1u, 0u)] uint rn,
                                [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                [CombinatorialMemberData(nameof(_1D_))] ulong a,
                                [CombinatorialValues(1u, 64u)] uint shift)
        {
            uint immHb = (64 + shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImm_Sri_S_D([CombinatorialMemberData(nameof(_ShrImm_Sri_S_D_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                   [CombinatorialMemberData(nameof(_1D_))] ulong a,
                                   [CombinatorialValues(1u, 64u)] uint shift)
        {
            uint immHb = (128 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImm_Sri_V_8B_16B([CombinatorialMemberData(nameof(_ShrImm_Sri_V_8B_16B_))] uint opcodes,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B_))] ulong a,
                                        [CombinatorialValues(1u, 8u)] uint shift,
                                        [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            uint immHb = (16 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImm_Sri_V_4H_8H([CombinatorialMemberData(nameof(_ShrImm_Sri_V_4H_8H_))] uint opcodes,
                                       [CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                       [CombinatorialMemberData(nameof(_4H_))] ulong a,
                                       [CombinatorialValues(1u, 16u)] uint shift,
                                       [CombinatorialValues(0b0u, 0b1u)] uint q) // <4H, 8H>
        {
            uint immHb = (32 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImm_Sri_V_2S_4S([CombinatorialMemberData(nameof(_ShrImm_Sri_V_2S_4S_))] uint opcodes,
                                       [CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                       [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                       [CombinatorialValues(1u, 32u)] uint shift,
                                       [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            uint immHb = (64 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImm_Sri_V_2D([CombinatorialMemberData(nameof(_ShrImm_Sri_V_2D_))] uint opcodes,
                                    [CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                    [CombinatorialMemberData(nameof(_1D_))] ulong a,
                                    [CombinatorialValues(1u, 64u)] uint shift)
        {
            uint immHb = (128 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImmNarrow_V_8H8B_8H16B([CombinatorialMemberData(nameof(_ShrImmNarrow_V_8H8B_8H16B_))] uint opcodes,
                                              [CombinatorialValues(0u)] uint rd,
                                              [CombinatorialValues(1u, 0u)] uint rn,
                                              [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                              [CombinatorialMemberData(nameof(_4H_))] ulong a,
                                              [CombinatorialValues(1u, 8u)] uint shift,
                                              [CombinatorialValues(0b0u, 0b1u)] uint q) // <8H8B, 8H16B>
        {
            uint immHb = (16 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImmNarrow_V_4S4H_4S8H([CombinatorialMemberData(nameof(_ShrImmNarrow_V_4S4H_4S8H_))] uint opcodes,
                                             [CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                             [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                             [CombinatorialValues(1u, 16u)] uint shift,
                                             [CombinatorialValues(0b0u, 0b1u)] uint q) // <4S4H, 4S8H>
        {
            uint immHb = (32 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImmNarrow_V_2D2S_2D4S([CombinatorialMemberData(nameof(_ShrImmNarrow_V_2D2S_2D4S_))] uint opcodes,
                                             [CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                             [CombinatorialMemberData(nameof(_1D_))] ulong a,
                                             [CombinatorialValues(1u, 32u)] uint shift,
                                             [CombinatorialValues(0b0u, 0b1u)] uint q) // <2D2S, 2D4S>
        {
            uint immHb = (64 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImmSaturatingNarrow_S_HB([CombinatorialMemberData(nameof(_ShrImmSaturatingNarrow_S_HB_))] uint opcodes,
                                                [CombinatorialValues(0u)] uint rd,
                                                [CombinatorialValues(1u, 0u)] uint rn,
                                                [CombinatorialMemberData(nameof(_1H_))] ulong z,
                                                [CombinatorialMemberData(nameof(_1H_))] ulong a,
                                                [CombinatorialValues(1u, 8u)] uint shift)
        {
            uint immHb = (16 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImmSaturatingNarrow_S_SH([CombinatorialMemberData(nameof(_ShrImmSaturatingNarrow_S_SH_))] uint opcodes,
                                                [CombinatorialValues(0u)] uint rd,
                                                [CombinatorialValues(1u, 0u)] uint rn,
                                                [CombinatorialMemberData(nameof(_1S_))] ulong z,
                                                [CombinatorialMemberData(nameof(_1S_))] ulong a,
                                                [CombinatorialValues(1u, 16u)] uint shift)
        {
            uint immHb = (32 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImmSaturatingNarrow_S_DS([CombinatorialMemberData(nameof(_ShrImmSaturatingNarrow_S_DS_))] uint opcodes,
                                                [CombinatorialValues(0u)] uint rd,
                                                [CombinatorialValues(1u, 0u)] uint rn,
                                                [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                                [CombinatorialMemberData(nameof(_1D_))] ulong a,
                                                [CombinatorialValues(1u, 32u)] uint shift)
        {
            uint immHb = (64 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImmSaturatingNarrow_V_8H8B_8H16B([CombinatorialMemberData(nameof(_ShrImmSaturatingNarrow_V_8H8B_8H16B_))] uint opcodes,
                                                        [CombinatorialValues(0u)] uint rd,
                                                        [CombinatorialValues(1u, 0u)] uint rn,
                                                        [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                                        [CombinatorialMemberData(nameof(_4H_))] ulong a,
                                                        [CombinatorialValues(1u, 8u)] uint shift,
                                                        [CombinatorialValues(0b0u, 0b1u)] uint q) // <8H8B, 8H16B>
        {
            uint immHb = (16 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImmSaturatingNarrow_V_4S4H_4S8H([CombinatorialMemberData(nameof(_ShrImmSaturatingNarrow_V_4S4H_4S8H_))] uint opcodes,
                                                       [CombinatorialValues(0u)] uint rd,
                                                       [CombinatorialValues(1u, 0u)] uint rn,
                                                       [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                                       [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                                       [CombinatorialValues(1u, 16u)] uint shift,
                                                       [CombinatorialValues(0b0u, 0b1u)] uint q) // <4S4H, 4S8H>
        {
            uint immHb = (32 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShrImmSaturatingNarrow_V_2D2S_2D4S([CombinatorialMemberData(nameof(_ShrImmSaturatingNarrow_V_2D2S_2D4S_))] uint opcodes,
                                                       [CombinatorialValues(0u)] uint rd,
                                                       [CombinatorialValues(1u, 0u)] uint rn,
                                                       [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                                       [CombinatorialMemberData(nameof(_1D_))] ulong a,
                                                       [CombinatorialValues(1u, 32u)] uint shift,
                                                       [CombinatorialValues(0b0u, 0b1u)] uint q) // <2D2S, 2D4S>
        {
            uint immHb = (64 - shift) & 0x7F;

            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (immHb << 16);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }
#endif
    }
}
