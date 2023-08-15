using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Numerics;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class Declarations
    {
        public static void Declare(CodeGenContext context, StructuredProgramInfo info)
        {
            context.AppendLine("#include <metal_stdlib>");
            context.AppendLine("#include <simd/simd.h>");
            context.AppendLine();
            context.AppendLine("using namespace metal;");
            context.AppendLine();

            if ((info.HelperFunctionsMask & HelperFunctionsMask.SwizzleAdd) != 0)
            {

            }

            DeclareInputAttributes(context, info.IoDefinitions.Where(x => IsUserDefined(x, StorageKind.Input)));
        }

        static bool IsUserDefined(IoDefinition ioDefinition, StorageKind storageKind)
        {
            return ioDefinition.StorageKind == storageKind && ioDefinition.IoVariable == IoVariable.UserDefined;
        }

        public static void DeclareLocals(CodeGenContext context, StructuredFunction function)
        {
            foreach (AstOperand decl in function.Locals)
            {
                string name = context.OperandManager.DeclareLocal(decl);

                context.AppendLine(GetVarTypeName(context, decl.VarType) + " " + name + ";");
            }
        }

        public static string GetVarTypeName(CodeGenContext context, AggregateType type)
        {
            return type switch
            {
                AggregateType.Void => "void",
                AggregateType.Bool => "bool",
                AggregateType.FP32 => "float",
                AggregateType.S32 => "int",
                AggregateType.U32 => "uint",
                AggregateType.Vector2 | AggregateType.Bool => "bool2",
                AggregateType.Vector2 | AggregateType.FP32 => "float2",
                AggregateType.Vector2 | AggregateType.S32 => "int2",
                AggregateType.Vector2 | AggregateType.U32 => "uint2",
                AggregateType.Vector3 | AggregateType.Bool => "bool3",
                AggregateType.Vector3 | AggregateType.FP32 => "float3",
                AggregateType.Vector3 | AggregateType.S32 => "int3",
                AggregateType.Vector3 | AggregateType.U32 => "uint3",
                AggregateType.Vector4 | AggregateType.Bool => "bool4",
                AggregateType.Vector4 | AggregateType.FP32 => "float4",
                AggregateType.Vector4 | AggregateType.S32 => "int4",
                AggregateType.Vector4 | AggregateType.U32 => "uint4",
                _ => throw new ArgumentException($"Invalid variable type \"{type}\"."),
            };
        }

        private static void DeclareInputAttributes(CodeGenContext context, IEnumerable<IoDefinition> inputs)
        {
            if (context.Definitions.IaIndexing)
            {
                // Not handled
            }
            else
            {
                if (inputs.Any())
                {
                    string prefix = "";

                    switch (context.Definitions.Stage)
                    {
                        case ShaderStage.Vertex:
                            prefix = "Vertex";
                            break;
                        case ShaderStage.Fragment:
                            prefix = "Fragment";
                            break;
                        case ShaderStage.Compute:
                            prefix = "Compute";
                            break;
                    }

                    context.AppendLine($"struct {prefix}In");
                    context.EnterScope();

                    foreach (var ioDefinition in inputs.OrderBy(x => x.Location))
                    {
                        string type = GetVarTypeName(context, context.Definitions.GetUserDefinedType(ioDefinition.Location, isOutput: false));
                        string name = $"{DefaultNames.IAttributePrefix}{ioDefinition.Location}";

                        context.AppendLine($"{type} {name} [[attribute({ioDefinition.Location})]];");
                    }

                    context.LeaveScope(";");
                }
            }
        }
    }
}