using Ryujinx.Graphics.Shader.CodeGen;
using Ryujinx.Graphics.Shader.CodeGen.Glsl;
using Ryujinx.Graphics.Shader.CodeGen.Spirv;
using Ryujinx.Graphics.Shader.Decoders;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation.Optimizations;
using Ryujinx.Graphics.Shader.Translation.Transforms;
using System;
using System.Collections.Generic;
using System.Numerics;
using static Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandHelper;
using static Ryujinx.Graphics.Shader.Translation.Translator;

namespace Ryujinx.Graphics.Shader.Translation
{
    public class TranslatorContext
    {
        private readonly DecodedProgram _program;
        private readonly int _localMemorySize;
        private IoUsage _vertexOutput;

        public ulong Address { get; }
        public int Size { get; }
        public int Cb1DataSize => _program.Cb1DataSize;

        internal AttributeUsage AttributeUsage => _program.AttributeUsage;

        internal ShaderDefinitions Definitions { get; }

        public ShaderStage Stage => Definitions.Stage;

        internal IGpuAccessor GpuAccessor { get; }

        internal TranslationOptions Options { get; }

        private bool IsTransformFeedbackEmulated => !GpuAccessor.QueryHostSupportsTransformFeedback() && GpuAccessor.QueryTransformFeedbackEnabled();
        public bool HasStore => _program.UsedFeatures.HasFlag(FeatureFlags.Store) || (IsTransformFeedbackEmulated && Definitions.LastInVertexPipeline);

        public bool LayerOutputWritten { get; private set; }
        public int LayerOutputAttribute { get; private set; }

        internal TranslatorContext(
            ulong address,
            int size,
            int localMemorySize,
            ShaderDefinitions definitions,
            IGpuAccessor gpuAccessor,
            TranslationOptions options,
            DecodedProgram program)
        {
            Address = address;
            Size = size;
            _program = program;
            _localMemorySize = localMemorySize;
            _vertexOutput = new IoUsage(FeatureFlags.None, 0, -1);
            Definitions = definitions;
            GpuAccessor = gpuAccessor;
            Options = options;
        }

        private static bool IsLoadUserDefined(Operation operation)
        {
            // TODO: Check if sources count match and all sources are constant.
            return operation.Inst == Instruction.Load && (IoVariable)operation.GetSource(0).Value == IoVariable.UserDefined;
        }

        private static bool IsStoreUserDefined(Operation operation)
        {
            // TODO: Check if sources count match and all sources are constant.
            return operation.Inst == Instruction.Store && (IoVariable)operation.GetSource(0).Value == IoVariable.UserDefined;
        }

        private static FunctionCode[] Combine(FunctionCode[] a, FunctionCode[] b, int aStart)
        {
            // Here we combine two shaders.
            // For shader A:
            // - All user attribute stores on shader A are turned into copies to a
            // temporary variable. It's assumed that shader B will consume them.
            // - All return instructions are turned into branch instructions, the
            // branch target being the start of the shader B code.
            // For shader B:
            // - All user attribute loads on shader B are turned into copies from a
            // temporary variable, as long that attribute is written by shader A.
            FunctionCode[] output = new FunctionCode[a.Length + b.Length - 1];

            List<Operation> ops = new(a.Length + b.Length);

            Operand[] temps = new Operand[AttributeConsts.UserAttributesCount * 4];

            Operand lblB = Label();

            for (int index = aStart; index < a[0].Code.Length; index++)
            {
                Operation operation = a[0].Code[index];

                if (IsStoreUserDefined(operation))
                {
                    int tIndex = operation.GetSource(1).Value * 4 + operation.GetSource(2).Value;

                    Operand temp = temps[tIndex];

                    if (temp == null)
                    {
                        temp = Local();

                        temps[tIndex] = temp;
                    }

                    operation.Dest = temp;
                    operation.TurnIntoCopy(operation.GetSource(operation.SourcesCount - 1));
                }

                if (operation.Inst == Instruction.Return)
                {
                    ops.Add(new Operation(Instruction.Branch, lblB));
                }
                else
                {
                    ops.Add(operation);
                }
            }

            ops.Add(new Operation(Instruction.MarkLabel, lblB));

            for (int index = 0; index < b[0].Code.Length; index++)
            {
                Operation operation = b[0].Code[index];

                if (IsLoadUserDefined(operation))
                {
                    int tIndex = operation.GetSource(1).Value * 4 + operation.GetSource(2).Value;

                    Operand temp = temps[tIndex];

                    if (temp != null)
                    {
                        operation.TurnIntoCopy(temp);
                    }
                }

                ops.Add(operation);
            }

            output[0] = new FunctionCode(ops.ToArray());

            for (int i = 1; i < a.Length; i++)
            {
                output[i] = a[i];
            }

            for (int i = 1; i < b.Length; i++)
            {
                output[a.Length + i - 1] = b[i];
            }

            return output;
        }

        internal int GetDepthRegister()
        {
            // The depth register is always two registers after the last color output.
            return BitOperations.PopCount((uint)Definitions.OmapTargets) + 1;
        }

        public void SetLayerOutputAttribute(int attr)
        {
            LayerOutputWritten = true;
            LayerOutputAttribute = attr;
        }

        public void SetLastInVertexPipeline()
        {
            Definitions.LastInVertexPipeline = true;
        }

        public void SetNextStage(TranslatorContext nextStage)
        {
            AttributeUsage.MergeFromtNextStage(
                Definitions.GpPassthrough,
                nextStage._program.UsedFeatures.HasFlag(FeatureFlags.FixedFuncAttr),
                nextStage.AttributeUsage);

            // We don't consider geometry shaders using the geometry shader passthrough feature
            // as being the last because when this feature is used, it can't actually modify any of the outputs,
            // so the stage that comes before it is the last one that can do modifications.
            if (nextStage.Definitions.Stage != ShaderStage.Fragment &&
                (nextStage.Definitions.Stage != ShaderStage.Geometry || !nextStage.Definitions.GpPassthrough))
            {
                Definitions.LastInVertexPipeline = false;
            }
        }

        public ShaderProgram Translate(bool asCompute = false)
        {
            ResourceManager resourceManager = CreateResourceManager(asCompute);

            bool usesLocalMemory = _program.UsedFeatures.HasFlag(FeatureFlags.LocalMemory);

            resourceManager.SetCurrentLocalMemory(_localMemorySize, usesLocalMemory);

            if (Stage == ShaderStage.Compute)
            {
                bool usesSharedMemory = _program.UsedFeatures.HasFlag(FeatureFlags.SharedMemory);

                resourceManager.SetCurrentSharedMemory(GpuAccessor.QueryComputeSharedMemorySize(), usesSharedMemory);
            }

            FunctionCode[] code = EmitShader(this, resourceManager, _program, asCompute, initializeOutputs: true, out _);

            return Translate(code, resourceManager, _program.UsedFeatures, _program.ClipDistancesWritten, asCompute);
        }

        public ShaderProgram Translate(TranslatorContext other, bool asCompute = false)
        {
            ResourceManager resourceManager = CreateResourceManager(asCompute);

            bool usesLocalMemory = _program.UsedFeatures.HasFlag(FeatureFlags.LocalMemory);
            resourceManager.SetCurrentLocalMemory(_localMemorySize, usesLocalMemory);

            FunctionCode[] code = EmitShader(this, resourceManager, _program, asCompute, initializeOutputs: false, out _);

            bool otherUsesLocalMemory = other._program.UsedFeatures.HasFlag(FeatureFlags.LocalMemory);
            resourceManager.SetCurrentLocalMemory(other._localMemorySize, otherUsesLocalMemory);

            FunctionCode[] otherCode = EmitShader(other, resourceManager, other._program, asCompute, initializeOutputs: true, out int aStart);

            code = Combine(otherCode, code, aStart);

            return Translate(
                code,
                resourceManager,
                _program.UsedFeatures | other._program.UsedFeatures,
                (byte)(_program.ClipDistancesWritten | other._program.ClipDistancesWritten),
                asCompute);
        }

        private ShaderProgram Translate(FunctionCode[] functions, ResourceManager resourceManager, FeatureFlags usedFeatures, byte clipDistancesWritten, bool asCompute)
        {
            if (asCompute)
            {
                usedFeatures |= FeatureFlags.VtgAsCompute;
            }

            var cfgs = new ControlFlowGraph[functions.Length];
            var frus = new RegisterUsage.FunctionRegisterUsage[functions.Length];

            for (int i = 0; i < functions.Length; i++)
            {
                cfgs[i] = ControlFlowGraph.Create(functions[i].Code);

                if (i != 0)
                {
                    frus[i] = RegisterUsage.RunPass(cfgs[i]);
                }
            }

            List<Function> funcs = new(functions.Length);

            for (int i = 0; i < functions.Length; i++)
            {
                funcs.Add(null);
            }

            HelperFunctionManager hfm = new(funcs, Definitions.Stage);

            for (int i = 0; i < functions.Length; i++)
            {
                var cfg = cfgs[i];

                int inArgumentsCount = 0;
                int outArgumentsCount = 0;

                if (i != 0)
                {
                    var fru = frus[i];

                    inArgumentsCount = fru.InArguments.Length;
                    outArgumentsCount = fru.OutArguments.Length;
                }

                if (cfg.Blocks.Length != 0)
                {
                    RegisterUsage.FixupCalls(cfg.Blocks, frus);

                    Dominance.FindDominators(cfg);
                    Dominance.FindDominanceFrontiers(cfg.Blocks);

                    Ssa.Rename(cfg.Blocks);

                    TransformContext context = new(
                        hfm,
                        cfg.Blocks,
                        Definitions,
                        resourceManager,
                        GpuAccessor,
                        Options.TargetApi,
                        Options.TargetLanguage,
                        Definitions.Stage,
                        ref usedFeatures);

                    Optimizer.RunPass(context);
                    TransformPasses.RunPass(context);
                }

                funcs[i] = new Function(cfg.Blocks, $"fun{i}", false, inArgumentsCount, outArgumentsCount);
            }

            return Generate(
                funcs,
                AttributeUsage,
                GetDefinitions(asCompute),
                Definitions,
                resourceManager,
                usedFeatures,
                clipDistancesWritten);
        }

        private ShaderProgram Generate(
            IReadOnlyList<Function> funcs,
            AttributeUsage attributeUsage,
            ShaderDefinitions definitions,
            ShaderDefinitions originalDefinitions,
            ResourceManager resourceManager,
            FeatureFlags usedFeatures,
            byte clipDistancesWritten)
        {
            var sInfo = StructuredProgram.MakeStructuredProgram(
                funcs,
                attributeUsage,
                definitions,
                resourceManager,
                Options.TargetLanguage,
                Options.Flags.HasFlag(TranslationFlags.DebugMode));

            int geometryVerticesPerPrimitive = Definitions.OutputTopology switch
            {
                OutputTopology.LineStrip => 2,
                OutputTopology.TriangleStrip => 3,
                _ => 1
            };

            var info = new ShaderProgramInfo(
                resourceManager.GetConstantBufferDescriptors(),
                resourceManager.GetStorageBufferDescriptors(),
                resourceManager.GetTextureDescriptors(),
                resourceManager.GetImageDescriptors(),
                originalDefinitions.Stage,
                geometryVerticesPerPrimitive,
                originalDefinitions.MaxOutputVertices,
                originalDefinitions.ThreadsPerInputPrimitive,
                usedFeatures.HasFlag(FeatureFlags.FragCoordXY),
                usedFeatures.HasFlag(FeatureFlags.InstanceId),
                usedFeatures.HasFlag(FeatureFlags.DrawParameters),
                usedFeatures.HasFlag(FeatureFlags.RtLayer),
                clipDistancesWritten,
                originalDefinitions.OmapTargets);

            var hostCapabilities = new HostCapabilities(
                GpuAccessor.QueryHostReducedPrecision(),
                GpuAccessor.QueryHostSupportsFragmentShaderInterlock(),
                GpuAccessor.QueryHostSupportsFragmentShaderOrderingIntel(),
                GpuAccessor.QueryHostSupportsGeometryShaderPassthrough(),
                GpuAccessor.QueryHostSupportsShaderBallot(),
                GpuAccessor.QueryHostSupportsShaderBarrierDivergence(),
                GpuAccessor.QueryHostSupportsTextureShadowLod(),
                GpuAccessor.QueryHostSupportsViewportMask());

            var parameters = new CodeGenParameters(attributeUsage, definitions, resourceManager.Properties, hostCapabilities, GpuAccessor, Options.TargetApi);

            return Options.TargetLanguage switch
            {
                TargetLanguage.Glsl => new ShaderProgram(info, TargetLanguage.Glsl, GlslGenerator.Generate(sInfo, parameters)),
                TargetLanguage.Spirv => new ShaderProgram(info, TargetLanguage.Spirv, SpirvGenerator.Generate(sInfo, parameters)),
                _ => throw new NotImplementedException(Options.TargetLanguage.ToString()),
            };
        }

        private ResourceManager CreateResourceManager(bool vertexAsCompute)
        {
            ResourceManager resourceManager = new(Definitions.Stage, GpuAccessor, GetResourceReservations());

            if (IsTransformFeedbackEmulated)
            {
                StructureType tfeDataStruct = new(new StructureField[]
                {
                    new StructureField(AggregateType.Array | AggregateType.U32, "data", 0)
                });

                for (int i = 0; i < ResourceReservations.TfeBuffersCount; i++)
                {
                    int binding = resourceManager.Reservations.GetTfeBufferStorageBufferBinding(i);
                    BufferDefinition tfeDataBuffer = new(BufferLayout.Std430, 1, binding, $"tfe_data{i}", tfeDataStruct);
                    resourceManager.Properties.AddOrUpdateStorageBuffer(tfeDataBuffer);
                }
            }

            if (vertexAsCompute)
            {
                int vertexInfoCbBinding = resourceManager.Reservations.VertexInfoConstantBufferBinding;
                BufferDefinition vertexInfoBuffer = new(BufferLayout.Std140, 0, vertexInfoCbBinding, "vb_info", VertexInfoBuffer.GetStructureType());
                resourceManager.Properties.AddOrUpdateConstantBuffer(vertexInfoBuffer);

                StructureType vertexOutputStruct = new(new StructureField[]
                {
                    new StructureField(AggregateType.Array | AggregateType.FP32, "data", 0)
                });

                int vertexOutputSbBinding = resourceManager.Reservations.VertexOutputStorageBufferBinding;
                BufferDefinition vertexOutputBuffer = new(BufferLayout.Std430, 1, vertexOutputSbBinding, "vertex_output", vertexOutputStruct);
                resourceManager.Properties.AddOrUpdateStorageBuffer(vertexOutputBuffer);

                if (Stage == ShaderStage.Vertex)
                {
                    int ibBinding = resourceManager.Reservations.IndexBufferTextureBinding;
                    TextureDefinition indexBuffer = new(2, ibBinding, 1, "ib_data", SamplerType.TextureBuffer, TextureFormat.Unknown, TextureUsageFlags.None);
                    resourceManager.Properties.AddOrUpdateTexture(indexBuffer);

                    int inputMap = _program.AttributeUsage.UsedInputAttributes;

                    while (inputMap != 0)
                    {
                        int location = BitOperations.TrailingZeroCount(inputMap);
                        int binding = resourceManager.Reservations.GetVertexBufferTextureBinding(location);
                        TextureDefinition vaBuffer = new(2, binding, 1, $"vb_data{location}", SamplerType.TextureBuffer, TextureFormat.Unknown, TextureUsageFlags.None);
                        resourceManager.Properties.AddOrUpdateTexture(vaBuffer);

                        inputMap &= ~(1 << location);
                    }
                }
                else if (Stage == ShaderStage.Geometry)
                {
                    int trbBinding = resourceManager.Reservations.TopologyRemapBufferTextureBinding;
                    TextureDefinition remapBuffer = new(2, trbBinding, 1, "trb_data", SamplerType.TextureBuffer, TextureFormat.Unknown, TextureUsageFlags.None);
                    resourceManager.Properties.AddOrUpdateTexture(remapBuffer);

                    int geometryVbOutputSbBinding = resourceManager.Reservations.GeometryVertexOutputStorageBufferBinding;
                    BufferDefinition geometryVbOutputBuffer = new(BufferLayout.Std430, 1, geometryVbOutputSbBinding, "geometry_vb_output", vertexOutputStruct);
                    resourceManager.Properties.AddOrUpdateStorageBuffer(geometryVbOutputBuffer);

                    StructureType geometryIbOutputStruct = new(new StructureField[]
                    {
                        new StructureField(AggregateType.Array | AggregateType.U32, "data", 0)
                    });

                    int geometryIbOutputSbBinding = resourceManager.Reservations.GeometryIndexOutputStorageBufferBinding;
                    BufferDefinition geometryIbOutputBuffer = new(BufferLayout.Std430, 1, geometryIbOutputSbBinding, "geometry_ib_output", geometryIbOutputStruct);
                    resourceManager.Properties.AddOrUpdateStorageBuffer(geometryIbOutputBuffer);
                }

                resourceManager.SetVertexAsComputeLocalMemories(Definitions.Stage, Definitions.InputTopology);
            }

            return resourceManager;
        }

        private ShaderDefinitions GetDefinitions(bool vertexAsCompute)
        {
            if (vertexAsCompute)
            {
                return new ShaderDefinitions(ShaderStage.Compute, 32, 32, 1);
            }
            else
            {
                return Definitions;
            }
        }

        public ResourceReservations GetResourceReservations()
        {
            IoUsage ioUsage = _program.GetIoUsage();

            if (Definitions.GpPassthrough)
            {
                ioUsage = ioUsage.Combine(_vertexOutput);
            }

            return new ResourceReservations(GpuAccessor, IsTransformFeedbackEmulated, vertexAsCompute: true, _vertexOutput, ioUsage);
        }

        public void SetVertexOutputMapForGeometryAsCompute(TranslatorContext vertexContext)
        {
            _vertexOutput = vertexContext._program.GetIoUsage();
        }

        public ShaderProgram GenerateVertexPassthroughForCompute()
        {
            var attributeUsage = new AttributeUsage(GpuAccessor);
            var resourceManager = new ResourceManager(ShaderStage.Vertex, GpuAccessor);

            var reservations = GetResourceReservations();

            int vertexInfoCbBinding = reservations.VertexInfoConstantBufferBinding;

            if (Stage == ShaderStage.Vertex)
            {
                BufferDefinition vertexInfoBuffer = new(BufferLayout.Std140, 0, vertexInfoCbBinding, "vb_info", VertexInfoBuffer.GetStructureType());
                resourceManager.Properties.AddOrUpdateConstantBuffer(vertexInfoBuffer);
            }

            StructureType vertexInputStruct = new(new StructureField[]
            {
                new StructureField(AggregateType.Array | AggregateType.FP32, "data", 0)
            });

            int vertexDataSbBinding = reservations.VertexOutputStorageBufferBinding;
            BufferDefinition vertexOutputBuffer = new(BufferLayout.Std430, 1, vertexDataSbBinding, "vb_input", vertexInputStruct);
            resourceManager.Properties.AddOrUpdateStorageBuffer(vertexOutputBuffer);

            var context = new EmitterContext();

            Operand vertexIndex = Options.TargetApi == TargetApi.OpenGL
                ? context.Load(StorageKind.Input, IoVariable.VertexId)
                : context.Load(StorageKind.Input, IoVariable.VertexIndex);

            if (Stage == ShaderStage.Vertex)
            {
                Operand vertexCount = context.Load(StorageKind.ConstantBuffer, vertexInfoCbBinding, Const((int)VertexInfoBufferField.VertexCounts), Const(0));

                // Base instance will be always zero when this shader is used, so which one we use here doesn't really matter.
                Operand instanceId = Options.TargetApi == TargetApi.OpenGL
                    ? context.Load(StorageKind.Input, IoVariable.InstanceId)
                    : context.Load(StorageKind.Input, IoVariable.InstanceIndex);

                vertexIndex = context.IAdd(context.IMultiply(instanceId, vertexCount), vertexIndex);
            }

            Operand baseOffset = context.IMultiply(vertexIndex, Const(reservations.OutputSizePerInvocation));

            foreach ((IoDefinition ioDefinition, int inputOffset) in reservations.Offsets)
            {
                if (ioDefinition.StorageKind != StorageKind.Output)
                {
                    continue;
                }

                Operand vertexOffset = inputOffset != 0 ? context.IAdd(baseOffset, Const(inputOffset)) : baseOffset;
                Operand value = context.Load(StorageKind.StorageBuffer, vertexDataSbBinding, Const(0), vertexOffset);

                if (ioDefinition.IoVariable == IoVariable.UserDefined)
                {
                    context.Store(StorageKind.Output, ioDefinition.IoVariable, null, Const(ioDefinition.Location), Const(ioDefinition.Component), value);
                    attributeUsage.SetOutputUserAttribute(ioDefinition.Location);
                }
                else if (ResourceReservations.IsVectorOrArrayVariable(ioDefinition.IoVariable))
                {
                    context.Store(StorageKind.Output, ioDefinition.IoVariable, null, Const(ioDefinition.Component), value);
                }
                else
                {
                    context.Store(StorageKind.Output, ioDefinition.IoVariable, null, value);
                }
            }

            var operations = context.GetOperations();
            var cfg = ControlFlowGraph.Create(operations);
            var function = new Function(cfg.Blocks, "main", false, 0, 0);

            var transformFeedbackOutputs = GetTransformFeedbackOutputs(GpuAccessor, out ulong transformFeedbackVecMap);

            var definitions = new ShaderDefinitions(ShaderStage.Vertex, transformFeedbackVecMap, transformFeedbackOutputs)
            {
                LastInVertexPipeline = true
            };

            return Generate(
                new[] { function },
                attributeUsage,
                definitions,
                definitions,
                resourceManager,
                FeatureFlags.None,
                0);
        }

        public ShaderProgram GenerateGeometryPassthrough()
        {
            int outputAttributesMask = AttributeUsage.UsedOutputAttributes;
            int layerOutputAttr = LayerOutputAttribute;

            if (LayerOutputWritten)
            {
                outputAttributesMask |= 1 << ((layerOutputAttr - AttributeConsts.UserAttributeBase) / 16);
            }

            OutputTopology outputTopology;
            int maxOutputVertices;

            switch (Definitions.InputTopology)
            {
                case InputTopology.Points:
                    outputTopology = OutputTopology.PointList;
                    maxOutputVertices = 1;
                    break;
                case InputTopology.Lines:
                case InputTopology.LinesAdjacency:
                    outputTopology = OutputTopology.LineStrip;
                    maxOutputVertices = 2;
                    break;
                default:
                    outputTopology = OutputTopology.TriangleStrip;
                    maxOutputVertices = 3;
                    break;
            }

            var attributeUsage = new AttributeUsage(GpuAccessor);
            var resourceManager = new ResourceManager(ShaderStage.Geometry, GpuAccessor);

            var context = new EmitterContext();

            for (int v = 0; v < maxOutputVertices; v++)
            {
                int outAttrsMask = outputAttributesMask;

                while (outAttrsMask != 0)
                {
                    int attrIndex = BitOperations.TrailingZeroCount(outAttrsMask);

                    outAttrsMask &= ~(1 << attrIndex);

                    for (int c = 0; c < 4; c++)
                    {
                        int attr = AttributeConsts.UserAttributeBase + attrIndex * 16 + c * 4;

                        Operand value = context.Load(StorageKind.Input, IoVariable.UserDefined, Const(attrIndex), Const(v), Const(c));

                        if (attr == layerOutputAttr)
                        {
                            context.Store(StorageKind.Output, IoVariable.Layer, null, value);
                        }
                        else
                        {
                            context.Store(StorageKind.Output, IoVariable.UserDefined, null, Const(attrIndex), Const(c), value);
                        }
                    }
                }

                for (int c = 0; c < 4; c++)
                {
                    Operand value = context.Load(StorageKind.Input, IoVariable.Position, Const(v), Const(c));

                    context.Store(StorageKind.Output, IoVariable.Position, null, Const(c), value);
                }

                context.EmitVertex();
            }

            context.EndPrimitive();

            var operations = context.GetOperations();
            var cfg = ControlFlowGraph.Create(operations);
            var function = new Function(cfg.Blocks, "main", false, 0, 0);

            var definitions = new ShaderDefinitions(
                ShaderStage.Geometry,
                GpuAccessor.QueryGraphicsState(),
                false,
                1,
                outputTopology,
                maxOutputVertices);

            return Generate(
                new[] { function },
                attributeUsage,
                definitions,
                definitions,
                resourceManager,
                FeatureFlags.RtLayer,
                0);
        }
    }
}
