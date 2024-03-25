using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Shader.StructuredIr
{
    static class PhiFunctions
    {
        public static void Remove(BasicBlock[] blocks)
        {
            for (int blkIndex = 0; blkIndex < blocks.Length; blkIndex++)
            {
                BasicBlock block = blocks[blkIndex];

                LinkedListNode<INode> node = block.Operations.First;

                while (node != null)
                {
                    LinkedListNode<INode> nextNode = node.Next;

                    if (node.Value is not PhiNode phi)
                    {
                        node = nextNode;

                        continue;
                    }

                    Operand temp = OperandHelper.Local();

                    for (int index = 0; index < phi.SourcesCount; index++)
                    {
                        Operand src = phi.GetSource(index);
                        BasicBlock srcBlock = phi.GetBlock(index);

                        Operation copyOp = new(Instruction.Copy, temp, src);

                        srcBlock.Append(copyOp);
                    }

                    Operation copyOp2 = new(Instruction.Copy, phi.Dest, temp);

                    nextNode = block.Operations.AddAfter(node, copyOp2).Next;
                    block.Operations.Remove(node);

                    node = nextNode;
                }
            }
        }
    }
}
