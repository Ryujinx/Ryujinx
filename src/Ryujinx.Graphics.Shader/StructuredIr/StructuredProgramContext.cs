using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.Translation;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Ryujinx.Graphics.Shader.StructuredIr.AstHelper;

namespace Ryujinx.Graphics.Shader.StructuredIr
{
    class StructuredProgramContext
    {
        private HashSet<BasicBlock> _loopTails;

        private Stack<(AstBlock Block, int CurrEndIndex, int LoopEndIndex)> _blockStack;

        private Dictionary<Operand, AstOperand> _localsMap;

        private Dictionary<int, AstAssignment> _gotoTempAsgs;

        private List<GotoStatement> _gotos;

        private AstBlock _currBlock;

        private int _currEndIndex;
        private int _loopEndIndex;

        public StructuredFunction CurrentFunction { get; private set; }

        public StructuredProgramInfo Info { get; }

        public ShaderDefinitions Definitions { get; }
        public ResourceManager ResourceManager { get; }
        public bool DebugMode { get; }

        public StructuredProgramContext(
            AttributeUsage attributeUsage,
            ShaderDefinitions definitions,
            ResourceManager resourceManager,
            bool precise,
            bool debugMode)
        {
            Info = new StructuredProgramInfo(precise);

            Definitions = definitions;
            ResourceManager = resourceManager;
            DebugMode = debugMode;

            if (definitions.GpPassthrough)
            {
                int passthroughAttributes = attributeUsage.PassthroughAttributes;
                while (passthroughAttributes != 0)
                {
                    int index = BitOperations.TrailingZeroCount(passthroughAttributes);

                    Info.IoDefinitions.Add(new IoDefinition(StorageKind.Input, IoVariable.UserDefined, index));

                    passthroughAttributes &= ~(1 << index);
                }

                Info.IoDefinitions.Add(new IoDefinition(StorageKind.Input, IoVariable.Position));
                Info.IoDefinitions.Add(new IoDefinition(StorageKind.Input, IoVariable.PointSize));
                Info.IoDefinitions.Add(new IoDefinition(StorageKind.Input, IoVariable.ClipDistance));
            }
        }

        public void EnterFunction(
            int blocksCount,
            string name,
            AggregateType returnType,
            AggregateType[] inArguments,
            AggregateType[] outArguments)
        {
            _loopTails = new HashSet<BasicBlock>();

            _blockStack = new Stack<(AstBlock, int, int)>();

            _localsMap = new Dictionary<Operand, AstOperand>();

            _gotoTempAsgs = new Dictionary<int, AstAssignment>();

            _gotos = new List<GotoStatement>();

            _currBlock = new AstBlock(AstBlockType.Main);

            _currEndIndex = blocksCount;
            _loopEndIndex = blocksCount;

            CurrentFunction = new StructuredFunction(_currBlock, name, returnType, inArguments, outArguments);
        }

        public void LeaveFunction()
        {
            Info.Functions.Add(CurrentFunction);
        }

        public void EnterBlock(BasicBlock block)
        {
            while (_currEndIndex == block.Index)
            {
                (_currBlock, _currEndIndex, _loopEndIndex) = _blockStack.Pop();
            }

            if (_gotoTempAsgs.TryGetValue(block.Index, out AstAssignment gotoTempAsg))
            {
                AddGotoTempReset(block, gotoTempAsg);
            }

            LookForDoWhileStatements(block);
        }

        public void LeaveBlock(BasicBlock block, Operation branchOp)
        {
            LookForIfStatements(block, branchOp);
        }

        private void LookForDoWhileStatements(BasicBlock block)
        {
            // Check if we have any predecessor whose index is greater than the
            // current block, this indicates a loop.
            bool done = false;

            foreach (BasicBlock predecessor in block.Predecessors.OrderByDescending(x => x.Index))
            {
                // If not a loop, break.
                if (predecessor.Index < block.Index)
                {
                    break;
                }

                // Check if we can create a do-while loop here (only possible if the loop end
                // falls inside the current scope), if not add a goto instead.
                if (predecessor.Index < _currEndIndex && !done)
                {
                    // Create do-while loop block. We must avoid inserting a goto at the end
                    // of the loop later, when the tail block is processed. So we add the predecessor
                    // to a list of loop tails to prevent it from being processed later.
                    Operation branchOp = (Operation)predecessor.GetLastOp();

                    NewBlock(AstBlockType.DoWhile, branchOp, predecessor.Index + 1);

                    _loopTails.Add(predecessor);

                    done = true;
                }
                else
                {
                    // Failed to create loop. Since this block is the loop head, we reset the
                    // goto condition variable here. The variable is always reset on the jump
                    // target, and this block is the jump target for some loop.
                    AddGotoTempReset(block, GetGotoTempAsg(block.Index));

                    break;
                }
            }
        }

        private void LookForIfStatements(BasicBlock block, Operation branchOp)
        {
            if (block.Branch == null)
            {
                return;
            }

            // We can only enclose the "if" when the branch lands before
            // the end of the current block. If the current enclosing block
            // is not a loop, then we can also do so if the branch lands
            // right at the end of the current block. When it is a loop,
            // this is not valid as the loop condition would be evaluated,
            // and it could erroneously jump back to the start of the loop.
            bool inRange =
                block.Branch.Index < _currEndIndex ||
               (block.Branch.Index == _currEndIndex && block.Branch.Index < _loopEndIndex);

            bool isLoop = block.Branch.Index <= block.Index;

            if (inRange && !isLoop)
            {
                NewBlock(AstBlockType.If, branchOp, block.Branch.Index);
            }
            else if (!_loopTails.Contains(block))
            {
                AstAssignment gotoTempAsg = GetGotoTempAsg(block.Branch.Index);

                // We use DoWhile type here, as the condition should be true for
                // unconditional branches, or it should jump if the condition is true otherwise.
                IAstNode cond = GetBranchCond(AstBlockType.DoWhile, branchOp);

                AddNode(Assign(gotoTempAsg.Destination, cond));

                AstOperation branch = new(branchOp.Inst);

                AddNode(branch);

                GotoStatement gotoStmt = new(branch, gotoTempAsg, isLoop);

                _gotos.Add(gotoStmt);
            }
        }

        private AstAssignment GetGotoTempAsg(int index)
        {
            if (_gotoTempAsgs.TryGetValue(index, out AstAssignment gotoTempAsg))
            {
                return gotoTempAsg;
            }

            AstOperand gotoTemp = NewTemp(AggregateType.Bool);

            gotoTempAsg = Assign(gotoTemp, Const(IrConsts.False));

            _gotoTempAsgs.Add(index, gotoTempAsg);

            return gotoTempAsg;
        }

        private void AddGotoTempReset(BasicBlock block, AstAssignment gotoTempAsg)
        {
            // If it was already added, we don't need to add it again.
            if (gotoTempAsg.Parent != null)
            {
                return;
            }

            AddNode(gotoTempAsg);

            // For block 0, we don't need to add the extra "reset" at the beginning,
            // because it is already the first node to be executed on the shader,
            // so it is reset to false by the "local" assignment anyway.
            if (block.Index != 0)
            {
                CurrentFunction.MainBlock.AddFirst(Assign(gotoTempAsg.Destination, Const(IrConsts.False)));
            }
        }

        private void NewBlock(AstBlockType type, Operation branchOp, int endIndex)
        {
            NewBlock(type, GetBranchCond(type, branchOp), endIndex);
        }

        private void NewBlock(AstBlockType type, IAstNode cond, int endIndex)
        {
            AstBlock childBlock = new(type, cond);

            AddNode(childBlock);

            _blockStack.Push((_currBlock, _currEndIndex, _loopEndIndex));

            _currBlock = childBlock;
            _currEndIndex = endIndex;

            if (type == AstBlockType.DoWhile)
            {
                _loopEndIndex = endIndex;
            }
        }

        private IAstNode GetBranchCond(AstBlockType type, Operation branchOp)
        {
            IAstNode cond;

            if (branchOp.Inst == Instruction.Branch)
            {
                // If the branch is not conditional, the condition is a constant.
                // For if it's false (always jump over, if block never executed).
                // For loops it's always true (always loop).
                cond = Const(type == AstBlockType.If ? IrConsts.False : IrConsts.True);
            }
            else
            {
                cond = GetOperand(branchOp.GetSource(0));

                Instruction invInst = type == AstBlockType.If
                    ? Instruction.BranchIfTrue
                    : Instruction.BranchIfFalse;

                if (branchOp.Inst == invInst)
                {
                    cond = new AstOperation(Instruction.LogicalNot, cond);
                }
            }

            return cond;
        }

        public void AddNode(IAstNode node)
        {
            _currBlock.Add(node);
        }

        public GotoStatement[] GetGotos()
        {
            return _gotos.ToArray();
        }

        public AstOperand NewTemp(AggregateType type)
        {
            AstOperand newTemp = Local(type);

            CurrentFunction.Locals.Add(newTemp);

            return newTemp;
        }

        public IAstNode GetOperandOrCbLoad(Operand operand)
        {
            if (operand.Type == OperandType.ConstantBuffer)
            {
                int cbufSlot = operand.GetCbufSlot();
                int cbufOffset = operand.GetCbufOffset();

                int binding = ResourceManager.GetConstantBufferBinding(cbufSlot);
                int vecIndex = cbufOffset >> 2;
                int elemIndex = cbufOffset & 3;

                ResourceManager.SetUsedConstantBufferBinding(binding);

                IAstNode[] sources = new IAstNode[]
                {
                    new AstOperand(OperandType.Constant, binding),
                    new AstOperand(OperandType.Constant, 0),
                    new AstOperand(OperandType.Constant, vecIndex),
                    new AstOperand(OperandType.Constant, elemIndex),
                };

                return new AstOperation(Instruction.Load, StorageKind.ConstantBuffer, false, sources, sources.Length);
            }

            return GetOperand(operand);
        }

        public AstOperand GetOperand(Operand operand)
        {
            if (operand == null)
            {
                return null;
            }

            if (operand.Type != OperandType.LocalVariable)
            {
                return new AstOperand(operand);
            }

            if (!_localsMap.TryGetValue(operand, out AstOperand astOperand))
            {
                astOperand = new AstOperand(operand);

                _localsMap.Add(operand, astOperand);

                CurrentFunction.Locals.Add(astOperand);
            }

            return astOperand;
        }
    }
}
