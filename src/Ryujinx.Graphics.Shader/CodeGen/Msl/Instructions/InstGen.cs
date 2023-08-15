using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;

using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenCall;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenHelper;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenMemory;
using static Ryujinx.Graphics.Shader.StructuredIr.InstructionInfo;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGen
    {
        public static string GetExpression(CodeGenContext context, IAstNode node)
        {
            if (node is AstOperation operation)
            {
                return GetExpression(context, operation);
            }
            else if (node is AstOperand operand)
            {
                return context.OperandManager.GetExpression(context, operand);
            }

            throw new ArgumentException($"Invalid node type \"{node?.GetType().Name ?? "null"}\".");
        }

        private static string GetExpression(CodeGenContext context, AstOperation operation)
        {
            Instruction inst = operation.Inst;

            InstInfo info = GetInstructionInfo(inst);

            if ((info.Type & InstType.Call) != 0)
            {
                bool atomic = (info.Type & InstType.Atomic) != 0;

                int arity = (int)(info.Type & InstType.ArityMask);

                string args = string.Empty;

                if (atomic)
                {
                    // Hell
                }
                else
                {
                    for (int argIndex = 0; argIndex < arity; argIndex++)
                    {
                        if (argIndex != 0)
                        {
                            args += ", ";
                        }

                        AggregateType dstType = GetSrcVarType(inst, argIndex);

                        args += GetSourceExpr(context, operation.GetSource(argIndex), dstType);
                    }
                }

                return info.OpName + '(' + args + ')';
            }
            else if ((info.Type & InstType.Op) != 0)
            {
                string op = info.OpName;

                if (inst == Instruction.Return && operation.SourcesCount != 0)
                {
                    return $"{op} {GetSourceExpr(context, operation.GetSource(0), context.CurrentFunction.ReturnType)}";
                }

                int arity = (int)(info.Type & InstType.ArityMask);

                string[] expr = new string[arity];

                for (int index = 0; index < arity; index++)
                {
                    IAstNode src = operation.GetSource(index);

                    string srcExpr = GetSourceExpr(context, src, GetSrcVarType(inst, index));

                    bool isLhs = arity == 2 && index == 0;

                    expr[index] = Enclose(srcExpr, src, inst, info, isLhs);
                }

                switch (arity)
                {
                    case 0:
                        return op;

                    case 1:
                        return op + expr[0];

                    case 2:
                        return $"{expr[0]} {op} {expr[1]}";

                    case 3:
                        return $"{expr[0]} {op[0]} {expr[1]} {op[1]} {expr[2]}";
                }
            }
            else if ((info.Type & InstType.Special) != 0)
            {
                switch (inst & Instruction.Mask)
                {
                    case Instruction.Barrier:
                        return "|| BARRIER ||";
                    case Instruction.Call:
                        return Call(context, operation);
                    case Instruction.FSIBegin:
                        return "|| FSI BEGIN ||";
                    case Instruction.FSIEnd:
                        return "|| FSI END ||";
                    case Instruction.FindLSB:
                        return "|| FIND LSB ||";
                    case Instruction.FindMSBS32:
                        return "|| FIND MSB S32 ||";
                    case Instruction.FindMSBU32:
                        return "|| FIND MSB U32 ||";
                    case Instruction.GroupMemoryBarrier:
                        return "|| FIND GROUP MEMORY BARRIER ||";
                    case Instruction.ImageLoad:
                        return "|| IMAGE LOAD ||";
                    case Instruction.ImageStore:
                        return "|| IMAGE STORE ||";
                    case Instruction.ImageAtomic:
                        return "|| IMAGE ATOMIC ||";
                    case Instruction.Load:
                        return Load(context, operation);
                    case Instruction.Lod:
                        return "|| LOD ||";
                    case Instruction.MemoryBarrier:
                        return "|| MEMORY BARRIER ||";
                    case Instruction.Store:
                        return Store(context, operation);
                    case Instruction.TextureSample:
                        return TextureSample(context, operation);
                    case Instruction.TextureSize:
                        return "|| TEXTURE SIZE ||";
                    case Instruction.VectorExtract:
                        return "|| VECTOR EXTRACT ||";
                    case Instruction.VoteAllEqual:
                        return "|| VOTE ALL EQUAL ||";
                }
            }

            throw new InvalidOperationException($"Unexpected instruction type \"{info.Type}\".");
        }
    }
}
