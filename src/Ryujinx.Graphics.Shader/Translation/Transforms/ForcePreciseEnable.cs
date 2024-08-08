using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Shader.Translation.Transforms
{
    class ForcePreciseEnable : ITransformPass
    {
        public static bool IsEnabled(IGpuAccessor gpuAccessor, ShaderStage stage, TargetLanguage targetLanguage, FeatureFlags usedFeatures)
        {
            return stage == ShaderStage.Fragment && gpuAccessor.QueryHostReducedPrecision();
        }

        public static LinkedListNode<INode> RunPass(TransformContext context, LinkedListNode<INode> node)
        {
            // There are some cases where a small bias is added to values to prevent division by zero.
            // When operating with reduced precision, it is possible for this bias to get rounded to 0
            // and cause a division by zero.
            // To prevent that, we force those operations to be precise even if the host wants
            // imprecise operations for performance.

            Operation operation = (Operation)node.Value;

            if (operation.Inst == (Instruction.FP32 | Instruction.Divide) &&
                operation.GetSource(0).Type == OperandType.Constant &&
                operation.GetSource(0).AsFloat() == 1f &&
                operation.GetSource(1).AsgOp is Operation addOp &&
                addOp.Inst == (Instruction.FP32 | Instruction.Add) &&
                addOp.GetSource(1).Type == OperandType.Constant)
            {
                context.UsedFeatures |= FeatureFlags.Precise;

                addOp.ForcePrecise = true;
            }

            return node;
        }
    }
}
