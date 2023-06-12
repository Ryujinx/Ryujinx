using Ryujinx.Graphics.Shader.Decoders;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.Translation;

using static Ryujinx.Graphics.Shader.Instructions.InstEmitHelper;
using static Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandHelper;

namespace Ryujinx.Graphics.Shader.Instructions
{
    static partial class InstEmit
    {
        public static void Al2p(EmitterContext context)
        {
            InstAl2p op = context.GetOp<InstAl2p>();

            context.Copy(GetDest(op.Dest), context.IAdd(GetSrcReg(context, op.SrcA), Const(op.Imm11)));
        }

        public static void Ald(EmitterContext context)
        {
            InstAld op = context.GetOp<InstAld>();

            // Some of those attributes are per invocation,
            // so we should ignore any primitive vertex indexing for those.
            bool hasPrimitiveVertex = AttributeMap.HasPrimitiveVertex(context.Config.Stage, op.O) && !op.P;

            if (!op.Phys)
            {
                hasPrimitiveVertex &= HasPrimitiveVertex(op.Imm11);
            }

            Operand primVertex = hasPrimitiveVertex ? context.Copy(GetSrcReg(context, op.SrcB)) : null;

            for (int index = 0; index < (int)op.AlSize + 1; index++)
            {
                Register rd = new Register(op.Dest + index, RegisterType.Gpr);

                if (rd.IsRZ)
                {
                    break;
                }

                if (op.Phys)
                {
                    Operand offset = context.ISubtract(GetSrcReg(context, op.SrcA), Const(AttributeConsts.UserAttributeBase));
                    Operand vecIndex = context.ShiftRightU32(offset, Const(4));
                    Operand elemIndex = context.BitwiseAnd(context.ShiftRightU32(offset, Const(2)), Const(3));

                    StorageKind storageKind = op.O ? StorageKind.Output : StorageKind.Input;

                    context.Copy(Register(rd), context.Load(storageKind, IoVariable.UserDefined, primVertex, vecIndex, elemIndex));
                }
                else if (op.SrcB == RegisterConsts.RegisterZeroIndex || op.P)
                {
                    int offset = FixedFuncToUserAttribute(context.Config, op.Imm11 + index * 4, op.O);

                    context.FlagAttributeRead(offset);

                    bool isOutput = op.O && CanLoadOutput(offset);

                    if (!op.P && !isOutput && TryConvertIdToIndexForVulkan(context, offset, out Operand value))
                    {
                        context.Copy(Register(rd), value);
                    }
                    else
                    {
                        context.Copy(Register(rd), AttributeMap.GenerateAttributeLoad(context, primVertex, offset, isOutput, op.P));
                    }
                }
                else
                {
                    int offset = FixedFuncToUserAttribute(context.Config, op.Imm11 + index * 4, op.O);

                    context.FlagAttributeRead(offset);

                    bool isOutput = op.O && CanLoadOutput(offset);

                    context.Copy(Register(rd), AttributeMap.GenerateAttributeLoad(context, primVertex, offset, isOutput, false));
                }
            }
        }

        public static void Ast(EmitterContext context)
        {
            InstAst op = context.GetOp<InstAst>();

            for (int index = 0; index < (int)op.AlSize + 1; index++)
            {
                if (op.SrcB + index > RegisterConsts.RegisterZeroIndex)
                {
                    break;
                }

                Register rd = new Register(op.SrcB + index, RegisterType.Gpr);

                if (op.Phys)
                {
                    Operand offset = context.ISubtract(GetSrcReg(context, op.SrcA), Const(AttributeConsts.UserAttributeBase));
                    Operand vecIndex = context.ShiftRightU32(offset, Const(4));
                    Operand elemIndex = context.BitwiseAnd(context.ShiftRightU32(offset, Const(2)), Const(3));
                    Operand invocationId = AttributeMap.HasInvocationId(context.Config.Stage, isOutput: true)
                        ? context.Load(StorageKind.Input, IoVariable.InvocationId)
                        : null;

                    context.Store(StorageKind.Output, IoVariable.UserDefined, invocationId, vecIndex, elemIndex, Register(rd));
                }
                else
                {
                    // TODO: Support indirect stores using Ra.

                    int offset = op.Imm11 + index * 4;

                    if (!context.Config.IsUsedOutputAttribute(offset))
                    {
                        return;
                    }

                    offset = FixedFuncToUserAttribute(context.Config, offset, isOutput: true);

                    context.FlagAttributeWritten(offset);

                    AttributeMap.GenerateAttributeStore(context, offset, op.P, Register(rd));
                }
            }
        }

        public static void Ipa(EmitterContext context)
        {
            InstIpa op = context.GetOp<InstIpa>();

            context.FlagAttributeRead(op.Imm10);

            Operand res;

            bool isFixedFunc = false;

            if (op.Idx)
            {
                Operand offset = context.ISubtract(GetSrcReg(context, op.SrcA), Const(AttributeConsts.UserAttributeBase));
                Operand vecIndex = context.ShiftRightU32(offset, Const(4));
                Operand elemIndex = context.BitwiseAnd(context.ShiftRightU32(offset, Const(2)), Const(3));

                res = context.Load(StorageKind.Input, IoVariable.UserDefined, null, vecIndex, elemIndex);
                res = context.FPMultiply(res, context.Load(StorageKind.Input, IoVariable.FragmentCoord, null, Const(3)));
            }
            else
            {
                isFixedFunc = TryFixedFuncToUserAttributeIpa(context, op.Imm10, out res);

                if (op.Imm10 >= AttributeConsts.UserAttributeBase && op.Imm10 < AttributeConsts.UserAttributeEnd)
                {
                    int index = (op.Imm10 - AttributeConsts.UserAttributeBase) >> 4;

                    if (context.Config.ImapTypes[index].GetFirstUsedType() == PixelImap.Perspective)
                    {
                        res = context.FPMultiply(res, context.Load(StorageKind.Input, IoVariable.FragmentCoord, null, Const(3)));
                    }
                }
                else if (op.Imm10 == AttributeConsts.PositionX || op.Imm10 == AttributeConsts.PositionY)
                {
                    // FragCoord X/Y must be divided by the render target scale, if resolution scaling is active,
                    // because the shader code is not expecting scaled values.
                    res = context.FPDivide(res, context.Load(StorageKind.ConstantBuffer, SupportBuffer.Binding, Const((int)SupportBufferField.RenderScale), Const(0)));
                }
                else if (op.Imm10 == AttributeConsts.FrontFacing && context.Config.GpuAccessor.QueryHostHasFrontFacingBug())
                {
                    // gl_FrontFacing sometimes has incorrect (flipped) values depending how it is accessed on Intel GPUs.
                    // This weird trick makes it behave.
                    res = context.ICompareLess(context.INegate(context.IConvertS32ToFP32(res)), Const(0));
                }
            }

            if (op.IpaOp == IpaOp.Multiply && !isFixedFunc)
            {
                Operand srcB = GetSrcReg(context, op.SrcB);

                res = context.FPMultiply(res, srcB);
            }

            res = context.FPSaturate(res, op.Sat);

            context.Copy(GetDest(op.Dest), res);
        }

        public static void Isberd(EmitterContext context)
        {
            InstIsberd op = context.GetOp<InstIsberd>();

            // This instruction performs a load from ISBE (Internal Stage Buffer Entry) memory.
            // Here, we just propagate the offset, as the result from this instruction is usually
            // used with ALD to perform vertex load on geometry or tessellation shaders.
            // The offset is calculated as (PrimitiveIndex * VerticesPerPrimitive) + VertexIndex.
            // Since we hardcode PrimitiveIndex to zero, then the offset will be just VertexIndex.
            context.Copy(GetDest(op.Dest), GetSrcReg(context, op.SrcA));
        }

        public static void OutR(EmitterContext context)
        {
            InstOutR op = context.GetOp<InstOutR>();

            EmitOut(context, op.OutType.HasFlag(OutType.Emit), op.OutType.HasFlag(OutType.Cut));
        }

        public static void OutI(EmitterContext context)
        {
            InstOutI op = context.GetOp<InstOutI>();

            EmitOut(context, op.OutType.HasFlag(OutType.Emit), op.OutType.HasFlag(OutType.Cut));
        }

        public static void OutC(EmitterContext context)
        {
            InstOutC op = context.GetOp<InstOutC>();

            EmitOut(context, op.OutType.HasFlag(OutType.Emit), op.OutType.HasFlag(OutType.Cut));
        }

        private static void EmitOut(EmitterContext context, bool emit, bool cut)
        {
            if (!(emit || cut))
            {
                context.Config.GpuAccessor.Log("Invalid OUT encoding.");
            }

            if (emit)
            {
                if (context.Config.LastInVertexPipeline)
                {
                    context.PrepareForVertexReturn(out var tempXLocal, out var tempYLocal, out var tempZLocal);

                    context.EmitVertex();

                    // Restore output position value before transformation.

                    if (tempXLocal != null)
                    {
                        context.Copy(context.Load(StorageKind.Input, IoVariable.Position, null, Const(0)), tempXLocal);
                    }

                    if (tempYLocal != null)
                    {
                        context.Copy(context.Load(StorageKind.Input, IoVariable.Position, null, Const(1)), tempYLocal);
                    }

                    if (tempZLocal != null)
                    {
                        context.Copy(context.Load(StorageKind.Input, IoVariable.Position, null, Const(2)), tempZLocal);
                    }
                }
                else
                {
                    context.EmitVertex();
                }
            }

            if (cut)
            {
                context.EndPrimitive();
            }
        }

        private static bool HasPrimitiveVertex(int attr)
        {
            return attr != AttributeConsts.PrimitiveId &&
                   attr != AttributeConsts.TessCoordX &&
                   attr != AttributeConsts.TessCoordY;
        }

        private static bool CanLoadOutput(int attr)
        {
            return attr != AttributeConsts.TessCoordX && attr != AttributeConsts.TessCoordY;
        }

        private static bool TryFixedFuncToUserAttributeIpa(EmitterContext context, int attr, out Operand selectedAttr)
        {
            if (attr >= AttributeConsts.FrontColorDiffuseR && attr < AttributeConsts.BackColorDiffuseR)
            {
                // TODO: If two sided rendering is enabled, then this should return
                // FrontColor if the fragment is front facing, and back color otherwise.
                selectedAttr = GenerateIpaLoad(context, FixedFuncToUserAttribute(context.Config, attr, isOutput: false));
                return true;
            }
            else if (attr == AttributeConsts.FogCoord)
            {
                // TODO: We likely need to emulate the fixed-function functionality for FogCoord here.
                selectedAttr = GenerateIpaLoad(context, FixedFuncToUserAttribute(context.Config, attr, isOutput: false));
                return true;
            }
            else if (attr >= AttributeConsts.BackColorDiffuseR && attr < AttributeConsts.ClipDistance0)
            {
                selectedAttr = ConstF(((attr >> 2) & 3) == 3 ? 1f : 0f);
                return true;
            }
            else if (attr >= AttributeConsts.TexCoordBase && attr < AttributeConsts.TexCoordEnd)
            {
                selectedAttr = GenerateIpaLoad(context, FixedFuncToUserAttribute(context.Config, attr, isOutput: false));
                return true;
            }

            selectedAttr = GenerateIpaLoad(context, attr);
            return false;
        }

        private static Operand GenerateIpaLoad(EmitterContext context, int offset)
        {
            return AttributeMap.GenerateAttributeLoad(context, null, offset, isOutput: false, isPerPatch: false);
        }

        private static int FixedFuncToUserAttribute(ShaderConfig config, int attr, bool isOutput)
        {
            bool supportsLayerFromVertexOrTess = config.GpuAccessor.QueryHostSupportsLayerVertexTessellation();
            int fixedStartAttr = supportsLayerFromVertexOrTess ? 0 : 1;

            if (attr == AttributeConsts.Layer && config.Stage != ShaderStage.Geometry && !supportsLayerFromVertexOrTess)
            {
                attr = FixedFuncToUserAttribute(config, attr, AttributeConsts.Layer, 0, isOutput);
                config.SetLayerOutputAttribute(attr);
            }
            else if (attr == AttributeConsts.FogCoord)
            {
                attr = FixedFuncToUserAttribute(config, attr, AttributeConsts.FogCoord, fixedStartAttr, isOutput);
            }
            else if (attr >= AttributeConsts.FrontColorDiffuseR && attr < AttributeConsts.ClipDistance0)
            {
                attr = FixedFuncToUserAttribute(config, attr, AttributeConsts.FrontColorDiffuseR, fixedStartAttr + 1, isOutput);
            }
            else if (attr >= AttributeConsts.TexCoordBase && attr < AttributeConsts.TexCoordEnd)
            {
                attr = FixedFuncToUserAttribute(config, attr, AttributeConsts.TexCoordBase, fixedStartAttr + 5, isOutput);
            }

            return attr;
        }

        private static int FixedFuncToUserAttribute(ShaderConfig config, int attr, int baseAttr, int baseIndex, bool isOutput)
        {
            int index = (attr - baseAttr) >> 4;
            int userAttrIndex = config.GetFreeUserAttribute(isOutput, baseIndex + index);

            if ((uint)userAttrIndex < Constants.MaxAttributes)
            {
                attr = AttributeConsts.UserAttributeBase + userAttrIndex * 16 + (attr & 0xf);

                if (isOutput)
                {
                    config.SetOutputUserAttributeFixedFunc(userAttrIndex);
                }
                else
                {
                    config.SetInputUserAttributeFixedFunc(userAttrIndex);
                }
            }
            else
            {
                config.GpuAccessor.Log($"No enough user attributes for fixed attribute offset 0x{attr:X}.");
            }

            return attr;
        }

        private static bool TryConvertIdToIndexForVulkan(EmitterContext context, int attr, out Operand value)
        {
            if (context.Config.Options.TargetApi == TargetApi.Vulkan)
            {
                if (attr == AttributeConsts.InstanceId)
                {
                    value = context.ISubtract(
                        context.Load(StorageKind.Input, IoVariable.InstanceIndex),
                        context.Load(StorageKind.Input, IoVariable.BaseInstance));
                    return true;
                }
                else if (attr == AttributeConsts.VertexId)
                {
                    value = context.Load(StorageKind.Input, IoVariable.VertexIndex);
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}