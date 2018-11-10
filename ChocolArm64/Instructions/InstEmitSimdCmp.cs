using ChocolArm64.Decoders;
using ChocolArm64.State;
using ChocolArm64.Translation;
using System;
using System.Reflection.Emit;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using static ChocolArm64.Instructions.InstEmitAluHelper;
using static ChocolArm64.Instructions.InstEmitSimdHelper;

namespace ChocolArm64.Instructions
{
    static partial class InstEmit
    {
        public static void Cmeq_S(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Beq_S, scalar: true);
        }

        public static void Cmeq_V(ILEmitterCtx context)
        {
            if (context.CurrOp is OpCodeSimdReg64 op)
            {
                if (op.Size < 3 && Optimizations.UseSse2)
                {
                    EmitSse2Op(context, nameof(Sse2.CompareEqual));
                }
                else if (op.Size == 3 && Optimizations.UseSse41)
                {
                    EmitSse41Op(context, nameof(Sse41.CompareEqual));
                }
                else
                {
                    EmitCmp(context, OpCodes.Beq_S, scalar: false);
                }
            }
            else
            {
                EmitCmp(context, OpCodes.Beq_S, scalar: false);
            }
        }

        public static void Cmge_S(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Bge_S, scalar: true);
        }

        public static void Cmge_V(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Bge_S, scalar: false);
        }

        public static void Cmgt_S(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Bgt_S, scalar: true);
        }

        public static void Cmgt_V(ILEmitterCtx context)
        {
            if (context.CurrOp is OpCodeSimdReg64 op)
            {
                if (op.Size < 3 && Optimizations.UseSse2)
                {
                    EmitSse2Op(context, nameof(Sse2.CompareGreaterThan));
                }
                else if (op.Size == 3 && Optimizations.UseSse42)
                {
                    EmitSse42Op(context, nameof(Sse42.CompareGreaterThan));
                }
                else
                {
                    EmitCmp(context, OpCodes.Bgt_S, scalar: false);
                }
            }
            else
            {
                EmitCmp(context, OpCodes.Bgt_S, scalar: false);
            }
        }

        public static void Cmhi_S(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Bgt_Un_S, scalar: true);
        }

        public static void Cmhi_V(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Bgt_Un_S, scalar: false);
        }

        public static void Cmhs_S(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Bge_Un_S, scalar: true);
        }

        public static void Cmhs_V(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Bge_Un_S, scalar: false);
        }

        public static void Cmle_S(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Ble_S, scalar: true);
        }

        public static void Cmle_V(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Ble_S, scalar: false);
        }

        public static void Cmlt_S(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Blt_S, scalar: true);
        }

        public static void Cmlt_V(ILEmitterCtx context)
        {
            EmitCmp(context, OpCodes.Blt_S, scalar: false);
        }

        public static void Cmtst_S(ILEmitterCtx context)
        {
            EmitCmtst(context, scalar: true);
        }

        public static void Cmtst_V(ILEmitterCtx context)
        {
            EmitCmtst(context, scalar: false);
        }

        public static void Fccmp_S(ILEmitterCtx context)
        {
            OpCodeSimdFcond64 op = (OpCodeSimdFcond64)context.CurrOp;

            ILLabel lblTrue = new ILLabel();
            ILLabel lblEnd  = new ILLabel();

            context.EmitCondBranch(lblTrue, op.Cond);

            context.EmitLdc_I4(op.Nzcv);
            EmitSetNzcv(context);

            context.Emit(OpCodes.Br, lblEnd);

            context.MarkLabel(lblTrue);

            Fcmp_S(context);

            context.MarkLabel(lblEnd);
        }

        public static void Fccmpe_S(ILEmitterCtx context)
        {
            Fccmp_S(context);
        }

        public static void Fcmeq_S(ILEmitterCtx context)
        {
            if (context.CurrOp is OpCodeSimdReg64 && Optimizations.UseSse
                                                  && Optimizations.UseSse2)
            {
                EmitScalarSseOrSse2OpF(context, nameof(Sse.CompareEqualScalar));
            }
            else
            {
                EmitScalarFcmp(context, OpCodes.Beq_S);
            }
        }

        public static void Fcmeq_V(ILEmitterCtx context)
        {
            if (context.CurrOp is OpCodeSimdReg64 && Optimizations.UseSse
                                                  && Optimizations.UseSse2)
            {
                EmitVectorSseOrSse2OpF(context, nameof(Sse.CompareEqual));
            }
            else
            {
                EmitVectorFcmp(context, OpCodes.Beq_S);
            }
        }

        public static void Fcmge_S(ILEmitterCtx context)
        {
            if (context.CurrOp is OpCodeSimdReg64 && Optimizations.UseSse
                                                  && Optimizations.UseSse2)
            {
                EmitScalarSseOrSse2OpF(context, nameof(Sse.CompareGreaterThanOrEqualScalar));
            }
            else
            {
                EmitScalarFcmp(context, OpCodes.Bge_S);
            }
        }

        public static void Fcmge_V(ILEmitterCtx context)
        {
            if (context.CurrOp is OpCodeSimdReg64 && Optimizations.UseSse
                                                  && Optimizations.UseSse2)
            {
                EmitVectorSseOrSse2OpF(context, nameof(Sse.CompareGreaterThanOrEqual));
            }
            else
            {
                EmitVectorFcmp(context, OpCodes.Bge_S);
            }
        }

        public static void Fcmgt_S(ILEmitterCtx context)
        {
            if (context.CurrOp is OpCodeSimdReg64 && Optimizations.UseSse
                                                  && Optimizations.UseSse2)
            {
                EmitScalarSseOrSse2OpF(context, nameof(Sse.CompareGreaterThanScalar));
            }
            else
            {
                EmitScalarFcmp(context, OpCodes.Bgt_S);
            }
        }

        public static void Fcmgt_V(ILEmitterCtx context)
        {
            if (context.CurrOp is OpCodeSimdReg64 && Optimizations.UseSse
                                                  && Optimizations.UseSse2)
            {
                EmitVectorSseOrSse2OpF(context, nameof(Sse.CompareGreaterThan));
            }
            else
            {
                EmitVectorFcmp(context, OpCodes.Bgt_S);
            }
        }

        public static void Fcmle_S(ILEmitterCtx context)
        {
            EmitScalarFcmp(context, OpCodes.Ble_S);
        }

        public static void Fcmle_V(ILEmitterCtx context)
        {
            EmitVectorFcmp(context, OpCodes.Ble_S);
        }

        public static void Fcmlt_S(ILEmitterCtx context)
        {
            EmitScalarFcmp(context, OpCodes.Blt_S);
        }

        public static void Fcmlt_V(ILEmitterCtx context)
        {
            EmitVectorFcmp(context, OpCodes.Blt_S);
        }

        public static void Fcmp_S(ILEmitterCtx context)
        {
            OpCodeSimdReg64 op = (OpCodeSimdReg64)context.CurrOp;

            bool cmpWithZero = !(op is OpCodeSimdFcond64) ? op.Bit3 : false;

            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                if (op.Size == 0)
                {
                    Type[] typesCmp = new Type[] { typeof(Vector128<float>), typeof(Vector128<float>) };

                    ILLabel lblNaN = new ILLabel();
                    ILLabel lblEnd = new ILLabel();

                    context.EmitLdvec(op.Rn);

                    context.Emit(OpCodes.Dup);
                    context.EmitStvectmp();

                    if (cmpWithZero)
                    {
                        VectorHelper.EmitCall(context, nameof(VectorHelper.VectorSingleZero));
                    }
                    else
                    {
                        context.EmitLdvec(op.Rm);
                    }

                    context.Emit(OpCodes.Dup);
                    context.EmitStvectmp2();

                    context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.CompareOrderedScalar), typesCmp));
                    VectorHelper.EmitCall(context, nameof(VectorHelper.VectorSingleZero));

                    context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.CompareEqualOrderedScalar), typesCmp));

                    context.Emit(OpCodes.Brtrue_S, lblNaN);

                    context.EmitLdc_I4(0);

                    context.EmitLdvectmp();
                    context.EmitLdvectmp2();
                    context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.CompareGreaterThanOrEqualOrderedScalar), typesCmp));

                    context.EmitLdvectmp();
                    context.EmitLdvectmp2();
                    context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.CompareEqualOrderedScalar), typesCmp));

                    context.EmitLdvectmp();
                    context.EmitLdvectmp2();
                    context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.CompareLessThanOrderedScalar), typesCmp));

                    context.EmitStflg((int)PState.NBit);
                    context.EmitStflg((int)PState.ZBit);
                    context.EmitStflg((int)PState.CBit);
                    context.EmitStflg((int)PState.VBit);

                    context.Emit(OpCodes.Br_S, lblEnd);

                    context.MarkLabel(lblNaN);

                    context.EmitLdc_I4(1);
                    context.Emit(OpCodes.Dup);
                    context.EmitLdc_I4(0);
                    context.Emit(OpCodes.Dup);

                    context.EmitStflg((int)PState.NBit);
                    context.EmitStflg((int)PState.ZBit);
                    context.EmitStflg((int)PState.CBit);
                    context.EmitStflg((int)PState.VBit);

                    context.MarkLabel(lblEnd);
                }
                else /* if (op.Size == 1) */
                {
                    Type[] typesCmp = new Type[] { typeof(Vector128<double>), typeof(Vector128<double>) };

                    ILLabel lblNaN = new ILLabel();
                    ILLabel lblEnd = new ILLabel();

                    EmitLdvecWithCastToDouble(context, op.Rn);

                    context.Emit(OpCodes.Dup);
                    context.EmitStvectmp();

                    if (cmpWithZero)
                    {
                        VectorHelper.EmitCall(context, nameof(VectorHelper.VectorDoubleZero));
                    }
                    else
                    {
                        EmitLdvecWithCastToDouble(context, op.Rm);
                    }

                    context.Emit(OpCodes.Dup);
                    context.EmitStvectmp2();

                    context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.CompareOrderedScalar), typesCmp));
                    VectorHelper.EmitCall(context, nameof(VectorHelper.VectorDoubleZero));

                    context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.CompareEqualOrderedScalar), typesCmp));

                    context.Emit(OpCodes.Brtrue_S, lblNaN);

                    context.EmitLdc_I4(0);

                    context.EmitLdvectmp();
                    context.EmitLdvectmp2();
                    context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.CompareGreaterThanOrEqualOrderedScalar), typesCmp));

                    context.EmitLdvectmp();
                    context.EmitLdvectmp2();
                    context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.CompareEqualOrderedScalar), typesCmp));

                    context.EmitLdvectmp();
                    context.EmitLdvectmp2();
                    context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.CompareLessThanOrderedScalar), typesCmp));

                    context.EmitStflg((int)PState.NBit);
                    context.EmitStflg((int)PState.ZBit);
                    context.EmitStflg((int)PState.CBit);
                    context.EmitStflg((int)PState.VBit);

                    context.Emit(OpCodes.Br_S, lblEnd);

                    context.MarkLabel(lblNaN);

                    context.EmitLdc_I4(1);
                    context.Emit(OpCodes.Dup);
                    context.EmitLdc_I4(0);
                    context.Emit(OpCodes.Dup);

                    context.EmitStflg((int)PState.NBit);
                    context.EmitStflg((int)PState.ZBit);
                    context.EmitStflg((int)PState.CBit);
                    context.EmitStflg((int)PState.VBit);

                    context.MarkLabel(lblEnd);
                }
            }
            else
            {
                EmitVectorExtractF(context, op.Rn, 0, op.Size);

                if (cmpWithZero)
                {
                    if (op.Size == 0)
                    {
                        context.EmitLdc_R4(0f);
                    }
                    else // if (op.Size == 1)
                    {
                        context.EmitLdc_R8(0d);
                    }
                }
                else
                {
                    EmitVectorExtractF(context, op.Rm, 0, op.Size);
                }

                context.EmitLdc_I4(0);

                EmitSoftFloatCall(context, nameof(SoftFloat32.FPCompare));

                EmitSetNzcv(context);
            }
        }

        public static void Fcmpe_S(ILEmitterCtx context)
        {
            Fcmp_S(context);
        }

        private static void EmitCmp(ILEmitterCtx context, OpCode ilOp, bool scalar)
        {
            OpCodeSimd64 op = (OpCodeSimd64)context.CurrOp;

            int bytes = op.GetBitsCount() >> 3;
            int elems = !scalar ? bytes >> op.Size : 1;

            ulong szMask = ulong.MaxValue >> (64 - (8 << op.Size));

            for (int index = 0; index < elems; index++)
            {
                EmitVectorExtractSx(context, op.Rn, index, op.Size);

                if (op is OpCodeSimdReg64 binOp)
                {
                    EmitVectorExtractSx(context, binOp.Rm, index, op.Size);
                }
                else
                {
                    context.EmitLdc_I8(0L);
                }

                ILLabel lblTrue = new ILLabel();
                ILLabel lblEnd  = new ILLabel();

                context.Emit(ilOp, lblTrue);

                EmitVectorInsert(context, op.Rd, index, op.Size, 0);

                context.Emit(OpCodes.Br_S, lblEnd);

                context.MarkLabel(lblTrue);

                EmitVectorInsert(context, op.Rd, index, op.Size, (long)szMask);

                context.MarkLabel(lblEnd);
            }

            if ((op.RegisterSize == RegisterSize.Simd64) || scalar)
            {
                EmitVectorZeroUpper(context, op.Rd);
            }
        }

        private static void EmitCmtst(ILEmitterCtx context, bool scalar)
        {
            OpCodeSimdReg64 op = (OpCodeSimdReg64)context.CurrOp;

            int bytes = op.GetBitsCount() >> 3;
            int elems = !scalar ? bytes >> op.Size : 1;

            ulong szMask = ulong.MaxValue >> (64 - (8 << op.Size));

            for (int index = 0; index < elems; index++)
            {
                EmitVectorExtractZx(context, op.Rn, index, op.Size);
                EmitVectorExtractZx(context, op.Rm, index, op.Size);

                ILLabel lblTrue = new ILLabel();
                ILLabel lblEnd  = new ILLabel();

                context.Emit(OpCodes.And);

                context.EmitLdc_I8(0L);

                context.Emit(OpCodes.Bne_Un_S, lblTrue);

                EmitVectorInsert(context, op.Rd, index, op.Size, 0);

                context.Emit(OpCodes.Br_S, lblEnd);

                context.MarkLabel(lblTrue);

                EmitVectorInsert(context, op.Rd, index, op.Size, (long)szMask);

                context.MarkLabel(lblEnd);
            }

            if ((op.RegisterSize == RegisterSize.Simd64) || scalar)
            {
                EmitVectorZeroUpper(context, op.Rd);
            }
        }

        private static void EmitScalarFcmp(ILEmitterCtx context, OpCode ilOp)
        {
            EmitFcmp(context, ilOp, 0, scalar: true);
        }

        private static void EmitVectorFcmp(ILEmitterCtx context, OpCode ilOp)
        {
            OpCodeSimd64 op = (OpCodeSimd64)context.CurrOp;

            int sizeF = op.Size & 1;

            int bytes = op.GetBitsCount() >> 3;
            int elems = bytes >> sizeF + 2;

            for (int index = 0; index < elems; index++)
            {
                EmitFcmp(context, ilOp, index, scalar: false);
            }

            if (op.RegisterSize == RegisterSize.Simd64)
            {
                EmitVectorZeroUpper(context, op.Rd);
            }
        }

        private static void EmitFcmp(ILEmitterCtx context, OpCode ilOp, int index, bool scalar)
        {
            OpCodeSimd64 op = (OpCodeSimd64)context.CurrOp;

            int sizeF = op.Size & 1;

            ulong szMask = ulong.MaxValue >> (64 - (32 << sizeF));

            EmitVectorExtractF(context, op.Rn, index, sizeF);

            if (op is OpCodeSimdReg64 binOp)
            {
                EmitVectorExtractF(context, binOp.Rm, index, sizeF);
            }
            else if (sizeF == 0)
            {
                context.EmitLdc_R4(0f);
            }
            else /* if (sizeF == 1) */
            {
                context.EmitLdc_R8(0d);
            }

            ILLabel lblTrue = new ILLabel();
            ILLabel lblEnd  = new ILLabel();

            context.Emit(ilOp, lblTrue);

            if (scalar)
            {
                EmitVectorZeroAll(context, op.Rd);
            }
            else
            {
                EmitVectorInsert(context, op.Rd, index, sizeF + 2, 0);
            }

            context.Emit(OpCodes.Br_S, lblEnd);

            context.MarkLabel(lblTrue);

            if (scalar)
            {
                EmitVectorInsert(context, op.Rd, index, 3, (long)szMask);

                EmitVectorZeroUpper(context, op.Rd);
            }
            else
            {
                EmitVectorInsert(context, op.Rd, index, sizeF + 2, (long)szMask);
            }

            context.MarkLabel(lblEnd);
        }
    }
}
