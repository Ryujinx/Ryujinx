#define SimdReg32

using ARMeilleure.State;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdReg32")]
    public sealed class CpuTestSimdReg32 : CpuTest32
    {
        public CpuTestSimdReg32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdReg32

        #region "ValueSource (Opcodes)"
        private static uint[] _V_Add_Sub_Long_Wide_I_()
        {
            return new[]
            {
                0xf2800000u, // VADDL.S8 Q0, D0, D0
                0xf2800100u, // VADDW.S8 Q0, Q0, D0
                0xf2800200u, // VSUBL.S8 Q0, D0, D0
                0xf2800300u, // VSUBW.S8 Q0, Q0, D0
            };
        }

        private static uint[] _Vfma_Vfms_Vfnma_Vfnms_S_F32_()
        {
            return new[]
            {
                0xEEA00A00u, // VFMA. F32 S0, S0, S0
                0xEEA00A40u, // VFMS. F32 S0, S0, S0
                0xEE900A40u, // VFNMA.F32 S0, S0, S0
                0xEE900A00u, // VFNMS.F32 S0, S0, S0
            };
        }

        private static uint[] _Vfma_Vfms_Vfnma_Vfnms_S_F64_()
        {
            return new[]
            {
                0xEEA00B00u, // VFMA. F64 D0, D0, D0
                0xEEA00B40u, // VFMS. F64 D0, D0, D0
                0xEE900B40u, // VFNMA.F64 D0, D0, D0
                0xEE900B00u, // VFNMS.F64 D0, D0, D0
            };
        }

        private static uint[] _Vfma_Vfms_V_F32_()
        {
            return new[]
            {
                0xF2000C10u, // VFMA.F32 D0, D0, D0
                0xF2200C10u, // VFMS.F32 D0, D0, D0
            };
        }

        private static uint[] _Vmla_Vmls_Vnmla_Vnmls_S_F32_()
        {
            return new[]
            {
                0xEE000A00u, // VMLA. F32 S0, S0, S0
                0xEE000A40u, // VMLS. F32 S0, S0, S0
                0xEE100A40u, // VNMLA.F32 S0, S0, S0
                0xEE100A00u, // VNMLS.F32 S0, S0, S0
            };
        }

        private static uint[] _Vmla_Vmls_Vnmla_Vnmls_S_F64_()
        {
            return new[]
            {
                0xEE000B00u, // VMLA. F64 D0, D0, D0
                0xEE000B40u, // VMLS. F64 D0, D0, D0
                0xEE100B40u, // VNMLA.F64 D0, D0, D0
                0xEE100B00u, // VNMLS.F64 D0, D0, D0
            };
        }

        private static uint[] _Vmlal_Vmlsl_V_I_()
        {
            return new[]
            {
                0xf2800800u, // VMLAL.S8 Q0, D0, D0
                0xf2800a00u, // VMLSL.S8 Q0, D0, D0
            };
        }

        private static uint[] _Vp_Add_Max_Min_F_()
        {
            return new[]
            {
                0xf3000d00u, // VPADD.F32 D0, D0, D0
                0xf3000f00u, // VPMAX.F32 D0, D0, D0
                0xf3200f00u, // VPMIN.F32 D0, D0, D0
            };
        }

        private static uint[] _Vp_Add_I_()
        {
            return new[]
            {
                0xf2000b10u, // VPADD.I8 D0, D0, D0
            };
        }

        private static uint[] _V_Pmax_Pmin_Rhadd_I_()
        {
            return new[]
            {
                0xf2000a00u, // VPMAX .S8 D0, D0, D0
                0xf2000a10u, // VPMIN .S8 D0, D0, D0
                0xf2000100u, // VRHADD.S8 D0, D0, D0
            };
        }

        private static uint[] _Vq_Add_Sub_I_()
        {
            return new[]
            {
                0xf2000050u, // VQADD.S8 Q0, Q0, Q0
                0xf2000250u, // VQSUB.S8 Q0, Q0, Q0
            };
        }
        #endregion

        #region "ValueSource (Types)"
        private static ulong[] _8B1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
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

        private const int RndCnt = 2;

        private static readonly bool _noZeros = false;
        private static readonly bool _noInfs = false;
        private static readonly bool _noNaNs = false;

        [Theory(DisplayName = "SHA256H.32 <Qd>, <Qn>, <Qm>")]
        [PairwiseData]
        public void Sha256h_V([CombinatorialValues(0xF3000C40u)] uint opcode,
                              [CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(2u)] uint rn,
                              [CombinatorialValues(4u)] uint rm,
                              [CombinatorialValues(0xAEE65C11943FB939ul)] ulong z0,
                              [CombinatorialValues(0xA89A87F110291DA3ul)] ulong z1,
                              [CombinatorialValues(0xE9F766DB7A49EA7Dul)] ulong a0,
                              [CombinatorialValues(0x3053F46B0C2F3507ul)] ulong a1,
                              [CombinatorialValues(0x6E86A473B9D4A778ul)] ulong b0,
                              [CombinatorialValues(0x7BE4F9E638156BB1ul)] ulong b1,
                              [CombinatorialValues(0x1F1DC4A98DA9C132ul)] ulong resultL,
                              [CombinatorialValues(0xDB9A2A7B47031A0Dul)] ulong resultH)
        {
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);
            V128 v2 = MakeVectorE0E1(b0, b1);

            ExecutionContext context = SingleOpcode(opcode, v0: v0, v1: v1, v2: v2, runUnicorn: false);

            Assert.Multiple(() =>
            {
                Assert.Equal(resultL, GetVectorE0(context.GetV(0)));
                Assert.Equal(resultH, GetVectorE1(context.GetV(0)));
            });

            // Unicorn does not yet support hash instructions in A32.
            // CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "SHA256H2.32 <Qd>, <Qn>, <Qm>")]
        [PairwiseData]
        public void Sha256h2_V([CombinatorialValues(0xF3100C40u)] uint opcode,
                               [CombinatorialValues(0u)] uint rd,
                               [CombinatorialValues(2u)] uint rn,
                               [CombinatorialValues(4u)] uint rm,
                               [CombinatorialValues(0xAEE65C11943FB939ul)] ulong z0,
                               [CombinatorialValues(0xA89A87F110291DA3ul)] ulong z1,
                               [CombinatorialValues(0xE9F766DB7A49EA7Dul)] ulong a0,
                               [CombinatorialValues(0x3053F46B0C2F3507ul)] ulong a1,
                               [CombinatorialValues(0x6E86A473B9D4A778ul)] ulong b0,
                               [CombinatorialValues(0x7BE4F9E638156BB1ul)] ulong b1,
                               [CombinatorialValues(0x0A1177E9D9C9B611ul)] ulong resultL,
                               [CombinatorialValues(0xF5A826404928A515ul)] ulong resultH)
        {
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);
            V128 v2 = MakeVectorE0E1(b0, b1);

            ExecutionContext context = SingleOpcode(opcode, v0: v0, v1: v1, v2: v2, runUnicorn: false);

            Assert.Multiple(() =>
            {
                Assert.Equal(resultL, GetVectorE0(context.GetV(0)));
                Assert.Equal(resultH, GetVectorE1(context.GetV(0)));
            });

            // Unicorn does not yet support hash instructions in A32.
            // CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "SHA256SU1.32 <Qd>, <Qn>, <Qm>")]
        [PairwiseData]
        public void Sha256su1_V([CombinatorialValues(0xF3200C40u)] uint opcode,
                                [CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(2u)] uint rn,
                                [CombinatorialValues(4u)] uint rm,
                                [CombinatorialValues(0xAEE65C11943FB939ul)] ulong z0,
                                [CombinatorialValues(0xA89A87F110291DA3ul)] ulong z1,
                                [CombinatorialValues(0xE9F766DB7A49EA7Dul)] ulong a0,
                                [CombinatorialValues(0x3053F46B0C2F3507ul)] ulong a1,
                                [CombinatorialValues(0x6E86A473B9D4A778ul)] ulong b0,
                                [CombinatorialValues(0x7BE4F9E638156BB1ul)] ulong b1,
                                [CombinatorialValues(0x9EE69CC896D7DE66ul)] ulong resultL,
                                [CombinatorialValues(0x004A147155573E54ul)] ulong resultH)
        {
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);
            V128 v2 = MakeVectorE0E1(b0, b1);

            ExecutionContext context = SingleOpcode(opcode, v0: v0, v1: v1, v2: v2, runUnicorn: false);

            Assert.Multiple(() =>
            {
                Assert.Equal(resultL, GetVectorE0(context.GetV(0)));
                Assert.Equal(resultH, GetVectorE1(context.GetV(0)));
            });

            // Unicorn does not yet support hash instructions in A32.
            // CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VADD.f32 V0, V0, V0")]
        [PairwiseData]
        public void Vadd_F32([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(0u, 1u)] uint rn,
                             [CombinatorialValues(0u, 2u)] uint rm,
                             [CombinatorialMemberData(nameof(_2S_F_))] ulong z0,
                             [CombinatorialMemberData(nameof(_2S_F_))] ulong z1,
                             [CombinatorialMemberData(nameof(_2S_F_))] ulong a0,
                             [CombinatorialMemberData(nameof(_2S_F_))] ulong a1,
                             [CombinatorialMemberData(nameof(_2S_F_))] ulong b0,
                             [CombinatorialMemberData(nameof(_2S_F_))] ulong b1,
                             bool q)
        {
            uint opcode = 0xf2000d00u; // VADD.F32 D0, D0, D0
            if (q)
            {
                opcode |= 1 << 6;
                rm <<= 1;
                rn <<= 1;
                rd <<= 1;
            }

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);
            V128 v2 = MakeVectorE0E1(b0, b1);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void V_Add_Sub_Long_Wide_I([CombinatorialMemberData(nameof(_V_Add_Sub_Long_Wide_I_))] uint opcode,
                                          [CombinatorialRange(0u, 5u, 1)] uint rd,
                                          [CombinatorialRange(0u, 5u, 1)] uint rn,
                                          [CombinatorialRange(0u, 5u, 1)] uint rm,
                                          [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                          [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                          [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                          [CombinatorialValues(0u, 1u, 2u)] uint size, // <SU8, SU16, SU32>
                                          bool u) // <S, U>
        {
            if (u)
            {
                opcode |= 1 << 24;
            }

            rd >>= 1;
            rd <<= 1;
            rn >>= 1;
            rn <<= 1;

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            opcode |= (size & 0x3) << 20;

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VCMP.f<size> Vd, Vm")]
        [PairwiseData]
        public void Vcmp([CombinatorialValues(2u, 3u)] uint size,
                         [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                         [CombinatorialMemberData(nameof(_1S_F_))] ulong b,
                         bool e)
        {
            uint opcode = 0xeeb40840u;
            uint rm = 1;
            uint rd = 2;

            if (size == 3)
            {
                opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
                opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            }
            else
            {
                opcode |= ((rm & 0x1e) >> 1) | ((rm & 0x1) << 5);
                opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);
            }

            opcode |= ((size & 3) << 8);
            if (e)
            {
                opcode |= 1 << 7;
            }

            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            int fpscr = (int)(Random.Shared.NextUInt(0xf) << 28);

            SingleOpcode(opcode, v1: v1, v2: v2, fpscr: fpscr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Nzcv);
        }

        // Fused.
        [Theory]
        [PairwiseData]
        public void Vfma_Vfms_Vfnma_Vfnms_S_F32([CombinatorialMemberData(nameof(_Vfma_Vfms_Vfnma_Vfnms_S_F32_))] uint opcode,
                                                [CombinatorialValues(0u, 1u, 2u, 3u)] uint rd,
                                                [CombinatorialValues(0u, 1u, 2u, 3u)] uint rn,
                                                [CombinatorialValues(0u, 1u, 2u, 3u)] uint rm,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong s0,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong s1,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong s2,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong s3)
        {
            opcode |= (((rd & 0x1) << 22) | (rd & 0x1e) << 11);
            opcode |= (((rn & 0x1) << 7) | (rn & 0x1e) << 15);
            opcode |= (((rm & 0x1) << 5) | (rm & 0x1e) >> 1);

            V128 v0 = MakeVectorE0E1E2E3((uint)s0, (uint)s1, (uint)s2, (uint)s3);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        // Fused.
        [Theory]
        [PairwiseData]
        public void Vfma_Vfms_Vfnma_Vfnms_S_F64([CombinatorialMemberData(nameof(_Vfma_Vfms_Vfnma_Vfnms_S_F64_))] uint opcode,
                                                [CombinatorialValues(0u, 1u)] uint rd,
                                                [CombinatorialValues(0u, 1u)] uint rn,
                                                [CombinatorialValues(0u, 1u)] uint rm,
                                                [CombinatorialMemberData(nameof(_1D_F_))] ulong d0,
                                                [CombinatorialMemberData(nameof(_1D_F_))] ulong d1)
        {
            opcode |= (((rd & 0x10) << 18) | (rd & 0xf) << 12);
            opcode |= (((rn & 0x10) << 3) | (rn & 0xf) << 16);
            opcode |= (((rm & 0x10) << 1) | (rm & 0xf) << 0);

            V128 v0 = MakeVectorE0E1(d0, d1);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        // Fused.
        [Theory]
        [PairwiseData]
        public void Vfma_Vfms_V_F32([CombinatorialMemberData(nameof(_Vfma_Vfms_V_F32_))] uint opcode,
                                    [CombinatorialValues(0u, 1u, 2u, 3u)] uint rd,
                                    [CombinatorialValues(0u, 1u, 2u, 3u)] uint rn,
                                    [CombinatorialValues(0u, 1u, 2u, 3u)] uint rm,
                                    [CombinatorialMemberData(nameof(_2S_F_))] ulong d0,
                                    [CombinatorialMemberData(nameof(_2S_F_))] ulong d1,
                                    [CombinatorialMemberData(nameof(_2S_F_))] ulong d2,
                                    [CombinatorialMemberData(nameof(_2S_F_))] ulong d3,
                                    bool q)
        {
            if (q)
            {
                opcode |= 1 << 6;

                rd >>= 1;
                rd <<= 1;
                rn >>= 1;
                rn <<= 1;
                rm >>= 1;
                rm <<= 1;
            }

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(d0, d1);
            V128 v1 = MakeVectorE0E1(d2, d3);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vmla_Vmls_Vnmla_Vnmls_S_F32([CombinatorialMemberData(nameof(_Vmla_Vmls_Vnmla_Vnmls_S_F32_))] uint opcode,
                                                [CombinatorialValues(0u, 1u, 2u, 3u)] uint rd,
                                                [CombinatorialValues(0u, 1u, 2u, 3u)] uint rn,
                                                [CombinatorialValues(0u, 1u, 2u, 3u)] uint rm,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong s0,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong s1,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong s2,
                                                [CombinatorialMemberData(nameof(_1S_F_))] ulong s3)
        {
            opcode |= (((rd & 0x1) << 22) | (rd & 0x1e) << 11);
            opcode |= (((rn & 0x1) << 7) | (rn & 0x1e) << 15);
            opcode |= (((rm & 0x1) << 5) | (rm & 0x1e) >> 1);

            V128 v0 = MakeVectorE0E1E2E3((uint)s0, (uint)s1, (uint)s2, (uint)s3);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vmla_Vmls_Vnmla_Vnmls_S_F64([CombinatorialMemberData(nameof(_Vmla_Vmls_Vnmla_Vnmls_S_F64_))] uint opcode,
                                                [CombinatorialValues(0u, 1u)] uint rd,
                                                [CombinatorialValues(0u, 1u)] uint rn,
                                                [CombinatorialValues(0u, 1u)] uint rm,
                                                [CombinatorialMemberData(nameof(_1D_F_))] ulong d0,
                                                [CombinatorialMemberData(nameof(_1D_F_))] ulong d1)
        {
            opcode |= (((rd & 0x10) << 18) | (rd & 0xf) << 12);
            opcode |= (((rn & 0x10) << 3) | (rn & 0xf) << 16);
            opcode |= (((rm & 0x10) << 1) | (rm & 0xf) << 0);

            V128 v0 = MakeVectorE0E1(d0, d1);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vmlal_Vmlsl_I([CombinatorialMemberData(nameof(_Vmlal_Vmlsl_V_I_))] uint opcode,
                                  [CombinatorialValues(0u)] uint rd,
                                  [CombinatorialValues(1u, 0u)] uint rn,
                                  [CombinatorialValues(2u, 0u)] uint rm,
                                  [CombinatorialValues(0u, 1u, 2u)] uint size,
                                  [CombinatorialRandomData(Count = RndCnt)] ulong z,
                                  [CombinatorialRandomData(Count = RndCnt)] ulong a,
                                  [CombinatorialRandomData(Count = RndCnt)] ulong b,
                                  bool u)
        {
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);

            opcode |= size << 20;

            if (u)
            {
                opcode |= 1 << 24;
            }

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VMULL.<size> <Vd>, <Vn>, <Vm>")]
        [PairwiseData]
        public void Vmull_I([CombinatorialValues(0u)] uint rd,
                            [CombinatorialValues(1u, 0u)] uint rn,
                            [CombinatorialValues(2u, 0u)] uint rm,
                            [CombinatorialValues(0u, 1u, 2u)] uint size,
                            [CombinatorialRandomData(Count = RndCnt)] ulong z,
                            [CombinatorialRandomData(Count = RndCnt)] ulong a,
                            [CombinatorialRandomData(Count = RndCnt)] ulong b,
                            bool op,
                            bool u)
        {
            uint opcode = 0xf2800c00u; // VMULL.S8 Q0, D0, D0

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);

            if (op)
            {
                opcode |= 1 << 9;
                size = 0;
                u = false;
            }

            opcode |= size << 20;

            if (u)
            {
                opcode |= 1 << 24;
            }

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VMULL.<P8, P64> <Qd>, <Dn>, <Dm>")]
        [PairwiseData]
        public void Vmull_I_P8_P64([CombinatorialValues(0u, 1u)] uint rd,
                                   [CombinatorialValues(0u, 1u)] uint rn,
                                   [CombinatorialValues(0u, 1u)] uint rm,
                                   [CombinatorialMemberData(nameof(_8B1D_))] ulong d0,
                                   [CombinatorialMemberData(nameof(_8B1D_))] ulong d1,
                                   [CombinatorialValues(0u/*, 2u*/)] uint size) // <P8, P64>
        {
            /*if (size == 2u)
            {
                Assert.Ignore("Ryujinx.Tests.Unicorn.UnicornException : Invalid instruction (UC_ERR_INSN_INVALID)");
            }*/

            uint opcode = 0xf2800e00u; // VMULL.P8 Q0, D0, D0

            rd >>= 1;
            rd <<= 1;

            opcode |= (((rd & 0x10) << 18) | (rd & 0xf) << 12);
            opcode |= (((rn & 0x10) << 3) | (rn & 0xf) << 16);
            opcode |= (((rm & 0x10) << 1) | (rm & 0xf) << 0);

            opcode |= (size & 0x3) << 20;

            V128 v0 = MakeVectorE0E1(d0, d1);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VSHL.<size> {<Vd>}, <Vm>, <Vn>")]
        [PairwiseData]
        public void Vshl([CombinatorialValues(0u)] uint rd,
                         [CombinatorialValues(1u, 0u)] uint rn,
                         [CombinatorialValues(2u, 0u)] uint rm,
                         [CombinatorialValues(0u, 1u, 2u, 3u)] uint size,
                         [CombinatorialRandomData(Count = RndCnt)] ulong z,
                         [CombinatorialRandomData(Count = RndCnt)] ulong a,
                         [CombinatorialRandomData(Count = RndCnt)] ulong b,
                         bool q,
                         bool u)
        {
            Skip.If(RuntimeInformation.ProcessArchitecture == Architecture.Arm64, "Unicorn on ARM64 crashes while executing this test");

            uint opcode = 0xf2000400u; // VSHL.S8 D0, D0, D0
            if (q)
            {
                opcode |= 1 << 6;
                rm <<= 1;
                rn <<= 1;
                rd <<= 1;
            }

            if (u)
            {
                opcode |= 1 << 24;
            }

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);

            opcode |= size << 20;

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vp_Add_Max_Min_F([CombinatorialMemberData(nameof(_Vp_Add_Max_Min_F_))] uint opcode,
                                     [CombinatorialValues(0u)] uint rd,
                                     [CombinatorialRange(0u, 7u, 1)] uint rn,
                                     [CombinatorialRange(0u, 7u, 1)] uint rm,
                                     [CombinatorialMemberData(nameof(_2S_F_))] ulong z0,
                                     [CombinatorialMemberData(nameof(_2S_F_))] ulong z1,
                                     [CombinatorialMemberData(nameof(_2S_F_))] ulong a0,
                                     [CombinatorialMemberData(nameof(_2S_F_))] ulong a1,
                                     [CombinatorialMemberData(nameof(_2S_F_))] ulong b0,
                                     [CombinatorialMemberData(nameof(_2S_F_))] ulong b1)
        {
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);
            V128 v2 = MakeVectorE0E1(b0, b1);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vp_Add_I([CombinatorialMemberData(nameof(_Vp_Add_I_))] uint opcode,
                             [CombinatorialValues(0u)] uint rd,
                             [CombinatorialRange(0u, 5u, 1)] uint rn,
                             [CombinatorialRange(0u, 5u, 1)] uint rm,
                             [CombinatorialValues(0u, 1u, 2u)] uint size,
                             [CombinatorialRandomData(Count = RndCnt)] ulong z,
                             [CombinatorialRandomData(Count = RndCnt)] ulong a,
                             [CombinatorialRandomData(Count = RndCnt)] ulong b)
        {
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);

            opcode |= size << 20;

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void V_Pmax_Pmin_Rhadd_I([CombinatorialMemberData(nameof(_V_Pmax_Pmin_Rhadd_I_))] uint opcode,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialRange(0u, 5u, 1)] uint rn,
                                        [CombinatorialRange(0u, 5u, 1)] uint rm,
                                        [CombinatorialValues(0u, 1u, 2u)] uint size,
                                        [CombinatorialRandomData(Count = RndCnt)] ulong z,
                                        [CombinatorialRandomData(Count = RndCnt)] ulong a,
                                        [CombinatorialRandomData(Count = RndCnt)] ulong b,
                                        bool u)
        {
            if (u)
            {
                opcode |= 1 << 24;
            }

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);

            opcode |= size << 20;

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vq_Add_Sub_I([CombinatorialMemberData(nameof(_Vq_Add_Sub_I_))] uint opcode,
                                 [CombinatorialRange(0u, 5u, 1)] uint rd,
                                 [CombinatorialRange(0u, 5u, 1)] uint rn,
                                 [CombinatorialRange(0u, 5u, 1)] uint rm,
                                 [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                 [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                 [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                                 [CombinatorialValues(0u, 1u, 2u)] uint size, // <SU8, SU16, SU32>
                                 bool u) // <S, U>
        {
            if (u)
            {
                opcode |= 1 << 24;
            }

            rd >>= 1;
            rd <<= 1;
            rn >>= 1;
            rn <<= 1;
            rm >>= 1;
            rm <<= 1;

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            opcode |= (size & 0x3) << 20;

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VQDMULH.<S16, S32> <Qd>, <Qn>, <Qm>")]
        [PairwiseData]
        public void Vqdmulh_I([CombinatorialRange(0u, 5u, 1)] uint rd,
                              [CombinatorialRange(0u, 5u, 1)] uint rn,
                              [CombinatorialRange(0u, 5u, 1)] uint rm,
                              [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                              [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong b,
                              [CombinatorialValues(1u, 2u)] uint size) // <S16, S32>
        {
            rd >>= 1;
            rd <<= 1;
            rn >>= 1;
            rn <<= 1;
            rm >>= 1;
            rm <<= 1;

            uint opcode = 0xf2100b40u & ~(3u << 20); // VQDMULH.S16 Q0, Q0, Q0

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            opcode |= (size & 0x3) << 20;

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }
#endif
    }
}
