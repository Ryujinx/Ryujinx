#define SimdCvt32

using ARMeilleure.State;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdCvt32")]
    public sealed class CpuTestSimdCvt32 : CpuTest32
    {
        public CpuTestSimdCvt32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdCvt32

        #region "ValueSource (Opcodes)"
        private static uint[] _Vrint_AMNP_V_F32_()
        {
            return new[]
            {
                0xf3ba0500u, // VRINTA.F32 Q0, Q0
                0xf3ba0680u, // VRINTM.F32 Q0, Q0
                0xf3ba0400u, // VRINTN.F32 Q0, Q0
                0xf3ba0780u, // VRINTP.F32 Q0, Q0
            };
        }
        #endregion

        #region "ValueSource (Types)"
        private static uint[] _1S_()
        {
            return new[] {
                0x00000000u, 0x7FFFFFFFu,
                0x80000000u, 0xFFFFFFFFu,
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

        [Theory(DisplayName = "VCVT.<dt>.F32 <Sd>, <Sm>")]
        [PairwiseData]
        public void Vcvt_F32_I32([CombinatorialValues(0u, 1u, 2u, 3u)] uint rd,
                                 [CombinatorialValues(0u, 1u, 2u, 3u)] uint rm,
                                 [CombinatorialMemberData(nameof(_1S_F_))] ulong s0,
                                 [CombinatorialMemberData(nameof(_1S_F_))] ulong s1,
                                 [CombinatorialMemberData(nameof(_1S_F_))] ulong s2,
                                 [CombinatorialMemberData(nameof(_1S_F_))] ulong s3,
                                 bool unsigned) // <U32, S32>
        {
            uint opcode = 0xeebc0ac0u; // VCVT.U32.F32 S0, S0

            if (!unsigned)
            {
                opcode |= 1 << 16; // opc2<0>
            }

            opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);
            opcode |= ((rm & 0x1e) >> 1) | ((rm & 0x1) << 5);

            V128 v0 = MakeVectorE0E1E2E3((uint)s0, (uint)s1, (uint)s2, (uint)s3);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VCVT.<dt>.F64 <Sd>, <Dm>")]
        [PairwiseData]
        public void Vcvt_F64_I32([CombinatorialValues(0u, 1u, 2u, 3u)] uint rd,
                                 [CombinatorialValues(0u, 1u)] uint rm,
                                 [CombinatorialMemberData(nameof(_1D_F_))] ulong d0,
                                 [CombinatorialMemberData(nameof(_1D_F_))] ulong d1,
                                 bool unsigned) // <U32, S32>
        {
            uint opcode = 0xeebc0bc0u; // VCVT.U32.F64 S0, D0

            if (!unsigned)
            {
                opcode |= 1 << 16; // opc2<0>
            }

            opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(d0, d1);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VCVT.F32.<dt> <Sd>, <Sm>")]
        [PairwiseData]
        public void Vcvt_I32_F32([CombinatorialValues(0u, 1u, 2u, 3u)] uint rd,
                                 [CombinatorialValues(0u, 1u, 2u, 3u)] uint rm,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s0,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s1,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s2,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s3,
                                 bool unsigned, // <U32, S32>
                                 [CombinatorialValues(RMode.Rn)] RMode rMode)
        {
            uint opcode = 0xeeb80a40u; // VCVT.F32.U32 S0, S0

            if (!unsigned)
            {
                opcode |= 1 << 7; // op
            }

            opcode |= ((rm & 0x1e) >> 1) | ((rm & 0x1) << 5);
            opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);

            V128 v0 = MakeVectorE0E1E2E3(s0, s1, s2, s3);

            int fpscr = (int)rMode << (int)Fpcr.RMode;

            SingleOpcode(opcode, v0: v0, fpscr: fpscr);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VCVT.F64.<dt> <Dd>, <Sm>")]
        [PairwiseData]
        public void Vcvt_I32_F64([CombinatorialValues(0u, 1u)] uint rd,
                                 [CombinatorialValues(0u, 1u, 2u, 3u)] uint rm,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s0,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s1,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s2,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s3,
                                 bool unsigned, // <U32, S32>
                                 [CombinatorialValues(RMode.Rn)] RMode rMode)
        {
            uint opcode = 0xeeb80b40u; // VCVT.F64.U32 D0, S0

            if (!unsigned)
            {
                opcode |= 1 << 7; // op
            }

            opcode |= ((rm & 0x1e) >> 1) | ((rm & 0x1) << 5);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);

            V128 v0 = MakeVectorE0E1E2E3(s0, s1, s2, s3);

            int fpscr = (int)rMode << (int)Fpcr.RMode;

            SingleOpcode(opcode, v0: v0, fpscr: fpscr);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vrint_AMNP_V_F32([CombinatorialMemberData(nameof(_Vrint_AMNP_V_F32_))] uint opcode,
                                     [CombinatorialValues(0u, 1u, 2u, 3u)] uint rd,
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
                rm >>= 1;
                rm <<= 1;
            }

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(d0, d1);
            V128 v1 = MakeVectorE0E1(d2, d3);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VRINTX.F<size> <Sd>, <Sm>")]
        [PairwiseData]
        public void Vrintx_S([CombinatorialValues(0u, 1u)] uint rd,
                             [CombinatorialValues(0u, 1u)] uint rm,
                             [CombinatorialValues(2u, 3u)] uint size,
                             [CombinatorialMemberData(nameof(_1D_F_))] ulong s0,
                             [CombinatorialMemberData(nameof(_1D_F_))] ulong s1,
                             [CombinatorialMemberData(nameof(_1D_F_))] ulong s2,
                             [CombinatorialValues(RMode.Rn, RMode.Rm, RMode.Rp)] RMode rMode)
        {
            uint opcode = 0xEB70A40;
            V128 v0, v1, v2;
            if (size == 2)
            {
                opcode |= ((rm & 0x1e) >> 1) | ((rm & 0x1) << 5);
                opcode |= ((rd & 0x1e) >> 11) | ((rm & 0x1) << 22);
                v0 = MakeVectorE0E1((uint)BitConverter.SingleToInt32Bits(s0), (uint)BitConverter.SingleToInt32Bits(s0));
                v1 = MakeVectorE0E1((uint)BitConverter.SingleToInt32Bits(s1), (uint)BitConverter.SingleToInt32Bits(s0));
                v2 = MakeVectorE0E1((uint)BitConverter.SingleToInt32Bits(s2), (uint)BitConverter.SingleToInt32Bits(s1));
            }
            else
            {
                opcode |= ((rm & 0xf) << 0) | ((rd & 0x10) << 1);
                opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
                v0 = MakeVectorE0E1((uint)BitConverter.DoubleToInt64Bits(s0), (uint)BitConverter.DoubleToInt64Bits(s0));
                v1 = MakeVectorE0E1((uint)BitConverter.DoubleToInt64Bits(s1), (uint)BitConverter.DoubleToInt64Bits(s0));
                v2 = MakeVectorE0E1((uint)BitConverter.DoubleToInt64Bits(s2), (uint)BitConverter.DoubleToInt64Bits(s1));
            }

            opcode |= ((size & 3) << 8);

            int fpscr = (int)rMode << (int)Fpcr.RMode;
            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2, fpscr: fpscr);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VCVT<top>.F16.F32 <Sd>, <Dm>")]
        [PairwiseData]
        public void Vcvt_F32_F16([CombinatorialValues(0u, 1u, 2u, 3u)] uint rd,
                                 [CombinatorialValues(0u, 1u, 2u, 3u)] uint rm,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s0,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s1,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s2,
                                 [CombinatorialMemberData(nameof(_1S_))] uint s3,
                                 bool top)
        {
            uint opcode = 0xeeb30a40; // VCVTB.F16.F32 S0, D0

            if (top)
            {
                opcode |= 1 << 7;
            }

            opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);
            opcode |= ((rm & 0x1e) >> 1) | ((rm & 0x1) << 5);

            V128 v0 = MakeVectorE0E1E2E3(s0, s1, s2, s3);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VCVT<top>.F16.F64 <Sd>, <Dm>")]
        [PairwiseData]
        public void Vcvt_F64_F16([CombinatorialValues(0u, 1u, 2u, 3u)] uint rd,
                                 [CombinatorialValues(0u, 1u)] uint rm,
                                 [CombinatorialMemberData(nameof(_1D_F_))] ulong d0,
                                 [CombinatorialMemberData(nameof(_1D_F_))] ulong d1,
                                 bool top)
        {
            uint opcode = 0xeeb30b40; // VCVTB.F16.F64 S0, D0

            if (top)
            {
                opcode |= 1 << 7;
            }

            opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(d0, d1);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VCVT<top>.F<size>.F16 <Vd>, <Sm>")]
        [PairwiseData]
        public void Vcvt_F16_Fx([CombinatorialValues(0u, 1u, 2u, 3u)] uint rd,
                                [CombinatorialValues(0u, 1u, 2u, 3u)] uint rm,
                                [CombinatorialMemberData(nameof(_1D_F_))] ulong d0,
                                [CombinatorialMemberData(nameof(_1D_F_))] ulong d1,
                                bool top,
                                bool sz)
        {
            uint opcode = 0xeeb20a40; // VCVTB.F32.F16 S0, S0

            if (top)
            {
                opcode |= 1 << 7;
            }

            if (sz)
            {
                opcode |= 1 << 8;
                opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            }
            else
            {
                opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);
            }

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(d0, d1);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        public static readonly uint[] RandomFixImm =
        {
            Random.Shared.NextUInt(32u, 64u),
        };

        [Theory(DisplayName = "VCVT.I32.F32 <Vd>, <Vm>, #<fbits>")]
        [PairwiseData]
        public void Vcvt_V_Fixed_F32_I32([CombinatorialValues(0u, 1u, 2u, 3u)] uint vd,
                                         [CombinatorialValues(0u, 1u, 2u, 3u)] uint vm,
                                         [CombinatorialMemberData(nameof(_1S_F_))][CombinatorialRandomData(Count = RndCnt)] ulong s0,
                                         [CombinatorialMemberData(nameof(_1S_F_))][CombinatorialRandomData(Count = RndCnt)] ulong s1,
                                         [CombinatorialMemberData(nameof(_1S_F_))][CombinatorialRandomData(Count = RndCnt)] ulong s2,
                                         [CombinatorialMemberData(nameof(_1S_F_))][CombinatorialRandomData(Count = RndCnt)] ulong s3,
                                         [CombinatorialMemberData(nameof(RandomFixImm))] uint fixImm,
                                         bool unsigned,
                                         bool q)
        {
            uint opcode = 0xF2800F10u; // VCVT.U32.F32 D0, D0, #0

            if (q)
            {
                opcode |= 1 << 6;
                vm <<= 1;
                vd <<= 1;
            }

            if (unsigned)
            {
                opcode |= 1 << 24;
            }

            opcode |= ((vm & 0x10) << 1);
            opcode |= ((vm & 0xf) << 0);

            opcode |= ((vd & 0x10) << 18);
            opcode |= ((vd & 0xf) << 12);

            opcode |= (fixImm & 0x3f) << 16;

            var v0 = new V128((uint)s0, (uint)s1, (uint)s2, (uint)s3);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VCVT.F32.I32 <Vd>, <Vm>, #<fbits>")]
        [PairwiseData]
        public void Vcvt_V_Fixed_I32_F32([CombinatorialValues(0u, 1u, 2u, 3u)] uint vd,
                                         [CombinatorialValues(0u, 1u, 2u, 3u)] uint vm,
                                         [CombinatorialMemberData(nameof(_1S_))][CombinatorialRandomData(Count = RndCnt)] uint s0,
                                         [CombinatorialMemberData(nameof(_1S_))][CombinatorialRandomData(Count = RndCnt)] uint s1,
                                         [CombinatorialMemberData(nameof(_1S_))][CombinatorialRandomData(Count = RndCnt)] uint s2,
                                         [CombinatorialMemberData(nameof(_1S_))][CombinatorialRandomData(Count = RndCnt)] uint s3,
                                         [CombinatorialRange(32u, 63u, 1)] uint fixImm,
                                         bool unsigned,
                                         bool q)
        {
            uint opcode = 0xF2800E10u; // VCVT.F32.U32 D0, D0, #0

            if (q)
            {
                opcode |= 1 << 6;
                vm <<= 1;
                vd <<= 1;
            }

            if (unsigned)
            {
                opcode |= 1 << 24;
            }

            opcode |= ((vm & 0x10) << 1);
            opcode |= ((vm & 0xf) << 0);

            opcode |= ((vd & 0x10) << 18);
            opcode |= ((vd & 0xf) << 12);

            opcode |= (fixImm & 0x3f) << 16;

            var v0 = new V128(s0, s1, s2, s3);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VRINTR.F<size> <Sd>, <Sm>")]
        [PairwiseData]
        public void Vrintr([CombinatorialValues(0u, 1u)] uint rd,
                           [CombinatorialValues(0u, 1u)] uint rm,
                           [CombinatorialValues(2u, 3u)] uint size,
                           [CombinatorialMemberData(nameof(_1D_F_))] ulong s0,
                           [CombinatorialMemberData(nameof(_1D_F_))] ulong s1,
                           [CombinatorialMemberData(nameof(_1D_F_))] ulong s2,
                           [CombinatorialValues(RMode.Rn, RMode.Rm, RMode.Rp)] RMode rMode)
        {
            Skip.If(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Instruction not testable with Unicorn");
            
            uint opcode = 0xEEB60A40;

            V128 v0, v1, v2;

            if (size == 2)
            {
                opcode |= ((rm & 0x1e) >> 1) | ((rm & 0x1) << 5);
                opcode |= ((rd & 0x1e) << 11) | ((rd & 0x1) << 22);
                v0 = MakeVectorE0E1((uint)BitConverter.SingleToInt32Bits(s0), (uint)BitConverter.SingleToInt32Bits(s0));
                v1 = MakeVectorE0E1((uint)BitConverter.SingleToInt32Bits(s1), (uint)BitConverter.SingleToInt32Bits(s0));
                v2 = MakeVectorE0E1((uint)BitConverter.SingleToInt32Bits(s2), (uint)BitConverter.SingleToInt32Bits(s1));
            }
            else
            {
                opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
                opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
                v0 = MakeVectorE0E1((uint)BitConverter.DoubleToInt64Bits(s0), (uint)BitConverter.DoubleToInt64Bits(s0));
                v1 = MakeVectorE0E1((uint)BitConverter.DoubleToInt64Bits(s1), (uint)BitConverter.DoubleToInt64Bits(s0));
                v2 = MakeVectorE0E1((uint)BitConverter.DoubleToInt64Bits(s2), (uint)BitConverter.DoubleToInt64Bits(s1));
            }

            opcode |= ((size & 3) << 8);

            int fpscr = (int)rMode << (int)Fpcr.RMode;
            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2, fpscr: fpscr);

            CompareAgainstUnicorn();
        }
#endif
    }
}
