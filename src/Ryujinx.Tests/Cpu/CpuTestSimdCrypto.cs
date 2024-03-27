#define SimdCrypto
// https://www.intel.com/content/dam/doc/white-paper/advanced-encryption-standard-new-instructions-set-paper.pdf

using ARMeilleure.State;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    public class CpuTestSimdCrypto : CpuTest
    {
        public CpuTestSimdCrypto(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdCrypto

        public static readonly ulong[] RandomRoundKeysH =
        {
            Random.Shared.NextULong(),
            Random.Shared.NextULong(),
        };
        public static readonly ulong[] RandomRoundKeysL =
        {
            Random.Shared.NextULong(),
            Random.Shared.NextULong(),
        };

        [SkippableTheory(DisplayName = "AESD <Vd>.16B, <Vn>.16B")]
        [CombinatorialData]
        public void Aesd_V([CombinatorialValues(0u)] uint rd,
                           [CombinatorialValues(1u)] uint rn,
                           [CombinatorialValues(0x7B5B546573745665ul)] ulong valueH,
                           [CombinatorialValues(0x63746F725D53475Dul)] ulong valueL,
                           [CombinatorialMemberData(nameof(RandomRoundKeysH))] ulong roundKeyH,
                           [CombinatorialMemberData(nameof(RandomRoundKeysL))] ulong roundKeyL,
                           [CombinatorialValues(0x8DCAB9BC035006BCul)] ulong resultH,
                           [CombinatorialValues(0x8F57161E00CAFD8Dul)] ulong resultL)
        {
            uint opcode = 0x4E285800; // AESD V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(roundKeyL ^ valueL, roundKeyH ^ valueH);
            V128 v1 = MakeVectorE0E1(roundKeyL, roundKeyH);

            ExecutionContext context = SingleOpcode(opcode, v0: v0, v1: v1);

            Assert.Multiple(() =>
            {
                Assert.Equal(resultL, GetVectorE0(context.GetV(0)));
                Assert.Equal(resultH, GetVectorE1(context.GetV(0)));
            });
            Assert.Multiple(() =>
            {
                Assert.Equal(roundKeyL, GetVectorE0(context.GetV(1)));
                Assert.Equal(roundKeyH, GetVectorE1(context.GetV(1)));
            });

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "AESE <Vd>.16B, <Vn>.16B")]
        [CombinatorialData]
        public void Aese_V([CombinatorialValues(0u)] uint rd,
                           [CombinatorialValues(1u)] uint rn,
                           [CombinatorialValues(0x7B5B546573745665ul)] ulong valueH,
                           [CombinatorialValues(0x63746F725D53475Dul)] ulong valueL,
                           [CombinatorialMemberData(nameof(RandomRoundKeysH))] ulong roundKeyH,
                           [CombinatorialMemberData(nameof(RandomRoundKeysL))] ulong roundKeyL,
                           [CombinatorialValues(0x8F92A04DFBED204Dul)] ulong resultH,
                           [CombinatorialValues(0x4C39B1402192A84Cul)] ulong resultL)
        {
            uint opcode = 0x4E284800; // AESE V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(roundKeyL ^ valueL, roundKeyH ^ valueH);
            V128 v1 = MakeVectorE0E1(roundKeyL, roundKeyH);

            ExecutionContext context = SingleOpcode(opcode, v0: v0, v1: v1);

            Assert.Multiple(() =>
            {
                Assert.Equal(resultL, GetVectorE0(context.GetV(0)));
                Assert.Equal(resultH, GetVectorE1(context.GetV(0)));
            });
            Assert.Multiple(() =>
            {
                Assert.Equal(roundKeyL, GetVectorE0(context.GetV(1)));
                Assert.Equal(roundKeyH, GetVectorE1(context.GetV(1)));
            });

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "AESIMC <Vd>.16B, <Vn>.16B")]
        [CombinatorialData]
        public void Aesimc_V([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(0x8DCAB9DC035006BCul)] ulong valueH,
                             [CombinatorialValues(0x8F57161E00CAFD8Dul)] ulong valueL,
                             [CombinatorialValues(0xD635A667928B5EAEul)] ulong resultH,
                             [CombinatorialValues(0xEEC9CC3BC55F5777ul)] ulong resultL)
        {
            uint opcode = 0x4E287800; // AESIMC V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v = MakeVectorE0E1(valueL, valueH);

            ExecutionContext context = SingleOpcode(
                opcode,
                v0: rn == 0u ? v : default,
                v1: rn == 1u ? v : default);

            Assert.Multiple(() =>
            {
                Assert.Equal(resultL, GetVectorE0(context.GetV(0)));
                Assert.Equal(resultH, GetVectorE1(context.GetV(0)));
            });
            if (rn == 1u)
            {
                Assert.Multiple(() =>
                {
                    Assert.Equal(valueL, GetVectorE0(context.GetV(1)));
                    Assert.Equal(valueH, GetVectorE1(context.GetV(1)));
                });
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "AESMC <Vd>.16B, <Vn>.16B")]
        [CombinatorialData]
        public void Aesmc_V([CombinatorialValues(0u)] uint rd,
                            [CombinatorialValues(1u, 0u)] uint rn,
                            [CombinatorialValues(0x627A6F6644B109C8ul)] ulong valueH,
                            [CombinatorialValues(0x2B18330A81C3B3E5ul)] ulong valueL,
                            [CombinatorialValues(0x7B5B546573745665ul)] ulong resultH,
                            [CombinatorialValues(0x63746F725D53475Dul)] ulong resultL)
        {
            uint opcode = 0x4E286800; // AESMC V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v = MakeVectorE0E1(valueL, valueH);

            ExecutionContext context = SingleOpcode(
                opcode,
                v0: rn == 0u ? v : default,
                v1: rn == 1u ? v : default);

            Assert.Multiple(() =>
            {
                Assert.Equal(resultL, GetVectorE0(context.GetV(0)));
                Assert.Equal(resultH, GetVectorE1(context.GetV(0)));
            });
            if (rn == 1u)
            {
                Assert.Multiple(() =>
                {
                    Assert.Equal(valueL, GetVectorE0(context.GetV(1)));
                    Assert.Equal(valueH, GetVectorE1(context.GetV(1)));
                });
            }

            CompareAgainstUnicorn();
        }
#endif
    }
}
