using ARMeilleure.Memory;
using Ryujinx.Cpu.LightningJit.Graph;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Ryujinx.Cpu.LightningJit.Arm64.Target.Arm64
{
    static class Decoder
    {
        private const int MaxInstructionsPerFunction = 10000;

        private const uint NzcvFlags = 0xfu << 28;
        private const uint CFlag = 0x1u << 29;

        public static MultiBlock DecodeMulti(CpuPreset cpuPreset, IMemoryManager memoryManager, ulong address)
        {
            List<Block> blocks = new();
            List<ulong> branchTargets = new();

            RegisterMask useMask = RegisterMask.Zero;

            bool hasHostCall = false;
            bool hasMemoryInstruction = false;
            int totalInsts = 0;

            while (true)
            {
                Block block = Decode(cpuPreset, memoryManager, address, ref totalInsts, ref useMask, ref hasHostCall, ref hasMemoryInstruction);

                if (!block.IsTruncated && TryGetBranchTarget(block, out ulong targetAddress))
                {
                    branchTargets.Add(targetAddress);
                }

                blocks.Add(block);

                if (block.IsTruncated || !HasNextBlock(block, block.EndAddress - 4UL, branchTargets))
                {
                    break;
                }

                address = block.EndAddress;
            }

            branchTargets.Sort();
            SplitBlocks(blocks, branchTargets);
            NumberAndLinkBlocks(blocks);

            return new(blocks, useMask, hasHostCall, hasMemoryInstruction);
        }

        private static bool TryGetBranchTarget(Block block, out ulong targetAddress)
        {
            return TryGetBranchTarget(block.Instructions[^1].Name, block.EndAddress - 4UL, block.Instructions[^1].Encoding, out targetAddress);
        }

        private static bool TryGetBranchTarget(InstName name, ulong pc, uint encoding, out ulong targetAddress)
        {
            int originalOffset;

            switch (name)
            {
                case InstName.BUncond:
                    originalOffset = ImmUtils.ExtractSImm26Times4(encoding);
                    targetAddress = pc + (ulong)originalOffset;

                    return true;

                case InstName.BCond:
                case InstName.Cbnz:
                case InstName.Cbz:
                case InstName.Tbnz:
                case InstName.Tbz:
                    if (name == InstName.Tbnz || name == InstName.Tbz)
                    {
                        originalOffset = ImmUtils.ExtractSImm14Times4(encoding);
                    }
                    else
                    {
                        originalOffset = ImmUtils.ExtractSImm19Times4(encoding);
                    }

                    targetAddress = pc + (ulong)originalOffset;

                    return true;
            }

            targetAddress = 0;

            return false;
        }

        private static void SplitBlocks(List<Block> blocks, List<ulong> branchTargets)
        {
            int btIndex = 0;

            while (btIndex < branchTargets.Count)
            {
                for (int blockIndex = 0; blockIndex < blocks.Count && btIndex < branchTargets.Count; blockIndex++)
                {
                    Block block = blocks[blockIndex];
                    ulong currentBranchTarget = branchTargets[btIndex];

                    while (currentBranchTarget >= block.Address && currentBranchTarget < block.EndAddress)
                    {
                        if (block.Address != currentBranchTarget)
                        {
                            (Block leftBlock, Block rightBlock) = block.SplitAtAddress(currentBranchTarget);

                            blocks.Insert(blockIndex, leftBlock);
                            blocks[blockIndex + 1] = rightBlock;

                            block = leftBlock;
                        }

                        btIndex++;

                        while (btIndex < branchTargets.Count && branchTargets[btIndex] == currentBranchTarget)
                        {
                            btIndex++;
                        }

                        if (btIndex >= branchTargets.Count)
                        {
                            break;
                        }

                        currentBranchTarget = branchTargets[btIndex];
                    }
                }

                Debug.Assert(btIndex < int.MaxValue);
                btIndex++;
            }
        }

        private static void NumberAndLinkBlocks(List<Block> blocks)
        {
            Dictionary<ulong, Block> blocksByAddress = new();

            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                Block block = blocks[blockIndex];

                blocksByAddress.Add(block.Address, block);
            }

            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                Block block = blocks[blockIndex];

                block.Number(blockIndex);

                if (!block.IsTruncated)
                {
                    bool hasNext = !block.EndsWithBranch;
                    bool hasBranch = false;

                    switch (block.Instructions[^1].Name)
                    {
                        case InstName.BUncond:
                            hasBranch = true;
                            break;

                        case InstName.BCond:
                        case InstName.Cbnz:
                        case InstName.Cbz:
                        case InstName.Tbnz:
                        case InstName.Tbz:
                            hasNext = true;
                            hasBranch = true;
                            break;

                        case InstName.Bl:
                        case InstName.Blr:
                            hasNext = true;
                            break;

                        case InstName.Ret:
                            hasNext = false;
                            hasBranch = false;
                            break;
                    }

                    if (hasNext && blocksByAddress.TryGetValue(block.EndAddress, out Block nextBlock))
                    {
                        block.AddSuccessor(nextBlock);
                        nextBlock.AddPredecessor(block);
                    }

                    if (hasBranch &&
                        TryGetBranchTarget(block, out ulong targetAddress) &&
                        blocksByAddress.TryGetValue(targetAddress, out Block branchBlock))
                    {
                        block.AddSuccessor(branchBlock);
                        branchBlock.AddPredecessor(block);
                    }
                }
            }
        }

        private static bool HasNextBlock(in Block block, ulong pc, List<ulong> branchTargets)
        {
            switch (block.Instructions[^1].Name)
            {
                case InstName.BUncond:
                    return branchTargets.Contains(pc + 4UL) ||
                        (TryGetBranchTarget(block, out ulong targetAddress) && targetAddress >= pc && targetAddress < pc + 0x1000);

                case InstName.BCond:
                case InstName.Bl:
                case InstName.Blr:
                case InstName.Cbnz:
                case InstName.Cbz:
                case InstName.Tbnz:
                case InstName.Tbz:
                    return true;

                case InstName.Br:
                    return false;

                case InstName.Ret:
                    return branchTargets.Contains(pc + 4UL);
            }

            return !block.EndsWithBranch;
        }

        private static Block Decode(
            CpuPreset cpuPreset,
            IMemoryManager memoryManager,
            ulong address,
            ref int totalInsts,
            ref RegisterMask useMask,
            ref bool hasHostCall,
            ref bool hasMemoryInstruction)
        {
            ulong startAddress = address;

            List<InstInfo> insts = new();

            uint gprUseMask = useMask.GprMask;
            uint fpSimdUseMask = useMask.FpSimdMask;
            uint pStateUseMask = useMask.PStateMask;

            uint encoding;
            InstName name;
            InstFlags flags;
            bool isControlFlow;
            bool isTruncated = false;

            do
            {
                encoding = memoryManager.Read<uint>(address);
                address += 4UL;

                (name, flags, AddressForm addressForm) = InstTable.GetInstNameAndFlags(encoding, cpuPreset.Version, cpuPreset.Features);

                if (name.IsPrivileged() || (name == InstName.Sys && IsPrivilegedSys(encoding)))
                {
                    name = InstName.UdfPermUndef;
                    flags = InstFlags.None;
                    addressForm = AddressForm.None;
                }

                (uint instGprReadMask, uint instFpSimdReadMask) = RegisterUtils.PopulateReadMasks(name, flags, encoding);
                (uint instGprWriteMask, uint instFpSimdWriteMask) = RegisterUtils.PopulateWriteMasks(name, flags, encoding);

                if (name.IsCall())
                {
                    instGprWriteMask |= 1u << RegisterUtils.LrIndex;
                }

                uint tempGprUseMask = gprUseMask | instGprReadMask | instGprWriteMask;

                if (CalculateAvailableTemps(tempGprUseMask) < CalculateRequiredGprTemps(tempGprUseMask) || totalInsts++ >= MaxInstructionsPerFunction)
                {
                    isTruncated = true;
                    address -= 4UL;

                    break;
                }

                gprUseMask = tempGprUseMask;

                uint instPStateReadMask = 0;
                uint instPStateWriteMask = 0;

                if (flags.HasFlag(InstFlags.Nzcv) || IsMrsNzcv(encoding))
                {
                    instPStateReadMask = NzcvFlags;
                }
                else if (flags.HasFlag(InstFlags.C))
                {
                    instPStateReadMask = CFlag;
                }

                if (flags.HasFlag(InstFlags.S) || IsMsrNzcv(encoding))
                {
                    instPStateWriteMask = NzcvFlags;
                }

                if (flags.HasFlag(InstFlags.Memory) || name == InstName.Sys)
                {
                    hasMemoryInstruction = true;
                }

                fpSimdUseMask |= instFpSimdReadMask | instFpSimdWriteMask;
                pStateUseMask |= instPStateReadMask | instPStateWriteMask;

                if (name.IsSystemOrCall() && !hasHostCall)
                {
                    hasHostCall = name.IsCall() || InstEmitSystem.NeedsCall(encoding);
                }

                isControlFlow = name.IsControlFlowOrException();

                RegisterUse registerUse = new(
                    instGprReadMask,
                    instGprWriteMask,
                    instFpSimdReadMask,
                    instFpSimdWriteMask,
                    instPStateReadMask,
                    instPStateWriteMask);

                insts.Add(new(encoding, name, flags, addressForm, registerUse));
            }
            while (!isControlFlow);

            bool isLoopEnd = false;

            if (!isTruncated && IsBackwardsBranch(name, encoding))
            {
                hasHostCall = true;
                isLoopEnd = true;
            }

            useMask = new(gprUseMask, fpSimdUseMask, pStateUseMask);

            return new(startAddress, address, insts, !isTruncated && !name.IsException(), isTruncated, isLoopEnd);
        }

        private static bool IsPrivilegedSys(uint encoding)
        {
            return !SysUtils.IsCacheInstEl0(encoding);
        }

        private static bool IsMrsNzcv(uint encoding)
        {
            return (encoding & ~0x1fu) == 0xd53b4200u;
        }

        private static bool IsMsrNzcv(uint encoding)
        {
            return (encoding & ~0x1fu) == 0xd51b4200u;
        }

        private static bool IsBackwardsBranch(InstName name, uint encoding)
        {
            switch (name)
            {
                case InstName.BUncond:
                    return ImmUtils.ExtractSImm26Times4(encoding) < 0;

                case InstName.BCond:
                case InstName.Cbnz:
                case InstName.Cbz:
                case InstName.Tbnz:
                case InstName.Tbz:
                    int imm = name == InstName.Tbnz || name == InstName.Tbz
                        ? ImmUtils.ExtractSImm14Times4(encoding)
                        : ImmUtils.ExtractSImm19Times4(encoding);

                    return imm < 0;
            }

            return false;
        }

        private static int CalculateRequiredGprTemps(uint gprUseMask)
        {
            return BitOperations.PopCount(gprUseMask & RegisterUtils.ReservedRegsMask) + RegisterAllocator.MaxTempsInclFixed;
        }

        private static int CalculateAvailableTemps(uint gprUseMask)
        {
            return BitOperations.PopCount(~(gprUseMask | RegisterUtils.ReservedRegsMask));
        }
    }
}
