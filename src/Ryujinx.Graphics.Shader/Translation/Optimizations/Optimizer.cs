using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ryujinx.Graphics.Shader.Translation.Optimizations
{
    static class Optimizer
    {
        public static void RunPass(TransformContext context)
        {
            RunOptimizationPasses(context.Blocks, context.ResourceManager);

            // TODO: Some of those are not optimizations and shouldn't be here.

            GlobalToStorage.RunPass(context.Hfm, context.Blocks, context.ResourceManager, context.GpuAccessor, context.TargetLanguage);

            bool hostSupportsShaderFloat64 = context.GpuAccessor.QueryHostSupportsShaderFloat64();

            // Those passes are looking for specific patterns and only needs to run once.
            for (int blkIndex = 0; blkIndex < context.Blocks.Length; blkIndex++)
            {
                if (context.TargetApi == TargetApi.OpenGL)
                {
                    BindlessToArray.RunPassOgl(context.Blocks[blkIndex], context.ResourceManager);
                }
                else
                {
                    BindlessToArray.RunPass(context.Blocks[blkIndex], context.ResourceManager, context.GpuAccessor);
                }

                BindlessElimination.RunPass(context.Blocks[blkIndex], context.ResourceManager, context.GpuAccessor);

                // FragmentCoord only exists on fragment shaders, so we don't need to check other stages.
                if (context.Stage == ShaderStage.Fragment)
                {
                    EliminateMultiplyByFragmentCoordW(context.Blocks[blkIndex]);
                }

                // If the host does not support double operations, we need to turn them into float operations.
                if (!hostSupportsShaderFloat64)
                {
                    DoubleToFloat.RunPass(context.Hfm, context.Blocks[blkIndex]);
                }
            }

            // Run optimizations one last time to remove any code that is now optimizable after above passes.
            RunOptimizationPasses(context.Blocks, context.ResourceManager);
        }

        private static void RunOptimizationPasses(BasicBlock[] blocks, ResourceManager resourceManager)
        {
            bool modified;

            do
            {
                modified = false;

                for (int blkIndex = 0; blkIndex < blocks.Length; blkIndex++)
                {
                    BasicBlock block = blocks[blkIndex];

                    LinkedListNode<INode> node = block.Operations.First;

                    while (node != null)
                    {
                        LinkedListNode<INode> nextNode = node.Next;

                        bool isUnused = IsUnused(node.Value);

                        if (node.Value is not Operation operation || isUnused)
                        {
                            if (node.Value is PhiNode phi && !isUnused)
                            {
                                isUnused = PropagatePhi(phi);
                            }

                            if (isUnused)
                            {
                                RemoveNode(block, node);

                                modified = true;
                            }

                            node = nextNode;

                            continue;
                        }

                        ConstantFolding.RunPass(resourceManager, operation);
                        Simplification.RunPass(operation);

                        if (DestIsLocalVar(operation))
                        {
                            if (operation.Inst == Instruction.Copy)
                            {
                                PropagateCopy(operation);

                                RemoveNode(block, node);

                                modified = true;
                            }
                            else if ((operation.Inst == Instruction.PackHalf2x16 && PropagatePack(operation)) ||
                                     (operation.Inst == Instruction.ShuffleXor && MatchDdxOrDdy(operation)))
                            {
                                if (DestHasNoUses(operation))
                                {
                                    RemoveNode(block, node);
                                }

                                modified = true;
                            }
                        }

                        node = nextNode;
                    }

                    if (BranchElimination.RunPass(block))
                    {
                        RemoveNode(block, block.Operations.Last);

                        modified = true;
                    }
                }
            }
            while (modified);
        }

        private static void PropagateCopy(Operation copyOp)
        {
            // Propagate copy source operand to all uses of
            // the destination operand.

            Operand dest = copyOp.Dest;
            Operand src = copyOp.GetSource(0);

            INode[] uses = dest.UseOps.ToArray();

            foreach (INode useNode in uses)
            {
                for (int index = 0; index < useNode.SourcesCount; index++)
                {
                    if (useNode.GetSource(index) == dest)
                    {
                        useNode.SetSource(index, src);
                    }
                }
            }
        }

        private static bool PropagatePhi(PhiNode phi)
        {
            // If all phi sources are the same, we can propagate it and remove the phi.

            Operand firstSrc = phi.GetSource(0);

            for (int index = 1; index < phi.SourcesCount; index++)
            {
                if (!IsSameOperand(firstSrc, phi.GetSource(index)))
                {
                    return false;
                }
            }

            // All sources are equal, we can propagate the value.

            Operand dest = phi.Dest;

            INode[] uses = dest.UseOps.ToArray();

            foreach (INode useNode in uses)
            {
                for (int index = 0; index < useNode.SourcesCount; index++)
                {
                    if (useNode.GetSource(index) == dest)
                    {
                        useNode.SetSource(index, firstSrc);
                    }
                }
            }

            return true;
        }

        private static bool IsSameOperand(Operand x, Operand y)
        {
            if (x.Type != y.Type || x.Value != y.Value)
            {
                return false;
            }

            // TODO: Handle Load operations with the same storage and the same constant parameters.
            return x.Type == OperandType.Constant || x.Type == OperandType.ConstantBuffer;
        }

        private static bool PropagatePack(Operation packOp)
        {
            // Propagate pack source operands to uses by unpack
            // instruction. The source depends on the unpack instruction.
            bool modified = false;

            Operand dest = packOp.Dest;
            Operand src0 = packOp.GetSource(0);
            Operand src1 = packOp.GetSource(1);

            INode[] uses = dest.UseOps.ToArray();

            foreach (INode useNode in uses)
            {
                if (useNode is not Operation operation || operation.Inst != Instruction.UnpackHalf2x16)
                {
                    continue;
                }

                if (operation.GetSource(0) == dest)
                {
                    operation.TurnIntoCopy(operation.Index == 1 ? src1 : src0);

                    modified = true;
                }
            }

            return modified;
        }

        public static bool MatchDdxOrDdy(Operation operation)
        {
            // It's assumed that "operation.Inst" is ShuffleXor,
            // that should be checked before calling this method.
            Debug.Assert(operation.Inst == Instruction.ShuffleXor);

            bool modified = false;

            Operand src2 = operation.GetSource(1);
            Operand src3 = operation.GetSource(2);

            if (src2.Type != OperandType.Constant || (src2.Value != 1 && src2.Value != 2))
            {
                return false;
            }

            if (src3.Type != OperandType.Constant || src3.Value != 0x1c03)
            {
                return false;
            }

            bool isDdy = src2.Value == 2;
            bool isDdx = !isDdy;

            // We can replace any use by a FSWZADD with DDX/DDY, when
            // the following conditions are true:
            // - The mask should be 0b10100101 for DDY, or 0b10011001 for DDX.
            // - The first source operand must be the shuffle output.
            // - The second source operand must be the shuffle first source operand.
            INode[] uses = operation.Dest.UseOps.ToArray();

            foreach (INode use in uses)
            {
                if (use is not Operation test)
                {
                    continue;
                }

                if (use is not Operation useOp || useOp.Inst != Instruction.SwizzleAdd)
                {
                    continue;
                }

                Operand fswzaddSrc1 = useOp.GetSource(0);
                Operand fswzaddSrc2 = useOp.GetSource(1);
                Operand fswzaddSrc3 = useOp.GetSource(2);

                if (fswzaddSrc1 != operation.Dest)
                {
                    continue;
                }

                if (fswzaddSrc2 != operation.GetSource(0))
                {
                    continue;
                }

                if (fswzaddSrc3.Type != OperandType.Constant)
                {
                    continue;
                }

                int mask = fswzaddSrc3.Value;

                if ((isDdx && mask != 0b10011001) ||
                    (isDdy && mask != 0b10100101))
                {
                    continue;
                }

                useOp.TurnInto(isDdx ? Instruction.Ddx : Instruction.Ddy, fswzaddSrc2);

                modified = true;
            }

            return modified;
        }

        private static void EliminateMultiplyByFragmentCoordW(BasicBlock block)
        {
            foreach (INode node in block.Operations)
            {
                if (node is Operation operation)
                {
                    EliminateMultiplyByFragmentCoordW(operation);
                }
            }
        }

        private static void EliminateMultiplyByFragmentCoordW(Operation operation)
        {
            // We're looking for the pattern:
            //  y = x * gl_FragCoord.w
            //  v = y * (1.0 / gl_FragCoord.w)
            // Then we transform it into:
            //  v = x
            // This pattern is common on fragment shaders due to the way how perspective correction is done.

            // We are expecting a multiplication by the reciprocal of gl_FragCoord.w.
            if (operation.Inst != (Instruction.FP32 | Instruction.Multiply))
            {
                return;
            }

            Operand lhs = operation.GetSource(0);
            Operand rhs = operation.GetSource(1);

            // Check LHS of the main multiplication operation. We expect an input being multiplied by gl_FragCoord.w.
            if (lhs.AsgOp is not Operation attrMulOp || attrMulOp.Inst != (Instruction.FP32 | Instruction.Multiply))
            {
                return;
            }

            Operand attrMulLhs = attrMulOp.GetSource(0);
            Operand attrMulRhs = attrMulOp.GetSource(1);

            // LHS should be any input, RHS should be exactly gl_FragCoord.w.
            if (!Utils.IsInputLoad(attrMulLhs.AsgOp) || !Utils.IsInputLoad(attrMulRhs.AsgOp, IoVariable.FragmentCoord, 3))
            {
                return;
            }

            // RHS of the main multiplication should be a reciprocal operation (1.0 / x).
            if (rhs.AsgOp is not Operation reciprocalOp || reciprocalOp.Inst != (Instruction.FP32 | Instruction.Divide))
            {
                return;
            }

            Operand reciprocalLhs = reciprocalOp.GetSource(0);
            Operand reciprocalRhs = reciprocalOp.GetSource(1);

            // Check if the divisor is a constant equal to 1.0.
            if (reciprocalLhs.Type != OperandType.Constant || reciprocalLhs.AsFloat() != 1.0f)
            {
                return;
            }

            // Check if the dividend is gl_FragCoord.w.
            if (!Utils.IsInputLoad(reciprocalRhs.AsgOp, IoVariable.FragmentCoord, 3))
            {
                return;
            }

            // If everything matches, we can replace the operation with the input load result.
            operation.TurnIntoCopy(attrMulLhs);
        }

        private static void RemoveNode(BasicBlock block, LinkedListNode<INode> llNode)
        {
            // Remove a node from the nodes list, and also remove itself
            // from all the use lists on the operands that this node uses.
            block.Operations.Remove(llNode);

            Queue<INode> nodes = new();

            nodes.Enqueue(llNode.Value);

            while (nodes.TryDequeue(out INode node))
            {
                for (int index = 0; index < node.SourcesCount; index++)
                {
                    Operand src = node.GetSource(index);

                    if (src.Type != OperandType.LocalVariable)
                    {
                        continue;
                    }

                    if (src.UseOps.Remove(node) && src.UseOps.Count == 0)
                    {
                        Debug.Assert(src.AsgOp != null);
                        nodes.Enqueue(src.AsgOp);
                    }
                }
            }
        }

        private static bool IsUnused(INode node)
        {
            return !HasSideEffects(node) && DestIsLocalVar(node) && DestHasNoUses(node);
        }

        private static bool HasSideEffects(INode node)
        {
            if (node is Operation operation)
            {
                switch (operation.Inst & Instruction.Mask)
                {
                    case Instruction.AtomicAdd:
                    case Instruction.AtomicAnd:
                    case Instruction.AtomicCompareAndSwap:
                    case Instruction.AtomicMaxS32:
                    case Instruction.AtomicMaxU32:
                    case Instruction.AtomicMinS32:
                    case Instruction.AtomicMinU32:
                    case Instruction.AtomicOr:
                    case Instruction.AtomicSwap:
                    case Instruction.AtomicXor:
                    case Instruction.Call:
                    case Instruction.ImageAtomic:
                        return true;
                }
            }

            return false;
        }

        private static bool DestIsLocalVar(INode node)
        {
            if (node.DestsCount == 0)
            {
                return false;
            }

            for (int index = 0; index < node.DestsCount; index++)
            {
                Operand dest = node.GetDest(index);

                if (dest != null && dest.Type != OperandType.LocalVariable)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool DestHasNoUses(INode node)
        {
            for (int index = 0; index < node.DestsCount; index++)
            {
                Operand dest = node.GetDest(index);

                if (dest != null && dest.UseOps.Count != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
