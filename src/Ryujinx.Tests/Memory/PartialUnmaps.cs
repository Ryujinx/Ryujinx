using ARMeilleure.Common;
using ARMeilleure.Signal;
using ARMeilleure.Translation;
using NUnit.Framework;
using Ryujinx.Common.Memory.PartialUnmaps;
using Ryujinx.Cpu;
using Ryujinx.Cpu.Jit;
using Ryujinx.Memory;
using Ryujinx.Memory.Tracking;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Tests.Memory
{
    [TestFixture]
    internal class PartialUnmaps
    {
        private static Translator _translator;

        private static (MemoryBlock virt, MemoryBlock mirror, MemoryEhMeilleure exceptionHandler) GetVirtual(ulong asSize)
        {
            MemoryAllocationFlags asFlags = MemoryAllocationFlags.Reserve | MemoryAllocationFlags.ViewCompatible;

            var addressSpace = new MemoryBlock(asSize, asFlags);
            var addressSpaceMirror = new MemoryBlock(asSize, asFlags);

            var tracking = new MemoryTracking(new MockVirtualMemoryManager(asSize, 0x1000), 0x1000);
            var exceptionHandler = new MemoryEhMeilleure(addressSpace, addressSpaceMirror, tracking);

            return (addressSpace, addressSpaceMirror, exceptionHandler);
        }

        private static int CountThreads(ref PartialUnmapState state)
        {
            int count = 0;

            ref var ids = ref state.LocalCounts.ThreadIds;

            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static void EnsureTranslator()
        {
            // Create a translator, as one is needed to register the signal handler or emit methods.
            _translator ??= new Translator(
                new JitMemoryAllocator(),
                new MockMemoryManager(),
                AddressTable<ulong>.CreateForArm(true, ARMeilleure.Memory.MemoryManagerType.SoftwarePageTable));
        }

        [Test]
        // Memory aliasing tests fail on CI at the moment.
        [Platform(Exclude = "MacOsX")]
        public void PartialUnmap([Values] bool readOnly)
        {
            // Set up an address space to test partial unmapping.
            // Should register the signal handler to deal with this on Windows.
            ulong vaSize = 0x100000;

            // The first 0x100000 is mapped to start. It is replaced from the center with the 0x200000 mapping.
            var backing = new MemoryBlock(vaSize * 2, MemoryAllocationFlags.Mirrorable);

            (MemoryBlock unusedMainMemory, MemoryBlock memory, MemoryEhMeilleure exceptionHandler) = GetVirtual(vaSize * 2);

            EnsureTranslator();

            ref var state = ref PartialUnmapState.GetRef();

            Thread testThread = null;
            bool shouldAccess = true;

            try
            {
                // Globally reset the struct for handling partial unmap races.
                PartialUnmapState.Reset();
                bool error = false;

                // Create a large mapping.
                memory.MapView(backing, 0, 0, vaSize);

                if (readOnly)
                {
                    memory.Reprotect(0, vaSize, MemoryPermission.Read);
                }

                if (readOnly)
                {
                    // Write a value to the physical memory, then try to read it repeately from virtual.
                    // It should not change.
                    testThread = new Thread(() =>
                    {
                        int i = 12345;
                        backing.Write(vaSize - 0x1000, i);

                        while (shouldAccess)
                        {
                            if (memory.Read<int>(vaSize - 0x1000) != i)
                            {
                                error = true;
                                shouldAccess = false;
                            }
                        }
                    });
                }
                else
                {
                    // Repeatedly write and check the value on the last page of the mapping on another thread.
                    testThread = new Thread(() =>
                    {
                        int i = 0;
                        while (shouldAccess)
                        {
                            memory.Write(vaSize - 0x1000, i);
                            if (memory.Read<int>(vaSize - 0x1000) != i)
                            {
                                error = true;
                                shouldAccess = false;
                            }

                            i++;
                        }
                    });
                }

                testThread.Start();

                // Create a smaller mapping, covering the larger mapping.
                // Immediately try to write to the part of the larger mapping that did not change.
                // Do this a lot, with the smaller mapping gradually increasing in size. Should not crash, data should not be lost.

                ulong pageSize = 0x1000;
                int mappingExpandCount = (int)(vaSize / (pageSize * 2)) - 1;
                ulong vaCenter = vaSize / 2;

                for (int i = 1; i <= mappingExpandCount; i++)
                {
                    ulong start = vaCenter - (pageSize * (ulong)i);
                    ulong size = pageSize * (ulong)i * 2;

                    ulong startPa = start + vaSize;

                    memory.MapView(backing, startPa, start, size);
                }

                // On Windows, this should put unmap counts on the thread local map.
                if (OperatingSystem.IsWindows())
                {
                    // One thread should be present on the thread local map. Trimming should remove it.
                    Assert.AreEqual(1, CountThreads(ref state));
                }

                shouldAccess = false;
                testThread.Join();

                Assert.False(error);

                string test = null;

                try
                {
                    test.IndexOf('1');
                }
                catch (NullReferenceException)
                {
                    // This shouldn't freeze.
                }

                if (OperatingSystem.IsWindows())
                {
                    state.TrimThreads();

                    Assert.AreEqual(0, CountThreads(ref state));
                }

                /*
                * Use this to test invalid access. Can't put this in the test suite unfortunately as invalid access crashes the test process.
                * memory.Reprotect(vaSize - 0x1000, 0x1000, MemoryPermission.None);
                * //memory.UnmapView(backing, vaSize - 0x1000, 0x1000);
                * memory.Read<int>(vaSize - 0x1000);
                */
            }
            finally
            {
                // In case something failed, we want to ensure the test thread is dead before disposing of the memory.
                shouldAccess = false;
                testThread?.Join();

                exceptionHandler.Dispose();
                unusedMainMemory.Dispose();
                memory.Dispose();
                backing.Dispose();
            }
        }

        [Test]
        // Memory aliasing tests fail on CI at the moment.
        [Platform(Exclude = "MacOsX")]
        public unsafe void PartialUnmapNative()
        {

            // Set up an address space to test partial unmapping.
            // Should register the signal handler to deal with this on Windows.
            ulong vaSize = 0x100000;

            // The first 0x100000 is mapped to start. It is replaced from the center with the 0x200000 mapping.
            var backing = new MemoryBlock(vaSize * 2, MemoryAllocationFlags.Mirrorable);

            (MemoryBlock mainMemory, MemoryBlock unusedMirror, MemoryEhMeilleure exceptionHandler) = GetVirtual(vaSize * 2);

            EnsureTranslator();

            ref var state = ref PartialUnmapState.GetRef();

            // Create some state to be used for managing the native writing loop.
            int stateSize = Unsafe.SizeOf<NativeWriteLoopState>();
            var statePtr = Marshal.AllocHGlobal(stateSize);
            Unsafe.InitBlockUnaligned((void*)statePtr, 0, (uint)stateSize);

            ref NativeWriteLoopState writeLoopState = ref Unsafe.AsRef<NativeWriteLoopState>((void*)statePtr);
            writeLoopState.Running = 1;
            writeLoopState.Error = 0;

            try
            {
                // Globally reset the struct for handling partial unmap races.
                PartialUnmapState.Reset();

                // Create a large mapping.
                mainMemory.MapView(backing, 0, 0, vaSize);

                var writeFunc = TestMethods.GenerateDebugNativeWriteLoop();
                IntPtr writePtr = mainMemory.GetPointer(vaSize - 0x1000, 4);

                Thread testThread = new(() =>
                {
                    writeFunc(statePtr, writePtr);
                });

                testThread.Start();

                // Create a smaller mapping, covering the larger mapping.
                // Immediately try to write to the part of the larger mapping that did not change.
                // Do this a lot, with the smaller mapping gradually increasing in size. Should not crash, data should not be lost.

                ulong pageSize = 0x1000;
                int mappingExpandCount = (int)(vaSize / (pageSize * 2)) - 1;
                ulong vaCenter = vaSize / 2;

                for (int i = 1; i <= mappingExpandCount; i++)
                {
                    ulong start = vaCenter - (pageSize * (ulong)i);
                    ulong size = pageSize * (ulong)i * 2;

                    ulong startPa = start + vaSize;

                    mainMemory.MapView(backing, startPa, start, size);
                }

                writeLoopState.Running = 0;
                testThread.Join();

                Assert.False(writeLoopState.Error != 0);
            }
            finally
            {
                Marshal.FreeHGlobal(statePtr);

                exceptionHandler.Dispose();
                mainMemory.Dispose();
                unusedMirror.Dispose();
                backing.Dispose();
            }
        }

        [Test]
        // Only test in Windows, as this is only used on Windows and uses Windows APIs for trimming.
        [Platform("Win")]
        [SuppressMessage("Interoperability", "CA1416: Validate platform compatibility")]
        public void ThreadLocalMap()
        {
            PartialUnmapState.Reset();
            ref var state = ref PartialUnmapState.GetRef();

            bool running = true;
            var testThread = new Thread(() =>
            {
                PartialUnmapState.GetRef().RetryFromAccessViolation();
                while (running)
                {
                    Thread.Sleep(1);
                }
            });

            testThread.Start();
            Thread.Sleep(200);

            Assert.AreEqual(1, CountThreads(ref state));

            // Trimming should not remove the thread as it's still active.
            state.TrimThreads();
            Assert.AreEqual(1, CountThreads(ref state));

            running = false;

            testThread.Join();

            // Should trim now that it's inactive.
            state.TrimThreads();
            Assert.AreEqual(0, CountThreads(ref state));
        }

        [Test]
        // Only test in Windows, as this is only used on Windows and uses Windows APIs for trimming.
        [Platform("Win")]
        public unsafe void ThreadLocalMapNative()
        {
            EnsureTranslator();

            PartialUnmapState.Reset();

            ref var state = ref PartialUnmapState.GetRef();

            fixed (void* localMap = &state.LocalCounts)
            {
                var getOrReserve = TestMethods.GenerateDebugThreadLocalMapGetOrReserve((IntPtr)localMap);

                for (int i = 0; i < ThreadLocalMap<int>.MapSize; i++)
                {
                    // Should obtain the index matching the call #.
                    Assert.AreEqual(i, getOrReserve(i + 1, i));

                    // Check that this and all previously reserved thread IDs and struct contents are intact.
                    for (int j = 0; j <= i; j++)
                    {
                        Assert.AreEqual(j + 1, state.LocalCounts.ThreadIds[j]);
                        Assert.AreEqual(j, state.LocalCounts.Structs[j]);
                    }
                }

                // Trying to reserve again when the map is full should return -1.
                Assert.AreEqual(-1, getOrReserve(200, 0));

                for (int i = 0; i < ThreadLocalMap<int>.MapSize; i++)
                {
                    // Should obtain the index matching the call #, as it already exists.
                    Assert.AreEqual(i, getOrReserve(i + 1, -1));

                    // The struct should not be reset to -1.
                    Assert.AreEqual(i, state.LocalCounts.Structs[i]);
                }

                // Clear one of the ids as if it were freed.
                state.LocalCounts.ThreadIds[13] = 0;

                // GetOrReserve should now obtain and return 13.
                Assert.AreEqual(13, getOrReserve(300, 301));
                Assert.AreEqual(300, state.LocalCounts.ThreadIds[13]);
                Assert.AreEqual(301, state.LocalCounts.Structs[13]);
            }
        }

        [Test]
        public void NativeReaderWriterLock()
        {
            var rwLock = new NativeReaderWriterLock();
            var threads = new List<Thread>();

            int value = 0;

            bool running = true;
            bool error = false;
            int readersAllowed = 1;

            for (int i = 0; i < 5; i++)
            {
                var readThread = new Thread(() =>
                {
                    int count = 0;
                    while (running)
                    {
                        rwLock.AcquireReaderLock();

                        int originalValue = Volatile.Read(ref value);

                        count++;

                        // Spin a bit.
                        for (int i = 0; i < 100; i++)
                        {
                            if (Volatile.Read(ref readersAllowed) == 0)
                            {
                                error = true;
                                running = false;
                            }
                        }

                        // Should not change while the lock is held.
                        if (Volatile.Read(ref value) != originalValue)
                        {
                            error = true;
                            running = false;
                        }

                        rwLock.ReleaseReaderLock();
                    }
                });

                threads.Add(readThread);
            }

            for (int i = 0; i < 2; i++)
            {
                var writeThread = new Thread(() =>
                {
                    int count = 0;
                    while (running)
                    {
                        rwLock.AcquireReaderLock();
                        rwLock.UpgradeToWriterLock();

                        Thread.Sleep(2);
                        count++;

                        Interlocked.Exchange(ref readersAllowed, 0);

                        for (int i = 0; i < 10; i++)
                        {
                            Interlocked.Increment(ref value);
                        }

                        Interlocked.Exchange(ref readersAllowed, 1);

                        rwLock.DowngradeFromWriterLock();
                        rwLock.ReleaseReaderLock();

                        Thread.Sleep(1);
                    }
                });

                threads.Add(writeThread);
            }

            foreach (var thread in threads)
            {
                thread.Start();
            }

            Thread.Sleep(1000);

            running = false;

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Assert.False(error);
        }
    }
}
