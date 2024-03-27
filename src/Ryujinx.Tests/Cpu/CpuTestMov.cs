#define Mov

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Mov")]
    public sealed class CpuTestMov : CpuTest
    {
        public CpuTestMov(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Mov
        private const int RndCnt = 2;

        [SkippableTheory(DisplayName = "MOVK <Xd>, #<imm>{, LSL #<shift>}")]
        [PairwiseData]
        public void Movk_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialRandomData(Count = RndCnt)] ulong xd,
                               [CombinatorialValues(0u, 65535u)] uint imm,
                               [CombinatorialValues(0u, 16u, 32u, 48u)] uint shift)
        {
            uint opcode = 0xF2800000; // MOVK X0, #0, LSL #0
            opcode |= ((rd & 31) << 0);
            opcode |= (((shift / 16) & 3) << 21) | ((imm & 65535) << 5);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x0: xd, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "MOVK <Wd>, #<imm>{, LSL #<shift>}")]
        [PairwiseData]
        public void Movk_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialRandomData(Count = RndCnt)] uint wd,
                               [CombinatorialValues(0u, 65535u)] uint imm,
                               [CombinatorialValues(0u, 16u)] uint shift)
        {
            uint opcode = 0x72800000; // MOVK W0, #0, LSL #0
            opcode |= ((rd & 31) << 0);
            opcode |= (((shift / 16) & 3) << 21) | ((imm & 65535) << 5);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x0: wd, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "MOVN <Xd>, #<imm>{, LSL #<shift>}")]
        [PairwiseData]
        public void Movn_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(0u, 65535u)] uint imm,
                               [CombinatorialValues(0u, 16u, 32u, 48u)] uint shift)
        {
            uint opcode = 0x92800000; // MOVN X0, #0, LSL #0
            opcode |= ((rd & 31) << 0);
            opcode |= (((shift / 16) & 3) << 21) | ((imm & 65535) << 5);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "MOVN <Wd>, #<imm>{, LSL #<shift>}")]
        [PairwiseData]
        public void Movn_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(0u, 65535u)] uint imm,
                               [CombinatorialValues(0u, 16u)] uint shift)
        {
            uint opcode = 0x12800000; // MOVN W0, #0, LSL #0
            opcode |= ((rd & 31) << 0);
            opcode |= (((shift / 16) & 3) << 21) | ((imm & 65535) << 5);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "MOVZ <Xd>, #<imm>{, LSL #<shift>}")]
        [PairwiseData]
        public void Movz_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(0u, 65535u)] uint imm,
                               [CombinatorialValues(0u, 16u, 32u, 48u)] uint shift)
        {
            uint opcode = 0xD2800000; // MOVZ X0, #0, LSL #0
            opcode |= ((rd & 31) << 0);
            opcode |= (((shift / 16) & 3) << 21) | ((imm & 65535) << 5);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "MOVZ <Wd>, #<imm>{, LSL #<shift>}")]
        [PairwiseData]
        public void Movz_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(0u, 65535u)] uint imm,
                               [CombinatorialValues(0u, 16u)] uint shift)
        {
            uint opcode = 0x52800000; // MOVZ W0, #0, LSL #0
            opcode |= ((rd & 31) << 0);
            opcode |= (((shift / 16) & 3) << 21) | ((imm & 65535) << 5);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x31: w31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
