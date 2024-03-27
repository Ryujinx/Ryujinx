using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using Spv.Generator;
using System;
using System.Collections.Generic;
using static Spv.Specification;
using Instruction = Spv.Generator.Instruction;

namespace Ryujinx.Graphics.Shader.CodeGen.Spirv
{
    using IrOperandType = IntermediateRepresentation.OperandType;

    partial class CodeGenContext : Module
    {
        private const uint SpirvVersionMajor = 1;
        private const uint SpirvVersionMinor = 3;
        private const uint SpirvVersionRevision = 0;
        private const uint SpirvVersionPacked = (SpirvVersionMajor << 16) | (SpirvVersionMinor << 8) | SpirvVersionRevision;

        public StructuredProgramInfo Info { get; }

        public AttributeUsage AttributeUsage { get; }
        public ShaderDefinitions Definitions { get; }
        public ShaderProperties Properties { get; }
        public HostCapabilities HostCapabilities { get; }
        public ILogger Logger { get; }
        public TargetApi TargetApi { get; }

        public Dictionary<int, Instruction> ConstantBuffers { get; } = new();
        public Dictionary<int, Instruction> StorageBuffers { get; } = new();

        public Dictionary<int, Instruction> LocalMemories { get; } = new();
        public Dictionary<int, Instruction> SharedMemories { get; } = new();

        public Dictionary<int, SamplerType> SamplersTypes { get; } = new();
        public Dictionary<int, SamplerDeclaration> Samplers { get; } = new();
        public Dictionary<int, ImageDeclaration> Images { get; } = new();

        public Dictionary<IoDefinition, Instruction> Inputs { get; } = new();
        public Dictionary<IoDefinition, Instruction> Outputs { get; } = new();
        public Dictionary<IoDefinition, Instruction> InputsPerPatch { get; } = new();
        public Dictionary<IoDefinition, Instruction> OutputsPerPatch { get; } = new();

        public StructuredFunction CurrentFunction { get; set; }
        private readonly Dictionary<AstOperand, Instruction> _locals = new();
        private readonly Dictionary<int, Instruction> _funcArgs = new();
        private readonly Dictionary<int, (StructuredFunction, Instruction)> _functions = new();

        private class BlockState
        {
            private int _entryCount;
            private readonly List<Instruction> _labels = new();

            public Instruction GetNextLabel(CodeGenContext context)
            {
                return GetLabel(context, _entryCount);
            }

            public Instruction GetNextLabelAutoIncrement(CodeGenContext context)
            {
                return GetLabel(context, _entryCount++);
            }

            public Instruction GetLabel(CodeGenContext context, int index)
            {
                while (index >= _labels.Count)
                {
                    _labels.Add(context.Label());
                }

                return _labels[index];
            }
        }

        private readonly Dictionary<AstBlock, BlockState> _labels = new();

        public Dictionary<AstBlock, (Instruction, Instruction)> LoopTargets { get; set; }

        public AstBlock CurrentBlock { get; private set; }

        public SpirvDelegates Delegates { get; }

        public bool IsMainFunction { get; private set; }
        public bool MayHaveReturned { get; set; }

        public CodeGenContext(
            StructuredProgramInfo info,
            CodeGenParameters parameters,
            GeneratorPool<Instruction> instPool,
            GeneratorPool<LiteralInteger> integerPool) : base(SpirvVersionPacked, instPool, integerPool)
        {
            Info = info;
            AttributeUsage = parameters.AttributeUsage;
            Definitions = parameters.Definitions;
            Properties = parameters.Properties;
            HostCapabilities = parameters.HostCapabilities;
            Logger = parameters.Logger;
            TargetApi = parameters.TargetApi;

            AddCapability(Capability.Shader);
            AddCapability(Capability.Float64);

            SetMemoryModel(AddressingModel.Logical, MemoryModel.GLSL450);

            Delegates = new SpirvDelegates(this);
        }

        public void StartFunction(bool isMainFunction)
        {
            IsMainFunction = isMainFunction;
            MayHaveReturned = false;
            _locals.Clear();
            _funcArgs.Clear();
        }

        public void EnterBlock(AstBlock block)
        {
            CurrentBlock = block;
            AddLabel(GetBlockStateLazy(block).GetNextLabelAutoIncrement(this));
        }

        public Instruction GetFirstLabel(AstBlock block)
        {
            return GetBlockStateLazy(block).GetLabel(this, 0);
        }

        public Instruction GetNextLabel(AstBlock block)
        {
            return GetBlockStateLazy(block).GetNextLabel(this);
        }

        private BlockState GetBlockStateLazy(AstBlock block)
        {
            if (!_labels.TryGetValue(block, out var blockState))
            {
                blockState = new BlockState();

                _labels.Add(block, blockState);
            }

            return blockState;
        }

        public Instruction NewBlock()
        {
            var label = Label();
            Branch(label);
            AddLabel(label);
            return label;
        }

        public Instruction[] GetMainInterface()
        {
            var mainInterface = new List<Instruction>();

            mainInterface.AddRange(Inputs.Values);
            mainInterface.AddRange(Outputs.Values);
            mainInterface.AddRange(InputsPerPatch.Values);
            mainInterface.AddRange(OutputsPerPatch.Values);

            return mainInterface.ToArray();
        }

        public void DeclareLocal(AstOperand local, Instruction spvLocal)
        {
            _locals.Add(local, spvLocal);
        }

        public void DeclareArgument(int argIndex, Instruction spvLocal)
        {
            _funcArgs.Add(argIndex, spvLocal);
        }

        public void DeclareFunction(int funcIndex, StructuredFunction function, Instruction spvFunc)
        {
            _functions.Add(funcIndex, (function, spvFunc));
        }

        public Instruction GetFP32(IAstNode node)
        {
            return Get(AggregateType.FP32, node);
        }

        public Instruction GetFP64(IAstNode node)
        {
            return Get(AggregateType.FP64, node);
        }

        public Instruction GetS32(IAstNode node)
        {
            return Get(AggregateType.S32, node);
        }

        public Instruction GetU32(IAstNode node)
        {
            return Get(AggregateType.U32, node);
        }

        public Instruction Get(AggregateType type, IAstNode node)
        {
            if (node is AstOperation operation)
            {
                var opResult = Instructions.Generate(this, operation);
                return BitcastIfNeeded(type, opResult.Type, opResult.Value);
            }
            else if (node is AstOperand operand)
            {
                return operand.Type switch
                {
                    IrOperandType.Argument => GetArgument(type, operand),
                    IrOperandType.Constant => GetConstant(type, operand),
                    IrOperandType.LocalVariable => GetLocal(type, operand),
                    IrOperandType.Undefined => GetUndefined(type),
                    _ => throw new ArgumentException($"Invalid operand type \"{operand.Type}\"."),
                };
            }

            throw new NotImplementedException(node.GetType().Name);
        }

        public Instruction GetWithType(IAstNode node, out AggregateType type)
        {
            if (node is AstOperation operation)
            {
                var opResult = Instructions.Generate(this, operation);
                type = opResult.Type;
                return opResult.Value;
            }
            else if (node is AstOperand operand)
            {
                switch (operand.Type)
                {
                    case IrOperandType.LocalVariable:
                        type = operand.VarType;
                        return GetLocal(type, operand);
                    default:
                        throw new ArgumentException($"Invalid operand type \"{operand.Type}\".");
                }
            }

            throw new NotImplementedException(node.GetType().Name);
        }

        private Instruction GetUndefined(AggregateType type)
        {
            return type switch
            {
                AggregateType.Bool => ConstantFalse(TypeBool()),
                AggregateType.FP32 => Constant(TypeFP32(), 0f),
                AggregateType.FP64 => Constant(TypeFP64(), 0d),
                _ => Constant(GetType(type), 0),
            };
        }

        public Instruction GetConstant(AggregateType type, AstOperand operand)
        {
            return type switch
            {
                AggregateType.Bool => operand.Value != 0 ? ConstantTrue(TypeBool()) : ConstantFalse(TypeBool()),
                AggregateType.FP32 => Constant(TypeFP32(), BitConverter.Int32BitsToSingle(operand.Value)),
                AggregateType.FP64 => Constant(TypeFP64(), (double)BitConverter.Int32BitsToSingle(operand.Value)),
                AggregateType.S32 => Constant(TypeS32(), operand.Value),
                AggregateType.U32 => Constant(TypeU32(), (uint)operand.Value),
                _ => throw new ArgumentException($"Invalid type \"{type}\"."),
            };
        }

        public Instruction GetLocalPointer(AstOperand local)
        {
            return _locals[local];
        }

        public Instruction GetArgumentPointer(AstOperand funcArg)
        {
            return _funcArgs[funcArg.Value];
        }

        public Instruction GetLocal(AggregateType dstType, AstOperand local)
        {
            var srcType = local.VarType;
            return BitcastIfNeeded(dstType, srcType, Load(GetType(srcType), GetLocalPointer(local)));
        }

        public Instruction GetArgument(AggregateType dstType, AstOperand funcArg)
        {
            var srcType = funcArg.VarType;
            return BitcastIfNeeded(dstType, srcType, Load(GetType(srcType), GetArgumentPointer(funcArg)));
        }

        public (StructuredFunction, Instruction) GetFunction(int funcIndex)
        {
            return _functions[funcIndex];
        }

        public Instruction GetType(AggregateType type, int length = 1)
        {
            if ((type & AggregateType.Array) != 0)
            {
                if (length > 0)
                {
                    return TypeArray(GetType(type & ~AggregateType.Array), Constant(TypeU32(), length));
                }
                else
                {
                    return TypeRuntimeArray(GetType(type & ~AggregateType.Array));
                }
            }
            else if ((type & AggregateType.ElementCountMask) != 0)
            {
                int vectorLength = (type & AggregateType.ElementCountMask) switch
                {
                    AggregateType.Vector2 => 2,
                    AggregateType.Vector3 => 3,
                    AggregateType.Vector4 => 4,
                    _ => 1,
                };

                return TypeVector(GetType(type & ~AggregateType.ElementCountMask), vectorLength);
            }

            return type switch
            {
                AggregateType.Void => TypeVoid(),
                AggregateType.Bool => TypeBool(),
                AggregateType.FP32 => TypeFP32(),
                AggregateType.FP64 => TypeFP64(),
                AggregateType.S32 => TypeS32(),
                AggregateType.U32 => TypeU32(),
                _ => throw new ArgumentException($"Invalid attribute type \"{type}\"."),
            };
        }

        public Instruction BitcastIfNeeded(AggregateType dstType, AggregateType srcType, Instruction value)
        {
            if (dstType == srcType)
            {
                return value;
            }

            if (dstType == AggregateType.Bool)
            {
                return INotEqual(TypeBool(), BitcastIfNeeded(AggregateType.S32, srcType, value), Constant(TypeS32(), 0));
            }
            else if (srcType == AggregateType.Bool)
            {
                var intTrue = Constant(TypeS32(), IrConsts.True);
                var intFalse = Constant(TypeS32(), IrConsts.False);

                return BitcastIfNeeded(dstType, AggregateType.S32, Select(TypeS32(), value, intTrue, intFalse));
            }
            else
            {
                return Bitcast(GetType(dstType, 1), value);
            }
        }

        public Instruction TypeS32()
        {
            return TypeInt(32, true);
        }

        public Instruction TypeU32()
        {
            return TypeInt(32, false);
        }

        public Instruction TypeFP32()
        {
            return TypeFloat(32);
        }

        public Instruction TypeFP64()
        {
            return TypeFloat(64);
        }
    }
}
