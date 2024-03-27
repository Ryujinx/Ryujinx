#define SimdReg

using ARMeilleure.State;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdReg")]
    public sealed class CpuTestSimdReg : CpuTest
    {
        public CpuTestSimdReg(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdReg

        #region "ValueSource (Types)"
        private static ulong[] _1B1H1S1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x000000000000007Ful,
                0x0000000000000080ul, 0x00000000000000FFul,
                0x0000000000007FFFul, 0x0000000000008000ul,
                0x000000000000FFFFul, 0x000000007FFFFFFFul,
                0x0000000080000000ul, 0x00000000FFFFFFFFul,
                0x7FFFFFFFFFFFFFFFul, 0x8000000000000000ul,
                0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _1H1S_()
        {
            return new[] {
                0x0000000000000000ul, 0x0000000000007FFFul,
                0x0000000000008000ul, 0x000000000000FFFFul,
                0x000000007FFFFFFFul, 0x0000000080000000ul,
                0x00000000FFFFFFFFul,
            };
        }

        private static ulong[] _4H2S_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _4H2S1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _8B_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _8B1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _8B4H2S_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _8B4H2S1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

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
        private static uint[] _F_Abd_Add_Div_Mul_Mulx_Nmul_Sub_S_S_()
        {
            return new[]
            {
                0x7EA2D420u, // FABD  S0, S1, S2
                0x1E222820u, // FADD  S0, S1, S2
                0x1E221820u, // FDIV  S0, S1, S2
                0x1E220820u, // FMUL  S0, S1, S2
                0x5E22DC20u, // FMULX S0, S1, S2
                0x1E228820u, // FNMUL S0, S1, S2
                0x1E223820u, // FSUB  S0, S1, S2
            };
        }

        private static uint[] _F_Abd_Add_Div_Mul_Mulx_Nmul_Sub_S_D_()
        {
            return new[]
            {
                0x7EE2D420u, // FABD  D0, D1, D2
                0x1E622820u, // FADD  D0, D1, D2
                0x1E621820u, // FDIV  D0, D1, D2
                0x1E620820u, // FMUL  D0, D1, D2
                0x5E62DC20u, // FMULX D0, D1, D2
                0x1E628820u, // FNMUL D0, D1, D2
                0x1E623820u, // FSUB  D0, D1, D2
            };
        }

        private static uint[] _F_Abd_Add_Div_Mul_Mulx_Sub_P_V_2S_4S_()
        {
            return new[]
            {
                0x2EA0D400u, // FABD  V0.2S, V0.2S, V0.2S
                0x0E20D400u, // FADD  V0.2S, V0.2S, V0.2S
                0x2E20D400u, // FADDP V0.2S, V0.2S, V0.2S
                0x2E20FC00u, // FDIV  V0.2S, V0.2S, V0.2S
                0x2E20DC00u, // FMUL  V0.2S, V0.2S, V0.2S
                0x0E20DC00u, // FMULX V0.2S, V0.2S, V0.2S
                0x0EA0D400u, // FSUB  V0.2S, V0.2S, V0.2S
            };
        }

        private static uint[] _F_Abd_Add_Div_Mul_Mulx_Sub_P_V_2D_()
        {
            return new[]
            {
                0x6EE0D400u, // FABD  V0.2D, V0.2D, V0.2D
                0x4E60D400u, // FADD  V0.2D, V0.2D, V0.2D
                0x6E60D400u, // FADDP V0.2D, V0.2D, V0.2D
                0x6E60FC00u, // FDIV  V0.2D, V0.2D, V0.2D
                0x6E60DC00u, // FMUL  V0.2D, V0.2D, V0.2D
                0x4E60DC00u, // FMULX V0.2D, V0.2D, V0.2D
                0x4EE0D400u, // FSUB  V0.2D, V0.2D, V0.2D
            };
        }

        private static uint[] _F_AcCm_EqGeGt_S_S_()
        {
            return new[]
            {
                0x7E22EC20u, // FACGE S0, S1, S2
                0x7EA2EC20u, // FACGT S0, S1, S2
                0x5E22E420u, // FCMEQ S0, S1, S2
                0x7E22E420u, // FCMGE S0, S1, S2
                0x7EA2E420u, // FCMGT S0, S1, S2
            };
        }

        private static uint[] _F_AcCm_EqGeGt_S_D_()
        {
            return new[]
            {
                0x7E62EC20u, // FACGE D0, D1, D2
                0x7EE2EC20u, // FACGT D0, D1, D2
                0x5E62E420u, // FCMEQ D0, D1, D2
                0x7E62E420u, // FCMGE D0, D1, D2
                0x7EE2E420u, // FCMGT D0, D1, D2
            };
        }

        private static uint[] _F_AcCm_EqGeGt_V_2S_4S_()
        {
            return new[]
            {
                0x2E20EC00u, // FACGE V0.2S, V0.2S, V0.2S
                0x2EA0EC00u, // FACGT V0.2S, V0.2S, V0.2S
                0x0E20E400u, // FCMEQ V0.2S, V0.2S, V0.2S
                0x2E20E400u, // FCMGE V0.2S, V0.2S, V0.2S
                0x2EA0E400u, // FCMGT V0.2S, V0.2S, V0.2S
            };
        }

        private static uint[] _F_AcCm_EqGeGt_V_2D_()
        {
            return new[]
            {
                0x6E60EC00u, // FACGE V0.2D, V0.2D, V0.2D
                0x6EE0EC00u, // FACGT V0.2D, V0.2D, V0.2D
                0x4E60E400u, // FCMEQ V0.2D, V0.2D, V0.2D
                0x6E60E400u, // FCMGE V0.2D, V0.2D, V0.2D
                0x6EE0E400u, // FCMGT V0.2D, V0.2D, V0.2D
            };
        }

        private static uint[] _F_Cmp_Cmpe_S_S_()
        {
            return new[]
            {
                0x1E222020u, // FCMP  S1, S2
                0x1E222030u, // FCMPE S1, S2
            };
        }

        private static uint[] _F_Cmp_Cmpe_S_D_()
        {
            return new[]
            {
                0x1E622020u, // FCMP  D1, D2
                0x1E622030u, // FCMPE D1, D2
            };
        }

        private static uint[] _F_Madd_Msub_Nmadd_Nmsub_S_S_()
        {
            return new[]
            {
                0x1F020C20u, // FMADD  S0, S1, S2, S3
                0x1F028C20u, // FMSUB  S0, S1, S2, S3
                0x1F220C20u, // FNMADD S0, S1, S2, S3
                0x1F228C20u, // FNMSUB S0, S1, S2, S3
            };
        }

        private static uint[] _F_Madd_Msub_Nmadd_Nmsub_S_D_()
        {
            return new[]
            {
                0x1F420C20u, // FMADD  D0, D1, D2, D3
                0x1F428C20u, // FMSUB  D0, D1, D2, D3
                0x1F620C20u, // FNMADD D0, D1, D2, D3
                0x1F628C20u, // FNMSUB D0, D1, D2, D3
            };
        }

        private static uint[] _F_Max_Min_Nm_S_S_()
        {
            return new[]
            {
                0x1E224820u, // FMAX   S0, S1, S2
                0x1E226820u, // FMAXNM S0, S1, S2
                0x1E225820u, // FMIN   S0, S1, S2
                0x1E227820u, // FMINNM S0, S1, S2
            };
        }

        private static uint[] _F_Max_Min_Nm_S_D_()
        {
            return new[]
            {
                0x1E624820u, // FMAX   D0, D1, D2
                0x1E626820u, // FMAXNM D0, D1, D2
                0x1E625820u, // FMIN   D0, D1, D2
                0x1E627820u, // FMINNM D0, D1, D2
            };
        }

        private static uint[] _F_Max_Min_Nm_P_V_2S_4S_()
        {
            return new[]
            {
                0x0E20F400u, // FMAX    V0.2S, V0.2S, V0.2S
                0x0E20C400u, // FMAXNM  V0.2S, V0.2S, V0.2S
                0x2E20C400u, // FMAXNMP V0.2S, V0.2S, V0.2S
                0x2E20F400u, // FMAXP   V0.2S, V0.2S, V0.2S
                0x0EA0F400u, // FMIN    V0.2S, V0.2S, V0.2S
                0x0EA0C400u, // FMINNM  V0.2S, V0.2S, V0.2S
                0x2EA0C400u, // FMINNMP V0.2S, V0.2S, V0.2S
                0x2EA0F400u, // FMINP   V0.2S, V0.2S, V0.2S
            };
        }

        private static uint[] _F_Max_Min_Nm_P_V_2D_()
        {
            return new[]
            {
                0x4E60F400u, // FMAX    V0.2D, V0.2D, V0.2D
                0x4E60C400u, // FMAXNM  V0.2D, V0.2D, V0.2D
                0x6E60C400u, // FMAXNMP V0.2D, V0.2D, V0.2D
                0x6E60F400u, // FMAXP   V0.2D, V0.2D, V0.2D
                0x4EE0F400u, // FMIN    V0.2D, V0.2D, V0.2D
                0x4EE0C400u, // FMINNM  V0.2D, V0.2D, V0.2D
                0x6EE0C400u, // FMINNMP V0.2D, V0.2D, V0.2D
                0x6EE0F400u, // FMINP   V0.2D, V0.2D, V0.2D
            };
        }

        private static uint[] _F_Mla_Mls_V_2S_4S_()
        {
            return new[]
            {
                0x0E20CC00u, // FMLA V0.2S, V0.2S, V0.2S
                0x0EA0CC00u, // FMLS V0.2S, V0.2S, V0.2S
            };
        }

        private static uint[] _F_Mla_Mls_V_2D_()
        {
            return new[]
            {
                0x4E60CC00u, // FMLA V0.2D, V0.2D, V0.2D
                0x4EE0CC00u, // FMLS V0.2D, V0.2D, V0.2D
            };
        }

        private static uint[] _F_Recps_Rsqrts_S_S_()
        {
            return new[]
            {
                0x5E22FC20u, // FRECPS  S0, S1, S2
                0x5EA2FC20u, // FRSQRTS S0, S1, S2
            };
        }

        private static uint[] _F_Recps_Rsqrts_S_D_()
        {
            return new[]
            {
                0x5E62FC20u, // FRECPS  D0, D1, D2
                0x5EE2FC20u, // FRSQRTS D0, D1, D2
            };
        }

        private static uint[] _F_Recps_Rsqrts_V_2S_4S_()
        {
            return new[]
            {
                0x0E20FC00u, // FRECPS  V0.2S, V0.2S, V0.2S
                0x0EA0FC00u, // FRSQRTS V0.2S, V0.2S, V0.2S
            };
        }

        private static uint[] _F_Recps_Rsqrts_V_2D_()
        {
            return new[]
            {
                0x4E60FC00u, // FRECPS  V0.2D, V0.2D, V0.2D
                0x4EE0FC00u, // FRSQRTS V0.2D, V0.2D, V0.2D
            };
        }

        private static uint[] _Mla_Mls_Mul_V_8B_4H_2S_()
        {
            return new[]
            {
                0x0E209400u, // MLA V0.8B, V0.8B, V0.8B
                0x2E209400u, // MLS V0.8B, V0.8B, V0.8B
                0x0E209C00u, // MUL V0.8B, V0.8B, V0.8B
            };
        }

        private static uint[] _Mla_Mls_Mul_V_16B_8H_4S_()
        {
            return new[]
            {
                0x4E209400u, // MLA V0.16B, V0.16B, V0.16B
                0x6E209400u, // MLS V0.16B, V0.16B, V0.16B
                0x4E209C00u, // MUL V0.16B, V0.16B, V0.16B
            };
        }

        private static uint[] _Sha1c_Sha1m_Sha1p_Sha1su0_V_()
        {
            return new[]
            {
                0x5E000000u, // SHA1C   Q0,    S0,    V0.4S
                0x5E002000u, // SHA1M   Q0,    S0,    V0.4S
                0x5E001000u, // SHA1P   Q0,    S0,    V0.4S
                0x5E003000u, // SHA1SU0 V0.4S, V0.4S, V0.4S
            };
        }

        private static uint[] _Sha256h_Sha256h2_Sha256su1_V_()
        {
            return new[]
            {
                0x5E004000u, // SHA256H   Q0,    Q0,    V0.4S
                0x5E005000u, // SHA256H2  Q0,    Q0,    V0.4S
                0x5E006000u, // SHA256SU1 V0.4S, V0.4S, V0.4S
            };
        }

        private static uint[] _SU_Max_Min_P_V_()
        {
            return new[]
            {
                0x0E206400u, // SMAX  V0.8B, V0.8B, V0.8B
                0x0E20A400u, // SMAXP V0.8B, V0.8B, V0.8B
                0x0E206C00u, // SMIN  V0.8B, V0.8B, V0.8B
                0x0E20AC00u, // SMINP V0.8B, V0.8B, V0.8B
                0x2E206400u, // UMAX  V0.8B, V0.8B, V0.8B
                0x2E20A400u, // UMAXP V0.8B, V0.8B, V0.8B
                0x2E206C00u, // UMIN  V0.8B, V0.8B, V0.8B
                0x2E20AC00u, // UMINP V0.8B, V0.8B, V0.8B
            };
        }

        private static uint[] _SU_Mlal_Mlsl_Mull_V_8B8H_4H4S_2S2D_()
        {
            return new[]
            {
                0x0E208000u, // SMLAL V0.8H, V0.8B, V0.8B
                0x0E20A000u, // SMLSL V0.8H, V0.8B, V0.8B
                0x0E20C000u, // SMULL V0.8H, V0.8B, V0.8B
                0x2E208000u, // UMLAL V0.8H, V0.8B, V0.8B
                0x2E20A000u, // UMLSL V0.8H, V0.8B, V0.8B
                0x2E20C000u, // UMULL V0.8H, V0.8B, V0.8B
            };
        }

        private static uint[] _SU_Mlal_Mlsl_Mull_V_16B8H_8H4S_4S2D_()
        {
            return new[]
            {
                0x4E208000u, // SMLAL2 V0.8H, V0.16B, V0.16B
                0x4E20A000u, // SMLSL2 V0.8H, V0.16B, V0.16B
                0x4E20C000u, // SMULL2 V0.8H, V0.16B, V0.16B
                0x6E208000u, // UMLAL2 V0.8H, V0.16B, V0.16B
                0x6E20A000u, // UMLSL2 V0.8H, V0.16B, V0.16B
                0x6E20C000u, // UMULL2 V0.8H, V0.16B, V0.16B
            };
        }

        private static uint[] _ShlReg_S_D_()
        {
            return new[]
            {
                0x5EE04400u, // SSHL D0, D0, D0
                0x7EE04400u, // USHL D0, D0, D0
            };
        }

        private static uint[] _ShlReg_V_8B_4H_2S_()
        {
            return new[]
            {
                0x0E205C00u, // SQRSHL V0.8B, V0.8B, V0.8B
                0x0E204C00u, // SQSHL  V0.8B, V0.8B, V0.8B
                0x0E205400u, // SRSHL  V0.8B, V0.8B, V0.8B
                0x0E204400u, // SSHL   V0.8B, V0.8B, V0.8B
                0x2E205C00u, // UQRSHL V0.8B, V0.8B, V0.8B
                0x2E204C00u, // UQSHL  V0.8B, V0.8B, V0.8B
                0x2E205400u, // URSHL  V0.8B, V0.8B, V0.8B
                0x2E204400u, // USHL   V0.8B, V0.8B, V0.8B
            };
        }

        private static uint[] _ShlReg_V_16B_8H_4S_2D_()
        {
            return new[]
            {
                0x4E205C00u, // SQRSHL V0.16B, V0.16B, V0.16B
                0x4E204C00u, // SQSHL  V0.16B, V0.16B, V0.16B
                0x4E205400u, // SRSHL  V0.16B, V0.16B, V0.16B
                0x4E204400u, // SSHL   V0.16B, V0.16B, V0.16B
                0x6E205C00u, // UQRSHL V0.16B, V0.16B, V0.16B
                0x6E204C00u, // UQSHL  V0.16B, V0.16B, V0.16B
                0x6E205400u, // URSHL  V0.16B, V0.16B, V0.16B
                0x6E204400u, // USHL   V0.16B, V0.16B, V0.16B
            };
        }
        #endregion

        private const int RndCnt = 2;

        private static readonly bool _noZeros = false;
        private static readonly bool _noInfs = false;
        private static readonly bool _noNaNs = false;

        [SkippableTheory(DisplayName = "ADD <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Add_S_D([CombinatorialValues(0u)] uint rd,
                            [CombinatorialValues(1u, 0u)] uint rn,
                            [CombinatorialValues(2u, 0u)] uint rm,
                            [CombinatorialMemberData(nameof(_1D_))] ulong z,
                            [CombinatorialMemberData(nameof(_1D_))] ulong a,
                            [CombinatorialMemberData(nameof(_1D_))] ulong b)
        {
            uint opcode = 0x5EE08400; // ADD D0, D0, D0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Add_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialValues(2u, 0u)] uint rm,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                   [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E208400; // ADD V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Add_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialValues(2u, 0u)] uint rm,
                                       [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                       [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                       [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                       [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E208400; // ADD V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDHN{2} <Vd>.<Tb>, <Vn>.<Ta>, <Vm>.<Ta>")]
        [PairwiseData]
        public void Addhn_V_8H8B_4S4H_2D2S([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                           [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                           [CombinatorialMemberData(nameof(_4H2S1D_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint opcode = 0x0E204000; // ADDHN V0.8B, V0.8H, V0.8H
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDHN{2} <Vd>.<Tb>, <Vn>.<Ta>, <Vm>.<Ta>")]
        [PairwiseData]
        public void Addhn_V_8H16B_4S8H_2D4S([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint opcode = 0x4E204000; // ADDHN2 V0.16B, V0.8H, V0.8H
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDP <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Addp_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E20BC00; // ADDP V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDP <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Addp_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E20BC00; // ADDP V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "AND <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void And_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a,
                             [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x0E201C00; // AND V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "AND <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void And_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x4E201C00; // AND V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BIC <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Bic_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a,
                             [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x0E601C00; // BIC V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BIC <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Bic_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x4E601C00; // BIC V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BIF <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Bif_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a,
                             [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x2EE01C00; // BIF V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BIF <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Bif_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x6EE01C00; // BIF V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BIT <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Bit_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a,
                             [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x2EA01C00; // BIT V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BIT <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Bit_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x6EA01C00; // BIT V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BSL <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Bsl_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a,
                             [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x2E601C00; // BSL V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "BSL <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Bsl_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x6E601C00; // BSL V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMEQ <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Cmeq_S_D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a,
                             [CombinatorialMemberData(nameof(_1D_))] ulong b)
        {
            uint opcode = 0x7EE08C00; // CMEQ D0, D0, D0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMEQ <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmeq_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E208C00; // CMEQ V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMEQ <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmeq_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E208C00; // CMEQ V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGE <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Cmge_S_D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a,
                             [CombinatorialMemberData(nameof(_1D_))] ulong b)
        {
            uint opcode = 0x5EE03C00; // CMGE D0, D0, D0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGE <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmge_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E203C00; // CMGE V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGE <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmge_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E203C00; // CMGE V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGT <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Cmgt_S_D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a,
                             [CombinatorialMemberData(nameof(_1D_))] ulong b)
        {
            uint opcode = 0x5EE03400; // CMGT D0, D0, D0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGT <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmgt_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E203400; // CMGT V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGT <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmgt_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E203400; // CMGT V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMHI <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Cmhi_S_D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a,
                             [CombinatorialMemberData(nameof(_1D_))] ulong b)
        {
            uint opcode = 0x7EE03400; // CMHI D0, D0, D0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMHI <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmhi_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E203400; // CMHI V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMHI <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmhi_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E203400; // CMHI V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMHS <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Cmhs_S_D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a,
                             [CombinatorialMemberData(nameof(_1D_))] ulong b)
        {
            uint opcode = 0x7EE03C00; // CMHS D0, D0, D0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMHS <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmhs_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E203C00; // CMHS V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMHS <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmhs_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E203C00; // CMHS V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMTST <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Cmtst_S_D([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialMemberData(nameof(_1D_))] ulong z,
                              [CombinatorialMemberData(nameof(_1D_))] ulong a,
                              [CombinatorialMemberData(nameof(_1D_))] ulong b)
        {
            uint opcode = 0x5EE08C00; // CMTST D0, D0, D0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMTST <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmtst_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E208C00; // CMTST V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMTST <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Cmtst_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialValues(2u, 0u)] uint rm,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                         [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E208C00; // CMTST V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EOR <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Eor_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a,
                             [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x2E201C00; // EOR V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EOR <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Eor_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x6E201C00; // EOR V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Abd_Add_Div_Mul_Mulx_Nmul_Sub_S_S([CombinatorialMemberData(nameof(_F_Abd_Add_Div_Mul_Mulx_Nmul_Sub_S_S_))] uint opcodes,
                                                        [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                                        [CombinatorialMemberData(nameof(_1S_F_))] ulong b)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Dzc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Abd_Add_Div_Mul_Mulx_Nmul_Sub_S_D([CombinatorialMemberData(nameof(_F_Abd_Add_Div_Mul_Mulx_Nmul_Sub_S_D_))] uint opcodes,
                                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong b)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Dzc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Abd_Add_Div_Mul_Mulx_Sub_P_V_2S_4S([CombinatorialMemberData(nameof(_F_Abd_Add_Div_Mul_Mulx_Sub_P_V_2S_4S_))] uint opcodes,
                                                         [CombinatorialValues(0u)] uint rd,
                                                         [CombinatorialValues(1u, 0u)] uint rn,
                                                         [CombinatorialValues(2u, 0u)] uint rm,
                                                         [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                                         [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                                         [CombinatorialMemberData(nameof(_2S_F_))] ulong b,
                                                         [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);
            V128 v2 = MakeVectorE0E1(b, b * q);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Dzc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Abd_Add_Div_Mul_Mulx_Sub_P_V_2D([CombinatorialMemberData(nameof(_F_Abd_Add_Div_Mul_Mulx_Sub_P_V_2D_))] uint opcodes,
                                                      [CombinatorialValues(0u)] uint rd,
                                                      [CombinatorialValues(1u, 0u)] uint rn,
                                                      [CombinatorialValues(2u, 0u)] uint rm,
                                                      [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                                      [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                                      [CombinatorialMemberData(nameof(_1D_F_))] ulong b)
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Dzc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_AcCm_EqGeGt_S_S([CombinatorialMemberData(nameof(_F_AcCm_EqGeGt_S_S_))] uint opcodes,
                                      [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                      [CombinatorialMemberData(nameof(_1S_F_))] ulong b)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_AcCm_EqGeGt_S_D([CombinatorialMemberData(nameof(_F_AcCm_EqGeGt_S_D_))] uint opcodes,
                                      [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                      [CombinatorialMemberData(nameof(_1D_F_))] ulong b)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_AcCm_EqGeGt_V_2S_4S([CombinatorialMemberData(nameof(_F_AcCm_EqGeGt_V_2S_4S_))] uint opcodes,
                                          [CombinatorialValues(0u)] uint rd,
                                          [CombinatorialValues(1u, 0u)] uint rn,
                                          [CombinatorialValues(2u, 0u)] uint rm,
                                          [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                          [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                          [CombinatorialMemberData(nameof(_2S_F_))] ulong b,
                                          [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);
            V128 v2 = MakeVectorE0E1(b, b * q);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_AcCm_EqGeGt_V_2D([CombinatorialMemberData(nameof(_F_AcCm_EqGeGt_V_2D_))] uint opcodes,
                                       [CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialValues(2u, 0u)] uint rm,
                                       [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                       [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                       [CombinatorialMemberData(nameof(_1D_F_))] ulong b)
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cmp_Cmpe_S_S([CombinatorialMemberData(nameof(_F_Cmp_Cmpe_S_S_))] uint opcodes,
                                   [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                   [CombinatorialMemberData(nameof(_1S_F_))] ulong b)
        {
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
        public void F_Cmp_Cmpe_S_D([CombinatorialMemberData(nameof(_F_Cmp_Cmpe_S_D_))] uint opcodes,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong b)
        {
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            bool v = Random.Shared.NextBool();
            bool c = Random.Shared.NextBool();
            bool z = Random.Shared.NextBool();
            bool n = Random.Shared.NextBool();

            SingleOpcode(opcodes, v1: v1, v2: v2, overflow: v, carry: c, zero: z, negative: n);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Madd_Msub_Nmadd_Nmsub_S_S([CombinatorialMemberData(nameof(_F_Madd_Msub_Nmadd_Nmsub_S_S_))] uint opcodes,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong b,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong c)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);
            V128 v3 = MakeVectorE0(c);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, v3: v3, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsS);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Madd_Msub_Nmadd_Nmsub_S_D([CombinatorialMemberData(nameof(_F_Madd_Msub_Nmadd_Nmsub_S_D_))] uint opcodes,
                                                [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                                [CombinatorialMemberData(nameof(_1D_F_))] ulong b,
                                                [CombinatorialMemberData(nameof(_1D_F_))] ulong c)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);
            V128 v3 = MakeVectorE0(c);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, v3: v3, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsD);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Max_Min_Nm_S_S([CombinatorialMemberData(nameof(_F_Max_Min_Nm_S_S_))] uint opcodes,
                                     [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                     [CombinatorialMemberData(nameof(_1S_F_))] ulong b)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Max_Min_Nm_S_D([CombinatorialMemberData(nameof(_F_Max_Min_Nm_S_D_))] uint opcodes,
                                     [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                     [CombinatorialMemberData(nameof(_1D_F_))] ulong b)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Max_Min_Nm_P_V_2S_4S([CombinatorialMemberData(nameof(_F_Max_Min_Nm_P_V_2S_4S_))] uint opcodes,
                                           [CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                           [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                           [CombinatorialMemberData(nameof(_2S_F_))] ulong b,
                                           [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);
            V128 v2 = MakeVectorE0E1(b, b * q);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Max_Min_Nm_P_V_2D([CombinatorialMemberData(nameof(_F_Max_Min_Nm_P_V_2D_))] uint opcodes,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong b)
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Mla_Mls_V_2S_4S([CombinatorialMemberData(nameof(_F_Mla_Mls_V_2S_4S_))] uint opcodes,
                                      [CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialValues(2u, 0u)] uint rm,
                                      [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                      [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                      [CombinatorialMemberData(nameof(_2S_F_))] ulong b,
                                      [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);
            V128 v2 = MakeVectorE0E1(b, b * q);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsS);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Mla_Mls_V_2D([CombinatorialMemberData(nameof(_F_Mla_Mls_V_2D_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialValues(2u, 0u)] uint rm,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong b)
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsD);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Recps_Rsqrts_S_S([CombinatorialMemberData(nameof(_F_Recps_Rsqrts_S_S_))] uint opcodes,
                                       [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                       [CombinatorialMemberData(nameof(_1S_F_))] ulong b)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsS);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Recps_Rsqrts_S_D([CombinatorialMemberData(nameof(_F_Recps_Rsqrts_S_D_))] uint opcodes,
                                       [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                       [CombinatorialMemberData(nameof(_1D_F_))] ulong b)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsD);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Recps_Rsqrts_V_2S_4S([CombinatorialMemberData(nameof(_F_Recps_Rsqrts_V_2S_4S_))] uint opcodes,
                                           [CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                           [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                           [CombinatorialMemberData(nameof(_2S_F_))] ulong b,
                                           [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);
            V128 v2 = MakeVectorE0E1(b, b * q);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsS);
        }

        // Fused.
        [SkippableTheory]
        [PairwiseData]
        public void F_Recps_Rsqrts_V_2D([CombinatorialMemberData(nameof(_F_Recps_Rsqrts_V_2D_))] uint opcodes,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong b)
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, fpcr: fpcr);

            CompareAgainstUnicorn(Fpsr.Ioc | Fpsr.Idc, FpSkips.IfUnderflow, FpTolerances.UpToOneUlpsD);
        }

        [SkippableTheory]
        [PairwiseData]
        public void Mla_Mls_Mul_V_8B_4H_2S([CombinatorialMemberData(nameof(_Mla_Mls_Mul_V_8B_4H_2S_))] uint opcodes,
                                           [CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Mla_Mls_Mul_V_16B_8H_4S([CombinatorialMemberData(nameof(_Mla_Mls_Mul_V_16B_8H_4S_))] uint opcodes,
                                            [CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORN <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Orn_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a,
                             [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x0EE01C00; // ORN V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORN <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Orn_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x4EE01C00; // ORN V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORR <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Orr_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a,
                             [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x0EA01C00; // ORR V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ORR <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Orr_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialMemberData(nameof(_8B_))] ulong b)
        {
            uint opcode = 0x4EA01C00; // ORR V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "PMULL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Pmull_V([CombinatorialValues(0u)] uint rd,
                            [CombinatorialValues(1u, 0u)] uint rn,
                            [CombinatorialValues(2u, 0u)] uint rm,
                            [CombinatorialMemberData(nameof(_8B1D_))] ulong z0,
                            [CombinatorialMemberData(nameof(_8B1D_))] ulong z1,
                            [CombinatorialMemberData(nameof(_8B1D_))] ulong a0,
                            [CombinatorialMemberData(nameof(_8B1D_))] ulong a1,
                            [CombinatorialMemberData(nameof(_8B1D_))] ulong b0,
                            [CombinatorialMemberData(nameof(_8B1D_))] ulong b1,
                            [CombinatorialValues(0b00u, 0b11u)] uint size, // Q0: <8B,  1D> => <8H, 1Q>
                            [CombinatorialValues(0b0u, 0b1u)] uint q) // Q1: <16B, 2D> => <8H, 1Q>
        {
            uint opcode = 0x0E20E000; // PMULL V0.8H, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);
            V128 v2 = MakeVectorE0E1(b0, b1);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "RADDHN{2} <Vd>.<Tb>, <Vn>.<Ta>, <Vm>.<Ta>")]
        [PairwiseData]
        public void Raddhn_V_8H8B_4S4H_2D2S([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint opcode = 0x2E204000; // RADDHN V0.8B, V0.8H, V0.8H
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "RADDHN{2} <Vd>.<Tb>, <Vn>.<Ta>, <Vm>.<Ta>")]
        [PairwiseData]
        public void Raddhn_V_8H16B_4S8H_2D4S([CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialValues(2u, 0u)] uint rm,
                                             [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                             [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                             [CombinatorialMemberData(nameof(_4H2S1D_))] ulong b,
                                             [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint opcode = 0x6E204000; // RADDHN2 V0.16B, V0.8H, V0.8H
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "RSUBHN{2} <Vd>.<Tb>, <Vn>.<Ta>, <Vm>.<Ta>")]
        [PairwiseData]
        public void Rsubhn_V_8H8B_4S4H_2D2S([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint opcode = 0x2E206000; // RSUBHN V0.8B, V0.8H, V0.8H
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "RSUBHN{2} <Vd>.<Tb>, <Vn>.<Ta>, <Vm>.<Ta>")]
        [PairwiseData]
        public void Rsubhn_V_8H16B_4S8H_2D4S([CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialValues(2u, 0u)] uint rm,
                                             [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                             [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                             [CombinatorialMemberData(nameof(_4H2S1D_))] ulong b,
                                             [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint opcode = 0x6E206000; // RSUBHN2 V0.16B, V0.8H, V0.8H
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SABA <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Saba_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E207C00; // SABA V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SABA <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Saba_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x4E207C00; // SABA V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SABAL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Sabal_V_8B8H_4H4S_2S2D([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H, 4H4S, 2S2D>
        {
            uint opcode = 0x0E205000; // SABAL V0.8H, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SABAL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Sabal_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x4E205000; // SABAL2 V0.8H, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE1(a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SABD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sabd_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E207400; // SABD V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SABD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sabd_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x4E207400; // SABD V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SABDL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Sabdl_V_8B8H_4H4S_2S2D([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H, 4H4S, 2S2D>
        {
            uint opcode = 0x0E207000; // SABDL V0.8H, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SABDL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Sabdl_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x4E207000; // SABDL2 V0.8H, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE1(a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SADDL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Saddl_V_8B8H_4H4S_2S2D([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H, 4H4S, 2S2D>
        {
            uint opcode = 0x0E200000; // SADDL V0.8H, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SADDL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Saddl_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x4E200000; // SADDL2 V0.8H, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE1(a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SADDW{2} <Vd>.<Ta>, <Vn>.<Ta>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Saddw_V_8B8H8H_4H4S4S_2S2D2D([CombinatorialValues(0u)] uint rd,
                                                 [CombinatorialValues(1u, 0u)] uint rn,
                                                 [CombinatorialValues(2u, 0u)] uint rm,
                                                 [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                                 [CombinatorialMemberData(nameof(_4H2S1D_))][CombinatorialRandomData(Count = RndCnt)] ulong a,
                                                 [CombinatorialMemberData(nameof(_8B4H2S_))][CombinatorialRandomData(Count = RndCnt)] ulong b,
                                                 [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H8H, 4H4S4S, 2S2D2D>
        {
            uint opcode = 0x0E201000; // SADDW V0.8H, V0.8H, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SADDW{2} <Vd>.<Ta>, <Vn>.<Ta>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Saddw_V_16B8H8H_8H4S4S_4S2D2D([CombinatorialValues(0u)] uint rd,
                                                  [CombinatorialValues(1u, 0u)] uint rn,
                                                  [CombinatorialValues(2u, 0u)] uint rm,
                                                  [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                                  [CombinatorialMemberData(nameof(_4H2S1D_))][CombinatorialRandomData(Count = RndCnt)] ulong a,
                                                  [CombinatorialMemberData(nameof(_8B4H2S_))][CombinatorialRandomData(Count = RndCnt)] ulong b,
                                                  [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H8H, 8H4S4S, 4S2D2D>
        {
            uint opcode = 0x4E201000; // SADDW2 V0.8H, V0.8H, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        public static readonly ulong[] RandomZ0 =
        {
            Random.Shared.NextULong(),
        };
        public static readonly ulong[] RandomZ1 =
        {
            Random.Shared.NextULong(),
        };
        public static readonly ulong[] RandomA0 =
        {
            Random.Shared.NextULong(),
        };
        public static readonly ulong[] RandomA1 =
        {
            Random.Shared.NextULong(),
        };
        public static readonly ulong[] RandomB0 =
        {
            Random.Shared.NextULong(),
        };
        public static readonly ulong[] RandomB1 =
        {
            Random.Shared.NextULong(),
        };

        [SkippableTheory]
        [PairwiseData]
        public void Sha1c_Sha1m_Sha1p_Sha1su0_V([CombinatorialMemberData(nameof(_Sha1c_Sha1m_Sha1p_Sha1su0_V_))] uint opcodes,
                                                [CombinatorialValues(0u)] uint rd,
                                                [CombinatorialValues(1u, 0u)] uint rn,
                                                [CombinatorialValues(2u, 0u)] uint rm,
                                                [CombinatorialMemberData(nameof(RandomZ0))] ulong z0, [CombinatorialMemberData(nameof(RandomZ1))] ulong z1,
                                                [CombinatorialMemberData(nameof(RandomA0))] ulong a0, [CombinatorialMemberData(nameof(RandomA1))] ulong a1,
                                                [CombinatorialMemberData(nameof(RandomB0))] ulong b0, [CombinatorialMemberData(nameof(RandomB1))] ulong b1)
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);
            V128 v2 = MakeVectorE0E1(b0, b1);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Sha256h_Sha256h2_Sha256su1_V([CombinatorialMemberData(nameof(_Sha256h_Sha256h2_Sha256su1_V_))] uint opcodes,
                                                 [CombinatorialValues(0u)] uint rd,
                                                 [CombinatorialValues(1u, 0u)] uint rn,
                                                 [CombinatorialValues(2u, 0u)] uint rm,
                                                 [CombinatorialMemberData(nameof(RandomZ0))] ulong z0, [CombinatorialMemberData(nameof(RandomZ1))] ulong z1,
                                                 [CombinatorialMemberData(nameof(RandomA0))] ulong a0, [CombinatorialMemberData(nameof(RandomA1))] ulong a1,
                                                 [CombinatorialMemberData(nameof(RandomB0))] ulong b0, [CombinatorialMemberData(nameof(RandomB1))] ulong b1)
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);
            V128 v2 = MakeVectorE0E1(b0, b1);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SHADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Shadd_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E200400; // SHADD V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SHADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Shadd_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialValues(2u, 0u)] uint rm,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x4E200400; // SHADD V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SHSUB <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Shsub_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E202400; // SHSUB V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SHSUB <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Shsub_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialValues(2u, 0u)] uint rm,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x4E202400; // SHSUB V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Max_Min_P_V([CombinatorialMemberData(nameof(_SU_Max_Min_P_V_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialValues(2u, 0u)] uint rm,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                   [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size, // Q0: <8B,  4H, 2S>
                                   [CombinatorialValues(0b0u, 0b1u)] uint q)             // Q1: <16B, 8H, 4S>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);
            V128 v2 = MakeVectorE0E1(b, b * q);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Mlal_Mlsl_Mull_V_8B8H_4H4S_2S2D([CombinatorialMemberData(nameof(_SU_Mlal_Mlsl_Mull_V_8B8H_4H4S_2S2D_))] uint opcodes,
                                                       [CombinatorialValues(0u)] uint rd,
                                                       [CombinatorialValues(1u, 0u)] uint rn,
                                                       [CombinatorialValues(2u, 0u)] uint rm,
                                                       [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                                       [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                                       [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                                       [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H, 4H4S, 2S2D>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Mlal_Mlsl_Mull_V_16B8H_8H4S_4S2D([CombinatorialMemberData(nameof(_SU_Mlal_Mlsl_Mull_V_16B8H_8H4S_4S2D_))] uint opcodes,
                                                        [CombinatorialValues(0u)] uint rd,
                                                        [CombinatorialValues(1u, 0u)] uint rn,
                                                        [CombinatorialValues(2u, 0u)] uint rm,
                                                        [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                                        [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                                        [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                                        [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE1(a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SQADD <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Sqadd_S_B_H_S_D([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong z,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong a,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <b, H, S, D>
        {
            uint opcode = 0x5E200C00; // SQADD B0, B0, B0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sqadd_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E200C00; // SQADD V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sqadd_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialValues(2u, 0u)] uint rm,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                         [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E200C00; // SQADD V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQDMULH <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Sqdmulh_S_H_S([CombinatorialValues(0u)] uint rd,
                                  [CombinatorialValues(1u, 0u)] uint rn,
                                  [CombinatorialValues(2u, 0u)] uint rm,
                                  [CombinatorialMemberData(nameof(_1H1S_))] ulong z,
                                  [CombinatorialMemberData(nameof(_1H1S_))] ulong a,
                                  [CombinatorialMemberData(nameof(_1H1S_))] ulong b,
                                  [CombinatorialValues(0b01u, 0b10u)] uint size) // <H, S>
        {
            uint opcode = 0x5E20B400; // SQDMULH B0, B0, B0 (RESERVED)
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQDMULH <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sqdmulh_V_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_4H2S_))] ulong b,
                                    [CombinatorialValues(0b01u, 0b10u)] uint size) // <4H, 2S>
        {
            uint opcode = 0x0E20B400; // SQDMULH V0.8B, V0.8B, V0.8B (RESERVED)
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQDMULH <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sqdmulh_V_8H_4S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_4H2S_))] ulong b,
                                    [CombinatorialValues(0b01u, 0b10u)] uint size) // <8H, 4S>
        {
            uint opcode = 0x4E20B400; // SQDMULH V0.16B, V0.16B, V0.16B (RESERVED)
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQRDMULH <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Sqrdmulh_S_H_S([CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialValues(2u, 0u)] uint rm,
                                   [CombinatorialMemberData(nameof(_1H1S_))] ulong z,
                                   [CombinatorialMemberData(nameof(_1H1S_))] ulong a,
                                   [CombinatorialMemberData(nameof(_1H1S_))] ulong b,
                                   [CombinatorialValues(0b01u, 0b10u)] uint size) // <H, S>
        {
            uint opcode = 0x7E20B400; // SQRDMULH B0, B0, B0 (RESERVED)
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQRDMULH <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sqrdmulh_V_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_4H2S_))] ulong b,
                                     [CombinatorialValues(0b01u, 0b10u)] uint size) // <4H, 2S>
        {
            uint opcode = 0x2E20B400; // SQRDMULH V0.8B, V0.8B, V0.8B (RESERVED)
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQRDMULH <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sqrdmulh_V_8H_4S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_4H2S_))] ulong b,
                                     [CombinatorialValues(0b01u, 0b10u)] uint size) // <8H, 4S>
        {
            uint opcode = 0x6E20B400; // SQRDMULH V0.16B, V0.16B, V0.16B (RESERVED)
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQSUB <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Sqsub_S_B_H_S_D([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong z,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong a,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <b, H, S, D>
        {
            uint opcode = 0x5E202C00; // SQSUB B0, B0, B0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQSUB <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sqsub_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E202C00; // SQSUB V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQSUB <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sqsub_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialValues(2u, 0u)] uint rm,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                         [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E202C00; // SQSUB V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SRHADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Srhadd_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialValues(2u, 0u)] uint rm,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E201400; // SRHADD V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SRHADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Srhadd_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialValues(2u, 0u)] uint rm,
                                       [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                       [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                       [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                       [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x4E201400; // SRHADD V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShlReg_S_D([CombinatorialMemberData(nameof(_ShlReg_S_D_))] uint opcodes,
                               [CombinatorialValues(0u)] uint rd,
                               [CombinatorialValues(1u, 0u)] uint rn,
                               [CombinatorialValues(2u, 0u)] uint rm,
                               [CombinatorialMemberData(nameof(_1D_))] ulong z,
                               [CombinatorialMemberData(nameof(_1D_))] ulong a,
                               [CombinatorialMemberData(nameof(_1D_))] ulong b)
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShlReg_V_8B_4H_2S([CombinatorialMemberData(nameof(_ShlReg_V_8B_4H_2S_))] uint opcodes,
                                      [CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialValues(2u, 0u)] uint rm,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void ShlReg_V_16B_8H_4S_2D([CombinatorialMemberData(nameof(_ShlReg_V_16B_8H_4S_2D_))] uint opcodes,
                                          [CombinatorialValues(0u)] uint rd,
                                          [CombinatorialValues(1u, 0u)] uint rn,
                                          [CombinatorialValues(2u, 0u)] uint rm,
                                          [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                          [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                          [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                          [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SSUBL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Ssubl_V_8B8H_4H4S_2S2D([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H, 4H4S, 2S2D>
        {
            uint opcode = 0x0E202000; // SSUBL V0.8H, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SSUBL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Ssubl_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x4E202000; // SSUBL2 V0.8H, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE1(a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SSUBW{2} <Vd>.<Ta>, <Vn>.<Ta>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Ssubw_V_8B8H8H_4H4S4S_2S2D2D([CombinatorialValues(0u)] uint rd,
                                                 [CombinatorialValues(1u, 0u)] uint rn,
                                                 [CombinatorialValues(2u, 0u)] uint rm,
                                                 [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                                 [CombinatorialMemberData(nameof(_4H2S1D_))][CombinatorialRandomData(Count = RndCnt)] ulong a,
                                                 [CombinatorialMemberData(nameof(_8B4H2S_))][CombinatorialRandomData(Count = RndCnt)] ulong b,
                                                 [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H8H, 4H4S4S, 2S2D2D>
        {
            uint opcode = 0x0E203000; // SSUBW V0.8H, V0.8H, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SSUBW{2} <Vd>.<Ta>, <Vn>.<Ta>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Ssubw_V_16B8H8H_8H4S4S_4S2D2D([CombinatorialValues(0u)] uint rd,
                                                  [CombinatorialValues(1u, 0u)] uint rn,
                                                  [CombinatorialValues(2u, 0u)] uint rm,
                                                  [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                                  [CombinatorialMemberData(nameof(_4H2S1D_))][CombinatorialRandomData(Count = RndCnt)] ulong a,
                                                  [CombinatorialMemberData(nameof(_8B4H2S_))][CombinatorialRandomData(Count = RndCnt)] ulong b,
                                                  [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H8H, 8H4S4S, 4S2D2D>
        {
            uint opcode = 0x4E203000; // SSUBW2 V0.8H, V0.8H, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Sub_S_D([CombinatorialValues(0u)] uint rd,
                            [CombinatorialValues(1u, 0u)] uint rn,
                            [CombinatorialValues(2u, 0u)] uint rm,
                            [CombinatorialMemberData(nameof(_1D_))] ulong z,
                            [CombinatorialMemberData(nameof(_1D_))] ulong a,
                            [CombinatorialMemberData(nameof(_1D_))] ulong b)
        {
            uint opcode = 0x7EE08400; // SUB D0, D0, D0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sub_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialValues(2u, 0u)] uint rm,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                   [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E208400; // SUB V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Sub_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialValues(2u, 0u)] uint rm,
                                       [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                       [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                       [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                       [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E208400; // SUB V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBHN{2} <Vd>.<Tb>, <Vn>.<Ta>, <Vm>.<Ta>")]
        [PairwiseData]
        public void Subhn_V_8H8B_4S4H_2D2S([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                           [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                           [CombinatorialMemberData(nameof(_4H2S1D_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint opcode = 0x0E206000; // SUBHN V0.8B, V0.8H, V0.8H
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBHN{2} <Vd>.<Tb>, <Vn>.<Ta>, <Vm>.<Ta>")]
        [PairwiseData]
        public void Subhn_V_8H16B_4S8H_2D4S([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint opcode = 0x4E206000; // SUBHN2 V0.16B, V0.8H, V0.8H
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "TRN1 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Trn1_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E002800; // TRN1 V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "TRN1 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Trn1_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E002800; // TRN1 V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "TRN2 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Trn2_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E006800; // TRN2 V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "TRN2 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Trn2_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E006800; // TRN2 V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UABA <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uaba_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E207C00; // UABA V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UABA <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uaba_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x6E207C00; // UABA V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UABAL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Uabal_V_8B8H_4H4S_2S2D([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H, 4H4S, 2S2D>
        {
            uint opcode = 0x2E205000; // UABAL V0.8H, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UABAL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Uabal_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x6E205000; // UABAL2 V0.8H, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE1(a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UABD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uabd_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E207400; // UABD V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UABD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uabd_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x6E207400; // UABD V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UABDL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Uabdl_V_8B8H_4H4S_2S2D([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H, 4H4S, 2S2D>
        {
            uint opcode = 0x2E207000; // UABDL V0.8H, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UABDL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Uabdl_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x6E207000; // UABDL2 V0.8H, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE1(a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UADDL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Uaddl_V_8B8H_4H4S_2S2D([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H, 4H4S, 2S2D>
        {
            uint opcode = 0x2E200000; // UADDL V0.8H, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UADDL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Uaddl_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x6E200000; // UADDL2 V0.8H, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE1(a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UADDW{2} <Vd>.<Ta>, <Vn>.<Ta>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Uaddw_V_8B8H8H_4H4S4S_2S2D2D([CombinatorialValues(0u)] uint rd,
                                                 [CombinatorialValues(1u, 0u)] uint rn,
                                                 [CombinatorialValues(2u, 0u)] uint rm,
                                                 [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                                 [CombinatorialMemberData(nameof(_4H2S1D_))][CombinatorialRandomData(Count = RndCnt)] ulong a,
                                                 [CombinatorialMemberData(nameof(_8B4H2S_))][CombinatorialRandomData(Count = RndCnt)] ulong b,
                                                 [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H8H, 4H4S4S, 2S2D2D>
        {
            uint opcode = 0x2E201000; // UADDW V0.8H, V0.8H, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UADDW{2} <Vd>.<Ta>, <Vn>.<Ta>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Uaddw_V_16B8H8H_8H4S4S_4S2D2D([CombinatorialValues(0u)] uint rd,
                                                  [CombinatorialValues(1u, 0u)] uint rn,
                                                  [CombinatorialValues(2u, 0u)] uint rm,
                                                  [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                                  [CombinatorialMemberData(nameof(_4H2S1D_))][CombinatorialRandomData(Count = RndCnt)] ulong a,
                                                  [CombinatorialMemberData(nameof(_8B4H2S_))][CombinatorialRandomData(Count = RndCnt)] ulong b,
                                                  [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H8H, 8H4S4S, 4S2D2D>
        {
            uint opcode = 0x6E201000; // UADDW2 V0.8H, V0.8H, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UHADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uhadd_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E200400; // UHADD V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UHADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uhadd_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialValues(2u, 0u)] uint rm,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x6E200400; // UHADD V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UHSUB <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uhsub_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E202400; // UHSUB V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UHSUB <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uhsub_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialValues(2u, 0u)] uint rm,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x6E202400; // UHSUB V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UQADD <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Uqadd_S_B_H_S_D([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong z,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong a,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <b, H, S, D>
        {
            uint opcode = 0x7E200C00; // UQADD B0, B0, B0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "UQADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uqadd_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E200C00; // UQADD V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "UQADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uqadd_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialValues(2u, 0u)] uint rm,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                         [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E200C00; // UQADD V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "UQSUB <V><d>, <V><n>, <V><m>")]
        [PairwiseData]
        public void Uqsub_S_B_H_S_D([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong z,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong a,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <b, H, S, D>
        {
            uint opcode = 0x7E202C00; // UQSUB B0, B0, B0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "UQSUB <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uqsub_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialValues(2u, 0u)] uint rm,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E202C00; // UQSUB V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "UQSUB <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uqsub_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialValues(2u, 0u)] uint rm,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                         [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E202C00; // UQSUB V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "URHADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Urhadd_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialValues(2u, 0u)] uint rm,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E201400; // URHADD V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "URHADD <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Urhadd_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialValues(2u, 0u)] uint rm,
                                       [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                       [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                       [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                       [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x6E201400; // URHADD V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "USUBL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Usubl_V_8B8H_4H4S_2S2D([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialValues(2u, 0u)] uint rm,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H, 4H4S, 2S2D>
        {
            uint opcode = 0x2E202000; // USUBL V0.8H, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "USUBL{2} <Vd>.<Ta>, <Vn>.<Tb>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Usubl_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialValues(2u, 0u)] uint rm,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x6E202000; // USUBL2 V0.8H, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE1(a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "USUBW{2} <Vd>.<Ta>, <Vn>.<Ta>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Usubw_V_8B8H8H_4H4S4S_2S2D2D([CombinatorialValues(0u)] uint rd,
                                                 [CombinatorialValues(1u, 0u)] uint rn,
                                                 [CombinatorialValues(2u, 0u)] uint rm,
                                                 [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                                 [CombinatorialMemberData(nameof(_4H2S1D_))][CombinatorialRandomData(Count = RndCnt)] ulong a,
                                                 [CombinatorialMemberData(nameof(_8B4H2S_))][CombinatorialRandomData(Count = RndCnt)] ulong b,
                                                 [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B8H8H, 4H4S4S, 2S2D2D>
        {
            uint opcode = 0x2E203000; // USUBW V0.8H, V0.8H, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "USUBW{2} <Vd>.<Ta>, <Vn>.<Ta>, <Vm>.<Tb>")]
        [PairwiseData]
        public void Usubw_V_16B8H8H_8H4S4S_4S2D2D([CombinatorialValues(0u)] uint rd,
                                                  [CombinatorialValues(1u, 0u)] uint rn,
                                                  [CombinatorialValues(2u, 0u)] uint rm,
                                                  [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                                  [CombinatorialMemberData(nameof(_4H2S1D_))][CombinatorialRandomData(Count = RndCnt)] ulong a,
                                                  [CombinatorialMemberData(nameof(_8B4H2S_))][CombinatorialRandomData(Count = RndCnt)] ulong b,
                                                  [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H8H, 8H4S4S, 4S2D2D>
        {
            uint opcode = 0x6E203000; // USUBW2 V0.8H, V0.8H, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE1(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UZP1 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uzp1_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E001800; // UZP1 V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UZP1 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uzp1_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E001800; // UZP1 V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UZP2 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uzp2_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E005800; // UZP2 V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UZP2 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Uzp2_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E005800; // UZP2 V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ZIP1 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Zip1_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E003800; // ZIP1 V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ZIP1 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Zip1_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E003800; // ZIP1 V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ZIP2 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Zip2_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialValues(2u, 0u)] uint rm,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E007800; // ZIP2 V0.8B, V0.8B, V0.8B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ZIP2 <Vd>.<T>, <Vn>.<T>, <Vm>.<T>")]
        [PairwiseData]
        public void Zip2_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialValues(2u, 0u)] uint rm,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E007800; // ZIP2 V0.16B, V0.16B, V0.16B
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }
#endif
    }
}
