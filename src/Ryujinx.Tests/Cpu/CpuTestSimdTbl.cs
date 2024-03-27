#define SimdTbl

using ARMeilleure.State;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdTbl")]
    public sealed class CpuTestSimdTbl : CpuTest
    {
        public CpuTestSimdTbl(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdTbl

        #region "Helper methods"
        private static ulong GenIdxsForTbls(int regs)
        {
            const byte IdxInRngMin = 0;
            byte idxInRngMax = (byte)((16 * regs) - 1);
            byte idxOutRngMin = (byte)(16 * regs);
            const byte IdxOutRngMax = 255;

            ulong idxs = 0ul;

            for (int cnt = 1; cnt <= 8; cnt++)
            {
                ulong idxInRng = Random.Shared.NextByte(IdxInRngMin, idxInRngMax);
                ulong idxOutRng = Random.Shared.NextByte(idxOutRngMin, IdxOutRngMax);

                ulong idx = Random.Shared.NextBool() ? idxInRng : idxOutRng;

                idxs = (idxs << 8) | idx;
            }

            return idxs;
        }
        #endregion

        #region "ValueSource (Types)"
        private static ulong[] _8B_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static IEnumerable<ulong> _GenIdxsForTbl1_()
        {
            yield return 0x0000000000000000ul;
            yield return 0x7F7F7F7F7F7F7F7Ful;
            yield return 0x8080808080808080ul;
            yield return 0xFFFFFFFFFFFFFFFFul;

            for (int cnt = 1; cnt <= RndCntIdxs; cnt++)
            {
                yield return GenIdxsForTbls(regs: 1);
            }
        }

        private static IEnumerable<ulong> _GenIdxsForTbl2_()
        {
            yield return 0x0000000000000000ul;
            yield return 0x7F7F7F7F7F7F7F7Ful;
            yield return 0x8080808080808080ul;
            yield return 0xFFFFFFFFFFFFFFFFul;

            for (int cnt = 1; cnt <= RndCntIdxs; cnt++)
            {
                yield return GenIdxsForTbls(regs: 2);
            }
        }

        private static IEnumerable<ulong> _GenIdxsForTbl3_()
        {
            yield return 0x0000000000000000ul;
            yield return 0x7F7F7F7F7F7F7F7Ful;
            yield return 0x8080808080808080ul;
            yield return 0xFFFFFFFFFFFFFFFFul;

            for (int cnt = 1; cnt <= RndCntIdxs; cnt++)
            {
                yield return GenIdxsForTbls(regs: 3);
            }
        }

        private static IEnumerable<ulong> _GenIdxsForTbl4_()
        {
            yield return 0x0000000000000000ul;
            yield return 0x7F7F7F7F7F7F7F7Ful;
            yield return 0x8080808080808080ul;
            yield return 0xFFFFFFFFFFFFFFFFul;

            for (int cnt = 1; cnt <= RndCntIdxs; cnt++)
            {
                yield return GenIdxsForTbls(regs: 4);
            }
        }
        #endregion

        #region "ValueSource (Opcodes)"
        private static uint[] _SingleRegisterTable_V_8B_16B_()
        {
            return new[]
            {
                0x0E000000u, // TBL V0.8B, { V0.16B }, V0.8B
                0x0E001000u, // TBX V0.8B, { V0.16B }, V0.8B
            };
        }

        private static uint[] _TwoRegisterTable_V_8B_16B_()
        {
            return new[]
            {
                0x0E002000u, // TBL V0.8B, { V0.16B, V1.16B }, V0.8B
                0x0E003000u, // TBX V0.8B, { V0.16B, V1.16B }, V0.8B
            };
        }

        private static uint[] _ThreeRegisterTable_V_8B_16B_()
        {
            return new[]
            {
                0x0E004000u, // TBL V0.8B, { V0.16B, V1.16B, V2.16B }, V0.8B
                0x0E005000u, // TBX V0.8B, { V0.16B, V1.16B, V2.16B }, V0.8B
            };
        }

        private static uint[] _FourRegisterTable_V_8B_16B_()
        {
            return new[]
            {
                0x0E006000u, // TBL V0.8B, { V0.16B, V1.16B, V2.16B, V3.16B }, V0.8B
                0x0E006000u, // TBX V0.8B, { V0.16B, V1.16B, V2.16B, V3.16B }, V0.8B
            };
        }
        #endregion

        private const int RndCntIdxs = 2;

        [SkippableTheory]
        [PairwiseData]
        public void SingleRegisterTable_V_8B_16B([CombinatorialMemberData(nameof(_SingleRegisterTable_V_8B_16B_))] uint opcodes,
                                                 [CombinatorialValues(0u)] uint rd,
                                                 [CombinatorialValues(1u)] uint rn,
                                                 [CombinatorialValues(2u)] uint rm,
                                                 [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                                 [CombinatorialMemberData(nameof(_8B_))] ulong table0,
                                                 [CombinatorialMemberData(nameof(_GenIdxsForTbl1_))] ulong indexes,
                                                 [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(table0, table0);
            V128 v2 = MakeVectorE0E1(indexes, q == 1u ? indexes : 0ul);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void TwoRegisterTable_V_8B_16B([CombinatorialMemberData(nameof(_TwoRegisterTable_V_8B_16B_))] uint opcodes,
                                              [CombinatorialValues(0u)] uint rd,
                                              [CombinatorialValues(1u)] uint rn,
                                              [CombinatorialValues(3u)] uint rm,
                                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                              [CombinatorialMemberData(nameof(_8B_))] ulong table0,
                                              [CombinatorialMemberData(nameof(_8B_))] ulong table1,
                                              [CombinatorialMemberData(nameof(_GenIdxsForTbl2_))] ulong indexes,
                                              [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(table0, table0);
            V128 v2 = MakeVectorE0E1(table1, table1);
            V128 v3 = MakeVectorE0E1(indexes, q == 1u ? indexes : 0ul);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, v3: v3);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Mod_TwoRegisterTable_V_8B_16B([CombinatorialMemberData(nameof(_TwoRegisterTable_V_8B_16B_))] uint opcodes,
                                                  [CombinatorialValues(30u, 1u)] uint rd,
                                                  [CombinatorialValues(31u)] uint rn,
                                                  [CombinatorialValues(1u, 30u)] uint rm,
                                                  [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                                  [CombinatorialMemberData(nameof(_8B_))] ulong table0,
                                                  [CombinatorialMemberData(nameof(_8B_))] ulong table1,
                                                  [CombinatorialMemberData(nameof(_GenIdxsForTbl2_))] ulong indexes,
                                                  [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v30 = MakeVectorE0E1(z, z);
            V128 v31 = MakeVectorE0E1(table0, table0);
            V128 v0 = MakeVectorE0E1(table1, table1);
            V128 v1 = MakeVectorE0E1(indexes, indexes);

            SingleOpcode(opcodes, v0: v0, v1: v1, v30: v30, v31: v31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void ThreeRegisterTable_V_8B_16B([CombinatorialMemberData(nameof(_ThreeRegisterTable_V_8B_16B_))] uint opcodes,
                                                [CombinatorialValues(0u)] uint rd,
                                                [CombinatorialValues(1u)] uint rn,
                                                [CombinatorialValues(4u)] uint rm,
                                                [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                                [CombinatorialMemberData(nameof(_8B_))] ulong table0,
                                                [CombinatorialMemberData(nameof(_8B_))] ulong table1,
                                                [CombinatorialMemberData(nameof(_8B_))] ulong table2,
                                                [CombinatorialMemberData(nameof(_GenIdxsForTbl3_))] ulong indexes,
                                                [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(table0, table0);
            V128 v2 = MakeVectorE0E1(table1, table1);
            V128 v3 = MakeVectorE0E1(table2, table2);
            V128 v4 = MakeVectorE0E1(indexes, q == 1u ? indexes : 0ul);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, v3: v3, v4: v4);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Mod_ThreeRegisterTable_V_8B_16B([CombinatorialMemberData(nameof(_ThreeRegisterTable_V_8B_16B_))] uint opcodes,
                                                    [CombinatorialValues(30u, 2u)] uint rd,
                                                    [CombinatorialValues(31u)] uint rn,
                                                    [CombinatorialValues(2u, 30u)] uint rm,
                                                    [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                                    [CombinatorialMemberData(nameof(_8B_))] ulong table0,
                                                    [CombinatorialMemberData(nameof(_8B_))] ulong table1,
                                                    [CombinatorialMemberData(nameof(_8B_))] ulong table2,
                                                    [CombinatorialMemberData(nameof(_GenIdxsForTbl3_))] ulong indexes,
                                                    [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v30 = MakeVectorE0E1(z, z);
            V128 v31 = MakeVectorE0E1(table0, table0);
            V128 v0 = MakeVectorE0E1(table1, table1);
            V128 v1 = MakeVectorE0E1(table2, table2);
            V128 v2 = MakeVectorE0E1(indexes, indexes);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, v30: v30, v31: v31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void FourRegisterTable_V_8B_16B([CombinatorialMemberData(nameof(_FourRegisterTable_V_8B_16B_))] uint opcodes,
                                               [CombinatorialValues(0u)] uint rd,
                                               [CombinatorialValues(1u)] uint rn,
                                               [CombinatorialValues(5u)] uint rm,
                                               [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                               [CombinatorialMemberData(nameof(_8B_))] ulong table0,
                                               [CombinatorialMemberData(nameof(_8B_))] ulong table1,
                                               [CombinatorialMemberData(nameof(_8B_))] ulong table2,
                                               [CombinatorialMemberData(nameof(_8B_))] ulong table3,
                                               [CombinatorialMemberData(nameof(_GenIdxsForTbl4_))] ulong indexes,
                                               [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(table0, table0);
            V128 v2 = MakeVectorE0E1(table1, table1);
            V128 v3 = MakeVectorE0E1(table2, table2);
            V128 v4 = MakeVectorE0E1(table3, table3);
            V128 v5 = MakeVectorE0E1(indexes, q == 1u ? indexes : 0ul);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, v3: v3, v4: v4, v5: v5);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Mod_FourRegisterTable_V_8B_16B([CombinatorialMemberData(nameof(_FourRegisterTable_V_8B_16B_))] uint opcodes,
                                                   [CombinatorialValues(30u, 3u)] uint rd,
                                                   [CombinatorialValues(31u)] uint rn,
                                                   [CombinatorialValues(3u, 30u)] uint rm,
                                                   [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                                   [CombinatorialMemberData(nameof(_8B_))] ulong table0,
                                                   [CombinatorialMemberData(nameof(_8B_))] ulong table1,
                                                   [CombinatorialMemberData(nameof(_8B_))] ulong table2,
                                                   [CombinatorialMemberData(nameof(_8B_))] ulong table3,
                                                   [CombinatorialMemberData(nameof(_GenIdxsForTbl4_))] ulong indexes,
                                                   [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            opcodes |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v30 = MakeVectorE0E1(z, z);
            V128 v31 = MakeVectorE0E1(table0, table0);
            V128 v0 = MakeVectorE0E1(table1, table1);
            V128 v1 = MakeVectorE0E1(table2, table2);
            V128 v2 = MakeVectorE0E1(table3, table3);
            V128 v3 = MakeVectorE0E1(indexes, indexes);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2, v3: v3, v30: v30, v31: v31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
