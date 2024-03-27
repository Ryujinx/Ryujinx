#define AluRx

using System;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("AluRx")]
    public sealed class CpuTestAluRx : CpuTest
    {
        public CpuTestAluRx(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if AluRx

        [SkippableTheory(DisplayName = "ADD <Xd|SP>, <Xn|SP>, <X><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Add_X_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                [CombinatorialValues((ulong)0x0000000000000000, (ulong)0x7FFFFFFFFFFFFFFF,
                                        0x8000000000000000, 0xFFFFFFFFFFFFFFFF)] ulong xm,
                                [CombinatorialValues(0b011u, 0b111u)] uint extend, // <LSL|UXTX, SXTX>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x8B206000; // ADD X0, X0, X0, UXTX #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                ulong x31 = Random.Shared.NextULong();

                SingleOpcode(opcode, x1: xnSp, x2: xm, x31: x31);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp, x2: xm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Xd|SP>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Add_W_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                [CombinatorialValues((uint)0x00000000, (uint)0x7FFFFFFF,
                                        0x80000000, 0xFFFFFFFF)] uint wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                        0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x8B200000; // ADD X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                ulong x31 = Random.Shared.NextULong();

                SingleOpcode(opcode, x1: xnSp, x2: wm, x31: x31);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Xd|SP>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Add_H_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                [CombinatorialValues((ushort)0x0000, (ushort)0x7FFF,
                                        (ushort)0x8000, (ushort)0xFFFF)] ushort wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                        0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x8B200000; // ADD X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                ulong x31 = Random.Shared.NextULong();

                SingleOpcode(opcode, x1: xnSp, x2: wm, x31: x31);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Xd|SP>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Add_B_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                [CombinatorialValues((byte)0x00, (byte)0x7F,
                                        (byte)0x80, (byte)0xFF)] byte wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                        0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x8B200000; // ADD X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                ulong x31 = Random.Shared.NextULong();

                SingleOpcode(opcode, x1: xnSp, x2: wm, x31: x31);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Wd|WSP>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Add_W_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                [CombinatorialValues((uint)0x00000000, (uint)0x7FFFFFFF,
                                        0x80000000, 0xFFFFFFFF)] uint wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                        0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x0B200000; // ADD W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                uint w31 = Random.Shared.NextUInt();

                SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: w31);
            }
            else
            {
                SingleOpcode(opcode, x31: wnWsp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Wd|WSP>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Add_H_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                [CombinatorialValues((ushort)0x0000, (ushort)0x7FFF,
                                        (ushort)0x8000, (ushort)0xFFFF)] ushort wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                        0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x0B200000; // ADD W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                uint w31 = Random.Shared.NextUInt();

                SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: w31);
            }
            else
            {
                SingleOpcode(opcode, x31: wnWsp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADD <Wd|WSP>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Add_B_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                [CombinatorialValues((byte)0x00, (byte)0x7F,
                                        (byte)0x80, (byte)0xFF)] byte wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                        0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x0B200000; // ADD W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                uint w31 = Random.Shared.NextUInt();

                SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: w31);
            }
            else
            {
                SingleOpcode(opcode, x31: wnWsp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Xd>, <Xn|SP>, <X><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Adds_X_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                 [CombinatorialValues((ulong)0x0000000000000000, (ulong)0x7FFFFFFFFFFFFFFF,
                                         0x8000000000000000, 0xFFFFFFFFFFFFFFFF)] ulong xm,
                                 [CombinatorialValues(0b011u, 0b111u)] uint extend, // <LSL|UXTX, SXTX>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xAB206000; // ADDS X0, X0, X0, UXTX #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: xnSp, x2: xm, x31: xnSp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Xd>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Adds_W_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                 [CombinatorialValues((uint)0x00000000, (uint)0x7FFFFFFF,
                                         0x80000000, 0xFFFFFFFF)] uint wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                         0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xAB200000; // ADDS X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: xnSp, x2: wm, x31: xnSp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Xd>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Adds_H_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                 [CombinatorialValues((ushort)0x0000, (ushort)0x7FFF,
                                         (ushort)0x8000, (ushort)0xFFFF)] ushort wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                         0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xAB200000; // ADDS X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: xnSp, x2: wm, x31: xnSp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Xd>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Adds_B_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                 [CombinatorialValues((byte)0x00, (byte)0x7F,
                                         (byte)0x80, (byte)0xFF)] byte wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                         0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xAB200000; // ADDS X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: xnSp, x2: wm, x31: xnSp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Wd>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Adds_W_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                 [CombinatorialValues((uint)0x00000000, (uint)0x7FFFFFFF,
                                         0x80000000, 0xFFFFFFFF)] uint wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                         0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x2B200000; // ADDS W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: wnWsp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Wd>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Adds_H_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                 [CombinatorialValues((ushort)0x0000, (ushort)0x7FFF,
                                         (ushort)0x8000, (ushort)0xFFFF)] ushort wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                         0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x2B200000; // ADDS W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: wnWsp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDS <Wd>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Adds_B_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                 [CombinatorialValues((byte)0x00, (byte)0x7F,
                                         (byte)0x80, (byte)0xFF)] byte wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                         0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x2B200000; // ADDS W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: wnWsp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Xd|SP>, <Xn|SP>, <X><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Sub_X_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                [CombinatorialValues((ulong)0x0000000000000000, (ulong)0x7FFFFFFFFFFFFFFF,
                                        0x8000000000000000, 0xFFFFFFFFFFFFFFFF)] ulong xm,
                                [CombinatorialValues(0b011u, 0b111u)] uint extend, // <LSL|UXTX, SXTX>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xCB206000; // SUB X0, X0, X0, UXTX #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                ulong x31 = Random.Shared.NextULong();

                SingleOpcode(opcode, x1: xnSp, x2: xm, x31: x31);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp, x2: xm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Xd|SP>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Sub_W_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                [CombinatorialValues((uint)0x00000000, (uint)0x7FFFFFFF,
                                        0x80000000, 0xFFFFFFFF)] uint wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                        0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xCB200000; // SUB X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                ulong x31 = Random.Shared.NextULong();

                SingleOpcode(opcode, x1: xnSp, x2: wm, x31: x31);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Xd|SP>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Sub_H_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                [CombinatorialValues((ushort)0x0000, (ushort)0x7FFF,
                                        (ushort)0x8000, (ushort)0xFFFF)] ushort wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                        0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xCB200000; // SUB X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                ulong x31 = Random.Shared.NextULong();

                SingleOpcode(opcode, x1: xnSp, x2: wm, x31: x31);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Xd|SP>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Sub_B_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                        0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                [CombinatorialValues((byte)0x00, (byte)0x7F,
                                        (byte)0x80, (byte)0xFF)] byte wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                        0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xCB200000; // SUB X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                ulong x31 = Random.Shared.NextULong();

                SingleOpcode(opcode, x1: xnSp, x2: wm, x31: x31);
            }
            else
            {
                SingleOpcode(opcode, x31: xnSp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Wd|WSP>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Sub_W_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                [CombinatorialValues((uint)0x00000000, (uint)0x7FFFFFFF,
                                        0x80000000, 0xFFFFFFFF)] uint wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                        0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x4B200000; // SUB W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                uint w31 = Random.Shared.NextUInt();

                SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: w31);
            }
            else
            {
                SingleOpcode(opcode, x31: wnWsp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Wd|WSP>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Sub_H_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                [CombinatorialValues((ushort)0x0000, (ushort)0x7FFF,
                                        (ushort)0x8000, (ushort)0xFFFF)] ushort wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                        0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x4B200000; // SUB W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                uint w31 = Random.Shared.NextUInt();

                SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: w31);
            }
            else
            {
                SingleOpcode(opcode, x31: wnWsp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUB <Wd|WSP>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Sub_B_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                [CombinatorialValues(1u, 31u)] uint rn,
                                [CombinatorialValues(2u, 31u)] uint rm,
                                [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                        0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                [CombinatorialValues((byte)0x00, (byte)0x7F,
                                        (byte)0x80, (byte)0xFF)] byte wm,
                                [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                        0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x4B200000; // SUB W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            if (rn != 31)
            {
                uint w31 = Random.Shared.NextUInt();

                SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: w31);
            }
            else
            {
                SingleOpcode(opcode, x31: wnWsp, x2: wm);
            }

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Xd>, <Xn|SP>, <X><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Subs_X_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                 [CombinatorialValues((ulong)0x0000000000000000, (ulong)0x7FFFFFFFFFFFFFFF,
                                         0x8000000000000000, 0xFFFFFFFFFFFFFFFF)] ulong xm,
                                 [CombinatorialValues(0b011u, 0b111u)] uint extend, // <LSL|UXTX, SXTX>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xEB206000; // SUBS X0, X0, X0, UXTX #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: xnSp, x2: xm, x31: xnSp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Xd>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Subs_W_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                 [CombinatorialValues((uint)0x00000000, (uint)0x7FFFFFFF,
                                         0x80000000, 0xFFFFFFFF)] uint wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                         0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xEB200000; // SUBS X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: xnSp, x2: wm, x31: xnSp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Xd>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Subs_H_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                 [CombinatorialValues((ushort)0x0000, (ushort)0x7FFF,
                                         (ushort)0x8000, (ushort)0xFFFF)] ushort wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                         0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xEB200000; // SUBS X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: xnSp, x2: wm, x31: xnSp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Xd>, <Xn|SP>, <W><m>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Subs_B_64bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                                         0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul)] ulong xnSp,
                                 [CombinatorialValues((byte)0x00, (byte)0x7F,
                                         (byte)0x80, (byte)0xFF)] byte wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u,               // <UXTB, UXTH, UXTW,
                                         0b100u, 0b101u, 0b110u)] uint extend, //  SXTB, SXTH, SXTW>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0xEB200000; // SUBS X0, X0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: xnSp, x2: wm, x31: xnSp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Wd>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Subs_W_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                 [CombinatorialValues((uint)0x00000000, (uint)0x7FFFFFFF,
                                         0x80000000, 0xFFFFFFFF)] uint wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                         0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x6B200000; // SUBS W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: wnWsp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Wd>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Subs_H_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                 [CombinatorialValues((ushort)0x0000, (ushort)0x7FFF,
                                         (ushort)0x8000, (ushort)0xFFFF)] ushort wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                         0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x6B200000; // SUBS W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: wnWsp);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SUBS <Wd>, <Wn|WSP>, <Wm>{, <extend> {#<amount>}}")]
        [PairwiseData]
        public void Subs_B_32bit([CombinatorialValues(0u, 31u)] uint rd,
                                 [CombinatorialValues(1u, 31u)] uint rn,
                                 [CombinatorialValues(2u, 31u)] uint rm,
                                 [CombinatorialValues(0x00000000u, 0x7FFFFFFFu,
                                         0x80000000u, 0xFFFFFFFFu)] uint wnWsp,
                                 [CombinatorialValues((byte)0x00, (byte)0x7F,
                                         (byte)0x80, (byte)0xFF)] byte wm,
                                 [CombinatorialValues(0b000u, 0b001u, 0b010u, 0b011u,               // <UXTB, UXTH, LSL|UXTW, UXTX,
                                         0b100u, 0b101u, 0b110u, 0b111u)] uint extend, //  SXTB, SXTH, SXTW, SXTX>
                                 [CombinatorialValues(0u, 1u, 2u, 3u, 4u)] uint amount)
        {
            uint opcode = 0x6B200000; // SUBS W0, W0, W0, UXTB #0
            opcode |= ((rm & 31) << 16) | ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((extend & 7) << 13) | ((amount & 7) << 10);

            SingleOpcode(opcode, x1: wnWsp, x2: wm, x31: wnWsp);

            CompareAgainstUnicorn();
        }
#endif
    }
}
