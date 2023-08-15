using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.Translation;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class IoMap
    {
        public static (string, AggregateType) GetMslBuiltIn(IoVariable ioVariable)
        {
            return ioVariable switch
            {
                IoVariable.BaseInstance => ("base_instance", AggregateType.S32),
                IoVariable.BaseVertex => ("base_vertex", AggregateType.S32),
                IoVariable.ClipDistance => ("clip_distance", AggregateType.Array | AggregateType.FP32),
                IoVariable.FragmentOutputColor => ("color", AggregateType.Vector2 | AggregateType.Vector3 | AggregateType.Vector4),
                IoVariable.FragmentOutputDepth => ("depth", AggregateType.FP32),
                IoVariable.FrontFacing => ("front_facing", AggregateType.Bool),
                IoVariable.InstanceId => ("instance_id", AggregateType.S32),
                IoVariable.PointCoord => ("point_coord", AggregateType.Vector2),
                IoVariable.PointSize => ("point_size", AggregateType.FP32),
                IoVariable.Position => ("position", AggregateType.Vector4),
                IoVariable.PrimitiveId => ("primitive_id", AggregateType.S32),
                IoVariable.VertexId => ("vertex_id", AggregateType.S32),
                IoVariable.ViewportIndex => ("viewport_array_index", AggregateType.S32),
                _ => (null, AggregateType.Invalid),
            };
        }
    }
}
