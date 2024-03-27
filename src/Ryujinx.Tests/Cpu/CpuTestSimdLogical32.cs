#define SimdLogical32

using ARMeilleure.State;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdLogical32")]
    public sealed class CpuTestSimdLogical32 : CpuTest32
    {
        public CpuTestSimdLogical32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdLogical32

        #region "ValueSource (Types)"
        private static ulong[] _8B4H2S_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }
        #endregion

        #region "ValueSource (Opcodes)"
        private static uint[] _Vbic_Vbif_Vbit_Vbsl_Vand_Vorn_Vorr_Veor_I_()
        {
            return new[]
            {
                0xf2100110u, // VBIC D0, D0, D0
                0xf3300110u, // VBIF D0, D0, D0
                0xf3200110u, // VBIT D0, D0, D0
                0xf3100110u, // VBSL D0, D0, D0
                0xf2000110u, // VAND D0, D0, D0
                0xf2300110u, // VORN D0, D0, D0
                0xf2200110u, // VORR D0, D0, D0
                0xf3000110u, // VEOR D0, D0, D0
            };
        }

        private static uint[] _Vbic_Vorr_II_()
        {
            return new[]
            {
                0xf2800130u, // VBIC.I32 D0, #0 (A1)
                0xf2800930u, // VBIC.I16 D0, #0 (A2)
                0xf2800110u, // VORR.I32 D0, #0 (A1)
                0xf2800910u, // VORR.I16 D0, #0 (A2)
            };
        }
        #endregion

        [Theory]
        [PairwiseData]
        public void Vbic_Vbif_Vbit_Vbsl_Vand_Vorn_Vorr_Veor_I([CombinatorialMemberData(nameof(_Vbic_Vbif_Vbit_Vbsl_Vand_Vorn_Vorr_Veor_I_))] uint opcode,
                                                              [CombinatorialRange(0u, 5u, 1)] uint rd,
                                                              [CombinatorialRange(0u, 5u, 1)] uint rn,
                                                              [CombinatorialRange(0u, 5u, 1)] uint rm,
                                                              [CombinatorialValues(ulong.MinValue, ulong.MaxValue)] ulong z,
                                                              [CombinatorialValues(ulong.MinValue, ulong.MaxValue)] ulong a,
                                                              [CombinatorialValues(ulong.MinValue, ulong.MaxValue)] ulong b,
                                                              bool q)
        {
            if (q)
            {
                opcode |= 1 << 6;

                rd >>= 1;
                rd <<= 1;
                rn >>= 1;
                rn <<= 1;
                rm >>= 1;
                rm <<= 1;
            }

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [Theory]
        [PairwiseData]
        public void Vbic_Vorr_II([CombinatorialMemberData(nameof(_Vbic_Vorr_II_))] uint opcode,
                                 [CombinatorialValues(0u, 1u)] uint rd,
                                 [CombinatorialValues(ulong.MinValue, ulong.MaxValue)] ulong z,
                                 [CombinatorialValues(byte.MinValue, byte.MaxValue)] byte imm,
                                 [CombinatorialValues(0u, 1u, 2u, 3u)] uint cMode,
                                 bool q)
        {
            if ((opcode & 0x800) != 0) // cmode<3> == '1' (A2)
            {
                cMode &= 1;
            }

            if (q)
            {
                opcode |= 1 << 6;

                rd >>= 1;
                rd <<= 1;
            }

            opcode |= ((uint)imm & 0xf) << 0;
            opcode |= ((uint)imm & 0x70) << 12;
            opcode |= ((uint)imm & 0x80) << 17;
            opcode |= (cMode & 0x3) << 9;
            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);

            V128 v0 = MakeVectorE0E1(z, ~z);

            SingleOpcode(opcode, v0: v0);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VTST.<dt> <Vd>, <Vn>, <Vm>")]
        [PairwiseData]
        public void Vtst([CombinatorialRange(0u, 5u, 1)] uint rd,
                         [CombinatorialRange(0u, 5u, 1)] uint rn,
                         [CombinatorialRange(0u, 5u, 1)] uint rm,
                         [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                         [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                         [CombinatorialMemberData(nameof(_8B4H2S_))] ulong b,
                         [CombinatorialValues(0u, 1u, 2u)] uint size,
                         bool q)
        {
            uint opcode = 0xf2000810u; // VTST.8 D0, D0, D0

            if (q)
            {
                opcode |= 1 << 6;

                rd >>= 1;
                rd <<= 1;
                rn >>= 1;
                rn <<= 1;
                rm >>= 1;
                rm <<= 1;
            }

            opcode |= ((rd & 0xf) << 12) | ((rd & 0x10) << 18);
            opcode |= ((rn & 0xf) << 16) | ((rn & 0x10) << 3);
            opcode |= ((rm & 0xf) << 0) | ((rm & 0x10) << 1);

            opcode |= (size & 0x3) << 20;

            V128 v0 = MakeVectorE0E1(z, ~z);
            V128 v1 = MakeVectorE0E1(a, ~a);
            V128 v2 = MakeVectorE0E1(b, ~b);

            SingleOpcode(opcode, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }
#endif
    }
}
