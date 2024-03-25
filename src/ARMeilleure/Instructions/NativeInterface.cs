using ARMeilleure.Memory;
using ARMeilleure.State;
using ARMeilleure.Translation;
using System;

namespace ARMeilleure.Instructions
{
    static class NativeInterface
    {
        private class ThreadContext
        {
            public ExecutionContext Context { get; }
            public IMemoryManager Memory { get; }
            public Translator Translator { get; }

            public ThreadContext(ExecutionContext context, IMemoryManager memory, Translator translator)
            {
                Context = context;
                Memory = memory;
                Translator = translator;
            }
        }

        [ThreadStatic]
        private static ThreadContext Context;

        public static void RegisterThread(ExecutionContext context, IMemoryManager memory, Translator translator)
        {
            Context = new ThreadContext(context, memory, translator);
        }

        public static void UnregisterThread()
        {
            Context = null;
        }

        public static void Break(ulong address, int imm)
        {
            Statistics.PauseTimer();

            GetContext().OnBreak(address, imm);

            Statistics.ResumeTimer();
        }

        public static void SupervisorCall(ulong address, int imm)
        {
            Statistics.PauseTimer();

            GetContext().OnSupervisorCall(address, imm);

            Statistics.ResumeTimer();
        }

        public static void Undefined(ulong address, int opCode)
        {
            Statistics.PauseTimer();

            GetContext().OnUndefined(address, opCode);

            Statistics.ResumeTimer();
        }

        #region "System registers"
        public static ulong GetCtrEl0()
        {
            return GetContext().CtrEl0;
        }

        public static ulong GetDczidEl0()
        {
            return GetContext().DczidEl0;
        }

        public static ulong GetCntfrqEl0()
        {
            return GetContext().CntfrqEl0;
        }

        public static ulong GetCntpctEl0()
        {
            return GetContext().CntpctEl0;
        }

        public static ulong GetCntvctEl0()
        {
            return GetContext().CntvctEl0;
        }
        #endregion

        #region "Read"
        public static byte ReadByte(ulong address)
        {
            return GetMemoryManager().ReadGuest<byte>(address);
        }

        public static ushort ReadUInt16(ulong address)
        {
            return GetMemoryManager().ReadGuest<ushort>(address);
        }

        public static uint ReadUInt32(ulong address)
        {
            return GetMemoryManager().ReadGuest<uint>(address);
        }

        public static ulong ReadUInt64(ulong address)
        {
            return GetMemoryManager().ReadGuest<ulong>(address);
        }

        public static V128 ReadVector128(ulong address)
        {
            return GetMemoryManager().ReadGuest<V128>(address);
        }
        #endregion

        #region "Write"
        public static void WriteByte(ulong address, byte value)
        {
            GetMemoryManager().WriteGuest(address, value);
        }

        public static void WriteUInt16(ulong address, ushort value)
        {
            GetMemoryManager().WriteGuest(address, value);
        }

        public static void WriteUInt32(ulong address, uint value)
        {
            GetMemoryManager().WriteGuest(address, value);
        }

        public static void WriteUInt64(ulong address, ulong value)
        {
            GetMemoryManager().WriteGuest(address, value);
        }

        public static void WriteVector128(ulong address, V128 value)
        {
            GetMemoryManager().WriteGuest(address, value);
        }
        #endregion

        public static void EnqueueForRejit(ulong address)
        {
            Context.Translator.EnqueueForRejit(address, GetContext().ExecutionMode);
        }

        public static void SignalMemoryTracking(ulong address, ulong size, bool write)
        {
            GetMemoryManager().SignalMemoryTracking(address, size, write);
        }

        public static void ThrowInvalidMemoryAccess(ulong address)
        {
            throw new InvalidAccessException(address);
        }

        public static ulong GetFunctionAddress(ulong address)
        {
            TranslatedFunction function = Context.Translator.GetOrTranslate(address, GetContext().ExecutionMode);

            return (ulong)function.FuncPointer.ToInt64();
        }

        public static void InvalidateCacheLine(ulong address)
        {
            Context.Translator.InvalidateJitCacheRegion(address, InstEmit.DczSizeInBytes);
        }

        public static bool CheckSynchronization()
        {
            Statistics.PauseTimer();

            ExecutionContext context = GetContext();

            context.CheckInterrupt();

            Statistics.ResumeTimer();

            return context.Running;
        }

        public static ExecutionContext GetContext()
        {
            return Context.Context;
        }

        public static IMemoryManager GetMemoryManager()
        {
            return Context.Memory;
        }
    }
}
