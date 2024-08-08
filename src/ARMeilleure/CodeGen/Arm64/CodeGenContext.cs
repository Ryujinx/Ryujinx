using ARMeilleure.CodeGen.Linking;
using ARMeilleure.CodeGen.RegisterAllocators;
using ARMeilleure.IntermediateRepresentation;
using Microsoft.IO;
using Ryujinx.Common.Memory;
using System;
using System.Collections.Generic;
using System.IO;

namespace ARMeilleure.CodeGen.Arm64
{
    class CodeGenContext
    {
        private const int BccInstLength = 4;
        private const int CbnzInstLength = 4;
        private const int LdrLitInstLength = 4;

        private readonly RecyclableMemoryStream _stream;

        public int StreamOffset => (int)_stream.Length;

        public AllocationResult AllocResult { get; }

        public Assembler Assembler { get; }

        public BasicBlock CurrBlock { get; private set; }

        public bool HasCall { get; }

        public int CallArgsRegionSize { get; }
        public int FpLrSaveRegionSize { get; }

        private readonly Dictionary<BasicBlock, long> _visitedBlocks;
        private readonly Dictionary<BasicBlock, List<(ArmCondition Condition, long BranchPos)>> _pendingBranches;

        private readonly struct ConstantPoolEntry
        {
            public readonly int Offset;
            public readonly Symbol Symbol;
            public readonly List<(Operand, int)> LdrOffsets;

            public ConstantPoolEntry(int offset, Symbol symbol)
            {
                Offset = offset;
                Symbol = symbol;
                LdrOffsets = new List<(Operand, int)>();
            }
        }

        private readonly Dictionary<ulong, ConstantPoolEntry> _constantPool;

        private bool _constantPoolWritten;
        private long _constantPoolOffset;

        private ArmCondition _jNearCondition;
        private Operand _jNearValue;

        private long _jNearPosition;

        private readonly bool _relocatable;

        public CodeGenContext(AllocationResult allocResult, int maxCallArgs, bool relocatable)
        {
            _stream = MemoryStreamManager.Shared.GetStream();

            AllocResult = allocResult;

            Assembler = new Assembler(_stream);

            bool hasCall = maxCallArgs >= 0;

            HasCall = hasCall;

            if (maxCallArgs < 0)
            {
                maxCallArgs = 0;
            }

            CallArgsRegionSize = maxCallArgs * 16;
            FpLrSaveRegionSize = hasCall ? 16 : 0;

            _visitedBlocks = new Dictionary<BasicBlock, long>();
            _pendingBranches = new Dictionary<BasicBlock, List<(ArmCondition, long)>>();
            _constantPool = new Dictionary<ulong, ConstantPoolEntry>();

            _relocatable = relocatable;
        }

        public void EnterBlock(BasicBlock block)
        {
            CurrBlock = block;

            long target = _stream.Position;

            if (_pendingBranches.TryGetValue(block, out var list))
            {
                foreach ((ArmCondition condition, long branchPos) in list)
                {
                    _stream.Seek(branchPos, SeekOrigin.Begin);
                    WriteBranch(condition, target);
                }

                _stream.Seek(target, SeekOrigin.Begin);
                _pendingBranches.Remove(block);
            }

            _visitedBlocks.Add(block, target);
        }

        public void JumpTo(BasicBlock target)
        {
            JumpTo(ArmCondition.Al, target);
        }

        public void JumpTo(ArmCondition condition, BasicBlock target)
        {
            if (_visitedBlocks.TryGetValue(target, out long offset))
            {
                WriteBranch(condition, offset);
            }
            else
            {
                if (!_pendingBranches.TryGetValue(target, out var list))
                {
                    list = new List<(ArmCondition, long)>();
                    _pendingBranches.Add(target, list);
                }

                list.Add((condition, _stream.Position));

                _stream.Seek(BccInstLength, SeekOrigin.Current);
            }
        }

        private void WriteBranch(ArmCondition condition, long to)
        {
            int imm = checked((int)(to - _stream.Position));

            if (condition != ArmCondition.Al)
            {
                Assembler.B(condition, imm);
            }
            else
            {
                Assembler.B(imm);
            }
        }

        public void JumpToNear(ArmCondition condition)
        {
            _jNearCondition = condition;
            _jNearPosition = _stream.Position;

            _stream.Seek(BccInstLength, SeekOrigin.Current);
        }

        public void JumpToNearIfNotZero(Operand value)
        {
            _jNearValue = value;
            _jNearPosition = _stream.Position;

            _stream.Seek(CbnzInstLength, SeekOrigin.Current);
        }

        public void JumpHere()
        {
            long currentPosition = _stream.Position;
            long offset = currentPosition - _jNearPosition;

            _stream.Seek(_jNearPosition, SeekOrigin.Begin);

            if (_jNearValue != default)
            {
                Assembler.Cbnz(_jNearValue, checked((int)offset));
                _jNearValue = default;
            }
            else
            {
                Assembler.B(_jNearCondition, checked((int)offset));
            }

            _stream.Seek(currentPosition, SeekOrigin.Begin);
        }

        public void ReserveRelocatableConstant(Operand rt, Symbol symbol, ulong value)
        {
            if (!_constantPool.TryGetValue(value, out ConstantPoolEntry cpe))
            {
                cpe = new ConstantPoolEntry(_constantPool.Count * sizeof(ulong), symbol);
                _constantPool.Add(value, cpe);
            }

            cpe.LdrOffsets.Add((rt, (int)_stream.Position));
            _stream.Seek(LdrLitInstLength, SeekOrigin.Current);
        }

        private long WriteConstantPool()
        {
            if (_constantPoolWritten)
            {
                return _constantPoolOffset;
            }

            long constantPoolBaseOffset = _stream.Position;

            foreach (ulong value in _constantPool.Keys)
            {
                WriteUInt64(value);
            }

            foreach (ConstantPoolEntry cpe in _constantPool.Values)
            {
                foreach ((Operand rt, int ldrOffset) in cpe.LdrOffsets)
                {
                    _stream.Seek(ldrOffset, SeekOrigin.Begin);

                    int absoluteOffset = checked((int)(constantPoolBaseOffset + cpe.Offset));
                    int pcRelativeOffset = absoluteOffset - ldrOffset;

                    Assembler.LdrLit(rt, pcRelativeOffset);
                }
            }

            _stream.Seek(constantPoolBaseOffset + _constantPool.Count * sizeof(ulong), SeekOrigin.Begin);

            _constantPoolOffset = constantPoolBaseOffset;
            _constantPoolWritten = true;

            return constantPoolBaseOffset;
        }

        public (byte[], RelocInfo) GetCode()
        {
            long constantPoolBaseOffset = WriteConstantPool();

            byte[] code = new byte[_stream.Length];

            long originalPosition = _stream.Position;

            _stream.Seek(0, SeekOrigin.Begin);
            _stream.ReadExactly(code, 0, code.Length);
            _stream.Seek(originalPosition, SeekOrigin.Begin);

            RelocInfo relocInfo;

            if (_relocatable)
            {
                RelocEntry[] relocs = new RelocEntry[_constantPool.Count];

                int index = 0;

                foreach (ConstantPoolEntry cpe in _constantPool.Values)
                {
                    if (cpe.Symbol.Type != SymbolType.None)
                    {
                        int absoluteOffset = checked((int)(constantPoolBaseOffset + cpe.Offset));
                        relocs[index++] = new RelocEntry(absoluteOffset, cpe.Symbol);
                    }
                }

                if (index != relocs.Length)
                {
                    Array.Resize(ref relocs, index);
                }

                relocInfo = new RelocInfo(relocs);
            }
            else
            {
                relocInfo = new RelocInfo(Array.Empty<RelocEntry>());
            }

            return (code, relocInfo);
        }

        private void WriteUInt64(ulong value)
        {
            _stream.WriteByte((byte)(value >> 0));
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)(value >> 16));
            _stream.WriteByte((byte)(value >> 24));
            _stream.WriteByte((byte)(value >> 32));
            _stream.WriteByte((byte)(value >> 40));
            _stream.WriteByte((byte)(value >> 48));
            _stream.WriteByte((byte)(value >> 56));
        }
    }
}
