using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.Translation;
using System.Collections.Generic;
using static Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandHelper;

namespace Ryujinx.Graphics.Shader.Instructions
{
    static class AttributeMap
    {
        private enum StagesMask : byte
        {
            None = 0,
            Compute = 1 << (int)ShaderStage.Compute,
            Vertex = 1 << (int)ShaderStage.Vertex,
            TessellationControl = 1 << (int)ShaderStage.TessellationControl,
            TessellationEvaluation = 1 << (int)ShaderStage.TessellationEvaluation,
            Geometry = 1 << (int)ShaderStage.Geometry,
            Fragment = 1 << (int)ShaderStage.Fragment,

            Tessellation = TessellationControl | TessellationEvaluation,
            VertexTessellationGeometry = Vertex | Tessellation | Geometry,
            TessellationGeometryFragment = Tessellation | Geometry | Fragment,
            AllGraphics = Vertex | Tessellation | Geometry | Fragment,
        }

        private readonly struct AttributeEntry
        {
            public int BaseOffset { get; }
            public AggregateType Type { get; }
            public IoVariable IoVariable { get; }
            public StagesMask InputMask { get; }
            public StagesMask OutputMask { get; }

            public AttributeEntry(
                int baseOffset,
                AggregateType type,
                IoVariable ioVariable,
                StagesMask inputMask,
                StagesMask outputMask)
            {
                BaseOffset = baseOffset;
                Type = type;
                IoVariable = ioVariable;
                InputMask = inputMask;
                OutputMask = outputMask;
            }
        }

        private static readonly Dictionary<int, AttributeEntry> _attributes;
        private static readonly IReadOnlyDictionary<int, AttributeEntry> _attributesPerPatch;

        static AttributeMap()
        {
            _attributes = CreateMap();
            _attributesPerPatch = CreatePerPatchMap();
        }

        private static Dictionary<int, AttributeEntry> CreateMap()
        {
            var map = new Dictionary<int, AttributeEntry>();

            Add(map, 0x060, AggregateType.S32, IoVariable.PrimitiveId, StagesMask.TessellationGeometryFragment, StagesMask.Geometry);
            Add(map, 0x064, AggregateType.S32, IoVariable.Layer, StagesMask.Fragment, StagesMask.VertexTessellationGeometry);
            Add(map, 0x068, AggregateType.S32, IoVariable.ViewportIndex, StagesMask.Fragment, StagesMask.VertexTessellationGeometry);
            Add(map, 0x06c, AggregateType.FP32, IoVariable.PointSize, StagesMask.None, StagesMask.VertexTessellationGeometry);
            Add(map, 0x070, AggregateType.Vector4 | AggregateType.FP32, IoVariable.Position, StagesMask.TessellationGeometryFragment, StagesMask.VertexTessellationGeometry);
            Add(map, 0x080, AggregateType.Vector4 | AggregateType.FP32, IoVariable.UserDefined, StagesMask.AllGraphics, StagesMask.VertexTessellationGeometry, 32);
            Add(map, 0x280, AggregateType.Vector4 | AggregateType.FP32, IoVariable.FrontColorDiffuse, StagesMask.TessellationGeometryFragment, StagesMask.VertexTessellationGeometry);
            Add(map, 0x290, AggregateType.Vector4 | AggregateType.FP32, IoVariable.FrontColorSpecular, StagesMask.TessellationGeometryFragment, StagesMask.VertexTessellationGeometry);
            Add(map, 0x2a0, AggregateType.Vector4 | AggregateType.FP32, IoVariable.BackColorDiffuse, StagesMask.TessellationGeometryFragment, StagesMask.VertexTessellationGeometry);
            Add(map, 0x2b0, AggregateType.Vector4 | AggregateType.FP32, IoVariable.BackColorSpecular, StagesMask.TessellationGeometryFragment, StagesMask.VertexTessellationGeometry);
            Add(map, 0x2c0, AggregateType.Array | AggregateType.FP32, IoVariable.ClipDistance, StagesMask.TessellationGeometryFragment, StagesMask.VertexTessellationGeometry, 8);
            Add(map, 0x2e0, AggregateType.Vector2 | AggregateType.FP32, IoVariable.PointCoord, StagesMask.Fragment, StagesMask.None);
            Add(map, 0x2e8, AggregateType.FP32, IoVariable.FogCoord, StagesMask.TessellationGeometryFragment, StagesMask.VertexTessellationGeometry);
            Add(map, 0x2f0, AggregateType.Vector2 | AggregateType.FP32, IoVariable.TessellationCoord, StagesMask.TessellationEvaluation, StagesMask.None);
            Add(map, 0x2f8, AggregateType.S32, IoVariable.InstanceId, StagesMask.Vertex, StagesMask.None);
            Add(map, 0x2fc, AggregateType.S32, IoVariable.VertexId, StagesMask.Vertex, StagesMask.None);
            Add(map, 0x300, AggregateType.Vector4 | AggregateType.FP32, IoVariable.TextureCoord, StagesMask.TessellationGeometryFragment, StagesMask.VertexTessellationGeometry);
            Add(map, 0x3a0, AggregateType.Array | AggregateType.S32, IoVariable.ViewportMask, StagesMask.Fragment, StagesMask.VertexTessellationGeometry);
            Add(map, 0x3fc, AggregateType.Bool, IoVariable.FrontFacing, StagesMask.Fragment, StagesMask.None);

            return map;
        }

        private static Dictionary<int, AttributeEntry> CreatePerPatchMap()
        {
            var map = new Dictionary<int, AttributeEntry>();

            Add(map, 0x000, AggregateType.Vector4 | AggregateType.FP32, IoVariable.TessellationLevelOuter, StagesMask.TessellationEvaluation, StagesMask.TessellationControl);
            Add(map, 0x010, AggregateType.Vector2 | AggregateType.FP32, IoVariable.TessellationLevelInner, StagesMask.TessellationEvaluation, StagesMask.TessellationControl);
            Add(map, 0x018, AggregateType.Vector4 | AggregateType.FP32, IoVariable.UserDefined, StagesMask.TessellationEvaluation, StagesMask.TessellationControl, 31, 0x200);

            return map;
        }

        private static void Add(
            Dictionary<int, AttributeEntry> attributes,
            int offset,
            AggregateType type,
            IoVariable ioVariable,
            StagesMask inputMask,
            StagesMask outputMask,
            int count = 1,
            int upperBound = 0x400)
        {
            int baseOffset = offset;

            int elementsCount = GetElementCount(type);

            for (int index = 0; index < count; index++)
            {
                for (int elementIndex = 0; elementIndex < elementsCount; elementIndex++)
                {
                    attributes.Add(offset, new AttributeEntry(baseOffset, type, ioVariable, inputMask, outputMask));

                    offset += 4;

                    if (offset >= upperBound)
                    {
                        return;
                    }
                }
            }
        }

        public static Operand GenerateAttributeLoad(EmitterContext context, Operand primVertex, int offset, bool isOutput, bool isPerPatch)
        {
            if (!(isPerPatch ? _attributesPerPatch : _attributes).TryGetValue(offset, out AttributeEntry entry))
            {
                context.TranslatorContext.GpuAccessor.Log($"Attribute offset 0x{offset:X} is not valid.");
                return Const(0);
            }

            StagesMask validUseMask = isOutput ? entry.OutputMask : entry.InputMask;

            if (((StagesMask)(1 << (int)context.TranslatorContext.Definitions.Stage) & validUseMask) == StagesMask.None)
            {
                context.TranslatorContext.GpuAccessor.Log($"Attribute offset 0x{offset:X} ({entry.IoVariable}) is not valid for stage {context.TranslatorContext.Definitions.Stage}.");
                return Const(0);
            }

            if (!IsSupportedByHost(context.TranslatorContext.GpuAccessor, context.TranslatorContext.Definitions.Stage, entry.IoVariable))
            {
                context.TranslatorContext.GpuAccessor.Log($"Attribute offset 0x{offset:X} ({entry.IoVariable}) is not supported by the host for stage {context.TranslatorContext.Definitions.Stage}.");
                return Const(0);
            }

            if (HasInvocationId(context.TranslatorContext.Definitions.Stage, isOutput) && !isPerPatch)
            {
                primVertex = context.Load(StorageKind.Input, IoVariable.InvocationId);
            }

            int innerOffset = offset - entry.BaseOffset;
            int innerIndex = innerOffset / 4;

            StorageKind storageKind = isPerPatch
                ? (isOutput ? StorageKind.OutputPerPatch : StorageKind.InputPerPatch)
                : (isOutput ? StorageKind.Output : StorageKind.Input);
            IoVariable ioVariable = GetIoVariable(context.TranslatorContext.Definitions.Stage, in entry);
            AggregateType type = GetType(context.TranslatorContext.Definitions, isOutput, innerIndex, in entry);
            int elementCount = GetElementCount(type);

            bool isArray = type.HasFlag(AggregateType.Array);
            bool hasArrayIndex = isArray || context.TranslatorContext.Definitions.HasPerLocationInputOrOutput(ioVariable, isOutput);

            bool hasElementIndex = elementCount > 1;

            if (hasArrayIndex && hasElementIndex)
            {
                int arrayIndex = innerIndex / elementCount;
                int elementIndex = innerIndex - (arrayIndex * elementCount);

                return primVertex == null || isArray
                    ? context.Load(storageKind, ioVariable, primVertex, Const(arrayIndex), Const(elementIndex))
                    : context.Load(storageKind, ioVariable, Const(arrayIndex), primVertex, Const(elementIndex));
            }
            else if (hasArrayIndex || hasElementIndex)
            {
                return primVertex == null || isArray || !hasArrayIndex
                    ? context.Load(storageKind, ioVariable, primVertex, Const(innerIndex))
                    : context.Load(storageKind, ioVariable, Const(innerIndex), primVertex);
            }
            else
            {
                return context.Load(storageKind, ioVariable, primVertex);
            }
        }

        public static void GenerateAttributeStore(EmitterContext context, int offset, bool isPerPatch, Operand value)
        {
            if (!(isPerPatch ? _attributesPerPatch : _attributes).TryGetValue(offset, out AttributeEntry entry))
            {
                context.TranslatorContext.GpuAccessor.Log($"Attribute offset 0x{offset:X} is not valid.");
                return;
            }

            if (((StagesMask)(1 << (int)context.TranslatorContext.Definitions.Stage) & entry.OutputMask) == StagesMask.None)
            {
                context.TranslatorContext.GpuAccessor.Log($"Attribute offset 0x{offset:X} ({entry.IoVariable}) is not valid for stage {context.TranslatorContext.Definitions.Stage}.");
                return;
            }

            if (!IsSupportedByHost(context.TranslatorContext.GpuAccessor, context.TranslatorContext.Definitions.Stage, entry.IoVariable))
            {
                context.TranslatorContext.GpuAccessor.Log($"Attribute offset 0x{offset:X} ({entry.IoVariable}) is not supported by the host for stage {context.TranslatorContext.Definitions.Stage}.");
                return;
            }

            Operand invocationId = null;

            if (HasInvocationId(context.TranslatorContext.Definitions.Stage, isOutput: true) && !isPerPatch)
            {
                invocationId = context.Load(StorageKind.Input, IoVariable.InvocationId);
            }

            int innerOffset = offset - entry.BaseOffset;
            int innerIndex = innerOffset / 4;

            StorageKind storageKind = isPerPatch ? StorageKind.OutputPerPatch : StorageKind.Output;
            IoVariable ioVariable = GetIoVariable(context.TranslatorContext.Definitions.Stage, in entry);
            AggregateType type = GetType(context.TranslatorContext.Definitions, isOutput: true, innerIndex, in entry);
            int elementCount = GetElementCount(type);

            bool isArray = type.HasFlag(AggregateType.Array);
            bool hasArrayIndex = isArray || context.TranslatorContext.Definitions.HasPerLocationInputOrOutput(ioVariable, isOutput: true);

            bool hasElementIndex = elementCount > 1;

            if (hasArrayIndex && hasElementIndex)
            {
                int arrayIndex = innerIndex / elementCount;
                int elementIndex = innerIndex - (arrayIndex * elementCount);

                if (invocationId == null || isArray)
                {
                    context.Store(storageKind, ioVariable, invocationId, Const(arrayIndex), Const(elementIndex), value);
                }
                else
                {
                    context.Store(storageKind, ioVariable, Const(arrayIndex), invocationId, Const(elementIndex), value);
                }
            }
            else if (hasArrayIndex || hasElementIndex)
            {
                if (invocationId == null || isArray || !hasArrayIndex)
                {
                    context.Store(storageKind, ioVariable, invocationId, Const(innerIndex), value);
                }
                else
                {
                    context.Store(storageKind, ioVariable, Const(innerIndex), invocationId, value);
                }
            }
            else
            {
                context.Store(storageKind, ioVariable, invocationId, value);
            }
        }

        private static bool IsSupportedByHost(IGpuAccessor gpuAccessor, ShaderStage stage, IoVariable ioVariable)
        {
            if (ioVariable == IoVariable.ViewportIndex && stage != ShaderStage.Geometry && stage != ShaderStage.Fragment)
            {
                return gpuAccessor.QueryHostSupportsViewportIndexVertexTessellation();
            }
            else if (ioVariable == IoVariable.ViewportMask)
            {
                return gpuAccessor.QueryHostSupportsViewportMask();
            }

            return true;
        }

        public static IoVariable GetIoVariable(ShaderDefinitions definitions, int offset, out int location)
        {
            location = 0;

            if (!_attributes.TryGetValue(offset, out AttributeEntry entry))
            {
                return IoVariable.Invalid;
            }

            if (((StagesMask)(1 << (int)definitions.Stage) & entry.OutputMask) == StagesMask.None)
            {
                return IoVariable.Invalid;
            }

            if (definitions.HasPerLocationInputOrOutput(entry.IoVariable, isOutput: true))
            {
                location = (offset - entry.BaseOffset) / 16;
            }

            return GetIoVariable(definitions.Stage, in entry);
        }

        private static IoVariable GetIoVariable(ShaderStage stage, in AttributeEntry entry)
        {
            if (entry.IoVariable == IoVariable.Position && stage == ShaderStage.Fragment)
            {
                return IoVariable.FragmentCoord;
            }

            return entry.IoVariable;
        }

        private static AggregateType GetType(ShaderDefinitions definitions, bool isOutput, int innerIndex, in AttributeEntry entry)
        {
            AggregateType type = entry.Type;

            if (entry.IoVariable == IoVariable.UserDefined)
            {
                type = definitions.GetUserDefinedType(innerIndex / 4, isOutput);
            }
            else if (entry.IoVariable == IoVariable.FragmentOutputColor)
            {
                type = definitions.GetFragmentOutputColorType(innerIndex / 4);
            }

            return type;
        }

        public static bool HasPrimitiveVertex(ShaderStage stage, bool isOutput)
        {
            if (isOutput)
            {
                return false;
            }

            return stage == ShaderStage.TessellationControl ||
                   stage == ShaderStage.TessellationEvaluation ||
                   stage == ShaderStage.Geometry;
        }

        public static bool HasInvocationId(ShaderStage stage, bool isOutput)
        {
            return isOutput && stage == ShaderStage.TessellationControl;
        }

        private static int GetElementCount(AggregateType type)
        {
            return (type & AggregateType.ElementCountMask) switch
            {
                AggregateType.Vector2 => 2,
                AggregateType.Vector3 => 3,
                AggregateType.Vector4 => 4,
                _ => 1,
            };
        }
    }
}
