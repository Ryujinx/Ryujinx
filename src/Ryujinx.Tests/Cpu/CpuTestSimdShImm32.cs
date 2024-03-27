#define SimdShImm32

using ARMeilleure.State;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdShImm32")]
    public sealed class CpuTestSimdShImm32 : CpuTest32
    {
        public CpuTestSimdShImm32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdShImm32

        #region "ValueSource (Types)"
        private static ulong[] _1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _2S_()
        {
            return new[]
            {
                0x0000000000000000ul, 0x7FFFFFFF7FFFFFFFul, 0x8000000080000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _4H_()
        {
            return new[]
            {
                0x0000000000000000ul, 0x7FFF7FFF7FFF7FFFul, 0x8000800080008000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _8B_()
        {
            return new[]
            {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful, 0x8080808080808080ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }
        #endregion

        #region "ValueSource (Opcodes)"
        private static uint[] _Vshr_Imm_SU8_()
        {
            return new[]
            {
                0xf2880010u, // VSHR.S8  D0, D0, #8
                0xf2880110u, // VSRA.S8  D0, D0, #8
                0xf2880210u, // VRSHR.S8 D0, D0, #8
                0xf2880310u, // VRSRA.S8 D0, D0, #8
            };
        }

        private static uint[] _Vshr_Imm_SU16_()
        {
            return new[]
            {
                0xf2900010u, // VSHR.S16  D0, D0, #16
                0xf2900110u, // VSRA.S16  D0, D0, #16
                0xf2900210u, // VRSHR.S16 D0, D0, #16
                0xf2900310u, // VRSRA.S16 D0, D0, #16
            };
        }

        private static uint[] _Vshr_Imm_SU32_()
        {
            return new[]
            {
                0xf2a00010u, // VSHR.S32  D0, D0, #32
                0xf2a00110u, // VSRA.S32  D0, D0, #32
                0xf2a00210u, // VRSHR.S32 D0, D0, #32
                0xf2a00310u, // VRSRA.S32 D0, D0, #32
            };
        }

        private static uint[] _Vshr_Imm_SU64_()
        {
            return new[]
            {
                0xf2800190u, // VSRA.S64  D0, D0, #64
                0xf2800290u, // VRSHR.S64 D0, D0, #64
                0xf2800090u, // VSHR.S64  D0, D0, #64
            };
        }

        private static uint[] _Vqshrn_Vqrshrn_Vrshrn_Imm_()
        {
            return new[]
            {
                0xf2800910u, // VORR.I16 D0, #0 (immediate value changes it into QSHRN)
                0xf2800950u, // VORR.I16 Q0, #0 (immediate value changes it into QRSHRN)
                0xf2800850u, // VMOV.I16 Q0, #0 (immediate value changes it into RSHRN)
            };
        }

        private static uint[] _Vqshrun_Vqrshrun_Imm_()
        {
            return new[]
            {
                0xf3800810u, // VMOV.I16 D0, #0x80 (immediate value changes it into QSHRUN)
                0xf3800850u, // VMOV.I16 Q0, #0x80 (immediate value changes it into QRSHRUN)
            };
        }
        #endregion

        private const int RndCnt = 2;
        private const int RndCntShiftImm = 2;

        [Theory]
        [PairwiseData]
        public void Vshr_Imm_SU8([CombinatorialMemberData(nameof(_Vshr_Imm_SU8_))] uint opcode,
                                 [CombinatorialRange(0u, 3u, 1)] uint rd,
                                 [CombinatorialRange(0u, 3u, 1)] uint rm,
                                 [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                 [CombinatorialMemberData(nameof(_8B_))] ulong b,
                                 [CombinatorialValues(1u, 8u)] uint shiftImm,
                                 bool u,
                                 bool q)
        {
            uint imm6 = 16 - shiftImm;

            Vshr_Imm_SU(opcode, rd, rm, z, b, imm6, u, q);
        }

        [Theory]
        [PairwiseData]
        public void Vshr_Imm_SU16([CombinatorialMemberData(nameof(_Vshr_Imm_SU16_))] uint opcode,
                                  [CombinatorialRange(0u, 3u, 1)] uint rd,
                                  [CombinatorialRange(0u, 3u, 1)] uint rm,
                                  [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                  [CombinatorialMemberData(nameof(_4H_))] ulong b,
                                  [CombinatorialValues(1u, 16u)] uint shiftImm,
                                  bool u,
                                  bool q)
        {
            uint imm6 = 32 - shiftImm;

            Vshr_Imm_SU(opcode, rd, rm, z, b, imm6, u, q);
        }

        [Theory]
        [PairwiseData]
        public void Vshr_Imm_SU32([CombinatorialMemberData(nameof(_Vshr_Imm_SU32_))] uint opcode,
                                  [CombinatorialRange(0u, 3u, 1)] uint rd,
                                  [CombinatorialRange(0u, 3u, 1)] uint rm,
                                  [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                  [CombinatorialMemberData(nameof(_2S_))] ulong b,
                                  [CombinatorialValues(1u, 32u)] uint shiftImm,
                                  bool u,
                                  bool q)
        {
            uint imm6 = 64 - shiftImm;

            Vshr_Imm_SU(opcode, rd, rm, z, b, imm6, u, q);
        }

        [Theory]
        [PairwiseData]
        public void Vshr_Imm_SU64([CombinatorialMemberData(nameof(_Vshr_Imm_SU64_))] uint opcode,
                                  [CombinatorialRange(0u, 3u, 1)] uint rd,
                                  [CombinatorialRange(0u, 3u, 1)] uint rm,
                                  [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                  [CombinatorialMemberData(nameof(_1D_))] ulong b,
                                  [CombinatorialValues(1u, 64u)] uint shiftImm,
                                  bool u,
                                  bool q)
        {
            uint imm6 = 64 - shiftImm;

            Vshr_Imm_SU(opcode, rd, rm, z, b, imm6, u, q);
        }

        private void Vshr_Imm_SU(uint opcode, uint rd, uint rm, ulong z, ulong b, uint imm6, bool u, bool q)
        {
            if (u)
            {
                opcode |= 1 << 24;
            }

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

            opcode |= (imm6 & 0x3f) << 16;

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VSHL.<size> {<Vd>}, <Vm>, #<imm>")]
        [PairwiseData]
        public void Vshl_Imm([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialValues(0u, 1u, 2u, 3u)] uint size,
                             [CombinatorialRandomData(Count = RndCntShiftImm)] uint shiftImm,
                             [CombinatorialRandomData(Count = RndCnt)] ulong z,
                             [CombinatorialRandomData(Count = RndCnt)] ulong a,
                             [CombinatorialRandomData(Count = RndCnt)] ulong b,
                             bool q)
        {
            uint opcode = 0xf2800510u; // VORR.I32 D0, #0 (immediate value changes it into SHL)
            if (q)
            {
                opcode |= 1 << 6;
                rm <<= 1;
                rd <<= 1;
            }

            uint imm = 1u << ((int)size + 3);
            imm |= shiftImm & (imm - 1);

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((imm & 0x3f) << 16) | ((imm & 0x40) << 1);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VSHRN.<size> <Vd>, <Vm>, #<imm>")]
        [PairwiseData]
        public void Vshrn_Imm([CombinatorialValues(0u, 1u)] uint rd,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialValues(0u, 1u, 2u)] uint size,
                              [CombinatorialRandomData(Count = RndCntShiftImm)] uint shiftImm,
                              [CombinatorialRandomData(Count = RndCnt)] ulong z,
                              [CombinatorialRandomData(Count = RndCnt)] ulong a,
                              [CombinatorialRandomData(Count = RndCnt)] ulong b)
        {
            uint opcode = 0xf2800810u; // VMOV.I16 D0, #0 (immediate value changes it into SHRN)

            uint imm = 1u << ((int)size + 3);
            imm |= shiftImm & (imm - 1);

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((imm & 0x3f) << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vqshrn_Vqrshrn_Vrshrn_Imm([CombinatorialMemberData(nameof(_Vqshrn_Vqrshrn_Vrshrn_Imm_))] uint opcode,
                                              [CombinatorialValues(0u, 1u)] uint rd,
                                              [CombinatorialValues(2u, 0u)] uint rm,
                                              [CombinatorialValues(0u, 1u, 2u)] uint size,
                                              [CombinatorialRandomData(Count = RndCntShiftImm)] uint shiftImm,
                                              [CombinatorialRandomData(Count = RndCnt)] ulong z,
                                              [CombinatorialRandomData(Count = RndCnt)] ulong a,
                                              [CombinatorialRandomData(Count = RndCnt)] ulong b,
                                              bool u)
        {
            uint imm = 1u << ((int)size + 3);
            imm |= shiftImm & (imm - 1);

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((imm & 0x3f) << 16);

            if (u)
            {
                opcode |= 1u << 24;
            }

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            int fpscr = (int)Random.Shared.NextUInt() & (int)Fpsr.Qc;

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2, fpscr: fpscr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [Theory]
        [PairwiseData]
        public void Vqshrun_Vqrshrun_Imm([CombinatorialMemberData(nameof(_Vqshrun_Vqrshrun_Imm_))] uint opcode,
                                         [CombinatorialValues(0u, 1u)] uint rd,
                                         [CombinatorialValues(2u, 0u)] uint rm,
                                         [CombinatorialValues(0u, 1u, 2u)] uint size,
                                         [CombinatorialRandomData(Count = RndCntShiftImm)] uint shiftImm,
                                         [CombinatorialRandomData(Count = RndCnt)] ulong z,
                                         [CombinatorialRandomData(Count = RndCnt)] ulong a,
                                         [CombinatorialRandomData(Count = RndCnt)] ulong b)
        {
            uint imm = 1u << ((int)size + 3);
            imm |= shiftImm & (imm - 1);

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((imm & 0x3f) << 16);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            int fpscr = (int)Random.Shared.NextUInt() & (int)Fpsr.Qc;

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2, fpscr: fpscr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }
#endif
    }
}
