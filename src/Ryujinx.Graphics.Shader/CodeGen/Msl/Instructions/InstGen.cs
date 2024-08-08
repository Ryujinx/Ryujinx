using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Text;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenBallot;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenBarrier;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenCall;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenHelper;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenMemory;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenVector;
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

                StringBuilder builder = new();

                if (atomic && (operation.StorageKind == StorageKind.StorageBuffer || operation.StorageKind == StorageKind.SharedMemory))
                {
                    AggregateType dstType = operation.Inst == Instruction.AtomicMaxS32 || operation.Inst == Instruction.AtomicMinS32
                        ? AggregateType.S32
                        : AggregateType.U32;

                    var shared = operation.StorageKind == StorageKind.SharedMemory;

                    builder.Append($"({(shared ? "threadgroup" : "device")} {Declarations.GetVarTypeName(dstType, true)}*)&{GenerateLoadOrStore(context, operation, isStore: false)}");

                    for (int argIndex = operation.SourcesCount - arity + 2; argIndex < operation.SourcesCount; argIndex++)
                    {
                        builder.Append($", {GetSourceExpr(context, operation.GetSource(argIndex), dstType)}, memory_order_relaxed");
                    }
                }
                else
                {
                    for (int argIndex = 0; argIndex < arity; argIndex++)
                    {
                        if (argIndex != 0)
                        {
                            builder.Append(", ");
                        }

                        AggregateType dstType = GetSrcVarType(inst, argIndex);

                        builder.Append(GetSourceExpr(context, operation.GetSource(argIndex), dstType));
                    }

                    if ((operation.Inst & Instruction.Mask) == Instruction.SwizzleAdd)
                    {
                        // SwizzleAdd takes one last argument, the thread_index_in_simdgroup
                        builder.Append(", thread_index_in_simdgroup");
                    }
                }

                return $"{info.OpName}({builder})";
            }
            else if ((info.Type & InstType.Op) != 0)
            {
                string op = info.OpName;

                if (inst == Instruction.Return && operation.SourcesCount != 0)
                {
                    return $"{op} {GetSourceExpr(context, operation.GetSource(0), context.CurrentFunction.ReturnType)}";
                }
                if (inst == Instruction.Return && context.Definitions.Stage is ShaderStage.Vertex or ShaderStage.Fragment)
                {
                    return $"{op} out";
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
                        if (operation.ForcePrecise)
                        {
                            var func = (inst & Instruction.Mask) switch
                            {
                                Instruction.Add => "PreciseFAdd",
                                Instruction.Subtract => "PreciseFSub",
                                Instruction.Multiply => "PreciseFMul",
                            };

                            return $"{func}({expr[0]}, {expr[1]})";
                        }

                        return $"{expr[0]} {op} {expr[1]}";

                    case 3:
                        return $"{expr[0]} {op[0]} {expr[1]} {op[1]} {expr[2]}";
                }
            }
            else if ((info.Type & InstType.Special) != 0)
            {
                switch (inst & Instruction.Mask)
                {
                    case Instruction.Ballot:
                        return Ballot(context, operation);
                    case Instruction.Call:
                        return Call(context, operation);
                    case Instruction.FSIBegin:
                    case Instruction.FSIEnd:
                        return "// FSI implemented with raster order groups in MSL";
                    case Instruction.GroupMemoryBarrier:
                    case Instruction.MemoryBarrier:
                    case Instruction.Barrier:
                        return Barrier(context, operation);
                    case Instruction.ImageLoad:
                    case Instruction.ImageStore:
                    case Instruction.ImageAtomic:
                        return ImageLoadOrStore(context, operation);
                    case Instruction.Load:
                        return Load(context, operation);
                    case Instruction.Lod:
                        return Lod(context, operation);
                    case Instruction.Store:
                        return Store(context, operation);
                    case Instruction.TextureSample:
                        return TextureSample(context, operation);
                    case Instruction.TextureQuerySamples:
                        return TextureQuerySamples(context, operation);
                    case Instruction.TextureQuerySize:
                        return TextureQuerySize(context, operation);
                    case Instruction.PackHalf2x16:
                        return PackHalf2x16(context, operation);
                    case Instruction.UnpackHalf2x16:
                        return UnpackHalf2x16(context, operation);
                    case Instruction.VectorExtract:
                        return VectorExtract(context, operation);
                    case Instruction.VoteAllEqual:
                        return VoteAllEqual(context, operation);
                }
            }

            // TODO: Return this to being an error
            return $"Unexpected instruction type \"{info.Type}\".";
        }
    }
}
