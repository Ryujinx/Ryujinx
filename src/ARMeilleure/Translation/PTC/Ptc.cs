using ARMeilleure.CodeGen;
using ARMeilleure.CodeGen.Linking;
using ARMeilleure.CodeGen.Unwinding;
using ARMeilleure.Common;
using ARMeilleure.Memory;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static ARMeilleure.Translation.PTC.PtcFormatter;

namespace ARMeilleure.Translation.PTC
{
    using Arm64HardwareCapabilities = CodeGen.Arm64.HardwareCapabilities;
    using X86HardwareCapabilities = CodeGen.X86.HardwareCapabilities;

    class Ptc : IPtcLoadState
    {
        private const string OuterHeaderMagicString = "PTCohd\0\0";
        private const string InnerHeaderMagicString = "PTCihd\0\0";

        private const uint InternalVersion = 6978; //! To be incremented manually for each change to the ARMeilleure project.

        private const string ActualDir = "0";
        private const string BackupDir = "1";

        private const string TitleIdTextDefault = "0000000000000000";
        private const string DisplayVersionDefault = "0";

        public static readonly Symbol PageTableSymbol = new(SymbolType.Special, 1);
        public static readonly Symbol CountTableSymbol = new(SymbolType.Special, 2);
        public static readonly Symbol DispatchStubSymbol = new(SymbolType.Special, 3);
        public static readonly Symbol FunctionTableSymbol = new(SymbolType.Special, 4);

        private const byte FillingByte = 0x00;
        private const CompressionLevel SaveCompressionLevel = CompressionLevel.Fastest;

        public PtcProfiler Profiler { get; }

        // Carriers.
        private MemoryStream _infosStream;
        private List<byte[]> _codesList;
        private MemoryStream _relocsStream;
        private MemoryStream _unwindInfosStream;

        private readonly ulong _outerHeaderMagic;
        private readonly ulong _innerHeaderMagic;

        private readonly ManualResetEvent _waitEvent;

        private readonly object _lock;

        private bool _disposed;

        public string TitleIdText { get; private set; }
        public string DisplayVersion { get; private set; }

        private MemoryManagerType _memoryMode;

        public string CachePathActual { get; private set; }
        public string CachePathBackup { get; private set; }

        public PtcState State { get; private set; }

        // Progress reporting helpers.
        private volatile int _translateCount;
        private volatile int _translateTotalCount;
        public event Action<PtcLoadingState, int, int> PtcStateChanged;

        public Ptc()
        {
            Profiler = new PtcProfiler(this);

            InitializeCarriers();

            _outerHeaderMagic = BinaryPrimitives.ReadUInt64LittleEndian(EncodingCache.UTF8NoBOM.GetBytes(OuterHeaderMagicString).AsSpan());
            _innerHeaderMagic = BinaryPrimitives.ReadUInt64LittleEndian(EncodingCache.UTF8NoBOM.GetBytes(InnerHeaderMagicString).AsSpan());

            _waitEvent = new ManualResetEvent(true);

            _lock = new object();

            _disposed = false;

            TitleIdText = TitleIdTextDefault;
            DisplayVersion = DisplayVersionDefault;

            CachePathActual = string.Empty;
            CachePathBackup = string.Empty;

            Disable();
        }

        public void Initialize(string titleIdText, string displayVersion, bool enabled, MemoryManagerType memoryMode)
        {
            Wait();

            Profiler.Wait();
            Profiler.ClearEntries();

            Logger.Info?.Print(LogClass.Ptc, $"Initializing Profiled Persistent Translation Cache (enabled: {enabled}).");

            if (!enabled || string.IsNullOrEmpty(titleIdText) || titleIdText == TitleIdTextDefault)
            {
                TitleIdText = TitleIdTextDefault;
                DisplayVersion = DisplayVersionDefault;

                CachePathActual = string.Empty;
                CachePathBackup = string.Empty;

                Disable();

                return;
            }

            TitleIdText = titleIdText;
            DisplayVersion = !string.IsNullOrEmpty(displayVersion) ? displayVersion : DisplayVersionDefault;
            _memoryMode = memoryMode;

            string workPathActual = Path.Combine(AppDataManager.GamesDirPath, TitleIdText, "cache", "cpu", ActualDir);
            string workPathBackup = Path.Combine(AppDataManager.GamesDirPath, TitleIdText, "cache", "cpu", BackupDir);

            if (!Directory.Exists(workPathActual))
            {
                Directory.CreateDirectory(workPathActual);
            }

            if (!Directory.Exists(workPathBackup))
            {
                Directory.CreateDirectory(workPathBackup);
            }

            CachePathActual = Path.Combine(workPathActual, DisplayVersion);
            CachePathBackup = Path.Combine(workPathBackup, DisplayVersion);

            PreLoad();
            Profiler.PreLoad();

            Enable();
        }

        private void InitializeCarriers()
        {
            _infosStream = MemoryStreamManager.Shared.GetStream();
            _codesList = new List<byte[]>();
            _relocsStream = MemoryStreamManager.Shared.GetStream();
            _unwindInfosStream = MemoryStreamManager.Shared.GetStream();
        }

        private void DisposeCarriers()
        {
            _infosStream.Dispose();
            _codesList.Clear();
            _relocsStream.Dispose();
            _unwindInfosStream.Dispose();
        }

        private bool AreCarriersEmpty()
        {
            return _infosStream.Length == 0L && _codesList.Count == 0 && _relocsStream.Length == 0L && _unwindInfosStream.Length == 0L;
        }

        private void ResetCarriersIfNeeded()
        {
            if (AreCarriersEmpty())
            {
                return;
            }

            DisposeCarriers();

            InitializeCarriers();
        }

        private void PreLoad()
        {
            string fileNameActual = $"{CachePathActual}.cache";
            string fileNameBackup = $"{CachePathBackup}.cache";

            FileInfo fileInfoActual = new(fileNameActual);
            FileInfo fileInfoBackup = new(fileNameBackup);

            if (fileInfoActual.Exists && fileInfoActual.Length != 0L)
            {
                if (!Load(fileNameActual, false))
                {
                    if (fileInfoBackup.Exists && fileInfoBackup.Length != 0L)
                    {
                        Load(fileNameBackup, true);
                    }
                }
            }
            else if (fileInfoBackup.Exists && fileInfoBackup.Length != 0L)
            {
                Load(fileNameBackup, true);
            }
        }

        private unsafe bool Load(string fileName, bool isBackup)
        {
            using (FileStream compressedStream = new(fileName, FileMode.Open))
            using (DeflateStream deflateStream = new(compressedStream, CompressionMode.Decompress, true))
            {
                OuterHeader outerHeader = DeserializeStructure<OuterHeader>(compressedStream);

                if (!outerHeader.IsHeaderValid())
                {
                    InvalidateCompressedStream(compressedStream);

                    return false;
                }

                if (outerHeader.Magic != _outerHeaderMagic)
                {
                    InvalidateCompressedStream(compressedStream);

                    return false;
                }

                if (outerHeader.CacheFileVersion != InternalVersion)
                {
                    InvalidateCompressedStream(compressedStream);

                    return false;
                }

                if (outerHeader.Endianness != GetEndianness())
                {
                    InvalidateCompressedStream(compressedStream);

                    return false;
                }

                if (outerHeader.FeatureInfo != GetFeatureInfo())
                {
                    InvalidateCompressedStream(compressedStream);

                    return false;
                }

                if (outerHeader.MemoryManagerMode != GetMemoryManagerMode())
                {
                    InvalidateCompressedStream(compressedStream);

                    return false;
                }

                if (outerHeader.OSPlatform != GetOSPlatform())
                {
                    InvalidateCompressedStream(compressedStream);

                    return false;
                }

                if (outerHeader.Architecture != (uint)RuntimeInformation.ProcessArchitecture)
                {
                    InvalidateCompressedStream(compressedStream);

                    return false;
                }

                IntPtr intPtr = IntPtr.Zero;

                try
                {
                    intPtr = Marshal.AllocHGlobal(new IntPtr(outerHeader.UncompressedStreamSize));

                    using UnmanagedMemoryStream stream = new((byte*)intPtr.ToPointer(), outerHeader.UncompressedStreamSize, outerHeader.UncompressedStreamSize, FileAccess.ReadWrite);
                    try
                    {
                        deflateStream.CopyTo(stream);
                    }
                    catch
                    {
                        InvalidateCompressedStream(compressedStream);

                        return false;
                    }

                    Debug.Assert(stream.Position == stream.Length);

                    stream.Seek(0L, SeekOrigin.Begin);

                    InnerHeader innerHeader = DeserializeStructure<InnerHeader>(stream);

                    if (!innerHeader.IsHeaderValid())
                    {
                        InvalidateCompressedStream(compressedStream);

                        return false;
                    }

                    if (innerHeader.Magic != _innerHeaderMagic)
                    {
                        InvalidateCompressedStream(compressedStream);

                        return false;
                    }

                    ReadOnlySpan<byte> infosBytes = new(stream.PositionPointer, innerHeader.InfosLength);
                    stream.Seek(innerHeader.InfosLength, SeekOrigin.Current);

                    Hash128 infosHash = XXHash128.ComputeHash(infosBytes);

                    if (innerHeader.InfosHash != infosHash)
                    {
                        InvalidateCompressedStream(compressedStream);

                        return false;
                    }

                    ReadOnlySpan<byte> codesBytes = (int)innerHeader.CodesLength > 0 ? new(stream.PositionPointer, (int)innerHeader.CodesLength) : ReadOnlySpan<byte>.Empty;
                    stream.Seek(innerHeader.CodesLength, SeekOrigin.Current);

                    Hash128 codesHash = XXHash128.ComputeHash(codesBytes);

                    if (innerHeader.CodesHash != codesHash)
                    {
                        InvalidateCompressedStream(compressedStream);

                        return false;
                    }

                    ReadOnlySpan<byte> relocsBytes = new(stream.PositionPointer, innerHeader.RelocsLength);
                    stream.Seek(innerHeader.RelocsLength, SeekOrigin.Current);

                    Hash128 relocsHash = XXHash128.ComputeHash(relocsBytes);

                    if (innerHeader.RelocsHash != relocsHash)
                    {
                        InvalidateCompressedStream(compressedStream);

                        return false;
                    }

                    ReadOnlySpan<byte> unwindInfosBytes = new(stream.PositionPointer, innerHeader.UnwindInfosLength);
                    stream.Seek(innerHeader.UnwindInfosLength, SeekOrigin.Current);

                    Hash128 unwindInfosHash = XXHash128.ComputeHash(unwindInfosBytes);

                    if (innerHeader.UnwindInfosHash != unwindInfosHash)
                    {
                        InvalidateCompressedStream(compressedStream);

                        return false;
                    }

                    Debug.Assert(stream.Position == stream.Length);

                    stream.Seek((long)Unsafe.SizeOf<InnerHeader>(), SeekOrigin.Begin);

                    _infosStream.Write(infosBytes);
                    stream.Seek(innerHeader.InfosLength, SeekOrigin.Current);

                    _codesList.ReadFrom(stream);

                    _relocsStream.Write(relocsBytes);
                    stream.Seek(innerHeader.RelocsLength, SeekOrigin.Current);

                    _unwindInfosStream.Write(unwindInfosBytes);
                    stream.Seek(innerHeader.UnwindInfosLength, SeekOrigin.Current);

                    Debug.Assert(stream.Position == stream.Length);
                }
                finally
                {
                    if (intPtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(intPtr);
                    }
                }
            }

            long fileSize = new FileInfo(fileName).Length;

            Logger.Info?.Print(LogClass.Ptc, $"{(isBackup ? "Loaded Backup Translation Cache" : "Loaded Translation Cache")} (size: {fileSize} bytes, translated functions: {GetEntriesCount()}).");

            return true;
        }

        private static void InvalidateCompressedStream(FileStream compressedStream)
        {
            compressedStream.SetLength(0L);
        }

        private void PreSave()
        {
            _waitEvent.Reset();

            try
            {
                string fileNameActual = $"{CachePathActual}.cache";
                string fileNameBackup = $"{CachePathBackup}.cache";

                FileInfo fileInfoActual = new(fileNameActual);

                if (fileInfoActual.Exists && fileInfoActual.Length != 0L)
                {
                    File.Copy(fileNameActual, fileNameBackup, true);
                }

                Save(fileNameActual);
            }
            finally
            {
                ResetCarriersIfNeeded();

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            }

            _waitEvent.Set();
        }

        private unsafe void Save(string fileName)
        {
            int translatedFuncsCount;

            InnerHeader innerHeader = new()
            {
                Magic = _innerHeaderMagic,

                InfosLength = (int)_infosStream.Length,
                CodesLength = _codesList.Length(),
                RelocsLength = (int)_relocsStream.Length,
                UnwindInfosLength = (int)_unwindInfosStream.Length,
            };

            OuterHeader outerHeader = new()
            {
                Magic = _outerHeaderMagic,

                CacheFileVersion = InternalVersion,
                Endianness = GetEndianness(),
                FeatureInfo = GetFeatureInfo(),
                MemoryManagerMode = GetMemoryManagerMode(),
                OSPlatform = GetOSPlatform(),
                Architecture = (uint)RuntimeInformation.ProcessArchitecture,

                UncompressedStreamSize =
                (long)Unsafe.SizeOf<InnerHeader>() +
                innerHeader.InfosLength +
                innerHeader.CodesLength +
                innerHeader.RelocsLength +
                innerHeader.UnwindInfosLength,
            };

            outerHeader.SetHeaderHash();

            IntPtr intPtr = IntPtr.Zero;

            try
            {
                intPtr = Marshal.AllocHGlobal(new IntPtr(outerHeader.UncompressedStreamSize));

                using UnmanagedMemoryStream stream = new((byte*)intPtr.ToPointer(), outerHeader.UncompressedStreamSize, outerHeader.UncompressedStreamSize, FileAccess.ReadWrite);
                stream.Seek((long)Unsafe.SizeOf<InnerHeader>(), SeekOrigin.Begin);

                ReadOnlySpan<byte> infosBytes = new(stream.PositionPointer, innerHeader.InfosLength);
                _infosStream.WriteTo(stream);

                ReadOnlySpan<byte> codesBytes = (int)innerHeader.CodesLength > 0 ? new(stream.PositionPointer, (int)innerHeader.CodesLength) : ReadOnlySpan<byte>.Empty;
                _codesList.WriteTo(stream);

                ReadOnlySpan<byte> relocsBytes = new(stream.PositionPointer, innerHeader.RelocsLength);
                _relocsStream.WriteTo(stream);

                ReadOnlySpan<byte> unwindInfosBytes = new(stream.PositionPointer, innerHeader.UnwindInfosLength);
                _unwindInfosStream.WriteTo(stream);

                Debug.Assert(stream.Position == stream.Length);

                innerHeader.InfosHash = XXHash128.ComputeHash(infosBytes);
                innerHeader.CodesHash = XXHash128.ComputeHash(codesBytes);
                innerHeader.RelocsHash = XXHash128.ComputeHash(relocsBytes);
                innerHeader.UnwindInfosHash = XXHash128.ComputeHash(unwindInfosBytes);

                innerHeader.SetHeaderHash();

                stream.Seek(0L, SeekOrigin.Begin);
                SerializeStructure(stream, innerHeader);

                translatedFuncsCount = GetEntriesCount();

                ResetCarriersIfNeeded();

                using FileStream compressedStream = new(fileName, FileMode.OpenOrCreate);
                using DeflateStream deflateStream = new(compressedStream, SaveCompressionLevel, true);
                try
                {
                    SerializeStructure(compressedStream, outerHeader);

                    stream.Seek(0L, SeekOrigin.Begin);
                    stream.CopyTo(deflateStream);
                }
                catch
                {
                    compressedStream.Position = 0L;
                }

                if (compressedStream.Position < compressedStream.Length)
                {
                    compressedStream.SetLength(compressedStream.Position);
                }
            }
            finally
            {
                if (intPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(intPtr);
                }
            }

            long fileSize = new FileInfo(fileName).Length;

            if (fileSize != 0L)
            {
                Logger.Info?.Print(LogClass.Ptc, $"Saved Translation Cache (size: {fileSize} bytes, translated functions: {translatedFuncsCount}).");
            }
        }

        public void LoadTranslations(Translator translator)
        {
            if (AreCarriersEmpty())
            {
                return;
            }

            long infosStreamLength = _infosStream.Length;
            long relocsStreamLength = _relocsStream.Length;
            long unwindInfosStreamLength = _unwindInfosStream.Length;

            _infosStream.Seek(0L, SeekOrigin.Begin);
            _relocsStream.Seek(0L, SeekOrigin.Begin);
            _unwindInfosStream.Seek(0L, SeekOrigin.Begin);

            using (BinaryReader relocsReader = new(_relocsStream, EncodingCache.UTF8NoBOM, true))
            using (BinaryReader unwindInfosReader = new(_unwindInfosStream, EncodingCache.UTF8NoBOM, true))
            {
                for (int index = 0; index < GetEntriesCount(); index++)
                {
                    InfoEntry infoEntry = DeserializeStructure<InfoEntry>(_infosStream);

                    if (infoEntry.Stubbed)
                    {
                        SkipCode(index, infoEntry.CodeLength);
                        SkipReloc(infoEntry.RelocEntriesCount);
                        SkipUnwindInfo(unwindInfosReader);

                        continue;
                    }

                    bool isEntryChanged = infoEntry.Hash != ComputeHash(translator.Memory, infoEntry.Address, infoEntry.GuestSize);

                    if (isEntryChanged || (!infoEntry.HighCq && Profiler.ProfiledFuncs.TryGetValue(infoEntry.Address, out var value) && value.HighCq))
                    {
                        infoEntry.Stubbed = true;
                        infoEntry.CodeLength = 0;
                        UpdateInfo(infoEntry);

                        StubCode(index);
                        StubReloc(infoEntry.RelocEntriesCount);
                        StubUnwindInfo(unwindInfosReader);

                        if (isEntryChanged)
                        {
                            Logger.Info?.Print(LogClass.Ptc, $"Invalidated translated function (address: 0x{infoEntry.Address:X16})");
                        }

                        continue;
                    }

                    byte[] code = ReadCode(index, infoEntry.CodeLength);

                    Counter<uint> callCounter = null;

                    if (infoEntry.RelocEntriesCount != 0)
                    {
                        RelocEntry[] relocEntries = GetRelocEntries(relocsReader, infoEntry.RelocEntriesCount);

                        PatchCode(translator, code, relocEntries, out callCounter);
                    }

                    UnwindInfo unwindInfo = ReadUnwindInfo(unwindInfosReader);

                    TranslatedFunction func = FastTranslate(code, callCounter, infoEntry.GuestSize, unwindInfo, infoEntry.HighCq);

                    translator.RegisterFunction(infoEntry.Address, func);

                    bool isAddressUnique = translator.Functions.TryAdd(infoEntry.Address, infoEntry.GuestSize, func);

                    Debug.Assert(isAddressUnique, $"The address 0x{infoEntry.Address:X16} is not unique.");
                }
            }

            if (_infosStream.Length != infosStreamLength || _infosStream.Position != infosStreamLength ||
                _relocsStream.Length != relocsStreamLength || _relocsStream.Position != relocsStreamLength ||
                _unwindInfosStream.Length != unwindInfosStreamLength || _unwindInfosStream.Position != unwindInfosStreamLength)
            {
                throw new Exception("The length of a memory stream has changed, or its position has not reached or has exceeded its end.");
            }

            Logger.Info?.Print(LogClass.Ptc, $"{translator.Functions.Count} translated functions loaded");
        }

        private int GetEntriesCount()
        {
            return _codesList.Count;
        }

        [Conditional("DEBUG")]
        private void SkipCode(int index, int codeLength)
        {
            Debug.Assert(_codesList[index].Length == 0);
            Debug.Assert(codeLength == 0);
        }

        private void SkipReloc(int relocEntriesCount)
        {
            _relocsStream.Seek(relocEntriesCount * RelocEntry.Stride, SeekOrigin.Current);
        }

        private void SkipUnwindInfo(BinaryReader unwindInfosReader)
        {
            int pushEntriesLength = unwindInfosReader.ReadInt32();

            _unwindInfosStream.Seek(pushEntriesLength * UnwindPushEntry.Stride + UnwindInfo.Stride, SeekOrigin.Current);
        }

        private byte[] ReadCode(int index, int codeLength)
        {
            Debug.Assert(_codesList[index].Length == codeLength);

            return _codesList[index];
        }

        private static RelocEntry[] GetRelocEntries(BinaryReader relocsReader, int relocEntriesCount)
        {
            RelocEntry[] relocEntries = new RelocEntry[relocEntriesCount];

            for (int i = 0; i < relocEntriesCount; i++)
            {
                int position = relocsReader.ReadInt32();
                SymbolType type = (SymbolType)relocsReader.ReadByte();
                ulong value = relocsReader.ReadUInt64();

                relocEntries[i] = new RelocEntry(position, new Symbol(type, value));
            }

            return relocEntries;
        }

        private static void PatchCode(Translator translator, Span<byte> code, RelocEntry[] relocEntries, out Counter<uint> callCounter)
        {
            callCounter = null;

            foreach (RelocEntry relocEntry in relocEntries)
            {
                IntPtr? imm = null;
                Symbol symbol = relocEntry.Symbol;

                if (symbol.Type == SymbolType.FunctionTable)
                {
                    ulong guestAddress = symbol.Value;

                    if (translator.FunctionTable.IsValid(guestAddress))
                    {
                        unsafe
                        {
                            imm = (IntPtr)Unsafe.AsPointer(ref translator.FunctionTable.GetValue(guestAddress));
                        }
                    }
                }
                else if (symbol.Type == SymbolType.DelegateTable)
                {
                    int index = (int)symbol.Value;

                    if (Delegates.TryGetDelegateFuncPtrByIndex(index, out IntPtr funcPtr))
                    {
                        imm = funcPtr;
                    }
                }
                else if (symbol == PageTableSymbol)
                {
                    imm = translator.Memory.PageTablePointer;
                }
                else if (symbol == CountTableSymbol)
                {
                    callCounter ??= new Counter<uint>(translator.CountTable);

                    unsafe
                    {
                        imm = (IntPtr)Unsafe.AsPointer(ref callCounter.Value);
                    }
                }
                else if (symbol == DispatchStubSymbol)
                {
                    imm = translator.Stubs.DispatchStub;
                }
                else if (symbol == FunctionTableSymbol)
                {
                    imm = translator.FunctionTable.Base;
                }

                if (imm == null)
                {
                    throw new Exception($"Unexpected reloc entry {relocEntry}.");
                }

                BinaryPrimitives.WriteUInt64LittleEndian(code.Slice(relocEntry.Position, 8), (ulong)imm.Value);
            }
        }

        private static UnwindInfo ReadUnwindInfo(BinaryReader unwindInfosReader)
        {
            int pushEntriesLength = unwindInfosReader.ReadInt32();

            UnwindPushEntry[] pushEntries = new UnwindPushEntry[pushEntriesLength];

            for (int i = 0; i < pushEntriesLength; i++)
            {
                int pseudoOp = unwindInfosReader.ReadInt32();
                int prologOffset = unwindInfosReader.ReadInt32();
                int regIndex = unwindInfosReader.ReadInt32();
                int stackOffsetOrAllocSize = unwindInfosReader.ReadInt32();

                pushEntries[i] = new UnwindPushEntry((UnwindPseudoOp)pseudoOp, prologOffset, regIndex, stackOffsetOrAllocSize);
            }

            int prologueSize = unwindInfosReader.ReadInt32();

            return new UnwindInfo(pushEntries, prologueSize);
        }

        private static TranslatedFunction FastTranslate(
            byte[] code,
            Counter<uint> callCounter,
            ulong guestSize,
            UnwindInfo unwindInfo,
            bool highCq)
        {
            var cFunc = new CompiledFunction(code, unwindInfo, RelocInfo.Empty);
            var gFunc = cFunc.MapWithPointer<GuestFunction>(out IntPtr gFuncPointer);

            return new TranslatedFunction(gFunc, gFuncPointer, callCounter, guestSize, highCq);
        }

        private void UpdateInfo(InfoEntry infoEntry)
        {
            _infosStream.Seek(-Unsafe.SizeOf<InfoEntry>(), SeekOrigin.Current);

            SerializeStructure(_infosStream, infoEntry);
        }

        private void StubCode(int index)
        {
            _codesList[index] = Array.Empty<byte>();
        }

        private void StubReloc(int relocEntriesCount)
        {
            for (int i = 0; i < relocEntriesCount * RelocEntry.Stride; i++)
            {
                _relocsStream.WriteByte(FillingByte);
            }
        }

        private void StubUnwindInfo(BinaryReader unwindInfosReader)
        {
            int pushEntriesLength = unwindInfosReader.ReadInt32();

            for (int i = 0; i < pushEntriesLength * UnwindPushEntry.Stride + UnwindInfo.Stride; i++)
            {
                _unwindInfosStream.WriteByte(FillingByte);
            }
        }

        public void MakeAndSaveTranslations(Translator translator)
        {
            var profiledFuncsToTranslate = Profiler.GetProfiledFuncsToTranslate(translator.Functions);

            _translateCount = 0;
            _translateTotalCount = profiledFuncsToTranslate.Count;

            if (_translateTotalCount == 0)
            {
                ResetCarriersIfNeeded();

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                return;
            }

            int degreeOfParallelism = Environment.ProcessorCount;

            // If there are enough cores lying around, we leave one alone for other tasks.
            if (degreeOfParallelism > 4)
            {
                degreeOfParallelism--;
            }

            Logger.Info?.Print(LogClass.Ptc, $"{_translateCount} of {_translateTotalCount} functions translated | Thread count: {degreeOfParallelism}");

            PtcStateChanged?.Invoke(PtcLoadingState.Start, _translateCount, _translateTotalCount);

            using AutoResetEvent progressReportEvent = new(false);

            Thread progressReportThread = new(ReportProgress)
            {
                Name = "Ptc.ProgressReporter",
                Priority = ThreadPriority.Lowest,
                IsBackground = true,
            };

            progressReportThread.Start(progressReportEvent);

            void TranslateFuncs()
            {
                while (profiledFuncsToTranslate.TryDequeue(out var item))
                {
                    ulong address = item.address;

                    Debug.Assert(Profiler.IsAddressInStaticCodeRange(address));

                    TranslatedFunction func = translator.Translate(address, item.funcProfile.Mode, item.funcProfile.HighCq);

                    bool isAddressUnique = translator.Functions.TryAdd(address, func.GuestSize, func);

                    Debug.Assert(isAddressUnique, $"The address 0x{address:X16} is not unique.");

                    Interlocked.Increment(ref _translateCount);

                    translator.RegisterFunction(address, func);

                    if (State != PtcState.Enabled)
                    {
                        break;
                    }
                }
            }

            List<Thread> threads = new();

            for (int i = 0; i < degreeOfParallelism; i++)
            {
                Thread thread = new(TranslateFuncs)
                {
                    IsBackground = true,
                };

                threads.Add(thread);
            }

            Stopwatch sw = Stopwatch.StartNew();

            foreach (var thread in threads)
            {
                thread.Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }

            threads.Clear();

            progressReportEvent.Set();
            progressReportThread.Join();

            sw.Stop();

            PtcStateChanged?.Invoke(PtcLoadingState.Loaded, _translateCount, _translateTotalCount);

            Logger.Info?.Print(LogClass.Ptc, $"{_translateCount} of {_translateTotalCount} functions translated | Thread count: {degreeOfParallelism} in {sw.Elapsed.TotalSeconds} s");

            Thread preSaveThread = new(PreSave)
            {
                IsBackground = true,
            };
            preSaveThread.Start();
        }

        private void ReportProgress(object state)
        {
            const int RefreshRate = 50; // ms.

            AutoResetEvent endEvent = (AutoResetEvent)state;

            int count = 0;

            do
            {
                int newCount = _translateCount;

                if (count != newCount)
                {
                    PtcStateChanged?.Invoke(PtcLoadingState.Loading, newCount, _translateTotalCount);
                    count = newCount;
                }
            }
            while (!endEvent.WaitOne(RefreshRate));
        }

        public static Hash128 ComputeHash(IMemoryManager memory, ulong address, ulong guestSize)
        {
            return XXHash128.ComputeHash(memory.GetSpan(address, checked((int)(guestSize))));
        }

        public void WriteCompiledFunction(ulong address, ulong guestSize, Hash128 hash, bool highCq, CompiledFunction compiledFunc)
        {
            lock (_lock)
            {
                byte[] code = compiledFunc.Code;
                RelocInfo relocInfo = compiledFunc.RelocInfo;
                UnwindInfo unwindInfo = compiledFunc.UnwindInfo;

                InfoEntry infoEntry = new()
                {
                    Address = address,
                    GuestSize = guestSize,
                    Hash = hash,
                    HighCq = highCq,
                    Stubbed = false,
                    CodeLength = code.Length,
                    RelocEntriesCount = relocInfo.Entries.Length,
                };

                SerializeStructure(_infosStream, infoEntry);

                WriteCode(code.AsSpan());

                // WriteReloc.
                using var relocInfoWriter = new BinaryWriter(_relocsStream, EncodingCache.UTF8NoBOM, true);

                foreach (RelocEntry entry in relocInfo.Entries)
                {
                    relocInfoWriter.Write(entry.Position);
                    relocInfoWriter.Write((byte)entry.Symbol.Type);
                    relocInfoWriter.Write(entry.Symbol.Value);
                }

                // WriteUnwindInfo.
                using var unwindInfoWriter = new BinaryWriter(_unwindInfosStream, EncodingCache.UTF8NoBOM, true);

                unwindInfoWriter.Write(unwindInfo.PushEntries.Length);

                foreach (UnwindPushEntry unwindPushEntry in unwindInfo.PushEntries)
                {
                    unwindInfoWriter.Write((int)unwindPushEntry.PseudoOp);
                    unwindInfoWriter.Write(unwindPushEntry.PrologOffset);
                    unwindInfoWriter.Write(unwindPushEntry.RegIndex);
                    unwindInfoWriter.Write(unwindPushEntry.StackOffsetOrAllocSize);
                }

                unwindInfoWriter.Write(unwindInfo.PrologSize);
            }
        }

        private void WriteCode(ReadOnlySpan<byte> code)
        {
            _codesList.Add(code.ToArray());
        }

        public static bool GetEndianness()
        {
            return BitConverter.IsLittleEndian;
        }

        private static FeatureInfo GetFeatureInfo()
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                return new FeatureInfo(
                    (ulong)Arm64HardwareCapabilities.LinuxFeatureInfoHwCap,
                    (ulong)Arm64HardwareCapabilities.LinuxFeatureInfoHwCap2,
                    (ulong)Arm64HardwareCapabilities.MacOsFeatureInfo,
                    0,
                    0);
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                return new FeatureInfo(
                    (ulong)X86HardwareCapabilities.FeatureInfo1Ecx,
                    (ulong)X86HardwareCapabilities.FeatureInfo1Edx,
                    (ulong)X86HardwareCapabilities.FeatureInfo7Ebx,
                    (ulong)X86HardwareCapabilities.FeatureInfo7Ecx,
                    (ulong)X86HardwareCapabilities.Xcr0InfoEax);
            }
            else
            {
                return new FeatureInfo(0, 0, 0, 0, 0);
            }
        }

        private byte GetMemoryManagerMode()
        {
            return (byte)_memoryMode;
        }

        private static uint GetOSPlatform()
        {
            uint osPlatform = 0u;

#pragma warning disable IDE0055 // Disable formatting
            osPlatform |= (OperatingSystem.IsFreeBSD() ? 1u : 0u) << 0;
            osPlatform |= (OperatingSystem.IsLinux()   ? 1u : 0u) << 1;
            osPlatform |= (OperatingSystem.IsMacOS()   ? 1u : 0u) << 2;
            osPlatform |= (OperatingSystem.IsWindows() ? 1u : 0u) << 3;
#pragma warning restore IDE0055

            return osPlatform;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1/*, Size = 86*/)]
        private struct OuterHeader
        {
            public ulong Magic;

            public uint CacheFileVersion;

            public bool Endianness;
            public FeatureInfo FeatureInfo;
            public byte MemoryManagerMode;
            public uint OSPlatform;
            public uint Architecture;

            public long UncompressedStreamSize;

            public Hash128 HeaderHash;

            public void SetHeaderHash()
            {
                Span<OuterHeader> spanHeader = MemoryMarshal.CreateSpan(ref this, 1);

                HeaderHash = XXHash128.ComputeHash(MemoryMarshal.AsBytes(spanHeader)[..(Unsafe.SizeOf<OuterHeader>() - Unsafe.SizeOf<Hash128>())]);
            }

            public bool IsHeaderValid()
            {
                Span<OuterHeader> spanHeader = MemoryMarshal.CreateSpan(ref this, 1);

                return XXHash128.ComputeHash(MemoryMarshal.AsBytes(spanHeader)[..(Unsafe.SizeOf<OuterHeader>() - Unsafe.SizeOf<Hash128>())]) == HeaderHash;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1/*, Size = 40*/)]
        private record struct FeatureInfo(ulong FeatureInfo0, ulong FeatureInfo1, ulong FeatureInfo2, ulong FeatureInfo3, ulong FeatureInfo4);

        [StructLayout(LayoutKind.Sequential, Pack = 1/*, Size = 128*/)]
        private struct InnerHeader
        {
            public ulong Magic;

            public int InfosLength;
            public long CodesLength;
            public int RelocsLength;
            public int UnwindInfosLength;

            public Hash128 InfosHash;
            public Hash128 CodesHash;
            public Hash128 RelocsHash;
            public Hash128 UnwindInfosHash;

            public Hash128 HeaderHash;

            public void SetHeaderHash()
            {
                Span<InnerHeader> spanHeader = MemoryMarshal.CreateSpan(ref this, 1);

                HeaderHash = XXHash128.ComputeHash(MemoryMarshal.AsBytes(spanHeader)[..(Unsafe.SizeOf<InnerHeader>() - Unsafe.SizeOf<Hash128>())]);
            }

            public bool IsHeaderValid()
            {
                Span<InnerHeader> spanHeader = MemoryMarshal.CreateSpan(ref this, 1);

                return XXHash128.ComputeHash(MemoryMarshal.AsBytes(spanHeader)[..(Unsafe.SizeOf<InnerHeader>() - Unsafe.SizeOf<Hash128>())]) == HeaderHash;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1/*, Size = 42*/)]
        private struct InfoEntry
        {
            public ulong Address;
            public ulong GuestSize;
            public Hash128 Hash;
            public bool HighCq;
            public bool Stubbed;
            public int CodeLength;
            public int RelocEntriesCount;
        }

        private void Enable()
        {
            State = PtcState.Enabled;
        }

        public void Continue()
        {
            if (State == PtcState.Enabled)
            {
                State = PtcState.Continuing;
            }
        }

        public void Close()
        {
            if (State == PtcState.Enabled ||
                State == PtcState.Continuing)
            {
                State = PtcState.Closing;
            }
        }

        public void Disable()
        {
            State = PtcState.Disabled;
        }

        private void Wait()
        {
            _waitEvent.WaitOne();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                Wait();
                _waitEvent.Dispose();

                DisposeCarriers();
            }
        }
    }
}
