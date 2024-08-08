using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.Translation;
using System.Globalization;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class IoMap
    {
        public static (string, AggregateType) GetMslBuiltIn(
            ShaderDefinitions definitions,
            IoVariable ioVariable,
            int location,
            int component,
            bool isOutput,
            bool isPerPatch)
        {
            var returnValue = ioVariable switch
            {
                IoVariable.BaseInstance => ("base_instance", AggregateType.U32),
                IoVariable.BaseVertex => ("base_vertex", AggregateType.U32),
                IoVariable.CtaId => ("threadgroup_position_in_grid", AggregateType.Vector3 | AggregateType.U32),
                IoVariable.ClipDistance => ("out.clip_distance", AggregateType.Array | AggregateType.FP32),
                IoVariable.FragmentOutputColor => ($"out.color{location}", definitions.GetFragmentOutputColorType(location)),
                IoVariable.FragmentOutputDepth => ("out.depth", AggregateType.FP32),
                IoVariable.FrontFacing => ("in.front_facing", AggregateType.Bool),
                IoVariable.GlobalId => ("thread_position_in_grid", AggregateType.Vector3 | AggregateType.U32),
                IoVariable.InstanceId => ("instance_id", AggregateType.U32),
                IoVariable.InstanceIndex => ("instance_index", AggregateType.U32),
                IoVariable.InvocationId => ("INVOCATION_ID", AggregateType.S32),
                IoVariable.PointCoord => ("in.point_coord", AggregateType.Vector2 | AggregateType.FP32),
                IoVariable.PointSize => ("out.point_size", AggregateType.FP32),
                IoVariable.Position => ("out.position", AggregateType.Vector4 | AggregateType.FP32),
                IoVariable.PrimitiveId => ("primitive_id", AggregateType.S32),
                IoVariable.SubgroupEqMask => ("thread_index_in_simdgroup >= 32 ? uint4(0, (1 << (thread_index_in_simdgroup - 32)), uint2(0)) : uint4(1 << thread_index_in_simdgroup, uint3(0))", AggregateType.Vector4 | AggregateType.U32),
                IoVariable.SubgroupGeMask => ("uint4(insert_bits(0u, 0xFFFFFFFF, thread_index_in_simdgroup, 32 - thread_index_in_simdgroup), uint3(0)) & (uint4((uint)((simd_vote::vote_t)simd_ballot(true) & 0xFFFFFFFF), (uint)(((simd_vote::vote_t)simd_ballot(true) >> 32) & 0xFFFFFFFF), 0, 0))", AggregateType.Vector4 | AggregateType.U32),
                IoVariable.SubgroupGtMask => ("uint4(insert_bits(0u, 0xFFFFFFFF, thread_index_in_simdgroup + 1, 32 - thread_index_in_simdgroup - 1), uint3(0)) & (uint4((uint)((simd_vote::vote_t)simd_ballot(true) & 0xFFFFFFFF), (uint)(((simd_vote::vote_t)simd_ballot(true) >> 32) & 0xFFFFFFFF), 0, 0))", AggregateType.Vector4 | AggregateType.U32),
                IoVariable.SubgroupLaneId => ("thread_index_in_simdgroup", AggregateType.U32),
                IoVariable.SubgroupLeMask => ("uint4(extract_bits(0xFFFFFFFF, 0, min(thread_index_in_simdgroup + 1, 32u)), extract_bits(0xFFFFFFFF, 0, (uint)max((int)thread_index_in_simdgroup + 1 - 32, 0)), uint2(0))", AggregateType.Vector4 | AggregateType.U32),
                IoVariable.SubgroupLtMask => ("uint4(extract_bits(0xFFFFFFFF, 0, min(thread_index_in_simdgroup, 32u)), extract_bits(0xFFFFFFFF, 0, (uint)max((int)thread_index_in_simdgroup - 32, 0)), uint2(0))", AggregateType.Vector4 | AggregateType.U32),
                IoVariable.ThreadKill => ("simd_is_helper_thread()", AggregateType.Bool),
                IoVariable.UserDefined => GetUserDefinedVariableName(definitions, location, component, isOutput, isPerPatch),
                IoVariable.ThreadId => ("thread_position_in_threadgroup", AggregateType.Vector3 | AggregateType.U32),
                IoVariable.VertexId => ("vertex_id", AggregateType.S32),
                // gl_VertexIndex does not have a direct equivalent in MSL
                IoVariable.VertexIndex => ("vertex_id", AggregateType.U32),
                IoVariable.ViewportIndex => ("viewport_array_index", AggregateType.S32),
                IoVariable.FragmentCoord => ("in.position", AggregateType.Vector4 | AggregateType.FP32),
                _ => (null, AggregateType.Invalid),
            };

            if (returnValue.Item2 == AggregateType.Invalid)
            {
                Logger.Warning?.PrintMsg(LogClass.Gpu, $"Unable to find type for IoVariable {ioVariable}!");
            }

            return returnValue;
        }

        private static (string, AggregateType) GetUserDefinedVariableName(ShaderDefinitions definitions, int location, int component, bool isOutput, bool isPerPatch)
        {
            string name = isPerPatch
                ? Defaults.PerPatchAttributePrefix
                : (isOutput ? Defaults.OAttributePrefix : Defaults.IAttributePrefix);

            if (location < 0)
            {
                return (name, definitions.GetUserDefinedType(0, isOutput));
            }

            name += location.ToString(CultureInfo.InvariantCulture);

            if (definitions.HasPerLocationInputOrOutputComponent(IoVariable.UserDefined, location, component, isOutput))
            {
                name += "_" + "xyzw"[component & 3];
            }

            string prefix = isOutput ? "out" : "in";

            return (prefix + "." + name, definitions.GetUserDefinedType(location, isOutput));
        }
    }
}
