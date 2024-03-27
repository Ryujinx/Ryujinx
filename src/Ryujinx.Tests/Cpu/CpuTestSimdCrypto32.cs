#define SimdCrypto32
// https://www.intel.com/content/dam/doc/white-paper/advanced-encryption-standard-new-instructions-set-paper.pdf

using ARMeilleure.State;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    public class CpuTestSimdCrypto32 : CpuTest32
    {
        public CpuTestSimdCrypto32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdCrypto32

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

        [Theory(DisplayName = "AESD.8 <Qd>, <Qm>")]
        [CombinatorialData]
        public void Aesd_V([CombinatorialValues(0u)] uint rd,
                           [CombinatorialValues(2u)] uint rm,
                           [CombinatorialValues(0x7B5B546573745665ul)] ulong valueH,
                           [CombinatorialValues(0x63746F725D53475Dul)] ulong valueL,
                           [CombinatorialMemberData(nameof(RandomRoundKeysH))] ulong roundKeyH,
                           [CombinatorialMemberData(nameof(RandomRoundKeysL))] ulong roundKeyL,
                           [CombinatorialValues(0x8DCAB9BC035006BCul)] ulong resultH,
                           [CombinatorialValues(0x8F57161E00CAFD8Dul)] ulong resultL)
        {
            uint opcode = 0xf3b00340; // AESD.8 Q0, Q0
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);

            V128 v0 = MakeVectorE0E1(roundKeyL ^ valueL, roundKeyH ^ valueH);
            V128 v1 = MakeVectorE0E1(roundKeyL, roundKeyH);

            ExecutionContext context = SingleOpcode(opcode, v0: v0, v1: v1, runUnicorn: false);

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

            // Unicorn does not yet support crypto instructions in A32.
            // CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "AESE.8 <Qd>, <Qm>")]
        [CombinatorialData]
        public void Aese_V([CombinatorialValues(0u)] uint rd,
                           [CombinatorialValues(2u)] uint rm,
                           [CombinatorialValues(0x7B5B546573745665ul)] ulong valueH,
                           [CombinatorialValues(0x63746F725D53475Dul)] ulong valueL,
                           [CombinatorialMemberData(nameof(RandomRoundKeysH))] ulong roundKeyH,
                           [CombinatorialMemberData(nameof(RandomRoundKeysL))] ulong roundKeyL,
                           [CombinatorialValues(0x8F92A04DFBED204Dul)] ulong resultH,
                           [CombinatorialValues(0x4C39B1402192A84Cul)] ulong resultL)
        {
            uint opcode = 0xf3b00300; // AESE.8 Q0, Q0
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);

            V128 v0 = MakeVectorE0E1(roundKeyL ^ valueL, roundKeyH ^ valueH);
            V128 v1 = MakeVectorE0E1(roundKeyL, roundKeyH);

            ExecutionContext context = SingleOpcode(opcode, v0: v0, v1: v1, runUnicorn: false);

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

            // Unicorn does not yet support crypto instructions in A32.
            // CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "AESIMC.8 <Qd>, <Qm>")]
        [CombinatorialData]
        public void Aesimc_V([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialValues(0x8DCAB9DC035006BCul)] ulong valueH,
                             [CombinatorialValues(0x8F57161E00CAFD8Dul)] ulong valueL,
                             [CombinatorialValues(0xD635A667928B5EAEul)] ulong resultH,
                             [CombinatorialValues(0xEEC9CC3BC55F5777ul)] ulong resultL)
        {
            uint opcode = 0xf3b003c0; // AESIMC.8 Q0, Q0
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);

            V128 v = MakeVectorE0E1(valueL, valueH);

            ExecutionContext context = SingleOpcode(
                opcode,
                v0: rm == 0u ? v : default,
                v1: rm == 2u ? v : default,
                runUnicorn: false);

            Assert.Multiple(() =>
            {
                Assert.Equal(resultL, GetVectorE0(context.GetV(0)));
                Assert.Equal(resultH, GetVectorE1(context.GetV(0)));
            });
            if (rm == 2u)
            {
                Assert.Multiple(() =>
                {
                    Assert.Equal(valueL, GetVectorE0(context.GetV(1)));
                    Assert.Equal(valueH, GetVectorE1(context.GetV(1)));
                });
            }

            // Unicorn does not yet support crypto instructions in A32.
            // CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "AESMC.8 <Qd>, <Qm>")]
        [CombinatorialData]
        public void Aesmc_V([CombinatorialValues(0u)] uint rd,
                            [CombinatorialValues(2u, 0u)] uint rm,
                            [CombinatorialValues(0x627A6F6644B109C8ul)] ulong valueH,
                            [CombinatorialValues(0x2B18330A81C3B3E5ul)] ulong valueL,
                            [CombinatorialValues(0x7B5B546573745665ul)] ulong resultH,
                            [CombinatorialValues(0x63746F725D53475Dul)] ulong resultL)
        {
            uint opcode = 0xf3b00380; // AESMC.8 Q0, Q0
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);

            V128 v = MakeVectorE0E1(valueL, valueH);

            ExecutionContext context = SingleOpcode(
                opcode,
                v0: rm == 0u ? v : default,
                v1: rm == 2u ? v : default,
                runUnicorn: false);

            Assert.Multiple(() =>
            {
                Assert.Equal(resultL, GetVectorE0(context.GetV(0)));
                Assert.Equal(resultH, GetVectorE1(context.GetV(0)));
            });
            if (rm == 2u)
            {
                Assert.Multiple(() =>
                {
                    Assert.Equal(valueL, GetVectorE0(context.GetV(1)));
                    Assert.Equal(valueH, GetVectorE1(context.GetV(1)));
                });
            }

            // Unicorn does not yet support crypto instructions in A32.
            // CompareAgainstUnicorn();
        }
#endif
    }
}
