#define Simd32

using ARMeilleure.State;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Simd32")]
    public sealed class CpuTestSimd32 : CpuTest32
    {
        public CpuTestSimd32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Simd32

        #region "ValueSource (Opcodes)"
        private static uint[] _Vabs_Vneg_Vpaddl_I_()
        {
            return new[]
            {
                0xf3b10300u, // VABS.S8   D0, D0
                0xf3b10380u, // VNEG.S8   D0, D0
                0xf3b00200u, // VPADDL.S8 D0, D0
            };
        }

        private static uint[] _Vabs_Vneg_F_()
        {
            return new[]
            {
                0xf3b90700u, // VABS.F32 D0, D0
                0xf3b90780u, // VNEG.F32 D0, D0
            };
        }
        #endregion

        #region "ValueSource (Types)"
        private static ulong[] _8B4H2S_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0xFFFFFFFFFFFFFFFFul,
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

        private static IEnumerable<ulong> _GenPopCnt8B_()
        {
            for (ulong cnt = 0ul; cnt <= 255ul; cnt++)
            {
                yield return (cnt << 56) | (cnt << 48) | (cnt << 40) | (cnt << 32) |
                             (cnt << 24) | (cnt << 16) | (cnt << 08) | cnt;
            }
        }
        #endregion

        private const int RndCnt = 2;

        private static readonly bool _noZeros = false;
        private static readonly bool _noInfs = false;
        private static readonly bool _noNaNs = false;

        [Theory(DisplayName = "SHA256SU0.32 <Qd>, <Qm>")]
        [PairwiseData]
        public void Sha256su0_V([CombinatorialValues(0xF3BA03C0u)] uint opcode,
                                [CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(2u)] uint rm,
                                [CombinatorialValues(0x9BCBBF7443FB4F91ul)] ulong z0,
                                [CombinatorialValues(0x482C58A58CBCBD59ul)] ulong z1,
                                [CombinatorialValues(0xA0099B803625F82Aul)] ulong a0,
                                [CombinatorialValues(0x1AA3B0B4E1AB4C8Cul)] ulong a1,
                                [CombinatorialValues(0x29A44D72598F15F3ul)] ulong resultL,
                                [CombinatorialValues(0x74CED221E2793F07ul)] ulong resultH)
        {
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);

            ExecutionContext context = SingleOpcode(opcode, v0: v0, v1: v1, runUnicorn: false);

            Assert.Multiple(() =>
            {
                Assert.Equal(resultL, GetVectorE0(context.GetV(0)));
                Assert.Equal(resultH, GetVectorE1(context.GetV(0)));
            });

            // Unicorn does not yet support hash instructions in A32.
            // CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vabs_Vneg_Vpaddl_V_I([CombinatorialMemberData(nameof(_Vabs_Vneg_Vpaddl_I_))] uint opcode,
                                         [CombinatorialRange(0u, 3u, 1u)] uint rd,
                                         [CombinatorialRange(0u, 3u, 1u)] uint rm,
                                         [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                         [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                                         [CombinatorialValues(0u, 1u, 2u)] uint size, // <S8, S16, S32>
                                         bool q)
        {
            if (q)
            {
                opcode |= 1 << 6;

                rd >>= 1;
                rd <<= 1;
                rm >>= 1;
                rm <<= 1;
            }

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            opcode |= (size & 0x3) << 18;

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vabs_Vneg_V_F32([CombinatorialMemberData(nameof(_Vabs_Vneg_F_))] uint opcode,
                                    [CombinatorialRange(0u, 3u, 1u)] uint rd,
                                    [CombinatorialRange(0u, 3u, 1u)] uint rm,
                                    [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                    [CombinatorialMemberData(nameof(_2S_F_))] ulong b,
                                    bool q)
        {
            if (q)
            {
                opcode |= 1 << 6;

                rd >>= 1;
                rd <<= 1;
                rm >>= 1;
                rm <<= 1;
            }

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VCNT.8 D0, D0 | VCNT.8 Q0, Q0")]
        [PairwiseData]
        public void Vcnt([CombinatorialValues(0u, 1u)] uint rd,
                         [CombinatorialValues(0u, 1u)] uint rm,
                         [CombinatorialMemberData(nameof(_GenPopCnt8B_))] ulong d0,
                         bool q)
        {
            ulong d1 = ~d0; // It's expensive to have a second generator.

            uint opcode = 0xf3b00500u; // VCNT.8 D0, D0

            if (q)
            {
                opcode |= 1u << 6;

                rd &= ~1u;
                rm &= ~1u;
            }

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(d0, d1);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vmovn_V([CombinatorialRange(0u, 3u, 1u)] uint rd,
                            [CombinatorialRange(0u, 3u, 1u)] uint rm,
                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                            [CombinatorialValues(0u, 1u, 2u, 3u)] uint op,
                            [CombinatorialValues(0u, 1u, 2u)] uint size) // <S8, S16, S32>
        {
            rm >>= 1;
            rm <<= 1;

            uint opcode = 0xf3b20200u; // VMOVN.S16 D0, Q0

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            opcode |= (op & 0x3) << 6;
            opcode |= (size & 0x3) << 18;

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }
#endif
    }
}
