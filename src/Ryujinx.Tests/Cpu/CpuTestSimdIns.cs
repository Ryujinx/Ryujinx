#define SimdIns

using ARMeilleure.State;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdIns")]
    public sealed class CpuTestSimdIns : CpuTest
    {
        public CpuTestSimdIns(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdIns

        #region "ValueSource"
        private static ulong[] _1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
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

        private static ulong[] _8B4H_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0xFFFFFFFFFFFFFFFFul,
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

        private static uint[] _W_()
        {
            return new[] {
                0x00000000u, 0x0000007Fu,
                0x00000080u, 0x000000FFu,
                0x00007FFFu, 0x00008000u,
                0x0000FFFFu, 0x7FFFFFFFu,
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
        #endregion

        [SkippableTheory(DisplayName = "DUP <Vd>.<T>, W<n>")]
        [PairwiseData]
        public void Dup_Gp_W([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 31u)] uint rn,
                             [CombinatorialMemberData(nameof(_W_))] uint wn,
                             [CombinatorialValues(0, 1, 2)] int size,  // Q0: <8B,  4H, 2S>
                             [CombinatorialValues(0b0u, 0b1u)] uint q) // Q1: <16B, 8H, 4S>
        {
            uint imm5 = (1u << size) & 0x1Fu;

            uint opcode = 0x0E000C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);
            opcode |= ((q & 1) << 30);

            uint w31 = Random.Shared.NextUInt();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcode, x1: wn, x31: w31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "DUP <Vd>.<T>, X<n>")]
        [PairwiseData]
        public void Dup_Gp_X([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 31u)] uint rn,
                             [CombinatorialMemberData(nameof(_X_))] ulong xn)
        {
            uint opcode = 0x4E080C00; // DUP V0.2D, X0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcode, x1: xn, x31: x31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "DUP B0, V1.B[<index>]")]
        [PairwiseData]
        public void Dup_S_B([CombinatorialMemberData(nameof(_8B_))] ulong a,
                            [CombinatorialValues(0u, 15u)] uint index)
        {
            const int TestSize = 0;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x5E000420; // RESERVED
            opcode |= (imm5 << 16);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "DUP H0, V1.H[<index>]")]
        [PairwiseData]
        public void Dup_S_H([CombinatorialMemberData(nameof(_4H_))] ulong a,
                            [CombinatorialValues(0u, 7u)] uint index)
        {
            const int TestSize = 1;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x5E000420; // RESERVED
            opcode |= (imm5 << 16);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "DUP S0, V1.S[<index>]")]
        [PairwiseData]
        public void Dup_S_S([CombinatorialMemberData(nameof(_2S_))] ulong a,
                            [CombinatorialValues(0u, 1u, 2u, 3u)] uint index)
        {
            const int TestSize = 2;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x5E000420; // RESERVED
            opcode |= (imm5 << 16);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "DUP D0, V1.D[<index>]")]
        [PairwiseData]
        public void Dup_S_D([CombinatorialMemberData(nameof(_1D_))] ulong a,
                            [CombinatorialValues(0u, 1u)] uint index)
        {
            const int TestSize = 3;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x5E000420; // RESERVED
            opcode |= (imm5 << 16);

            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "DUP <Vd>.<T>, <Vn>.B[<index>]")]
        [PairwiseData]
        public void Dup_V_8B_16B([CombinatorialValues(0u)] uint rd,
                                 [CombinatorialValues(1u, 0u)] uint rn,
                                 [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                 [CombinatorialMemberData(nameof(_8B_))] ulong a,
                                 [CombinatorialValues(0u, 15u)] uint index,
                                 [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            const int TestSize = 0;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x0E000400; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "DUP <Vd>.<T>, <Vn>.H[<index>]")]
        [PairwiseData]
        public void Dup_V_4H_8H([CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(1u, 0u)] uint rn,
                                [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                [CombinatorialMemberData(nameof(_4H_))] ulong a,
                                [CombinatorialValues(0u, 7u)] uint index,
                                [CombinatorialValues(0b0u, 0b1u)] uint q) // <4H, 8H>
        {
            const int TestSize = 1;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x0E000400; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "DUP <Vd>.<T>, <Vn>.S[<index>]")]
        [PairwiseData]
        public void Dup_V_2S_4S([CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(1u, 0u)] uint rn,
                                [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                [CombinatorialValues(0u, 1u, 2u, 3u)] uint index,
                                [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            const int TestSize = 2;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x0E000400; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "DUP <Vd>.<T>, <Vn>.D[<index>]")]
        [PairwiseData]
        public void Dup_V_2D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a,
                             [CombinatorialValues(0u, 1u)] uint index,
                             [CombinatorialValues(0b1u)] uint q) // <2D>
        {
            const int TestSize = 3;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x0E000400; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "INS <Vd>.B[<index>], W<n>")]
        [PairwiseData]
        public void Ins_Gp_WB([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_W_))] uint wn,
                              [CombinatorialValues(0u, 15u)] uint index)
        {
            const int TestSize = 0;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x4E001C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            uint w31 = Random.Shared.NextUInt();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcode, x1: wn, x31: w31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "INS <Vd>.H[<index>], W<n>")]
        [PairwiseData]
        public void Ins_Gp_WH([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialMemberData(nameof(_4H_))] ulong z,
                              [CombinatorialMemberData(nameof(_W_))] uint wn,
                              [CombinatorialValues(0u, 7u)] uint index)
        {
            const int TestSize = 1;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x4E001C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            uint w31 = Random.Shared.NextUInt();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcode, x1: wn, x31: w31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "INS <Vd>.S[<index>], W<n>")]
        [PairwiseData]
        public void Ins_Gp_WS([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialMemberData(nameof(_2S_))] ulong z,
                              [CombinatorialMemberData(nameof(_W_))] uint wn,
                              [CombinatorialValues(0u, 1u, 2u, 3u)] uint index)
        {
            const int TestSize = 2;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x4E001C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            uint w31 = Random.Shared.NextUInt();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcode, x1: wn, x31: w31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "INS <Vd>.D[<index>], X<n>")]
        [PairwiseData]
        public void Ins_Gp_XD([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 31u)] uint rn,
                              [CombinatorialMemberData(nameof(_1D_))] ulong z,
                              [CombinatorialMemberData(nameof(_X_))] ulong xn,
                              [CombinatorialValues(0u, 1u)] uint index)
        {
            const int TestSize = 3;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x4E001C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            ulong x31 = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcode, x1: xn, x31: x31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "INS <Vd>.B[<index1>], <Vn>.B[<index2>]")]
        [PairwiseData]
        public void Ins_V_BB([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a,
                             [CombinatorialValues(0u, 15u)] uint dstIndex,
                             [CombinatorialValues(0u, 15u)] uint srcIndex)
        {
            const int TestSize = 0;

            uint imm5 = (dstIndex << (TestSize + 1) | 1u << TestSize) & 0x1Fu;
            uint imm4 = (srcIndex << TestSize) & 0xFu;

            uint opcode = 0x6E000400; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);
            opcode |= (imm4 << 11);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "INS <Vd>.H[<index1>], <Vn>.H[<index2>]")]
        [PairwiseData]
        public void Ins_V_HH([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_4H_))] ulong z,
                             [CombinatorialMemberData(nameof(_4H_))] ulong a,
                             [CombinatorialValues(0u, 7u)] uint dstIndex,
                             [CombinatorialValues(0u, 7u)] uint srcIndex)
        {
            const int TestSize = 1;

            uint imm5 = (dstIndex << (TestSize + 1) | 1u << TestSize) & 0x1Fu;
            uint imm4 = (srcIndex << TestSize) & 0xFu;

            uint opcode = 0x6E000400; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);
            opcode |= (imm4 << 11);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "INS <Vd>.S[<index1>], <Vn>.S[<index2>]")]
        [PairwiseData]
        public void Ins_V_SS([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_2S_))] ulong z,
                             [CombinatorialMemberData(nameof(_2S_))] ulong a,
                             [CombinatorialValues(0u, 1u, 2u, 3u)] uint dstIndex,
                             [CombinatorialValues(0u, 1u, 2u, 3u)] uint srcIndex)
        {
            const int TestSize = 2;

            uint imm5 = (dstIndex << (TestSize + 1) | 1u << TestSize) & 0x1Fu;
            uint imm4 = (srcIndex << TestSize) & 0xFu;

            uint opcode = 0x6E000400; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);
            opcode |= (imm4 << 11);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "INS <Vd>.D[<index1>], <Vn>.D[<index2>]")]
        [PairwiseData]
        public void Ins_V_DD([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a,
                             [CombinatorialValues(0u, 1u)] uint dstIndex,
                             [CombinatorialValues(0u, 1u)] uint srcIndex)
        {
            const int TestSize = 3;

            uint imm5 = (dstIndex << (TestSize + 1) | 1u << TestSize) & 0x1Fu;
            uint imm4 = (srcIndex << TestSize) & 0xFu;

            uint opcode = 0x6E000400; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);
            opcode |= (imm4 << 11);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SMOV <Wd>, <Vn>.B[<index>]")]
        [PairwiseData]
        public void Smov_S_BW([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u)] uint rn,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialValues(0u, 15u)] uint index)
        {
            const int TestSize = 0;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x0E002C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            ulong x0 = (ulong)Random.Shared.NextUInt() << 32;
            uint w31 = Random.Shared.NextUInt();
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, x0: x0, x31: w31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SMOV <Wd>, <Vn>.H[<index>]")]
        [PairwiseData]
        public void Smov_S_HW([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u)] uint rn,
                              [CombinatorialMemberData(nameof(_4H_))] ulong a,
                              [CombinatorialValues(0u, 7u)] uint index)
        {
            const int TestSize = 1;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x0E002C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            ulong x0 = (ulong)Random.Shared.NextUInt() << 32;
            uint w31 = Random.Shared.NextUInt();
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, x0: x0, x31: w31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SMOV <Xd>, <Vn>.B[<index>]")]
        [PairwiseData]
        public void Smov_S_BX([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u)] uint rn,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialValues(0u, 15u)] uint index)
        {
            const int TestSize = 0;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x4E002C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            ulong x31 = Random.Shared.NextULong();
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, x31: x31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SMOV <Xd>, <Vn>.H[<index>]")]
        [PairwiseData]
        public void Smov_S_HX([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u)] uint rn,
                              [CombinatorialMemberData(nameof(_4H_))] ulong a,
                              [CombinatorialValues(0u, 7u)] uint index)
        {
            const int TestSize = 1;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x4E002C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            ulong x31 = Random.Shared.NextULong();
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, x31: x31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SMOV <Xd>, <Vn>.S[<index>]")]
        [PairwiseData]
        public void Smov_S_SX([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u)] uint rn,
                              [CombinatorialMemberData(nameof(_2S_))] ulong a,
                              [CombinatorialValues(0u, 1u, 2u, 3u)] uint index)
        {
            const int TestSize = 2;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x4E002C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            ulong x31 = Random.Shared.NextULong();
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, x31: x31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UMOV <Wd>, <Vn>.B[<index>]")]
        [PairwiseData]
        public void Umov_S_BW([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u)] uint rn,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialValues(0u, 15u)] uint index)
        {
            const int TestSize = 0;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x0E003C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            ulong x0 = (ulong)Random.Shared.NextUInt() << 32;
            uint w31 = Random.Shared.NextUInt();
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, x0: x0, x31: w31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UMOV <Wd>, <Vn>.H[<index>]")]
        [PairwiseData]
        public void Umov_S_HW([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u)] uint rn,
                              [CombinatorialMemberData(nameof(_4H_))] ulong a,
                              [CombinatorialValues(0u, 7u)] uint index)
        {
            const int TestSize = 1;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x0E003C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            ulong x0 = (ulong)Random.Shared.NextUInt() << 32;
            uint w31 = Random.Shared.NextUInt();
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, x0: x0, x31: w31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UMOV <Wd>, <Vn>.S[<index>]")]
        [PairwiseData]
        public void Umov_S_SW([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u)] uint rn,
                              [CombinatorialMemberData(nameof(_2S_))] ulong a,
                              [CombinatorialValues(0u, 1u, 2u, 3u)] uint index)
        {
            const int TestSize = 2;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x0E003C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            ulong x0 = (ulong)Random.Shared.NextUInt() << 32;
            uint w31 = Random.Shared.NextUInt();
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, x0: x0, x31: w31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UMOV <Xd>, <Vn>.D[<index>]")]
        [PairwiseData]
        public void Umov_S_DX([CombinatorialValues(0u, 31u)] uint rd,
                              [CombinatorialValues(1u)] uint rn,
                              [CombinatorialMemberData(nameof(_1D_))] ulong a,
                              [CombinatorialValues(0u, 1u)] uint index)
        {
            const int TestSize = 3;

            uint imm5 = (index << (TestSize + 1) | 1u << TestSize) & 0x1Fu;

            uint opcode = 0x4E003C00; // RESERVED
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm5 << 16);

            ulong x31 = Random.Shared.NextULong();
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, x31: x31, v1: v1);

            CompareAgainstUnicorn();
        }
#endif
    }
}
