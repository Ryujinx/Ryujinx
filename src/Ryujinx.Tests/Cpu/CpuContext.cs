using ARMeilleure.Memory;
using ARMeilleure.State;
using ARMeilleure.Translation;
using Ryujinx.Cpu;
using Ryujinx.Cpu.Jit;

namespace Ryujinx.Tests.Cpu
{
    public class CpuContext
    {
        private readonly Translator _translator;

        public CpuContext(IMemoryManager memory, bool for64Bit)
        {
            _translator = new Translator(new JitMemoryAllocator(), memory, for64Bit);
            memory.UnmapEvent += UnmapHandler;
        }

        private void UnmapHandler(ulong address, ulong size, bool clearRejitQueueOnly = false)
        {
            _translator.InvalidateJitCacheRegion(address, size, clearRejitQueueOnly);
        }

        public static ExecutionContext CreateExecutionContext()
        {
            return new ExecutionContext(new JitMemoryAllocator(), new TickSource(19200000));
        }

        public void Execute(ExecutionContext context, ulong address)
        {
            _translator.Execute(context, address);
        }

        public void InvalidateCacheRegion(ulong address, ulong size)
        {
            _translator.InvalidateJitCacheRegion(address, size);
        }
    }
}
