#define SimdMemory32

using ARMeilleure.State;
using Xunit;
using Ryujinx.Memory;
using System;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("SimdMemory32")]
    public sealed class CpuTestSimdMemory32 : CpuTest32
    {
        public CpuTestSimdMemory32(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        private static readonly uint _testOffset = DataBaseAddress + 0x500;
#if SimdMemory32

        private readonly uint[] _ldStModes =
        {
            // LD1
            0b0111,
            0b1010,
            0b0110,
            0b0010,

            // LD2
            0b1000,
            0b1001,
            0b0011,

            // LD3
            0b0100,
            0b0101,

            // LD4
            0b0000,
            0b0001,
        };

        [Theory(DisplayName = "VLDn.<size> <list>, [<Rn> {:<align>}]{ /!/, <Rm>} (single n element structure)")]
        [PairwiseData]
        public void Vldn_Single([CombinatorialValues(0u, 1u, 2u)] uint size,
                                [CombinatorialValues(0u, 13u)] uint rn,
                                [CombinatorialValues(1u, 13u, 15u)] uint rm,
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u)] uint vd,
                                [CombinatorialRange(0u, 7u, 1)] uint index,
                                [CombinatorialRange(0u, 3u, 1)] uint n,
                                [CombinatorialValues(0x0u)] uint offset)
        {
            var data = GenerateVectorSequence((int)MemoryBlock.GetPageSize());
            SetWorkingMemory(0, data);

            uint opcode = 0xf4a00000u; // VLD1.8 {D0[0]}, [R0], R0

            opcode |= ((size & 3) << 10) | ((rn & 15) << 16) | (rm & 15);

            uint indexAlign = (index << (int)(1 + size)) & 15;

            opcode |= (indexAlign) << 4;

            opcode |= ((vd & 0x10) << 18);
            opcode |= ((vd & 0xf) << 12);

            opcode |= (n & 3) << 8; // LD1 is 0, LD2 is 1 etc.

            SingleOpcode(opcode, r0: _testOffset, r1: offset, sp: _testOffset);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VLDn.<size> <list>, [<Rn> {:<align>}]{ /!/, <Rm>} (all lanes)")]
        [PairwiseData]
        public void Vldn_All([CombinatorialValues(0u, 13u)] uint rn,
                             [CombinatorialValues(1u, 13u, 15u)] uint rm,
                             [CombinatorialValues(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u)] uint vd,
                             [CombinatorialRange(0u, 3u, 1)] uint n,
                             [CombinatorialRange(0u, 2u, 1)] uint size,
                             bool t,
                             [CombinatorialValues(0x0u)] uint offset)
        {
            var data = GenerateVectorSequence((int)MemoryBlock.GetPageSize());
            SetWorkingMemory(0, data);

            uint opcode = 0xf4a00c00u; // VLD1.8 {D0[0]}, [R0], R0

            opcode |= ((size & 3) << 6) | ((rn & 15) << 16) | (rm & 15);

            opcode |= ((vd & 0x10) << 18);
            opcode |= ((vd & 0xf) << 12);

            opcode |= (n & 3) << 8; // LD1 is 0, LD2 is 1 etc.
            if (t)
            {
                opcode |= 1 << 5;
            }

            SingleOpcode(opcode, r0: _testOffset, r1: offset, sp: _testOffset);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VLDn.<size> <list>, [<Rn> {:<align>}]{ /!/, <Rm>} (multiple n element structures)")]
        [PairwiseData]
        public void Vldn_Pair([CombinatorialValues(0u, 1u, 2u, 3u)] uint size,
                              [CombinatorialValues(0u, 13u)] uint rn,
                              [CombinatorialValues(1u, 13u, 15u)] uint rm,
                              [CombinatorialValues(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u)] uint vd,
                              [CombinatorialRange(0u, 10u, 1)] uint mode,
                              [CombinatorialValues(0x0u)] uint offset)
        {
            var data = GenerateVectorSequence((int)MemoryBlock.GetPageSize());
            SetWorkingMemory(0, data);

            uint opcode = 0xf4200000u; // VLD4.8 {D0, D1, D2, D3}, [R0], R0

            if (mode > 3 && size == 3)
            {
                // A size of 3 is only valid for VLD1.
                size = 2;
            }

            opcode |= ((size & 3) << 6) | ((rn & 15) << 16) | (rm & 15) | (_ldStModes[mode] << 8);

            opcode |= ((vd & 0x10) << 18);
            opcode |= ((vd & 0xf) << 12);

            SingleOpcode(opcode, r0: _testOffset, r1: offset, sp: _testOffset);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VSTn.<size> <list>, [<Rn> {:<align>}]{ /!/, <Rm>} (single n element structure)")]
        [PairwiseData]
        public void Vstn_Single([CombinatorialValues(0u, 1u, 2u)] uint size,
                                [CombinatorialValues(0u, 13u)] uint rn,
                                [CombinatorialValues(1u, 13u, 15u)] uint rm,
                                [CombinatorialValues(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u)] uint vd,
                                [CombinatorialRange(0u, 7u, 1)] uint index,
                                [CombinatorialRange(0u, 3u, 1)] uint n,
                                [CombinatorialValues(0x0u)] uint offset)
        {
            var data = GenerateVectorSequence((int)MemoryBlock.GetPageSize());
            SetWorkingMemory(0, data);

            (V128 vec1, V128 vec2, V128 vec3, V128 vec4) = GenerateTestVectors();

            uint opcode = 0xf4800000u; // VST1.8 {D0[0]}, [R0], R0

            opcode |= ((size & 3) << 10) | ((rn & 15) << 16) | (rm & 15);

            uint indexAlign = (index << (int)(1 + size)) & 15;

            opcode |= (indexAlign) << 4;

            opcode |= ((vd & 0x10) << 18);
            opcode |= ((vd & 0xf) << 12);

            opcode |= (n & 3) << 8; // ST1 is 0, ST2 is 1 etc.

            SingleOpcode(opcode, r0: _testOffset, r1: offset, v1: vec1, v2: vec2, v3: vec3, v4: vec4, sp: _testOffset);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VSTn.<size> <list>, [<Rn> {:<align>}]{ /!/, <Rm>} (multiple n element structures)")]
        [PairwiseData]
        public void Vstn_Pair([CombinatorialValues(0u, 1u, 2u, 3u)] uint size,
                              [CombinatorialValues(0u, 13u)] uint rn,
                              [CombinatorialValues(1u, 13u, 15u)] uint rm,
                              [CombinatorialValues(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u)] uint vd,
                              [CombinatorialRange(0u, 10u, 1)] uint mode,
                              [CombinatorialValues(0x0u)] uint offset)
        {
            var data = GenerateVectorSequence((int)MemoryBlock.GetPageSize());
            SetWorkingMemory(0, data);

            (V128 vec1, V128 vec2, V128 vec3, V128 vec4) = GenerateTestVectors();

            uint opcode = 0xf4000000u; // VST4.8 {D0, D1, D2, D3}, [R0], R0

            if (mode > 3 && size == 3)
            {
                // A size of 3 is only valid for VST1.
                size = 2;
            }

            opcode |= ((size & 3) << 6) | ((rn & 15) << 16) | (rm & 15) | (_ldStModes[mode] << 8);

            opcode |= ((vd & 0x10) << 18);
            opcode |= ((vd & 0xf) << 12);

            SingleOpcode(opcode, r0: _testOffset, r1: offset, v1: vec1, v2: vec2, v3: vec3, v4: vec4, sp: _testOffset);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VLDM.<size> <Rn>{!}, <d/sreglist>")]
        [PairwiseData]
        public void Vldm([CombinatorialValues(0u, 13u)] uint rn,
                         [CombinatorialValues(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u)] uint vd,
                         [CombinatorialRange(0u, 2u, 1)] uint mode,
                         [CombinatorialValues(0x1u, 0x32u)] uint regs,
                         bool single)
        {
            var data = GenerateVectorSequence((int)MemoryBlock.GetPageSize());
            SetWorkingMemory(0, data);

            uint opcode = 0xec100a00u; // VST4.8 {D0, D1, D2, D3}, [R0], R0

            uint[] vldmModes =
            {
                // Note: 3rd 0 leaves a space for "D".
                0b0100, // Increment after.
                0b0101, // Increment after. (!)
                0b1001, // Decrement before. (!)
            };

            opcode |= ((vldmModes[mode] & 15) << 21);
            opcode |= ((rn & 15) << 16);

            opcode |= ((vd & 0x10) << 18);
            opcode |= ((vd & 0xf) << 12);

            opcode |= ((uint)(single ? 0 : 1) << 8);

            if (!single)
            {
                regs <<= 1; // Low bit must be 0 - must be even number of registers.
            }

            uint regSize = single ? 1u : 2u;

            if (vd + (regs / regSize) > 32) // Can't address further than S31 or D31.
            {
                regs -= (vd + (regs / regSize)) - 32;
            }

            if (regs / regSize > 16) // Can't do more than 16 registers at a time.
            {
                regs = 16 * regSize;
            }

            opcode |= regs & 0xff;

            SingleOpcode(opcode, r0: _testOffset, sp: _testOffset);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VLDR.<size> <Sd>, [<Rn> {, #{+/-}<imm>}]")]
        [PairwiseData]
        public void Vldr([CombinatorialValues(2u, 3u)] uint size, // FP16 is not supported for now
                         [CombinatorialValues(0u)] uint rn,
                         [CombinatorialValues(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u)] uint sd,
                         [CombinatorialValues(0x0u)] uint imm,
                         bool sub)
        {
            var data = GenerateVectorSequence((int)MemoryBlock.GetPageSize());
            SetWorkingMemory(0, data);

            uint opcode = 0xed900a00u; // VLDR.32 S0, [R0, #0]
            opcode |= ((size & 3) << 8) | ((rn & 15) << 16);

            if (sub)
            {
                opcode &= ~(uint)(1 << 23);
            }

            if (size == 2)
            {
                opcode |= ((sd & 0x1) << 22);
                opcode |= ((sd & 0x1e) << 11);
            }
            else
            {
                opcode |= ((sd & 0x10) << 18);
                opcode |= ((sd & 0xf) << 12);
            }
            opcode |= imm & 0xff;

            SingleOpcode(opcode, r0: _testOffset);

            CompareAgainstUnicorn();
        }

        [Theory(DisplayName = "VSTR.<size> <Sd>, [<Rn> {, #{+/-}<imm>}]")]
        [PairwiseData]
        public void Vstr([CombinatorialValues(2u, 3u)] uint size, // FP16 is not supported for now
                [CombinatorialValues(0u)] uint rn,
                [CombinatorialValues(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u)] uint sd,
                [CombinatorialValues(0x0u)] uint imm,
                bool sub)
        {
            var data = GenerateVectorSequence((int)MemoryBlock.GetPageSize());
            SetWorkingMemory(0, data);

            uint opcode = 0xed800a00u; // VSTR.32 S0, [R0, #0]
            opcode |= ((size & 3) << 8) | ((rn & 15) << 16);

            if (sub)
            {
                opcode &= ~(uint)(1 << 23);
            }

            if (size == 2)
            {
                opcode |= ((sd & 0x1) << 22);
                opcode |= ((sd & 0x1e) << 11);
            }
            else
            {
                opcode |= ((sd & 0x10) << 18);
                opcode |= ((sd & 0xf) << 12);
            }
            opcode |= imm & 0xff;

            (V128 vec1, V128 vec2, _, _) = GenerateTestVectors();

            SingleOpcode(opcode, r0: _testOffset, v0: vec1, v1: vec2);

            CompareAgainstUnicorn();
        }

        private static (V128, V128, V128, V128) GenerateTestVectors()
        {
            return (
                new V128(-12.43f, 1872.23f, 4456.23f, -5622.2f),
                new V128(0.0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity),
                new V128(1.23e10f, -0.0f, -0.123f, 0.123f),
                new V128(float.Epsilon, 3.5f, 925.23f, -104.9f)
                );
        }

        private static byte[] GenerateVectorSequence(int length)
        {
            int floatLength = length >> 2;
            float[] data = new float[floatLength];

            for (int i = 0; i < floatLength; i++)
            {
                data[i] = i + (i / 9f);
            }

            var result = new byte[length];
            Buffer.BlockCopy(data, 0, result, 0, result.Length);
            return result;
        }
#endif
    }
}
