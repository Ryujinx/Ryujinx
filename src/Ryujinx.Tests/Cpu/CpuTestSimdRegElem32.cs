#define SimdRegElem32

using ARMeilleure.State;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdRegElem32")]
    public sealed class CpuTestSimdRegElem32 : CpuTest32
    {
        public CpuTestSimdRegElem32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdRegElem32
        private const int RndCnt = 2;

        [Theory(DisplayName = "VMUL.<size> {<Vd>}, <Vn>, <Vm>[<index>]")]
        [PairwiseData]
        public void Vmul_1I([CombinatorialValues(1u, 0u)] uint rd,
                            [CombinatorialValues(1u, 0u)] uint rn,
                            [CombinatorialValues(26u, 25u, 10u, 9u, 2u, 0u)] uint rm,
                            [CombinatorialValues(1u, 2u)] uint size,
                            [CombinatorialRandomData(Count = RndCnt)] ulong z,
                            [CombinatorialRandomData(Count = RndCnt)] ulong a,
                            [CombinatorialRandomData(Count = RndCnt)] ulong b,
                            bool q)
        {
            uint opcode = 0xf2900840u & ~(3u << 20); // VMUL.I16 D0, D0, D0[0]
            if (q)
            {
                opcode |= 1 << 24;
                rn <<= 1;
                rd <<= 1;
            }

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);

            opcode |= size << 20;

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VMULL.<size> <Vd>, <Vn>, <Vm>[<index>]")]
        [PairwiseData]
        public void Vmull_1([CombinatorialValues(2u, 0u)] uint rd,
                            [CombinatorialValues(1u, 0u)] uint rn,
                            [CombinatorialValues(26u, 25u, 10u, 9u, 2u, 0u)] uint rm,
                            [CombinatorialValues(1u, 2u)] uint size,
                            [CombinatorialRandomData(Count = RndCnt)] ulong z,
                            [CombinatorialRandomData(Count = RndCnt)] ulong a,
                            [CombinatorialRandomData(Count = RndCnt)] ulong b,
                            bool u)
        {
            uint opcode = 0xf2900a40u & ~(3u << 20); // VMULL.S16 Q0, D0, D0[0]

            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);

            opcode |= size << 20;

            if (u)
            {
                opcode |= 1 << 24;
            }

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);
            V128 v2 = MakeVectorE0E1(b, z);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }
#endif
    }
}
