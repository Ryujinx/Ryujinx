using Ryujinx.Common;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Generic;
using System.Threading;
using static Spv.Specification;

namespace Ryujinx.Graphics.Shader.CodeGen.Spirv
{
    using SpvInstruction = Spv.Generator.Instruction;
    using SpvInstructionPool = Spv.Generator.GeneratorPool<Spv.Generator.Instruction>;
    using SpvLiteralInteger = Spv.Generator.LiteralInteger;
    using SpvLiteralIntegerPool = Spv.Generator.GeneratorPool<Spv.Generator.LiteralInteger>;

    static class SpirvGenerator
    {
        // Resource pools for Spirv generation. Note: Increase count when more threads are being used.
        private const int GeneratorPoolCount = 1;
        private static readonly ObjectPool<SpvInstructionPool> _instructionPool;
        private static readonly ObjectPool<SpvLiteralIntegerPool> _integerPool;
        private static readonly Lock _poolLock;

        static SpirvGenerator()
        {
            _instructionPool = new(() => new SpvInstructionPool(), GeneratorPoolCount);
            _integerPool = new(() => new SpvLiteralIntegerPool(), GeneratorPoolCount);
            _poolLock = new Lock();
        }

        private const HelperFunctionsMask NeedsInvocationIdMask = HelperFunctionsMask.SwizzleAdd;

        public static byte[] Generate(StructuredProgramInfo info, CodeGenParameters parameters)
        {
            SpvInstructionPool instPool;
            SpvLiteralIntegerPool integerPool;

            lock (_poolLock)
            {
                instPool = _instructionPool.Allocate();
                integerPool = _integerPool.Allocate();
            }

            CodeGenContext context = new(info, parameters, instPool, integerPool);

            context.AddCapability(Capability.Shader);

            context.SetMemoryModel(AddressingModel.Logical, MemoryModel.GLSL450);

            context.AddCapability(Capability.GroupNonUniformBallot);
            context.AddCapability(Capability.GroupNonUniformShuffle);
            context.AddCapability(Capability.GroupNonUniformVote);
            context.AddCapability(Capability.ImageBuffer);
            context.AddCapability(Capability.ImageGatherExtended);
            context.AddCapability(Capability.ImageQuery);
            context.AddCapability(Capability.SampledBuffer);

            if (parameters.HostCapabilities.SupportsShaderFloat64)
            {
                context.AddCapability(Capability.Float64);
            }

            if (parameters.Definitions.TransformFeedbackEnabled && parameters.Definitions.LastInVertexPipeline)
            {
                context.AddCapability(Capability.TransformFeedback);
            }

            if (parameters.Definitions.Stage == ShaderStage.Fragment)
            {
                if (context.Info.IoDefinitions.Contains(new IoDefinition(StorageKind.Input, IoVariable.Layer)) ||
                    context.Info.IoDefinitions.Contains(new IoDefinition(StorageKind.Input, IoVariable.PrimitiveId)))
                {
                    context.AddCapability(Capability.Geometry);
                }

                if (context.HostCapabilities.SupportsFragmentShaderInterlock)
                {
                    context.AddCapability(Capability.FragmentShaderPixelInterlockEXT);
                    context.AddExtension("SPV_EXT_fragment_shader_interlock");
                }
            }
            else if (parameters.Definitions.Stage == ShaderStage.Geometry)
            {
                context.AddCapability(Capability.Geometry);

                if (parameters.Definitions.GpPassthrough && context.HostCapabilities.SupportsGeometryShaderPassthrough)
                {
                    context.AddExtension("SPV_NV_geometry_shader_passthrough");
                    context.AddCapability(Capability.GeometryShaderPassthroughNV);
                }
            }
            else if (parameters.Definitions.Stage == ShaderStage.TessellationControl ||
                     parameters.Definitions.Stage == ShaderStage.TessellationEvaluation)
            {
                context.AddCapability(Capability.Tessellation);
            }
            else if (parameters.Definitions.Stage == ShaderStage.Vertex)
            {
                context.AddCapability(Capability.DrawParameters);
            }

            if (context.Definitions.Stage != ShaderStage.Fragment &&
                context.Definitions.Stage != ShaderStage.Geometry &&
                context.Definitions.Stage != ShaderStage.Compute &&
                (context.Info.IoDefinitions.Contains(new IoDefinition(StorageKind.Output, IoVariable.Layer)) ||
                context.Info.IoDefinitions.Contains(new IoDefinition(StorageKind.Output, IoVariable.ViewportIndex))))
            {
                context.AddExtension("SPV_EXT_shader_viewport_index_layer");
                context.AddCapability(Capability.ShaderViewportIndexLayerEXT);
            }

            if (context.Info.IoDefinitions.Contains(new IoDefinition(StorageKind.Output, IoVariable.ViewportMask)))
            {
                context.AddExtension("SPV_NV_viewport_array2");
                context.AddCapability(Capability.ShaderViewportMaskNV);
            }

            if ((info.HelperFunctionsMask & NeedsInvocationIdMask) != 0)
            {
                info.IoDefinitions.Add(new IoDefinition(StorageKind.Input, IoVariable.SubgroupLaneId));
            }

            Declarations.DeclareAll(context, info);

            for (int funcIndex = 0; funcIndex < info.Functions.Count; funcIndex++)
            {
                var function = info.Functions[funcIndex];
                var retType = context.GetType(function.ReturnType);

                var funcArgs = new SpvInstruction[function.InArguments.Length + function.OutArguments.Length];

                for (int argIndex = 0; argIndex < funcArgs.Length; argIndex++)
                {
                    var argType = context.GetType(function.GetArgumentType(argIndex));
                    var argPointerType = context.TypePointer(StorageClass.Function, argType);
                    funcArgs[argIndex] = argPointerType;
                }

                var funcType = context.TypeFunction(retType, false, funcArgs);
                var spvFunc = context.Function(retType, FunctionControlMask.MaskNone, funcType);

                context.DeclareFunction(funcIndex, function, spvFunc);
            }

            for (int funcIndex = 0; funcIndex < info.Functions.Count; funcIndex++)
            {
                Generate(context, info, funcIndex);
            }

            byte[] result = context.Generate();

            lock (_poolLock)
            {
                _instructionPool.Release(instPool);
                _integerPool.Release(integerPool);
            }

            return result;
        }

        private static void Generate(CodeGenContext context, StructuredProgramInfo info, int funcIndex)
        {
            var (function, spvFunc) = context.GetFunction(funcIndex);

            context.CurrentFunction = function;
            context.AddFunction(spvFunc);
            context.StartFunction(isMainFunction: funcIndex == 0);

            Declarations.DeclareParameters(context, function);

            context.EnterBlock(function.MainBlock);

            Declarations.DeclareLocals(context, function);

            Generate(context, function.MainBlock);

            // Functions must always end with a return.
            if (function.MainBlock.Last is not AstOperation operation ||
                (operation.Inst != Instruction.Return && operation.Inst != Instruction.Discard))
            {
                context.Return();
            }

            context.FunctionEnd();

            if (funcIndex == 0)
            {
                context.AddEntryPoint(context.Definitions.Stage.Convert(), spvFunc, "main", context.GetMainInterface());

                if (context.Definitions.Stage == ShaderStage.TessellationControl)
                {
                    context.AddExecutionMode(spvFunc, ExecutionMode.OutputVertices, (SpvLiteralInteger)context.Definitions.ThreadsPerInputPrimitive);
                }
                else if (context.Definitions.Stage == ShaderStage.TessellationEvaluation)
                {
                    switch (context.Definitions.TessPatchType)
                    {
                        case TessPatchType.Isolines:
                            context.AddExecutionMode(spvFunc, ExecutionMode.Isolines);
                            break;
                        case TessPatchType.Triangles:
                            context.AddExecutionMode(spvFunc, ExecutionMode.Triangles);
                            break;
                        case TessPatchType.Quads:
                            context.AddExecutionMode(spvFunc, ExecutionMode.Quads);
                            break;
                    }

                    switch (context.Definitions.TessSpacing)
                    {
                        case TessSpacing.EqualSpacing:
                            context.AddExecutionMode(spvFunc, ExecutionMode.SpacingEqual);
                            break;
                        case TessSpacing.FractionalEventSpacing:
                            context.AddExecutionMode(spvFunc, ExecutionMode.SpacingFractionalEven);
                            break;
                        case TessSpacing.FractionalOddSpacing:
                            context.AddExecutionMode(spvFunc, ExecutionMode.SpacingFractionalOdd);
                            break;
                    }

                    bool tessCw = context.Definitions.TessCw;

                    if (context.TargetApi == TargetApi.Vulkan)
                    {
                        // We invert the front face on Vulkan backend, so we need to do that here as well.
                        tessCw = !tessCw;
                    }

                    if (tessCw)
                    {
                        context.AddExecutionMode(spvFunc, ExecutionMode.VertexOrderCw);
                    }
                    else
                    {
                        context.AddExecutionMode(spvFunc, ExecutionMode.VertexOrderCcw);
                    }
                }
                else if (context.Definitions.Stage == ShaderStage.Geometry)
                {
                    context.AddExecutionMode(spvFunc, context.Definitions.InputTopology switch
                    {
                        InputTopology.Points => ExecutionMode.InputPoints,
                        InputTopology.Lines => ExecutionMode.InputLines,
                        InputTopology.LinesAdjacency => ExecutionMode.InputLinesAdjacency,
                        InputTopology.Triangles => ExecutionMode.Triangles,
                        InputTopology.TrianglesAdjacency => ExecutionMode.InputTrianglesAdjacency,
                        _ => throw new InvalidOperationException($"Invalid input topology \"{context.Definitions.InputTopology}\"."),
                    });

                    context.AddExecutionMode(spvFunc, ExecutionMode.Invocations, (SpvLiteralInteger)context.Definitions.ThreadsPerInputPrimitive);

                    context.AddExecutionMode(spvFunc, context.Definitions.OutputTopology switch
                    {
                        OutputTopology.PointList => ExecutionMode.OutputPoints,
                        OutputTopology.LineStrip => ExecutionMode.OutputLineStrip,
                        OutputTopology.TriangleStrip => ExecutionMode.OutputTriangleStrip,
                        _ => throw new InvalidOperationException($"Invalid output topology \"{context.Definitions.OutputTopology}\"."),
                    });

                    context.AddExecutionMode(spvFunc, ExecutionMode.OutputVertices, (SpvLiteralInteger)context.Definitions.MaxOutputVertices);
                }
                else if (context.Definitions.Stage == ShaderStage.Fragment)
                {
                    context.AddExecutionMode(spvFunc, context.Definitions.OriginUpperLeft
                        ? ExecutionMode.OriginUpperLeft
                        : ExecutionMode.OriginLowerLeft);

                    if (context.Info.IoDefinitions.Contains(new IoDefinition(StorageKind.Output, IoVariable.FragmentOutputDepth)))
                    {
                        context.AddExecutionMode(spvFunc, ExecutionMode.DepthReplacing);
                    }

                    if (context.Definitions.EarlyZForce)
                    {
                        context.AddExecutionMode(spvFunc, ExecutionMode.EarlyFragmentTests);
                    }

                    if ((info.HelperFunctionsMask & HelperFunctionsMask.FSI) != 0 &&
                        context.HostCapabilities.SupportsFragmentShaderInterlock)
                    {
                        context.AddExecutionMode(spvFunc, ExecutionMode.PixelInterlockOrderedEXT);
                    }
                }
                else if (context.Definitions.Stage == ShaderStage.Compute)
                {
                    var localSizeX = (SpvLiteralInteger)context.Definitions.ComputeLocalSizeX;
                    var localSizeY = (SpvLiteralInteger)context.Definitions.ComputeLocalSizeY;
                    var localSizeZ = (SpvLiteralInteger)context.Definitions.ComputeLocalSizeZ;

                    context.AddExecutionMode(
                        spvFunc,
                        ExecutionMode.LocalSize,
                        localSizeX,
                        localSizeY,
                        localSizeZ);
                }

                if (context.Definitions.TransformFeedbackEnabled && context.Definitions.LastInVertexPipeline)
                {
                    context.AddExecutionMode(spvFunc, ExecutionMode.Xfb);
                }
            }
        }

        private static void Generate(CodeGenContext context, AstBlock block)
        {
            AstBlockVisitor visitor = new(block);

            var loopTargets = new Dictionary<AstBlock, (SpvInstruction, SpvInstruction)>();

            context.LoopTargets = loopTargets;

            visitor.BlockEntered += (sender, e) =>
            {
                AstBlock mergeBlock = e.Block.Parent;

                if (e.Block.Type == AstBlockType.If)
                {
                    AstBlock ifTrueBlock = e.Block;
                    AstBlock ifFalseBlock;

                    if (AstHelper.Next(e.Block) is AstBlock nextBlock && nextBlock.Type == AstBlockType.Else)
                    {
                        ifFalseBlock = nextBlock;
                    }
                    else
                    {
                        ifFalseBlock = mergeBlock;
                    }

                    var condition = context.Get(AggregateType.Bool, e.Block.Condition);

                    context.SelectionMerge(context.GetNextLabel(mergeBlock), SelectionControlMask.MaskNone);
                    context.BranchConditional(condition, context.GetNextLabel(ifTrueBlock), context.GetNextLabel(ifFalseBlock));
                }
                else if (e.Block.Type == AstBlockType.DoWhile)
                {
                    var continueTarget = context.Label();

                    loopTargets.Add(e.Block, (context.NewBlock(), continueTarget));

                    context.LoopMerge(context.GetNextLabel(mergeBlock), continueTarget, LoopControlMask.MaskNone);
                    context.Branch(context.GetFirstLabel(e.Block));
                }

                context.EnterBlock(e.Block);
            };

            visitor.BlockLeft += (sender, e) =>
            {
                if (e.Block.Parent != null)
                {
                    if (e.Block.Type == AstBlockType.DoWhile)
                    {
                        // This is a loop, we need to jump back to the loop header
                        // if the condition is true.
                        AstBlock mergeBlock = e.Block.Parent;

                        var (loopTarget, continueTarget) = loopTargets[e.Block];

                        context.Branch(continueTarget);
                        context.AddLabel(continueTarget);

                        var condition = context.Get(AggregateType.Bool, e.Block.Condition);

                        context.BranchConditional(condition, loopTarget, context.GetNextLabel(mergeBlock));
                    }
                    else
                    {
                        // We only need a branch if the last instruction didn't
                        // already cause the program to exit or jump elsewhere.
                        bool lastIsCf = e.Block.Last is AstOperation lastOp &&
                            (lastOp.Inst == Instruction.Discard ||
                             lastOp.Inst == Instruction.LoopBreak ||
                             lastOp.Inst == Instruction.LoopContinue ||
                             lastOp.Inst == Instruction.Return);

                        if (!lastIsCf)
                        {
                            context.Branch(context.GetNextLabel(e.Block.Parent));
                        }
                    }

                    bool hasElse = AstHelper.Next(e.Block) is AstBlock nextBlock &&
                        (nextBlock.Type == AstBlockType.Else ||
                         nextBlock.Type == AstBlockType.ElseIf);

                    // Re-enter the parent block.
                    if (e.Block.Parent != null && !hasElse)
                    {
                        context.EnterBlock(e.Block.Parent);
                    }
                }
            };

            foreach (IAstNode node in visitor.Visit())
            {
                if (node is AstAssignment assignment)
                {
                    var dest = (AstOperand)assignment.Destination;

                    if (dest.Type == OperandType.LocalVariable)
                    {
                        var source = context.Get(dest.VarType, assignment.Source);
                        context.Store(context.GetLocalPointer(dest), source);
                    }
                    else if (dest.Type == OperandType.Argument)
                    {
                        var source = context.Get(dest.VarType, assignment.Source);
                        context.Store(context.GetArgumentPointer(dest), source);
                    }
                    else
                    {
                        throw new NotImplementedException(dest.Type.ToString());
                    }
                }
                else if (node is AstOperation operation)
                {
                    Instructions.Generate(context, operation);
                }
            }
        }
    }
}
