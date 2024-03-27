#define Mul

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Mul")]
    public sealed class CpuTestMul : CpuTest
    {
        public CpuTestMul(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Mul
        [SkippableTheory(DisplayName = "MADD <Xd>, <Xn>, <Xm>, <Xa>")]
        [PairwiseData]
        public void Madd_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(3u, 31u)] uint ra,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xa)
        {
            uint opcode = 0x9B000000; // MADD X0, X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((ra & 31) << 10) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x3: xa, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "MADD <Wd>, <Wn>, <Wm>, <Wa>")]
        [PairwiseData]
        public void Madd_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(3u, 31u)] uint ra,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wa)
        {
            uint opcode = 0x1B000000; // MADD W0, W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((ra & 31) << 10) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x3: wa, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "MSUB <Xd>, <Xn>, <Xm>, <Xa>")]
        [PairwiseData]
        public void Msub_64bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(3u, 31u)] uint ra,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm,
                               [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                       0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xa)
        {
            uint opcode = 0x9B008000; // MSUB X0, X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((ra & 31) << 10) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x3: xa, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "MSUB <Wd>, <Wn>, <Wm>, <Wa>")]
        [PairwiseData]
        public void Msub_32bit([CombinatorialValues(0u, 31u)] uint rd,
                               [CombinatorialValues(1u, 31u)] uint rn,
                               [CombinatorialValues(2u, 31u)] uint rm,
                               [CombinatorialValues(3u, 31u)] uint ra,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wn,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wm,
                               [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                       0x80000000u, 0xFFFFFFFFu)] uint wa)
        {
            uint opcode = 0x1B008000; // MSUB W0, W0, W0, W0
            opcode |= ((rm & 31) << 16) | ((ra & 31) << 10) | ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();

            SingleOpcode(opcode, x1: wn, x2: wm, x3: wa, x31: w31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SMADDL <Xd>, <Wn>, <Wm>, <Xa>")]
        [PairwiseData]
        public void Smaddl_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(3u, 31u)] uint ra,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wn,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xa)
        {
            uint opcode = 0x9B200000; // SMADDL X0, W0, W0, X0
            opcode |= ((rm & 31) << 16) | ((ra & 31) << 10) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: wn, x2: wm, x3: xa, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UMADDL <Xd>, <Wn>, <Wm>, <Xa>")]
        [PairwiseData]
        public void Umaddl_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(3u, 31u)] uint ra,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wn,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xa)
        {
            uint opcode = 0x9BA00000; // UMADDL X0, W0, W0, X0
            opcode |= ((rm & 31) << 16) | ((ra & 31) << 10) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: wn, x2: wm, x3: xa, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SMSUBL <Xd>, <Wn>, <Wm>, <Xa>")]
        [PairwiseData]
        public void Smsubl_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(3u, 31u)] uint ra,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wn,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xa)
        {
            uint opcode = 0x9B208000; // SMSUBL X0, W0, W0, X0
            opcode |= ((rm & 31) << 16) | ((ra & 31) << 10) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: wn, x2: wm, x3: xa, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UMSUBL <Xd>, <Wn>, <Wm>, <Xa>")]
        [PairwiseData]
        public void Umsubl_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(3u, 31u)] uint ra,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wn,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xa)
        {
            uint opcode = 0x9BA08000; // UMSUBL X0, W0, W0, X0
            opcode |= ((rm & 31) << 16) | ((ra & 31) << 10) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: wn, x2: wm, x3: xa, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SMULH <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Smulh_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm)
        {
            uint opcode = 0x9B407C00; // SMULH X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UMULH <Xd>, <Xn>, <Xm>")]
        [PairwiseData]
        public void Umulh_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xn,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xm)
        {
            uint opcode = 0x9BC07C00; // UMULH X0, X0, X0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();

            SingleOpcode(opcode, x1: xn, x2: xm, x31: x31);

            CompareAgainstUnicorn();
        }
#endif
    }
}
