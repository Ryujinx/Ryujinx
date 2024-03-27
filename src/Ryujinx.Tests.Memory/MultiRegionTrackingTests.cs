using Ryujinx.Memory;
using Ryujinx.Memory.Tracking;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ryujinx.Tests.Memory
{
    public class MultiRegionTrackingTests : IDisposable
    {
        private const ulong MemorySize = 0x8000;
        private const int PageSize = 4096;

        private readonly MemoryBlock _memoryBlock;
        private readonly MemoryTracking _tracking;
        private readonly MockVirtualMemoryManager _memoryManager;

        public MultiRegionTrackingTests()
        {
            _memoryBlock = new MemoryBlock(MemorySize);
            _memoryManager = new MockVirtualMemoryManager(MemorySize, PageSize);
            _tracking = new MemoryTracking(_memoryManager, PageSize);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            _memoryBlock.Dispose();
        }

        private IMultiRegionHandle GetGranular(bool smart, ulong address, ulong size, ulong granularity)
        {
            return smart ?
                _tracking.BeginSmartGranularTracking(address, size, granularity, 0) :
                (IMultiRegionHandle)_tracking.BeginGranularTracking(address, size, null, granularity, 0);
        }

        private static void RandomOrder(Random random, List<int> indices, Action<int> action)
        {
            List<int> choices = indices.ToList();

            while (choices.Count > 0)
            {
                int choice = random.Next(choices.Count);
                action(choices[choice]);
                choices.RemoveAt(choice);
            }
        }

        private static int ExpectQueryInOrder(IMultiRegionHandle handle, ulong startAddress, ulong size, Func<ulong, bool> addressPredicate)
        {
            int regionCount = 0;
            ulong lastAddress = startAddress;

            handle.QueryModified(startAddress, size, (address, range) =>
            {
                Assert.True(addressPredicate(address)); // Written pages must be even.
                Assert.True(address >= lastAddress); // Must be signalled in ascending order, regardless of write order.
                lastAddress = address;
                regionCount++;
            });

            return regionCount;
        }

        private static int ExpectQueryInOrder(IMultiRegionHandle handle, ulong startAddress, ulong size, Func<ulong, bool> addressPredicate, int sequenceNumber)
        {
            int regionCount = 0;
            ulong lastAddress = startAddress;

            handle.QueryModified(startAddress, size, (address, range) =>
            {
                Assert.True(addressPredicate(address)); // Written pages must be even.
                Assert.True(address >= lastAddress); // Must be signalled in ascending order, regardless of write order.
                lastAddress = address;
                regionCount++;
            }, sequenceNumber);

            return regionCount;
        }

        private static void PreparePages(IMultiRegionHandle handle, int pageCount, ulong address = 0)
        {
            Random random = new();

            // Make sure the list has minimum granularity (smart region changes granularity based on requested ranges)
            RandomOrder(random, Enumerable.Range(0, pageCount).ToList(), (i) =>
            {
                ulong resultAddress = ulong.MaxValue;
                handle.QueryModified((ulong)i * PageSize + address, PageSize, (address, range) =>
                {
                    resultAddress = address;
                });
                Assert.Equal(resultAddress, (ulong)i * PageSize + address);
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DirtyRegionOrdering(bool smart)
        {
            const int PageCount = 32;
            IMultiRegionHandle handle = GetGranular(smart, 0, PageSize * PageCount, PageSize);

            Random random = new();

            PreparePages(handle, PageCount);

            IEnumerable<int> halfRange = Enumerable.Range(0, PageCount / 2);
            List<int> odd = halfRange.Select(x => x * 2 + 1).ToList();
            List<int> even = halfRange.Select(x => x * 2).ToList();

            // Write to all the odd pages.
            RandomOrder(random, odd, (i) =>
            {
                _tracking.VirtualMemoryEvent((ulong)i * PageSize, PageSize, true);
            });

            int oddRegionCount = ExpectQueryInOrder(handle, 0, PageSize * PageCount, (address) => (address / PageSize) % 2 == 1);

            Assert.Equal(PageCount / 2, oddRegionCount); // Must have written to all odd pages.

            // Write to all the even pages.
            RandomOrder(random, even, (i) =>
            {
                _tracking.VirtualMemoryEvent((ulong)i * PageSize, PageSize, true);
            });

            int evenRegionCount = ExpectQueryInOrder(handle, 0, PageSize * PageCount, (address) => (address / PageSize) % 2 == 0);

            Assert.Equal(PageCount / 2, evenRegionCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SequenceNumber(bool smart)
        {
            // The sequence number can be used to ignore dirty flags, and defer their consumption until later.
            // If a user consumes a dirty flag with sequence number 1, then there is a write to the protected region,
            // the dirty flag will not be acknowledged until the sequence number is 2.

            // This is useful for situations where we know that the data was complete when the sequence number was set.
            // ...essentially, when that data can only be updated on a future sequence number.

            const int PageCount = 32;
            IMultiRegionHandle handle = GetGranular(smart, 0, PageSize * PageCount, PageSize);

            PreparePages(handle, PageCount);

            Random random = new();

            IEnumerable<int> halfRange = Enumerable.Range(0, PageCount / 2);
            List<int> odd = halfRange.Select(x => x * 2 + 1).ToList();
            List<int> even = halfRange.Select(x => x * 2).ToList();

            // Write to all the odd pages.
            RandomOrder(random, odd, (i) =>
            {
                _tracking.VirtualMemoryEvent((ulong)i * PageSize, PageSize, true);
            });

            int oddRegionCount = 0;

            // Track with sequence number 1. Future dirty flags should only be consumed with sequence number != 1.
            // Only track the odd pages, so the even ones don't have their sequence number set.

            foreach (int index in odd)
            {
                handle.QueryModified((ulong)index * PageSize, PageSize, (address, range) =>
                {
                    oddRegionCount++;
                }, 1);
            }

            Assert.Equal(PageCount / 2, oddRegionCount); // Must have written to all odd pages.

            // Write to all pages.

            _tracking.VirtualMemoryEvent(0, PageSize * PageCount, true);

            // Only the even regions should be reported for sequence number 1.

            int evenRegionCount = ExpectQueryInOrder(handle, 0, PageSize * PageCount, (address) => (address / PageSize) % 2 == 0, 1);

            Assert.Equal(PageCount / 2, evenRegionCount); // Must have written to all even pages.

            oddRegionCount = 0;

            handle.QueryModified(0, PageSize * PageCount, (address, range) => { oddRegionCount++; }, 1);

            Assert.Equal(0, oddRegionCount); // Sequence number has not changed, so found no dirty subregions.

            // With sequence number 2, all all pages should be reported as modified.

            oddRegionCount = ExpectQueryInOrder(handle, 0, PageSize * PageCount, (address) => (address / PageSize) % 2 == 1, 2);

            Assert.Equal(PageCount / 2, oddRegionCount); // Must have written to all odd pages.
        }

        [Fact]
        public void SmartRegionTracking()
        {
            // Smart multi region handles dynamically change their tracking granularity based on QueryMemory calls.
            // This can save on reprotects on larger resources.

            const int PageCount = 32;
            IMultiRegionHandle handle = GetGranular(true, 0, PageSize * PageCount, PageSize);

            // Query some large regions to prep the subdivision of the tracking region.

            int[] regionSizes = { 6, 4, 3, 2, 6, 1 };
            ulong address = 0;

            for (int i = 0; i < regionSizes.Length; i++)
            {
                int region = regionSizes[i];
                handle.QueryModified(address, (ulong)(PageSize * region), (address, size) => { });

                // There should be a gap between regions,
                // So that they don't combine and we can see the full effects.
                address += (ulong)(PageSize * (region + 1));
            }

            // Clear modified.
            handle.QueryModified((address, size) => { });

            // Trigger each region with a 1 byte write.
            address = 0;

            for (int i = 0; i < regionSizes.Length; i++)
            {
                int region = regionSizes[i];
                _tracking.VirtualMemoryEvent(address, 1, true);
                address += (ulong)(PageSize * (region + 1));
            }

            int regionInd = 0;
            ulong expectedAddress = 0;

            // Expect each region to trigger in its entirety, in address ascending order.
            handle.QueryModified((address, size) =>
            {
                int region = regionSizes[regionInd++];

                Assert.Equal(expectedAddress, address);
                Assert.Equal((ulong)(PageSize * region), size);

                expectedAddress += (ulong)(PageSize * (region + 1));
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DisposeMultiHandles(bool smart)
        {
            // Create and initialize two overlapping Multi Region Handles, with PageSize granularity.
            const int PageCount = 32;
            const int OverlapStart = 16;

            Assert.Equal(0, _tracking.GetRegionCount());

            IMultiRegionHandle handleLow = GetGranular(smart, 0, PageSize * PageCount, PageSize);
            PreparePages(handleLow, PageCount);

            Assert.Equal(PageCount, _tracking.GetRegionCount());

            IMultiRegionHandle handleHigh = GetGranular(smart, PageSize * OverlapStart, PageSize * PageCount, PageSize);
            PreparePages(handleHigh, PageCount, PageSize * OverlapStart);

            // Combined pages (and assuming overlapStart <= pageCount) should be pageCount after overlapStart.
            int totalPages = OverlapStart + PageCount;

            Assert.Equal(totalPages, _tracking.GetRegionCount());

            handleLow.Dispose(); // After disposing one, the pages for the other remain.

            Assert.Equal(PageCount, _tracking.GetRegionCount());

            handleHigh.Dispose(); // After disposing the other, there are no pages left.

            Assert.Equal(0, _tracking.GetRegionCount());
        }

        [Fact]
        public void InheritHandles()
        {
            // Test merging the following into a granular region handle:
            // - 3x gap (creates new granular handles)
            // - 3x from multiregion: not dirty, dirty and with action
            // - 2x gap
            // - 3x single page: not dirty, dirty and with action
            // - 3x two page: not dirty, dirty and with action (handle is not reused, but its state is copied to the granular handles)
            // - 1x gap
            // For a total of 18 pages.

            bool[] actionsTriggered = new bool[3];

            MultiRegionHandle granular = _tracking.BeginGranularTracking(PageSize * 3, PageSize * 3, null, PageSize, 0);
            PreparePages(granular, 3, PageSize * 3);

            // Write to the second handle in the multiregion.
            _tracking.VirtualMemoryEvent(PageSize * 4, PageSize, true);

            // Add an action to the third handle in the multiregion.
            granular.RegisterAction(PageSize * 5, PageSize, (_, _) => { actionsTriggered[0] = true; });

            RegionHandle[] singlePages = new RegionHandle[3];

            for (int i = 0; i < 3; i++)
            {
                singlePages[i] = _tracking.BeginTracking(PageSize * (8 + (ulong)i), PageSize, 0);
                singlePages[i].Reprotect();
            }

            // Write to the second handle.
            _tracking.VirtualMemoryEvent(PageSize * 9, PageSize, true);

            // Add an action to the third handle.
            singlePages[2].RegisterAction((_, _) => { actionsTriggered[1] = true; });

            RegionHandle[] doublePages = new RegionHandle[3];

            for (int i = 0; i < 3; i++)
            {
                doublePages[i] = _tracking.BeginTracking(PageSize * (11 + (ulong)i * 2), PageSize * 2, 0);
                doublePages[i].Reprotect();
            }

            // Write to the second handle.
            _tracking.VirtualMemoryEvent(PageSize * 13, PageSize * 2, true);

            // Add an action to the third handle.
            doublePages[2].RegisterAction((_, _) => { actionsTriggered[2] = true; });

            // Finally, create a granular handle that inherits all these handles.

            IEnumerable<IRegionHandle>[] handleGroups = {
                granular.GetHandles(),
                singlePages,
                doublePages,
            };

            MultiRegionHandle combined = _tracking.BeginGranularTracking(0, PageSize * 18, handleGroups.SelectMany((handles) => handles), PageSize, 0);

            bool[] expectedDirty = {
                true, true, true, // Gap.
                false, true, false, // Multi-region.
                true, true, // Gap.
                false, true, false, // Individual handles.
                false, false, true, true, false, false, // Double size handles.
                true, // Gap.
            };

            for (int i = 0; i < 18; i++)
            {
                bool modified = false;
                combined.QueryModified(PageSize * (ulong)i, PageSize, (_, _) => { modified = true; });

                Assert.Equal(expectedDirty[i], modified);
            }

            Assert.Equal(new bool[3], actionsTriggered);

            _tracking.VirtualMemoryEvent(PageSize * 5, PageSize, false);
            Assert.True(actionsTriggered[0]);

            _tracking.VirtualMemoryEvent(PageSize * 10, PageSize, false);
            Assert.True(actionsTriggered[1]);

            _tracking.VirtualMemoryEvent(PageSize * 15, PageSize, false);
            Assert.True(actionsTriggered[2]);

            // The double page handles should be disposed, as they were split into granular handles.
            foreach (RegionHandle doublePage in doublePages)
            {
                // These should have been disposed.
                bool throws = false;

                try
                {
                    doublePage.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    throws = true;
                }

                Assert.True(throws);
            }

            IEnumerable<IRegionHandle> combinedHandles = combined.GetHandles();

            Assert.Equal(handleGroups[0].ElementAt(0), combinedHandles.ElementAt(3));
            Assert.Equal(handleGroups[0].ElementAt(1), combinedHandles.ElementAt(4));
            Assert.Equal(handleGroups[0].ElementAt(2), combinedHandles.ElementAt(5));

            Assert.Equal(singlePages[0], combinedHandles.ElementAt(8));
            Assert.Equal(singlePages[1], combinedHandles.ElementAt(9));
            Assert.Equal(singlePages[2], combinedHandles.ElementAt(10));
        }

        [Fact]
        public void PreciseAction()
        {
            bool actionTriggered = false;

            MultiRegionHandle granular = _tracking.BeginGranularTracking(PageSize * 3, PageSize * 3, null, PageSize, 0);
            PreparePages(granular, 3, PageSize * 3);

            // Add a precise action to the second and third handle in the multiregion.
            granular.RegisterPreciseAction(PageSize * 4, PageSize * 2, (_, _, _) => { actionTriggered = true; return true; });

            // Precise write to first handle in the multiregion.
            _tracking.VirtualMemoryEvent(PageSize * 3, PageSize, true, precise: true);
            Assert.False(actionTriggered); // Action not triggered.

            bool firstPageModified = false;
            granular.QueryModified(PageSize * 3, PageSize, (_, _) => { firstPageModified = true; });
            Assert.True(firstPageModified); // First page is modified.

            // Precise write to all handles in the multiregion.
            _tracking.VirtualMemoryEvent(PageSize * 3, PageSize * 3, true, precise: true);

            bool[] pagesModified = new bool[3];

            for (int i = 3; i < 6; i++)
            {
                int index = i - 3;
                granular.QueryModified(PageSize * (ulong)i, PageSize, (_, _) => { pagesModified[index] = true; });
            }

            Assert.True(actionTriggered); // Action triggered.

            // Precise writes are ignored on two later handles due to the action returning true.
            Assert.Equal(new[] { true, false, false }, pagesModified);
        }
    }
}
