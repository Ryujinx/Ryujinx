using ARMeilleure.Signal;
using Ryujinx.Common;
using Ryujinx.Memory;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Cpu.Signal
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct SignalHandlerRange
    {
        public int IsActive;
        public nuint RangeAddress;
        public nuint RangeEndAddress;
        public IntPtr ActionPointer;
    }

    [InlineArray(NativeSignalHandlerGenerator.MaxTrackedRanges)]
    struct SignalHandlerRangeArray
    {
        public SignalHandlerRange Range0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct SignalHandlerConfig
    {
        /// <summary>
        /// The byte offset of the faulting address in the SigInfo or ExceptionRecord struct.
        /// </summary>
        public int StructAddressOffset;

        /// <summary>
        /// The byte offset of the write flag in the SigInfo or ExceptionRecord struct.
        /// </summary>
        public int StructWriteOffset;

        /// <summary>
        /// The sigaction handler that was registered before this one. (unix only)
        /// </summary>
        public nuint UnixOldSigaction;

        /// <summary>
        /// The type of the previous sigaction. True for the 3 argument variant. (unix only)
        /// </summary>
        public int UnixOldSigaction3Arg;

        /// <summary>
        /// Fixed size array of tracked ranges.
        /// </summary>
        public SignalHandlerRangeArray Ranges;
    }

    static class NativeSignalHandler
    {
        private static readonly IntPtr _handlerConfig;
        private static IntPtr _signalHandlerPtr;

        private static MemoryBlock _codeBlock;

        private static readonly Lock _lock = new();
        private static bool _initialized;

        static NativeSignalHandler()
        {
            _handlerConfig = Marshal.AllocHGlobal(Unsafe.SizeOf<SignalHandlerConfig>());
            ref SignalHandlerConfig config = ref GetConfigRef();

            config = new SignalHandlerConfig();
        }

        public static void InitializeSignalHandler(Func<IntPtr, IntPtr, IntPtr> customSignalHandlerFactory = null)
        {
            if (_initialized)
            {
                return;
            }

            lock (_lock)
            {
                if (_initialized)
                {
                    return;
                }

                int rangeStructSize = Unsafe.SizeOf<SignalHandlerRange>();

                ref SignalHandlerConfig config = ref GetConfigRef();

                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    _signalHandlerPtr = MapCode(NativeSignalHandlerGenerator.GenerateUnixSignalHandler(_handlerConfig, rangeStructSize));

                    if (customSignalHandlerFactory != null)
                    {
                        _signalHandlerPtr = customSignalHandlerFactory(UnixSignalHandlerRegistration.GetSegfaultExceptionHandler().sa_handler, _signalHandlerPtr);
                    }

                    var old = UnixSignalHandlerRegistration.RegisterExceptionHandler(_signalHandlerPtr);

                    config.UnixOldSigaction = (nuint)(ulong)old.sa_handler;
                    config.UnixOldSigaction3Arg = old.sa_flags & 4;
                }
                else
                {
                    config.StructAddressOffset = 40; // ExceptionInformation1
                    config.StructWriteOffset = 32; // ExceptionInformation0

                    _signalHandlerPtr = MapCode(NativeSignalHandlerGenerator.GenerateWindowsSignalHandler(_handlerConfig, rangeStructSize));

                    if (customSignalHandlerFactory != null)
                    {
                        _signalHandlerPtr = customSignalHandlerFactory(IntPtr.Zero, _signalHandlerPtr);
                    }

                    WindowsSignalHandlerRegistration.RegisterExceptionHandler(_signalHandlerPtr);
                }

                _initialized = true;
            }
        }

        private static IntPtr MapCode(ReadOnlySpan<byte> code)
        {
            Debug.Assert(_codeBlock == null);

            ulong codeSizeAligned = BitUtils.AlignUp((ulong)code.Length, MemoryBlock.GetPageSize());

            _codeBlock = new MemoryBlock(codeSizeAligned);
            _codeBlock.Write(0, code);
            _codeBlock.Reprotect(0, codeSizeAligned, MemoryPermission.ReadAndExecute);

            return _codeBlock.Pointer;
        }

        private static unsafe ref SignalHandlerConfig GetConfigRef()
        {
            return ref Unsafe.AsRef<SignalHandlerConfig>((void*)_handlerConfig);
        }

        public static bool AddTrackedRegion(nuint address, nuint endAddress, IntPtr action)
        {
            Span<SignalHandlerRange> ranges = GetConfigRef().Ranges;

            for (int i = 0; i < NativeSignalHandlerGenerator.MaxTrackedRanges; i++)
            {
                if (ranges[i].IsActive == 0)
                {
                    ranges[i].RangeAddress = address;
                    ranges[i].RangeEndAddress = endAddress;
                    ranges[i].ActionPointer = action;
                    ranges[i].IsActive = 1;

                    return true;
                }
            }

            return false;
        }

        public static bool RemoveTrackedRegion(nuint address)
        {
            Span<SignalHandlerRange> ranges = GetConfigRef().Ranges;

            for (int i = 0; i < NativeSignalHandlerGenerator.MaxTrackedRanges; i++)
            {
                if (ranges[i].IsActive == 1 && ranges[i].RangeAddress == address)
                {
                    ranges[i].IsActive = 0;

                    return true;
                }
            }

            return false;
        }

        public static bool SupportsFaultAddressPatching()
        {
            return NativeSignalHandlerGenerator.SupportsFaultAddressPatchingForHost();
        }
    }
}
