#define SimdRegElem

using ARMeilleure.State;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdRegElem")]
    public sealed class CpuTestSimdRegElem : CpuTest
    {
        public CpuTestSimdRegElem(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if SimdRegElem

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
        #endregion

        #region "ValueSource (Opcodes)"
        private static uint[] _Mla_Mls_Mul_Sqdmulh_Sqrdmulh_Ve_4H_8H_()
        {
            return new[]
            {
                0x2F400000u, // MLA      V0.4H, V0.4H, V0.H[0]
                0x2F404000u, // MLS      V0.4H, V0.4H, V0.H[0]
                0x0F408000u, // MUL      V0.4H, V0.4H, V0.H[0]
                0x0F40C000u, // SQDMULH  V0.4H, V0.4H, V0.H[0]
                0x0F40D000u, // SQRDMULH V0.4H, V0.4H, V0.H[0]
            };
        }

        private static uint[] _Mla_Mls_Mul_Sqdmulh_Sqrdmulh_Ve_2S_4S_()
        {
            return new[]
            {
                0x2F800000u, // MLA      V0.2S, V0.2S, V0.S[0]
                0x2F804000u, // MLS      V0.2S, V0.2S, V0.S[0]
                0x0F808000u, // MUL      V0.2S, V0.2S, V0.S[0]
                0x0F80C000u, // SQDMULH  V0.2S, V0.2S, V0.S[0]
                0x0F80D000u, // SQRDMULH V0.2S, V0.2S, V0.S[0]
            };
        }

        private static uint[] _SU_Mlal_Mlsl_Mull_Ve_4H4S_8H4S_()
        {
            return new[]
            {
                0x0F402000u, // SMLAL V0.4S, V0.4H, V0.H[0]
                0x0F406000u, // SMLSL V0.4S, V0.4H, V0.H[0]
                0x0F40A000u, // SMULL V0.4S, V0.4H, V0.H[0]
                0x2F402000u, // UMLAL V0.4S, V0.4H, V0.H[0]
                0x2F406000u, // UMLSL V0.4S, V0.4H, V0.H[0]
                0x2F40A000u, // UMULL V0.4S, V0.4H, V0.H[0]
            };
        }

        private static uint[] _SU_Mlal_Mlsl_Mull_Ve_2S2D_4S2D_()
        {
            return new[]
            {
                0x0F802000u, // SMLAL V0.2D, V0.2S, V0.S[0]
                0x0F806000u, // SMLSL V0.2D, V0.2S, V0.S[0]
                0x0F80A000u, // SMULL V0.2D, V0.2S, V0.S[0]
                0x2F802000u, // UMLAL V0.2D, V0.2S, V0.S[0]
                0x2F806000u, // UMLSL V0.2D, V0.2S, V0.S[0]
                0x2F80A000u, // UMULL V0.2D, V0.2S, V0.S[0]
            };
        }
        #endregion


        [SkippableTheory]
        [PairwiseData]
        public void Mla_Mls_Mul_Sqdmulh_Sqrdmulh_Ve_4H_8H([CombinatorialMemberData(nameof(_Mla_Mls_Mul_Sqdmulh_Sqrdmulh_Ve_4H_8H_))] uint opcodes,
                                                          [CombinatorialValues(0u)] uint rd,
                                                          [CombinatorialValues(1u, 0u)] uint rn,
                                                          [CombinatorialValues(2u, 0u)] uint rm,
                                                          [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                                          [CombinatorialMemberData(nameof(_4H_))] ulong a,
                                                          [CombinatorialMemberData(nameof(_4H_))] ulong b,
                                                          [CombinatorialValues(0u, 7u)] uint index,
                                                          [CombinatorialValues(0b0u, 0b1u)] uint q) // <4H, 8H>
        {
            uint h = (index >> 2) & 1;
            uint l = (index >> 1) & 1;
            uint m = index & 1;

            opcodes |= ((rm & 15) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (l << 21) | (m << 20) | (h << 11);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);
            V128 v2 = MakeVectorE0E1(b, b * h);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void Mla_Mls_Mul_Sqdmulh_Sqrdmulh_Ve_2S_4S([CombinatorialMemberData(nameof(_Mla_Mls_Mul_Sqdmulh_Sqrdmulh_Ve_2S_4S_))] uint opcodes,
                                                          [CombinatorialValues(0u)] uint rd,
                                                          [CombinatorialValues(1u, 0u)] uint rn,
                                                          [CombinatorialValues(2u, 0u)] uint rm,
                                                          [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                                          [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                                          [CombinatorialMemberData(nameof(_2S_))] ulong b,
                                                          [CombinatorialValues(0u, 1u, 2u, 3u)] uint index,
                                                          [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            uint h = (index >> 1) & 1;
            uint l = index & 1;

            opcodes |= ((rm & 15) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (l << 21) | (h << 11);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);
            V128 v2 = MakeVectorE0E1(b, b * h);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Mlal_Mlsl_Mull_Ve_4H4S_8H4S([CombinatorialMemberData(nameof(_SU_Mlal_Mlsl_Mull_Ve_4H4S_8H4S_))] uint opcodes,
                                                   [CombinatorialValues(0u)] uint rd,
                                                   [CombinatorialValues(1u, 0u)] uint rn,
                                                   [CombinatorialValues(2u, 0u)] uint rm,
                                                   [CombinatorialMemberData(nameof(_4H_))] ulong z,
                                                   [CombinatorialMemberData(nameof(_4H_))] ulong a,
                                                   [CombinatorialMemberData(nameof(_4H_))] ulong b,
                                                   [CombinatorialValues(0u, 7u)] uint index,
                                                   [CombinatorialValues(0b0u, 0b1u)] uint q) // <4H4S, 8H4S>
        {
            uint h = (index >> 2) & 1;
            uint l = (index >> 1) & 1;
            uint m = index & 1;

            opcodes |= ((rm & 15) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (l << 21) | (m << 20) | (h << 11);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(q == 0u ? a : 0ul, q == 1u ? a : 0ul);
            V128 v2 = MakeVectorE0E1(b, b * h);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Mlal_Mlsl_Mull_Ve_2S2D_4S2D([CombinatorialMemberData(nameof(_SU_Mlal_Mlsl_Mull_Ve_2S2D_4S2D_))] uint opcodes,
                                                   [CombinatorialValues(0u)] uint rd,
                                                   [CombinatorialValues(1u, 0u)] uint rn,
                                                   [CombinatorialValues(2u, 0u)] uint rm,
                                                   [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                                   [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                                   [CombinatorialMemberData(nameof(_2S_))] ulong b,
                                                   [CombinatorialValues(0u, 1u, 2u, 3u)] uint index,
                                                   [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S2D, 4S2D>
        {
            uint h = (index >> 1) & 1;
            uint l = index & 1;

            opcodes |= ((rm & 15) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= (l << 21) | (h << 11);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(q == 0u ? a : 0ul, q == 1u ? a : 0ul);
            V128 v2 = MakeVectorE0E1(b, b * h);

            SingleOpcode(opcodes, v0: v0, v1: v1, v2: v2);

            CompareAgainstUnicorn();
        }
#endif
    }
}
