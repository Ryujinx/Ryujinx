#define SimdExt

using ARMeilleure.State;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdExt")]
    public sealed class CpuTestSimdExt : CpuTest
    {
        public CpuTestSimdExt(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdExt

        #region "ValueSource"
        private static ulong[] _8B_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }
        #endregion

        [SkippableTheory(DisplayName = "EXT <Vd>.8B, <Vn>.8B, <Vm>.8B, #<index>")]
        [PairwiseData]
        public void Ext_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialValues(2u, 0u)] uint rm,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a,
                             [CombinatorialMemberData(nameof(_8B_))] ulong b,
                             [CombinatorialValues(0u, 7u)] uint index)
        {
            uint imm4 = index & 0x7u;

            uint opcode = 0x2E000000; // EXT V0.8B, V0.8B, V0.8B, #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm4 << 11);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);
            V128 v2 = MakeVectorE0(b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "EXT <Vd>.16B, <Vn>.16B, <Vm>.16B, #<index>")]
        [PairwiseData]
        public void Ext_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialValues(2u, 0u)] uint rm,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a,
                              [CombinatorialMemberData(nameof(_8B_))] ulong b,
                              [CombinatorialValues(0u, 15u)] uint index)
        {
            uint imm4 = index & 0xFu;

            uint opcode = 0x6E000000; // EXT V0.16B, V0.16B, V0.16B, #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= (imm4 << 11);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);
            V128 v2 = MakeVectorE0E1(b, b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }
#endif
    }
}
