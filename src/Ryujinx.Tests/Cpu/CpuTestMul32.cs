#define Mul32

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Mul32")]
    public sealed class CpuTestMul32 : CpuTest32
    {
        public CpuTestMul32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Mul32

        #region "ValueSource (Opcodes)"
        private static uint[] _Smlabb_Smlabt_Smlatb_Smlatt_()
        {
            return new[]
            {
                0xe1000080u, // SMLABB R0, R0, R0, R0
                0xe10000C0u, // SMLABT R0, R0, R0, R0
                0xe10000A0u, // SMLATB R0, R0, R0, R0
                0xe10000E0u, // SMLATT R0, R0, R0, R0
            };
        }

        private static uint[] _Smlawb_Smlawt_()
        {
            return new[]
            {
                0xe1200080u, // SMLAWB R0, R0, R0, R0
                0xe12000C0u, // SMLAWT R0, R0, R0, R0
            };
        }

        private static uint[] _Smulbb_Smulbt_Smultb_Smultt_()
        {
            return new[]
            {
                0xe1600080u, // SMULBB R0, R0, R0
                0xe16000C0u, // SMULBT R0, R0, R0
                0xe16000A0u, // SMULTB R0, R0, R0
                0xe16000E0u, // SMULTT R0, R0, R0
            };
        }

        private static uint[] _Smulwb_Smulwt_()
        {
            return new[]
            {
                0xe12000a0u, // SMULWB R0, R0, R0
                0xe12000e0u, // SMULWT R0, R0, R0
            };
        }
        #endregion

        [Theory(DisplayName = "SMLA<x><y> <Rd>, <Rn>, <Rm>, <Ra>")]
        [PairwiseData]
        public void Smla___32bit([CombinatorialMemberData(nameof(_Smlabb_Smlabt_Smlatb_Smlatt_))] uint opcode,
                                 [CombinatorialValues(0u, 0xdu)] uint rn,
                                 [CombinatorialValues(1u, 0xdu)] uint rm,
                                 [CombinatorialValues(2u, 0xdu)] uint ra,
                                 [CombinatorialValues(3u, 0xdu)] uint rd,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wn,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wm,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wa)
        {
            opcode |= ((rn & 15) << 0) | ((rm & 15) << 8) | ((ra & 15) << 12) | ((rd & 15) << 16);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: wn, r1: wm, r2: wa, sp: w31);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "SMLAW<x> <Rd>, <Rn>, <Rm>, <Ra>")]
        [PairwiseData]
        public void Smlaw__32bit([CombinatorialMemberData(nameof(_Smlawb_Smlawt_))] uint opcode,
                                 [CombinatorialValues(0u, 0xdu)] uint rn,
                                 [CombinatorialValues(1u, 0xdu)] uint rm,
                                 [CombinatorialValues(2u, 0xdu)] uint ra,
                                 [CombinatorialValues(3u, 0xdu)] uint rd,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wn,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wm,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wa)
        {
            opcode |= ((rn & 15) << 0) | ((rm & 15) << 8) | ((ra & 15) << 12) | ((rd & 15) << 16);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: wn, r1: wm, r2: wa, sp: w31);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "SMUL<x><y> <Rd>, <Rn>, <Rm>")]
        [PairwiseData]
        public void Smul___32bit([CombinatorialMemberData(nameof(_Smulbb_Smulbt_Smultb_Smultt_))] uint opcode,
                                 [CombinatorialValues(0u, 0xdu)] uint rn,
                                 [CombinatorialValues(1u, 0xdu)] uint rm,
                                 [CombinatorialValues(2u, 0xdu)] uint rd,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wn,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wm)
        {
            opcode |= ((rn & 15) << 0) | ((rm & 15) << 8) | ((rd & 15) << 16);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: wn, r1: wm, sp: w31);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "SMULW<x> <Rd>, <Rn>, <Rm>")]
        [PairwiseData]
        public void Smulw__32bit([CombinatorialMemberData(nameof(_Smulwb_Smulwt_))] uint opcode,
                                 [CombinatorialValues(0u, 0xdu)] uint rn,
                                 [CombinatorialValues(1u, 0xdu)] uint rm,
                                 [CombinatorialValues(2u, 0xdu)] uint rd,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wn,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wm)
        {
            opcode |= ((rn & 15) << 0) | ((rm & 15) << 8) | ((rd & 15) << 16);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, r0: wn, r1: wm, sp: w31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
