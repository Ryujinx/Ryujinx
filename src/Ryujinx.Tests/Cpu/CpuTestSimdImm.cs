#define SimdImm

using ARMeilleure.State;
using System;
using Xunit;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdImm")]
    public sealed class CpuTestSimdImm : CpuTest
    {
        public CpuTestSimdImm(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdImm

        #region "Helper methods"
        // abcdefgh -> aaaaaaaabbbbbbbbccccccccddddddddeeeeeeeeffffffffgggggggghhhhhhhh
        private static ulong ExpandImm8(byte imm8)
        {
            ulong imm64 = 0ul;

            for (int i = 0, j = 0; i < 8; i++, j += 8)
            {
                if (((imm8 >> i) & 0b1) != 0)
                {
                    imm64 |= 0b11111111ul << j;
                }
            }

            return imm64;
        }

        // aaaaaaaabbbbbbbbccccccccddddddddeeeeeeeeffffffffgggggggghhhhhhhh -> abcdefgh
        private static byte ShrinkImm64(ulong imm64)
        {
            byte imm8 = 0;

            for (int i = 0, j = 0; i < 8; i++, j += 8)
            {
                if (((imm64 >> j) & 0b11111111ul) != 0ul) // Note: no format check.
                {
                    imm8 |= (byte)(0b1 << i);
                }
            }

            return imm8;
        }
        #endregion

        #region "ValueSource (Types)"
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

        private static IEnumerable<byte> _8BIT_IMM_()
        {
            yield return 0x00;
            yield return 0x7F;
            yield return 0x80;
            yield return 0xFF;

            for (int cnt = 1; cnt <= RndCntImm8; cnt++)
            {
                byte imm8 = Random.Shared.NextByte();

                yield return imm8;
            }
        }

        private static IEnumerable<ulong> _64BIT_IMM_()
        {
            yield return ExpandImm8(0x00);
            yield return ExpandImm8(0x7F);
            yield return ExpandImm8(0x80);
            yield return ExpandImm8(0xFF);

            for (int cnt = 1; cnt <= RndCntImm64; cnt++)
            {
                byte imm8 = Random.Shared.NextByte();

                yield return ExpandImm8(imm8);
            }
        }
        #endregion

        #region "ValueSource (Opcodes)"
        private static uint[] _Bic_Orr_Vi_16bit_()
        {
            return new[]
            {
                0x2F009400u, // BIC V0.4H, #0
                0x0F009400u, // ORR V0.4H, #0
            };
        }

        private static uint[] _Bic_Orr_Vi_32bit_()
        {
            return new[]
            {
                0x2F001400u, // BIC V0.2S, #0
                0x0F001400u, // ORR V0.2S, #0
            };
        }

        private static uint[] _F_Mov_Vi_2S_()
        {
            return new[]
            {
                0x0F00F400u, // FMOV V0.2S, #2.0
            };
        }

        private static uint[] _F_Mov_Vi_4S_()
        {
            return new[]
            {
                0x4F00F400u, // FMOV V0.4S, #2.0
            };
        }

        private static uint[] _F_Mov_Vi_2D_()
        {
            return new[]
            {
                0x6F00F400u, // FMOV V0.2D, #2.0
            };
        }

        private static uint[] _Movi_V_8bit_()
        {
            return new[]
            {
                0x0F00E400u, // MOVI V0.8B, #0
            };
        }

        private static uint[] _Movi_Mvni_V_16bit_shifted_imm_()
        {
            return new[]
            {
                0x0F008400u, // MOVI V0.4H, #0
                0x2F008400u, // MVNI V0.4H, #0
            };
        }

        private static uint[] _Movi_Mvni_V_32bit_shifted_imm_()
        {
            return new[]
            {
                0x0F000400u, // MOVI V0.2S, #0
                0x2F000400u, // MVNI V0.2S, #0
            };
        }

        private static uint[] _Movi_Mvni_V_32bit_shifting_ones_()
        {
            return new[]
            {
                0x0F00C400u, // MOVI V0.2S, #0, MSL #8
                0x2F00C400u, // MVNI V0.2S, #0, MSL #8
            };
        }

        private static uint[] _Movi_V_64bit_scalar_()
        {
            return new[]
            {
                0x2F00E400u, // MOVI D0, #0
            };
        }

        private static uint[] _Movi_V_64bit_vector_()
        {
            return new[]
            {
                0x6F00E400u, // MOVI V0.2D, #0
            };
        }
        #endregion

        private const int RndCntImm8 = 2;
        private const int RndCntImm64 = 2;

        [SkippableTheory]
        [PairwiseData]
        public void Bic_Orr_Vi_16bit([CombinatorialMemberData(nameof(_Bic_Orr_Vi_16bit_))] uint opcodes,
                                     [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8BIT_IMM_))] byte imm8,
                                     [CombinatorialValues(0b0u, 0b1u)] uint amount, // <0, 8>
                                     [CombinatorialValues(0b0u, 0b1u)] uint q)      // <4H, 8H>
        {
            uint abc = (imm8 & 0xE0u) >> 5;
            uint defgh = (imm8 & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);
            opcodes |= ((amount & 1) << 13);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcodes, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Bic_Orr_Vi_32bit([CombinatorialMemberData(nameof(_Bic_Orr_Vi_32bit_))] uint opcodes,
                                     [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8BIT_IMM_))] byte imm8,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint amount, // <0, 8, 16, 24>
                                     [CombinatorialValues(0b0u, 0b1u)] uint q)                      // <2S, 4S>
        {
            uint abc = (imm8 & 0xE0u) >> 5;
            uint defgh = (imm8 & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);
            opcodes |= ((amount & 3) << 13);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcodes, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Vi_2S([CombinatorialMemberData(nameof(_F_Mov_Vi_2S_))] uint opcodes,
                                [CombinatorialRange(0u, 255u, 1u)] uint abcdefgh)
        {
            uint abc = (abcdefgh & 0xE0u) >> 5;
            uint defgh = (abcdefgh & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);

            SingleOpcode(opcodes, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Vi_4S([CombinatorialMemberData(nameof(_F_Mov_Vi_4S_))] uint opcodes,
                                [CombinatorialRange(0u, 255u, 1u)] uint abcdefgh)
        {
            uint abc = (abcdefgh & 0xE0u) >> 5;
            uint defgh = (abcdefgh & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);

            SingleOpcode(opcodes);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Vi_2D([CombinatorialMemberData(nameof(_F_Mov_Vi_2D_))] uint opcodes,
                                [CombinatorialRange(0u, 255u, 1u)] uint abcdefgh)
        {
            uint abc = (abcdefgh & 0xE0u) >> 5;
            uint defgh = (abcdefgh & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);

            SingleOpcode(opcodes);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Movi_V_8bit([CombinatorialMemberData(nameof(_Movi_V_8bit_))] uint opcodes,
                                [CombinatorialMemberData(nameof(_8BIT_IMM_))] byte imm8,
                                [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            uint abc = (imm8 & 0xE0u) >> 5;
            uint defgh = (imm8 & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);
            opcodes |= ((q & 1) << 30);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(q == 0u ? z : 0ul);

            SingleOpcode(opcodes, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Movi_Mvni_V_16bit_shifted_imm([CombinatorialMemberData(nameof(_Movi_Mvni_V_16bit_shifted_imm_))] uint opcodes,
                                                  [CombinatorialMemberData(nameof(_8BIT_IMM_))] byte imm8,
                                                  [CombinatorialValues(0b0u, 0b1u)] uint amount, // <0, 8>
                                                  [CombinatorialValues(0b0u, 0b1u)] uint q)      // <4H, 8H>
        {
            uint abc = (imm8 & 0xE0u) >> 5;
            uint defgh = (imm8 & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);
            opcodes |= ((amount & 1) << 13);
            opcodes |= ((q & 1) << 30);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(q == 0u ? z : 0ul);

            SingleOpcode(opcodes, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Movi_Mvni_V_32bit_shifted_imm([CombinatorialMemberData(nameof(_Movi_Mvni_V_32bit_shifted_imm_))] uint opcodes,
                                                  [CombinatorialMemberData(nameof(_8BIT_IMM_))] byte imm8,
                                                  [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint amount, // <0, 8, 16, 24>
                                                  [CombinatorialValues(0b0u, 0b1u)] uint q)                      // <2S, 4S>
        {
            uint abc = (imm8 & 0xE0u) >> 5;
            uint defgh = (imm8 & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);
            opcodes |= ((amount & 3) << 13);
            opcodes |= ((q & 1) << 30);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(q == 0u ? z : 0ul);

            SingleOpcode(opcodes, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Movi_Mvni_V_32bit_shifting_ones([CombinatorialMemberData(nameof(_Movi_Mvni_V_32bit_shifting_ones_))] uint opcodes,
                                                    [CombinatorialMemberData(nameof(_8BIT_IMM_))] byte imm8,
                                                    [CombinatorialValues(0b0u, 0b1u)] uint amount, // <8, 16>
                                                    [CombinatorialValues(0b0u, 0b1u)] uint q)      // <2S, 4S>
        {
            uint abc = (imm8 & 0xE0u) >> 5;
            uint defgh = (imm8 & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);
            opcodes |= ((amount & 1) << 12);
            opcodes |= ((q & 1) << 30);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(q == 0u ? z : 0ul);

            SingleOpcode(opcodes, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Movi_V_64bit_scalar([CombinatorialMemberData(nameof(_Movi_V_64bit_scalar_))] uint opcodes,
                                        [CombinatorialMemberData(nameof(_64BIT_IMM_))] ulong imm)
        {
            byte imm8 = ShrinkImm64(imm);

            uint abc = (imm8 & 0xE0u) >> 5;
            uint defgh = (imm8 & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);

            SingleOpcode(opcodes, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Movi_V_64bit_vector([CombinatorialMemberData(nameof(_Movi_V_64bit_vector_))] uint opcodes,
                                        [CombinatorialMemberData(nameof(_64BIT_IMM_))] ulong imm)
        {
            byte imm8 = ShrinkImm64(imm);

            uint abc = (imm8 & 0xE0u) >> 5;
            uint defgh = (imm8 & 0x1Fu);

            opcodes |= (abc << 16) | (defgh << 5);

            SingleOpcode(opcodes);

            CompareAgainstUnicorn();
        }
#endif
    }
}
