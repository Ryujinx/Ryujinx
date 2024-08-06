using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using Ryujinx.Cpu;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.HLE.HOS.Services;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.HOS.Services.Am.AppletAE.AllSystemAppletProxiesService.SystemAppletProxy;
using Ryujinx.HLE.HOS.Services.Apm;
using Ryujinx.HLE.HOS.Services.Caps;
using Ryujinx.HLE.HOS.Services.Mii;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp.NfpManager;
using Ryujinx.HLE.HOS.Services.Nv;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostCtrl;
using Ryujinx.HLE.HOS.Services.Pcv.Bpc;
using Ryujinx.HLE.HOS.Services.Sdb.Pl;
using Ryujinx.HLE.HOS.Services.Settings;
using Ryujinx.HLE.HOS.Services.Sm;
using Ryujinx.HLE.HOS.Services.SurfaceFlinger;
using Ryujinx.HLE.HOS.Services.Time.Clock;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.HLE.Loaders.Processes;
using Ryujinx.Horizon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Ryujinx.HLE.HOS
{
    using TimeServiceManager = Services.Time.TimeManager;

    public class Horizon : IDisposable
    {
        internal const int FontSize = 0x1100000;
        internal const int IirsSize = 0x8000;
        internal const int TimeSize = 0x1000;
        internal const int AppletCaptureBufferSize = 0x384000;

        internal KernelContext KernelContext { get; }

        internal Switch Device { get; private set; }

        internal ITickSource TickSource { get; }

        internal SurfaceFlinger SurfaceFlinger { get; private set; }

        public SystemStateMgr State { get; private set; }

        internal PerformanceState PerformanceState { get; private set; }

        internal AppletStateMgr AppletState { get; private set; }

        internal List<NfpDevice> NfpDevices { get; private set; }

        internal SmRegistry SmRegistry { get; private set; }

        internal ServerBase SmServer { get; private set; }
        internal ServerBase BsdServer { get; private set; }
        internal ServerBase FsServer { get; private set; }
        internal ServerBase NvDrvServer { get; private set; }
        internal ServerBase TimeServer { get; private set; }
        internal ServerBase ViServer { get; private set; }
        internal ServerBase ViServerM { get; private set; }
        internal ServerBase ViServerS { get; private set; }
        internal ServerBase LdnServer { get; private set; }

        internal KSharedMemory FontSharedMem { get; private set; }
        internal KSharedMemory IirsSharedMem { get; private set; }

        internal KTransferMemory AppletCaptureBufferTransfer { get; private set; }

        internal SharedFontManager SharedFontManager { get; private set; }
        internal AccountManager AccountManager { get; private set; }
        internal ContentManager ContentManager { get; private set; }
        internal CaptureManager CaptureManager { get; private set; }

        internal KEvent VsyncEvent { get; private set; }

        internal KEvent DisplayResolutionChangeEvent { get; private set; }

        public KeySet KeySet => Device.FileSystem.KeySet;

        private bool _isDisposed;

        public bool EnablePtc { get; set; }

        public IntegrityCheckLevel FsIntegrityCheckLevel { get; set; }

        public int GlobalAccessLogMode { get; set; }


        internal NvHostSyncpt HostSyncpoint { get; private set; }

        internal LibHacHorizonManager LibHacHorizonManager { get; private set; }

        internal ServiceTable ServiceTable { get; private set; }

        public bool IsPaused { get; private set; }

        public Horizon(Switch device)
        {
            TickSource = new TickSource(KernelConstants.CounterFrequency);

            KernelContext = new KernelContext(
                TickSource,
                device,
                device.Memory,
                device.Configuration.MemoryConfiguration.ToKernelMemorySize(),
                device.Configuration.MemoryConfiguration.ToKernelMemoryArrange());

            Device = device;

            State = new SystemStateMgr();

            PerformanceState = new PerformanceState();

            NfpDevices = new List<NfpDevice>();

            // Note: This is not really correct, but with HLE of services, the only memory
            // region used that is used is Application, so we can use the other ones for anything.
            KMemoryRegionManager region = KernelContext.MemoryManager.MemoryRegions[(int)MemoryRegion.NvServices];

            ulong fontPa = region.Address;
            ulong iirsPa = region.Address + FontSize;
            ulong timePa = region.Address + FontSize + IirsSize;
            ulong appletCaptureBufferPa = region.Address + FontSize + IirsSize + TimeSize;

            KPageList fontPageList = new();
            KPageList iirsPageList = new();
            KPageList timePageList = new();
            KPageList appletCaptureBufferPageList = new();

            fontPageList.AddRange(fontPa, FontSize / KPageTableBase.PageSize);
            iirsPageList.AddRange(iirsPa, IirsSize / KPageTableBase.PageSize);
            timePageList.AddRange(timePa, TimeSize / KPageTableBase.PageSize);
            appletCaptureBufferPageList.AddRange(appletCaptureBufferPa, AppletCaptureBufferSize / KPageTableBase.PageSize);

            var fontStorage = new SharedMemoryStorage(KernelContext, fontPageList);
            var iirsStorage = new SharedMemoryStorage(KernelContext, iirsPageList);
            var timeStorage = new SharedMemoryStorage(KernelContext, timePageList);
            var appletCaptureBufferStorage = new SharedMemoryStorage(KernelContext, appletCaptureBufferPageList);

            FontSharedMem = new KSharedMemory(KernelContext, fontStorage, 0, 0, KMemoryPermission.Read);
            IirsSharedMem = new KSharedMemory(KernelContext, iirsStorage, 0, 0, KMemoryPermission.Read);

            KSharedMemory timeSharedMemory = new(KernelContext, timeStorage, 0, 0, KMemoryPermission.Read);

            TimeServiceManager.Instance.Initialize(device, this, timeSharedMemory, timeStorage, TimeSize);

            AppletCaptureBufferTransfer = new KTransferMemory(KernelContext, appletCaptureBufferStorage);

            AppletState = new AppletStateMgr(this);

            AppletState.SetFocus(true);

            VsyncEvent = new KEvent(KernelContext);

            DisplayResolutionChangeEvent = new KEvent(KernelContext);

            SharedFontManager = new SharedFontManager(device, fontStorage);
            AccountManager = device.Configuration.AccountManager;
            ContentManager = device.Configuration.ContentManager;
            CaptureManager = new CaptureManager(device);

            LibHacHorizonManager = device.Configuration.LibHacHorizonManager;

            // We hardcode a clock source id to avoid it changing between each start.
            // TODO: use set:sys (and get external clock source id from settings)
            // TODO: use "time!standard_steady_clock_rtc_update_interval_minutes" and implement a worker thread to be accurate.
            UInt128 clockSourceId = new(0x36a0328702ce8bc1, 0x1608eaba02333284);
            IRtcManager.GetExternalRtcValue(out ulong rtcValue);

            // We assume the rtc is system time.
            TimeSpanType systemTime = TimeSpanType.FromSeconds((long)rtcValue);

            // Configure and setup internal offset
            TimeSpanType internalOffset = TimeSpanType.FromSeconds(device.Configuration.SystemTimeOffset);

            TimeSpanType systemTimeOffset = new(systemTime.NanoSeconds + internalOffset.NanoSeconds);

            if (systemTime.IsDaylightSavingTime() && !systemTimeOffset.IsDaylightSavingTime())
            {
                internalOffset = internalOffset.AddSeconds(3600L);
            }
            else if (!systemTime.IsDaylightSavingTime() && systemTimeOffset.IsDaylightSavingTime())
            {
                internalOffset = internalOffset.AddSeconds(-3600L);
            }

            systemTime = new TimeSpanType(systemTime.NanoSeconds + internalOffset.NanoSeconds);

            // First init the standard steady clock
            TimeServiceManager.Instance.SetupStandardSteadyClock(TickSource, clockSourceId, TimeSpanType.Zero, TimeSpanType.Zero, TimeSpanType.Zero, false);
            TimeServiceManager.Instance.SetupStandardLocalSystemClock(TickSource, new SystemClockContext(), systemTime.ToSeconds());
            TimeServiceManager.Instance.StandardLocalSystemClock.GetClockContext(TickSource, out SystemClockContext localSytemClockContext);

            if (NxSettings.Settings.TryGetValue("time!standard_network_clock_sufficient_accuracy_minutes", out object standardNetworkClockSufficientAccuracyMinutes))
            {
                TimeSpanType standardNetworkClockSufficientAccuracy = new((int)standardNetworkClockSufficientAccuracyMinutes * 60000000000);

                // The network system clock needs a valid system clock, as such we setup this system clock using the local system clock.
                TimeServiceManager.Instance.SetupStandardNetworkSystemClock(localSytemClockContext, standardNetworkClockSufficientAccuracy);
            }

            TimeServiceManager.Instance.SetupStandardUserSystemClock(TickSource, true, localSytemClockContext.SteadyTimePoint);

            // FIXME: TimeZone should be init here but it's actually done in ContentManager

            TimeServiceManager.Instance.SetupEphemeralNetworkSystemClock();

            DatabaseImpl.Instance.InitializeDatabase(TickSource, LibHacHorizonManager.SdbClient);

            HostSyncpoint = new NvHostSyncpt(device);

            SurfaceFlinger = new SurfaceFlinger(device);
        }

        public void InitializeServices()
        {
            SmRegistry = new SmRegistry();
            SmServer = new ServerBase(KernelContext, "SmServer", () => new IUserInterface(KernelContext, SmRegistry));

            // Wait until SM server thread is done with initialization,
            // only then doing connections to SM is safe.
            SmServer.InitDone.WaitOne();

            BsdServer = new ServerBase(KernelContext, "BsdServer");
            FsServer = new ServerBase(KernelContext, "FsServer");
            NvDrvServer = new ServerBase(KernelContext, "NvservicesServer");
            TimeServer = new ServerBase(KernelContext, "TimeServer");
            ViServer = new ServerBase(KernelContext, "ViServerU");
            ViServerM = new ServerBase(KernelContext, "ViServerM");
            ViServerS = new ServerBase(KernelContext, "ViServerS");
            LdnServer = new ServerBase(KernelContext, "LdnServer");

            StartNewServices();
        }

        private void StartNewServices()
        {
            HorizonFsClient fsClient = new(this);

            ServiceTable = new ServiceTable();
            var services = ServiceTable.GetServices(new HorizonOptions
                (Device.Configuration.IgnoreMissingServices,
                LibHacHorizonManager.BcatClient,
                fsClient,
                AccountManager,
                Device.AudioDeviceDriver,
                TickSource));

            foreach (var service in services)
            {
                const ProcessCreationFlags Flags =
                    ProcessCreationFlags.EnableAslr |
                    ProcessCreationFlags.AddressSpace64Bit |
                    ProcessCreationFlags.Is64Bit |
                    ProcessCreationFlags.PoolPartitionSystem;

                ProcessCreationInfo creationInfo = new("Service", 1, 0, 0x8000000, 1, Flags, 0, 0);

                uint[] defaultCapabilities = {
                    0x030363F7,
                    0x1FFFFFCF,
                    0x207FFFEF,
                    0x47E0060F,
                    0x0048BFFF,
                    0x01007FFF,
                };

                // TODO:
                // - Pass enough information (capabilities, process creation info, etc) on ServiceEntry for proper initialization.
                // - Have the ThreadStart function take the syscall, address space and thread context parameters instead of passing them here.
                KernelStatic.StartInitialProcess(KernelContext, creationInfo, defaultCapabilities, 44, () =>
                {
                    service.Start(KernelContext.Syscall, KernelStatic.GetCurrentProcess().CpuMemory, KernelStatic.GetCurrentThread().ThreadContext);
                });
            }
        }

        public bool LoadKip(string kipPath)
        {
            using var kipFile = new SharedRef<IStorage>(new LocalStorage(kipPath, FileAccess.Read));

            return ProcessLoaderHelper.LoadKip(KernelContext, new KipExecutable(in kipFile));
        }

        public void ChangeDockedModeState(bool newState)
        {
            if (newState != State.DockedMode)
            {
                State.DockedMode = newState;
                PerformanceState.PerformanceMode = State.DockedMode ? PerformanceMode.Boost : PerformanceMode.Default;

                AppletState.Messages.Enqueue(AppletMessage.OperationModeChanged);
                AppletState.Messages.Enqueue(AppletMessage.PerformanceModeChanged);
                AppletState.MessageEvent.ReadableEvent.Signal();

                SignalDisplayResolutionChange();

                Device.Configuration.RefreshInputConfig?.Invoke();
            }
        }

        public void ReturnFocus()
        {
            AppletState.SetFocus(true);
        }

        public void SimulateWakeUpMessage()
        {
            AppletState.Messages.Enqueue(AppletMessage.Resume);
            AppletState.MessageEvent.ReadableEvent.Signal();
        }

        public void ScanAmiibo(int nfpDeviceId, string amiiboId, bool useRandomUuid)
        {
            if (NfpDevices[nfpDeviceId].State == NfpDeviceState.SearchingForTag)
            {
                NfpDevices[nfpDeviceId].State = NfpDeviceState.TagFound;
                NfpDevices[nfpDeviceId].AmiiboId = amiiboId;
                NfpDevices[nfpDeviceId].UseRandomUuid = useRandomUuid;
            }
        }

        public bool SearchingForAmiibo(out int nfpDeviceId)
        {
            nfpDeviceId = default;

            for (int i = 0; i < NfpDevices.Count; i++)
            {
                if (NfpDevices[i].State == NfpDeviceState.SearchingForTag)
                {
                    nfpDeviceId = i;

                    return true;
                }
            }

            return false;
        }

        public void SignalDisplayResolutionChange()
        {
            DisplayResolutionChangeEvent.ReadableEvent.Signal();
        }

        public void SignalVsync()
        {
            VsyncEvent.ReadableEvent.Signal();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                _isDisposed = true;

                // "Soft" stops AudioRenderer and AudioManager to avoid some sound between resume and stop.
                if (IsPaused)
                {
                    TogglePauseEmulation(false);
                }

                KProcess terminationProcess = new(KernelContext);
                KThread terminationThread = new(KernelContext);

                terminationThread.Initialize(0, 0, 0, 3, 0, terminationProcess, ThreadType.Kernel, () =>
                {
                    // Force all threads to exit.
                    lock (KernelContext.Processes)
                    {
                        // Terminate application.
                        foreach (KProcess process in KernelContext.Processes.Values.Where(x => x.IsApplication))
                        {
                            process.Terminate();
                            process.DecrementReferenceCount();
                        }

                        // The application existed, now surface flinger can exit too.
                        SurfaceFlinger.Dispose();

                        // Terminate HLE services (must be done after the application is already terminated,
                        // otherwise the application will receive errors due to service termination).
                        foreach (KProcess process in KernelContext.Processes.Values.Where(x => !x.IsApplication))
                        {
                            process.Terminate();
                            process.DecrementReferenceCount();
                        }

                        KernelContext.Processes.Clear();
                    }

                    // Exit ourself now!
                    KernelStatic.GetCurrentThread().Exit();
                });

                terminationThread.Start();

                // Wait until the thread is actually started.
                while (terminationThread.HostThread.ThreadState == ThreadState.Unstarted)
                {
                    Thread.Sleep(10);
                }

                // Wait until the termination thread is done terminating all the other threads.
                terminationThread.HostThread.Join();

                // Destroy nvservices channels as KThread could be waiting on some user events.
                // This is safe as KThread that are likely to call ioctls are going to be terminated by the post handler hook on the SVC facade.
                INvDrvServices.Destroy();

                if (LibHacHorizonManager.ApplicationClient != null)
                {
                    LibHacHorizonManager.PmClient.Fs.UnregisterProgram(LibHacHorizonManager.ApplicationClient.Os.GetCurrentProcessId().Value).ThrowIfFailure();
                }

                KernelContext.Dispose();
            }
        }

        public void TogglePauseEmulation(bool pause)
        {
            lock (KernelContext.Processes)
            {
                foreach (KProcess process in KernelContext.Processes.Values)
                {
                    if (process.IsApplication)
                    {
                        // Only game process should be paused.
                        process.SetActivity(pause);
                    }
                }

                if (pause && !IsPaused)
                {
                    Device.AudioDeviceDriver.GetPauseEvent().Reset();
                    TickSource.Suspend();
                }
                else if (!pause && IsPaused)
                {
                    Device.AudioDeviceDriver.GetPauseEvent().Set();
                    TickSource.Resume();
                }
            }
            IsPaused = pause;
        }
    }
}
