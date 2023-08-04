using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using System;

using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenHelper;
using static Ryujinx.Graphics.Shader.StructuredIr.InstructionInfo;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGen
    {
        private static string GetExpression(CodeGenContext context, AstOperation operation)
        {
            Instruction inst = operation.Inst;

            InstInfo info = GetInstructionInfo(inst);

            if ((info.Type & InstType.Call) != 0)
            {
                bool atomic = (info.Type & InstType.Atomic) != 0;

                int arity = (int)(info.Type & InstType.ArityMask);

                string args = string.Empty;

                // Generate function

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
                        return "";
                    case Instruction.Call:
                        return "";
                    case Instruction.FSIBegin:
                        return "";
                    case Instruction.FSIEnd:
                        return "";
                    case Instruction.FindLSB:
                        return "";
                    case Instruction.FindMSBS32:
                        return "";
                    case Instruction.FindMSBU32:
                        return "";
                    case Instruction.GroupMemoryBarrier:
                        return "";
                    case Instruction.ImageLoad:
                        return "";
                    case Instruction.ImageStore:
                        return "";
                    case Instruction.ImageAtomic:
                        return "";
                    case Instruction.Load:
                        return "";
                    case Instruction.Lod:
                        return "";
                    case Instruction.MemoryBarrier:
                        return "";
                    case Instruction.Store:
                        return "";
                    case Instruction.TextureSample:
                        return "";
                    case Instruction.TextureSize:
                        return "";
                    case Instruction.VectorExtract:
                        return "";
                    case Instruction.VoteAllEqual:
                        return "";
                }
            }

            throw new InvalidOperationException($"Unexpected instruction type \"{info.Type}\".");
        }
    }
}
