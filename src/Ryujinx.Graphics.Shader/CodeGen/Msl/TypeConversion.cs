using Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class TypeConversion
    {
        public static string ReinterpretCast(
                    CodeGenContext context,
                    IAstNode node,
                    AggregateType srcType,
                    AggregateType dstType)
        {
            if (node is AstOperand operand && operand.Type == OperandType.Constant)
            {
                if (NumberFormatter.TryFormat(operand.Value, dstType, out string formatted))
                {
                    return formatted;
                }
            }

            string expr = InstGen.GetExpression(context, node);

            return ReinterpretCast(expr, node, srcType, dstType);
        }

        private static string ReinterpretCast(string expr, IAstNode node, AggregateType srcType, AggregateType dstType)
        {
            if (srcType == dstType)
            {
                return expr;
            }

            if (srcType == AggregateType.FP32)
            {
                switch (dstType)
                {
                    case AggregateType.Bool:
                        return $"(as_type<int>({expr}) != 0)";
                    case AggregateType.S32:
                        return $"as_type<int>({expr})";
                    case AggregateType.U32:
                        return $"as_type<uint>({expr})";
                }
            }
            else if (dstType == AggregateType.FP32)
            {
                switch (srcType)
                {
                    case AggregateType.Bool:
                        return $"as_type<float>({ReinterpretBoolToInt(expr, node, AggregateType.S32)})";
                    case AggregateType.S32:
                        return $"as_type<float>({expr})";
                    case AggregateType.U32:
                        return $"as_type<float>({expr})";
                }
            }
            else if (srcType == AggregateType.Bool)
            {
                return ReinterpretBoolToInt(expr, node, dstType);
            }
            else if (dstType == AggregateType.Bool)
            {
                expr = InstGenHelper.Enclose(expr, node, Instruction.CompareNotEqual, isLhs: true);

                return $"({expr} != 0)";
            }
            else if (dstType == AggregateType.S32)
            {
                return $"int({expr})";
            }
            else if (dstType == AggregateType.U32)
            {
                return $"uint({expr})";
            }

            throw new ArgumentException($"Invalid reinterpret cast from \"{srcType}\" to \"{dstType}\".");
        }

        private static string ReinterpretBoolToInt(string expr, IAstNode node, AggregateType dstType)
        {
            string trueExpr = NumberFormatter.FormatInt(IrConsts.True, dstType);
            string falseExpr = NumberFormatter.FormatInt(IrConsts.False, dstType);

            expr = InstGenHelper.Enclose(expr, node, Instruction.ConditionalSelect, isLhs: false);

            return $"({expr} ? {trueExpr} : {falseExpr})";
        }
    }
}
