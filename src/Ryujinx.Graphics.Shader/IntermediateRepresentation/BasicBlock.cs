using System.Collections.Generic;

namespace Ryujinx.Graphics.Shader.IntermediateRepresentation
{
    class BasicBlock
    {
        public int Index { get; set; }

        public LinkedList<INode> Operations { get; }

        private BasicBlock _next;
        private BasicBlock _branch;

        public BasicBlock Next
        {
            get => _next;
            set => _next = AddSuccessor(_next, value);
        }

        public BasicBlock Branch
        {
            get => _branch;
            set => _branch = AddSuccessor(_branch, value);
        }

        public bool HasSuccessor => _branch != null || _next != null;
        public bool HasBranch => _branch != null;
        public bool Reachable => Index == 0 || Predecessors.Count != 0;

        public List<BasicBlock> Predecessors { get; }

        public HashSet<BasicBlock> DominanceFrontiers { get; }

        public BasicBlock ImmediateDominator { get; set; }

        public BasicBlock()
        {
            Operations = new LinkedList<INode>();

            Predecessors = new List<BasicBlock>();

            DominanceFrontiers = new HashSet<BasicBlock>();
        }

        public BasicBlock(int index) : this()
        {
            Index = index;
        }

        private BasicBlock AddSuccessor(BasicBlock oldBlock, BasicBlock newBlock)
        {
            oldBlock?.Predecessors.Remove(this);
            newBlock?.Predecessors.Add(this);

            return newBlock;
        }

        public INode GetLastOp()
        {
            return Operations.Last?.Value;
        }

        public void Append(INode node)
        {
            INode lastOp = GetLastOp();

            if (lastOp is Operation operation && IsControlFlowInst(operation.Inst))
            {
                Operations.AddBefore(Operations.Last, node);
            }
            else
            {
                Operations.AddLast(node);
            }
        }

        private static bool IsControlFlowInst(Instruction inst)
        {
            switch (inst)
            {
                case Instruction.Branch:
                case Instruction.BranchIfFalse:
                case Instruction.BranchIfTrue:
                case Instruction.Discard:
                case Instruction.Return:
                    return true;
                default:
                    return false;
            }
        }
    }
}
