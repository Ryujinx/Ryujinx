using ARMeilleure.Common;
using ARMeilleure.Instructions;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.State;
using ARMeilleure.Translation.Cache;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using static ARMeilleure.IntermediateRepresentation.Operand.Factory;

namespace ARMeilleure.Translation
{
    /// <summary>
    /// Represents a stub manager.
    /// </summary>
    class TranslatorStubs : IDisposable
    {
        private readonly Lazy<IntPtr> _slowDispatchStub;

        private bool _disposed;

        private readonly IAddressTable<ulong> _functionTable;
        private readonly Lazy<IntPtr> _dispatchStub;
        private readonly Lazy<DispatcherFunction> _dispatchLoop;
        private readonly Lazy<WrapperFunction> _contextWrapper;

        /// <summary>
        /// Gets the dispatch stub.
        /// </summary>
        /// <exception cref="ObjectDisposedException"><see cref="TranslatorStubs"/> instance was disposed</exception>
        public IntPtr DispatchStub
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                return _dispatchStub.Value;
            }
        }

        /// <summary>
        /// Gets the slow dispatch stub.
        /// </summary>
        /// <exception cref="ObjectDisposedException"><see cref="TranslatorStubs"/> instance was disposed</exception>
        public IntPtr SlowDispatchStub
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                return _slowDispatchStub.Value;
            }
        }

        /// <summary>
        /// Gets the dispatch loop function.
        /// </summary>
        /// <exception cref="ObjectDisposedException"><see cref="TranslatorStubs"/> instance was disposed</exception>
        public DispatcherFunction DispatchLoop
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                return _dispatchLoop.Value;
            }
        }

        /// <summary>
        /// Gets the context wrapper function.
        /// </summary>
        /// <exception cref="ObjectDisposedException"><see cref="TranslatorStubs"/> instance was disposed</exception>
        public WrapperFunction ContextWrapper
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                return _contextWrapper.Value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TranslatorStubs"/> class with the specified
        /// <see cref="Translator"/> instance.
        /// </summary>
        /// <param name="functionTable">Function table used to store pointers to the functions that the guest code will call</param>
        /// <exception cref="ArgumentNullException"><paramref name="translator"/> is null</exception>
        public TranslatorStubs(IAddressTable<ulong> functionTable)
        {
            ArgumentNullException.ThrowIfNull(functionTable);

            _functionTable = functionTable;
            _slowDispatchStub = new(GenerateSlowDispatchStub, isThreadSafe: true);
            _dispatchStub = new(GenerateDispatchStub, isThreadSafe: true);
            _dispatchLoop = new(GenerateDispatchLoop, isThreadSafe: true);
            _contextWrapper = new(GenerateContextWrapper, isThreadSafe: true);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="TranslatorStubs"/> instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all unmanaged and optionally managed resources used by the <see cref="TranslatorStubs"/> instance.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to dispose managed resources also; otherwise just unmanaged resouces</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_dispatchStub.IsValueCreated)
                {
                    JitCache.Unmap(_dispatchStub.Value);
                }

                if (_dispatchLoop.IsValueCreated)
                {
                    JitCache.Unmap(Marshal.GetFunctionPointerForDelegate(_dispatchLoop.Value));
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Frees resources used by the <see cref="TranslatorStubs"/> instance.
        /// </summary>
        ~TranslatorStubs()
        {
            Dispose(false);
        }

        /// <summary>
        /// Generates a <see cref="DispatchStub"/>.
        /// </summary>
        /// <returns>Generated <see cref="DispatchStub"/></returns>
        private IntPtr GenerateDispatchStub()
        {
            var context = new EmitterContext();

            Operand lblFallback = Label();
            Operand lblEnd = Label();

            // Load the target guest address from the native context.
            Operand nativeContext = context.LoadArgument(OperandType.I64, 0);
            Operand guestAddress = context.Load(OperandType.I64,
                context.Add(nativeContext, Const((ulong)NativeContext.GetDispatchAddressOffset())));

            // Check if guest address is within range of the AddressTable.
            Operand masked = context.BitwiseAnd(guestAddress, Const(~_functionTable.Mask));
            context.BranchIfTrue(lblFallback, masked);

            Operand index = default;
            Operand page = Const((long)_functionTable.Base);

            for (int i = 0; i < _functionTable.Levels.Length; i++)
            {
                ref var level = ref _functionTable.Levels[i];

                // level.Mask is not used directly because it is more often bigger than 32-bits, so it will not
                // be encoded as an immediate on x86's bitwise and operation.
                Operand mask = Const(level.Mask >> level.Index);

                index = context.BitwiseAnd(context.ShiftRightUI(guestAddress, Const(level.Index)), mask);

                if (i < _functionTable.Levels.Length - 1)
                {
                    page = context.Load(OperandType.I64, context.Add(page, context.ShiftLeft(index, Const(3))));
                    context.BranchIfFalse(lblFallback, page);
                }
            }

            Operand hostAddress;
            Operand hostAddressAddr = context.Add(page, context.ShiftLeft(index, Const(3)));
            hostAddress = context.Load(OperandType.I64, hostAddressAddr);
            context.Tailcall(hostAddress, nativeContext);

            context.MarkLabel(lblFallback);
            hostAddress = context.Call(typeof(NativeInterface).GetMethod(nameof(NativeInterface.GetFunctionAddress)), guestAddress);
            context.Tailcall(hostAddress, nativeContext);

            var cfg = context.GetControlFlowGraph();
            var retType = OperandType.I64;
            var argTypes = new[] { OperandType.I64 };

            var func = Compiler.Compile(cfg, argTypes, retType, CompilerOptions.HighCq, RuntimeInformation.ProcessArchitecture).Map<GuestFunction>();

            return Marshal.GetFunctionPointerForDelegate(func);
        }

        /// <summary>
        /// Generates a <see cref="SlowDispatchStub"/>.
        /// </summary>
        /// <returns>Generated <see cref="SlowDispatchStub"/></returns>
        private IntPtr GenerateSlowDispatchStub()
        {
            var context = new EmitterContext();

            // Load the target guest address from the native context.
            Operand nativeContext = context.LoadArgument(OperandType.I64, 0);
            Operand guestAddress = context.Load(OperandType.I64,
                context.Add(nativeContext, Const((ulong)NativeContext.GetDispatchAddressOffset())));

            Operand hostAddress = context.Call(typeof(NativeInterface).GetMethod(nameof(NativeInterface.GetFunctionAddress)), guestAddress);
            context.Tailcall(hostAddress, nativeContext);

            var cfg = context.GetControlFlowGraph();
            var retType = OperandType.I64;
            var argTypes = new[] { OperandType.I64 };

            var func = Compiler.Compile(cfg, argTypes, retType, CompilerOptions.HighCq, RuntimeInformation.ProcessArchitecture).Map<GuestFunction>();

            return Marshal.GetFunctionPointerForDelegate(func);
        }

        /// <summary>
        /// Emits code that syncs FP state before executing guest code, or returns it to normal.
        /// </summary>
        /// <param name="context">Emitter context for the method</param>
        /// <param name="nativeContext">Pointer to the native context</param>
        /// <param name="enter">True if entering guest code, false otherwise</param>
        private static void EmitSyncFpContext(EmitterContext context, Operand nativeContext, bool enter)
        {
            if (enter)
            {
                InstEmitSimdHelper.EnterArmFpMode(context, (flag) =>
                {
                    Operand flagAddress = context.Add(nativeContext, Const((ulong)NativeContext.GetRegisterOffset(new Register((int)flag, RegisterType.FpFlag))));
                    return context.Load(OperandType.I32, flagAddress);
                });
            }
            else
            {
                InstEmitSimdHelper.ExitArmFpMode(context, (flag, value) =>
                {
                    Operand flagAddress = context.Add(nativeContext, Const((ulong)NativeContext.GetRegisterOffset(new Register((int)flag, RegisterType.FpFlag))));
                    context.Store(flagAddress, value);
                });
            }
        }

        /// <summary>
        /// Generates a <see cref="DispatchLoop"/> function.
        /// </summary>
        /// <returns><see cref="DispatchLoop"/> function</returns>
        private DispatcherFunction GenerateDispatchLoop()
        {
            var context = new EmitterContext();

            Operand beginLbl = Label();
            Operand endLbl = Label();

            Operand nativeContext = context.LoadArgument(OperandType.I64, 0);
            Operand guestAddress = context.Copy(
                context.AllocateLocal(OperandType.I64),
                context.LoadArgument(OperandType.I64, 1));

            Operand runningAddress = context.Add(nativeContext, Const((ulong)NativeContext.GetRunningOffset()));
            Operand dispatchAddress = context.Add(nativeContext, Const((ulong)NativeContext.GetDispatchAddressOffset()));

            EmitSyncFpContext(context, nativeContext, true);

            context.MarkLabel(beginLbl);
            context.Store(dispatchAddress, guestAddress);
            context.Copy(guestAddress, context.Call(Const((ulong)DispatchStub), OperandType.I64, nativeContext));
            context.BranchIfFalse(endLbl, guestAddress);
            context.BranchIfFalse(endLbl, context.Load(OperandType.I32, runningAddress));
            context.Branch(beginLbl);

            context.MarkLabel(endLbl);

            EmitSyncFpContext(context, nativeContext, false);

            context.Return();

            var cfg = context.GetControlFlowGraph();
            var retType = OperandType.None;
            var argTypes = new[] { OperandType.I64, OperandType.I64 };

            return Compiler.Compile(cfg, argTypes, retType, CompilerOptions.HighCq, RuntimeInformation.ProcessArchitecture).Map<DispatcherFunction>();
        }

        /// <summary>
        /// Generates a <see cref="ContextWrapper"/> function.
        /// </summary>
        /// <returns><see cref="ContextWrapper"/> function</returns>
        private WrapperFunction GenerateContextWrapper()
        {
            var context = new EmitterContext();

            Operand nativeContext = context.LoadArgument(OperandType.I64, 0);
            Operand guestMethod = context.LoadArgument(OperandType.I64, 1);

            EmitSyncFpContext(context, nativeContext, true);
            Operand returnValue = context.Call(guestMethod, OperandType.I64, nativeContext);
            EmitSyncFpContext(context, nativeContext, false);

            context.Return(returnValue);

            var cfg = context.GetControlFlowGraph();
            var retType = OperandType.I64;
            var argTypes = new[] { OperandType.I64, OperandType.I64 };

            return Compiler.Compile(cfg, argTypes, retType, CompilerOptions.HighCq, RuntimeInformation.ProcessArchitecture).Map<WrapperFunction>();
        }
    }
}
