using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Linq;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.TypeConversion;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class MslGenerator
    {
        public static string Generate(StructuredProgramInfo info, CodeGenParameters parameters)
        {
            if (parameters.Definitions.Stage is not (ShaderStage.Vertex or ShaderStage.Fragment or ShaderStage.Compute))
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Attempted to generate unsupported shader type {parameters.Definitions.Stage}!");
                return "";
            }

            CodeGenContext context = new(info, parameters);

            var sets = Declarations.Declare(context, info);

            if (info.Functions.Count != 0)
            {
                for (int i = 1; i < info.Functions.Count; i++)
                {
                    PrintFunction(context, info.Functions[i], parameters.Definitions.Stage, sets);

                    context.AppendLine();
                }
            }

            PrintFunction(context, info.Functions[0], parameters.Definitions.Stage, sets, true);

            return context.GetCode();
        }

        private static void PrintFunction(CodeGenContext context, StructuredFunction function, ShaderStage stage, int[] sets, bool isMainFunc = false)
        {
            context.CurrentFunction = function;

            context.AppendLine(GetFunctionSignature(context, function, stage, sets, isMainFunc));
            context.EnterScope();

            Declarations.DeclareLocals(context, function, stage, isMainFunc);

            PrintBlock(context, function.MainBlock, isMainFunc);

            // In case the shader hasn't returned, return
            if (isMainFunc && stage != ShaderStage.Compute)
            {
                context.AppendLine("return out;");
            }

            context.LeaveScope();
        }

        private static string GetFunctionSignature(
            CodeGenContext context,
            StructuredFunction function,
            ShaderStage stage,
            int[] sets,
            bool isMainFunc = false)
        {
            int additionalArgCount = isMainFunc ? 0 : CodeGenContext.AdditionalArgCount + (context.Definitions.Stage != ShaderStage.Compute ? 1 : 0);
            bool needsThreadIndex = false;

            // TODO: Replace this with a proper flag
            if (function.Name.Contains("Shuffle"))
            {
                needsThreadIndex = true;
                additionalArgCount++;
            }

            string[] args = new string[additionalArgCount + function.InArguments.Length + function.OutArguments.Length];

            // All non-main functions need to be able to access the support_buffer as well
            if (!isMainFunc)
            {
                if (stage != ShaderStage.Compute)
                {
                    args[0] = stage == ShaderStage.Vertex ? "VertexIn in" : "FragmentIn in";
                    args[1] = "constant ConstantBuffers &constant_buffers";
                    args[2] = "device StorageBuffers &storage_buffers";

                    if (needsThreadIndex)
                    {
                        args[3] = "uint thread_index_in_simdgroup";
                    }
                }
                else
                {
                    args[0] = "constant ConstantBuffers &constant_buffers";
                    args[1] = "device StorageBuffers &storage_buffers";

                    if (needsThreadIndex)
                    {
                        args[2] = "uint thread_index_in_simdgroup";
                    }
                }
            }

            int argIndex = additionalArgCount;
            for (int i = 0; i < function.InArguments.Length; i++)
            {
                args[argIndex++] = $"{Declarations.GetVarTypeName(function.InArguments[i])} {OperandManager.GetArgumentName(i)}";
            }

            for (int i = 0; i < function.OutArguments.Length; i++)
            {
                int j = i + function.InArguments.Length;

                args[argIndex++] = $"thread {Declarations.GetVarTypeName(function.OutArguments[i])} &{OperandManager.GetArgumentName(j)}";
            }

            string funcKeyword = "inline";
            string funcName = null;
            string returnType = Declarations.GetVarTypeName(function.ReturnType);

            if (isMainFunc)
            {
                if (stage == ShaderStage.Vertex)
                {
                    funcKeyword = "vertex";
                    funcName = "vertexMain";
                    returnType = "VertexOut";
                }
                else if (stage == ShaderStage.Fragment)
                {
                    funcKeyword = "fragment";
                    funcName = "fragmentMain";
                    returnType = "FragmentOut";
                }
                else if (stage == ShaderStage.Compute)
                {
                    funcKeyword = "kernel";
                    funcName = "kernelMain";
                    returnType = "void";
                }

                if (stage == ShaderStage.Vertex)
                {
                    args = args.Prepend("VertexIn in [[stage_in]]").ToArray();
                }
                else if (stage == ShaderStage.Fragment)
                {
                    args = args.Prepend("FragmentIn in [[stage_in]]").ToArray();
                }

                // TODO: add these only if they are used
                if (stage == ShaderStage.Vertex)
                {
                    args = args.Append("uint vertex_id [[vertex_id]]").ToArray();
                    args = args.Append("uint instance_id [[instance_id]]").ToArray();
                    args = args.Append("uint base_instance [[base_instance]]").ToArray();
                    args = args.Append("uint base_vertex [[base_vertex]]").ToArray();
                }
                else if (stage == ShaderStage.Compute)
                {
                    args = args.Append("uint3 threadgroup_position_in_grid [[threadgroup_position_in_grid]]").ToArray();
                    args = args.Append("uint3 thread_position_in_grid [[thread_position_in_grid]]").ToArray();
                    args = args.Append("uint3 thread_position_in_threadgroup [[thread_position_in_threadgroup]]").ToArray();
                    args = args.Append("uint thread_index_in_simdgroup [[thread_index_in_simdgroup]]").ToArray();
                }

                args = args.Append($"constant ConstantBuffers &constant_buffers [[buffer({Defaults.ConstantBuffersIndex})]]").ToArray();
                args = args.Append($"device StorageBuffers &storage_buffers [[buffer({Defaults.StorageBuffersIndex})]]").ToArray();

                foreach (var set in sets)
                {
                    var bindingIndex = set + Defaults.BaseSetIndex;
                    args = args.Append($"constant {Declarations.GetNameForSet(set)} &{Declarations.GetNameForSet(set, true)} [[buffer({bindingIndex})]]").ToArray();
                }
            }

            var funcPrefix = $"{funcKeyword} {returnType} {funcName ?? function.Name}(";
            var indent = new string(' ', funcPrefix.Length);

            return $"{funcPrefix}{string.Join($", \n{indent}", args)})";
        }

        private static void PrintBlock(CodeGenContext context, AstBlock block, bool isMainFunction)
        {
            AstBlockVisitor visitor = new(block);

            visitor.BlockEntered += (sender, e) =>
            {
                switch (e.Block.Type)
                {
                    case AstBlockType.DoWhile:
                        context.AppendLine("do");
                        break;

                    case AstBlockType.Else:
                        context.AppendLine("else");
                        break;

                    case AstBlockType.ElseIf:
                        context.AppendLine($"else if ({GetCondExpr(context, e.Block.Condition)})");
                        break;

                    case AstBlockType.If:
                        context.AppendLine($"if ({GetCondExpr(context, e.Block.Condition)})");
                        break;

                    default:
                        throw new InvalidOperationException($"Found unexpected block type \"{e.Block.Type}\".");
                }

                context.EnterScope();
            };

            visitor.BlockLeft += (sender, e) =>
            {
                context.LeaveScope();

                if (e.Block.Type == AstBlockType.DoWhile)
                {
                    context.AppendLine($"while ({GetCondExpr(context, e.Block.Condition)});");
                }
            };

            bool supportsBarrierDivergence = context.HostCapabilities.SupportsShaderBarrierDivergence;
            bool mayHaveReturned = false;

            foreach (IAstNode node in visitor.Visit())
            {
                if (node is AstOperation operation)
                {
                    if (!supportsBarrierDivergence)
                    {
                        if (operation.Inst == IntermediateRepresentation.Instruction.Barrier)
                        {
                            // Barrier on divergent control flow paths may cause the GPU to hang,
                            // so skip emitting the barrier for those cases.
                            if (visitor.Block.Type != AstBlockType.Main || mayHaveReturned || !isMainFunction)
                            {
                                context.Logger.Log($"Shader has barrier on potentially divergent block, the barrier will be removed.");

                                continue;
                            }
                        }
                        else if (operation.Inst == IntermediateRepresentation.Instruction.Return)
                        {
                            mayHaveReturned = true;
                        }
                    }

                    string expr = InstGen.GetExpression(context, operation);

                    if (expr != null)
                    {
                        context.AppendLine(expr + ";");
                    }
                }
                else if (node is AstAssignment assignment)
                {
                    AggregateType dstType = OperandManager.GetNodeDestType(context, assignment.Destination);
                    AggregateType srcType = OperandManager.GetNodeDestType(context, assignment.Source);

                    string dest = InstGen.GetExpression(context, assignment.Destination);
                    string src = ReinterpretCast(context, assignment.Source, srcType, dstType);

                    context.AppendLine(dest + " = " + src + ";");
                }
                else if (node is AstComment comment)
                {
                    context.AppendLine("// " + comment.Comment);
                }
                else
                {
                    throw new InvalidOperationException($"Found unexpected node type \"{node?.GetType().Name ?? "null"}\".");
                }
            }
        }

        private static string GetCondExpr(CodeGenContext context, IAstNode cond)
        {
            AggregateType srcType = OperandManager.GetNodeDestType(context, cond);

            return ReinterpretCast(context, cond, srcType, AggregateType.Bool);
        }
    }
}
