using LibHac.Account;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.Loader;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Arp;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using ApplicationId = LibHac.Ncm.ApplicationId;

namespace Ryujinx.HLE.Loaders.Processes
{
    static class ProcessLoaderHelper
    {
        // NOTE: If you want to change this value make sure to increment the InternalVersion of Ptc and PtcProfiler.
        //       You also need to add a new migration path and adjust the existing ones.
        // TODO: Remove this workaround when ASLR is implemented.
        private const ulong CodeStartOffset = 0x500000UL;

        public static LibHac.Result RegisterProgramMapInfo(Switch device, IFileSystem partitionFileSystem)
        {
            ulong applicationId = 0;
            int programCount = 0;

            Span<bool> hasIndex = stackalloc bool[0x10];

            foreach (DirectoryEntryEx fileEntry in partitionFileSystem.EnumerateEntries("/", "*.nca"))
            {
                Nca nca = partitionFileSystem.GetNca(device.FileSystem.KeySet, fileEntry.FullPath);

                if (!nca.IsProgram())
                {
                    continue;
                }

                ulong currentMainProgramId = nca.GetProgramIdBase();

                if (applicationId == 0 && currentMainProgramId != 0)
                {
                    applicationId = currentMainProgramId;
                }

                if (applicationId != currentMainProgramId)
                {
                    // Currently there aren't any known multi-application game cards containing multi-program applications,
                    // so because multi-application game cards are the only way we could run into multiple applications
                    // we'll just return that there's a single program.
                    programCount = 1;

                    break;
                }

                hasIndex[nca.GetProgramIndex()] = true;
            }

            if (programCount == 0)
            {
                for (int i = 0; i < hasIndex.Length && hasIndex[i]; i++)
                {
                    programCount++;
                }
            }

            if (programCount <= 0)
            {
                return LibHac.Result.Success;
            }

            Span<ProgramIndexMapInfo> mapInfo = stackalloc ProgramIndexMapInfo[0x10];

            for (int i = 0; i < programCount; i++)
            {
                mapInfo[i].ProgramId = new ProgramId(applicationId + (uint)i);
                mapInfo[i].MainProgramId = new ApplicationId(applicationId);
                mapInfo[i].ProgramIndex = (byte)i;
            }

            return device.System.LibHacHorizonManager.NsClient.Fs.RegisterProgramIndexMapInfo(mapInfo[..programCount]);
        }

        public static LibHac.Result EnsureSaveData(Switch device, ApplicationId applicationId, BlitStruct<ApplicationControlProperty> applicationControlProperty)
        {
            Logger.Info?.Print(LogClass.Application, "Ensuring required savedata exists.");

            ref ApplicationControlProperty control = ref applicationControlProperty.Value;

            if (LibHac.Common.Utilities.IsZeros(applicationControlProperty.ByteSpan))
            {
                // If the current application doesn't have a loaded control property, create a dummy one and set the savedata sizes so a user savedata will be created.
                control = ref new BlitStruct<ApplicationControlProperty>(1).Value;

                // The set sizes don't actually matter as long as they're non-zero because we use directory savedata.
                control.UserAccountSaveDataSize = 0x4000;
                control.UserAccountSaveDataJournalSize = 0x4000;
                control.SaveDataOwnerId = applicationId.Value;

                Logger.Warning?.Print(LogClass.Application, "No control file was found for this game. Using a dummy one instead. This may cause inaccuracies in some games.");
            }

            LibHac.Result resultCode = device.System.LibHacHorizonManager.RyujinxClient.Fs.EnsureApplicationCacheStorage(out _, out _, applicationId, in control);
            if (resultCode.IsFailure())
            {
                Logger.Error?.Print(LogClass.Application, $"Error calling EnsureApplicationCacheStorage. Result code {resultCode.ToStringWithName()}");

                return resultCode;
            }

            Uid userId = device.System.AccountManager.LastOpenedUser.UserId.ToLibHacUid();

            resultCode = device.System.LibHacHorizonManager.RyujinxClient.Fs.EnsureApplicationSaveData(out _, applicationId, in control, in userId);
            if (resultCode.IsFailure())
            {
                Logger.Error?.Print(LogClass.Application, $"Error calling EnsureApplicationSaveData. Result code {resultCode.ToStringWithName()}");
            }

            return resultCode;
        }

        public static bool LoadKip(KernelContext context, KipExecutable kip)
        {
            uint endOffset = kip.DataOffset + (uint)kip.Data.Length;

            if (kip.BssSize != 0)
            {
                endOffset = kip.BssOffset + kip.BssSize;
            }

            uint codeSize = BitUtils.AlignUp<uint>(kip.TextOffset + endOffset, KPageTableBase.PageSize);
            int codePagesCount = (int)(codeSize / KPageTableBase.PageSize);
            ulong codeBaseAddress = kip.Is64BitAddressSpace ? 0x8000000UL : 0x200000UL;
            ulong codeAddress = codeBaseAddress + kip.TextOffset;

            ProcessCreationFlags flags = 0;

            if (ProcessConst.AslrEnabled)
            {
                // TODO: Randomization.

                flags |= ProcessCreationFlags.EnableAslr;
            }

            if (kip.Is64BitAddressSpace)
            {
                flags |= ProcessCreationFlags.AddressSpace64Bit;
            }

            if (kip.Is64Bit)
            {
                flags |= ProcessCreationFlags.Is64Bit;
            }

            ProcessCreationInfo creationInfo = new(kip.Name, kip.Version, kip.ProgramId, codeAddress, codePagesCount, flags, 0, 0);
            MemoryRegion memoryRegion = kip.UsesSecureMemory ? MemoryRegion.Service : MemoryRegion.Application;
            KMemoryRegionManager region = context.MemoryManager.MemoryRegions[(int)memoryRegion];

            Result result = region.AllocatePages(out KPageList pageList, (ulong)codePagesCount);
            if (result != Result.Success)
            {
                Logger.Error?.Print(LogClass.Loader, $"Process initialization returned error \"{result}\".");

                return false;
            }

            KProcess process = new(context);

            var processContextFactory = new ArmProcessContextFactory(
                context.Device.System.TickSource,
                context.Device.Gpu,
                string.Empty,
                string.Empty,
                false,
                codeAddress,
                codeSize);

            result = process.InitializeKip(creationInfo, kip.Capabilities, pageList, context.ResourceLimit, memoryRegion, processContextFactory);
            if (result != Result.Success)
            {
                Logger.Error?.Print(LogClass.Loader, $"Process initialization returned error \"{result}\".");

                return false;
            }

            result = LoadIntoMemory(process, kip, codeBaseAddress);
            if (result != Result.Success)
            {
                Logger.Error?.Print(LogClass.Loader, $"Process initialization returned error \"{result}\".");

                return false;
            }

            process.DefaultCpuCore = kip.IdealCoreId;

            result = process.Start(kip.Priority, (ulong)kip.StackSize);
            if (result != Result.Success)
            {
                Logger.Error?.Print(LogClass.Loader, $"Process start returned error \"{result}\".");

                return false;
            }

            context.Processes.TryAdd(process.Pid, process);

            return true;
        }

        public static ProcessResult LoadNsos(
            Switch device,
            KernelContext context,
            MetaLoader metaLoader,
            BlitStruct<ApplicationControlProperty> applicationControlProperties,
            bool diskCacheEnabled,
            bool allowCodeMemoryForJit,
            string name,
            ulong programId,
            byte programIndex,
            byte[] arguments = null,
            params IExecutable[] executables)
        {
            context.Device.System.ServiceTable.WaitServicesReady();

            LibHac.Result resultCode = metaLoader.GetNpdm(out var npdm);

            if (resultCode.IsFailure())
            {
                Logger.Error?.Print(LogClass.Loader, $"Process initialization failed getting npdm. Result Code {resultCode.ToStringWithName()}");

                return ProcessResult.Failed;
            }

            ref readonly var meta = ref npdm.Meta;

            ulong argsStart = 0;
            uint argsSize = 0;
            ulong codeStart = ((meta.Flags & 1) != 0 ? 0x8000000UL : 0x200000UL) + CodeStartOffset;
            uint codeSize = 0;

            var buildIds = executables.Select(e => (e switch
            {
                NsoExecutable nso => Convert.ToHexString(nso.BuildId.ItemsRo.ToArray()),
                NroExecutable nro => Convert.ToHexString(nro.Header.BuildId),
                _ => "",
            }).ToUpper());

            ulong[] nsoBase = new ulong[executables.Length];

            for (int index = 0; index < executables.Length; index++)
            {
                IExecutable nso = executables[index];

                uint textEnd = nso.TextOffset + (uint)nso.Text.Length;
                uint roEnd = nso.RoOffset + (uint)nso.Ro.Length;
                uint dataEnd = nso.DataOffset + (uint)nso.Data.Length + nso.BssSize;

                uint nsoSize = textEnd;

                if (nsoSize < roEnd)
                {
                    nsoSize = roEnd;
                }

                if (nsoSize < dataEnd)
                {
                    nsoSize = dataEnd;
                }

                nsoSize = BitUtils.AlignUp<uint>(nsoSize, KPageTableBase.PageSize);

                nsoBase[index] = codeStart + codeSize;

                codeSize += nsoSize;

                if (arguments != null && argsSize == 0)
                {
                    argsStart = codeSize;

                    argsSize = (uint)BitUtils.AlignDown(arguments.Length * 2 + ProcessConst.NsoArgsTotalSize - 1, KPageTableBase.PageSize);

                    codeSize += argsSize;
                }
            }

            int codePagesCount = (int)(codeSize / KPageTableBase.PageSize);
            int personalMmHeapPagesCount = (int)(meta.SystemResourceSize / KPageTableBase.PageSize);

            ProcessCreationInfo creationInfo = new(
                name,
                (int)meta.Version,
                programId,
                codeStart,
                codePagesCount,
                (ProcessCreationFlags)meta.Flags | ProcessCreationFlags.IsApplication,
                0,
                personalMmHeapPagesCount);

            context.Device.System.LibHacHorizonManager.InitializeApplicationClient(new ProgramId(programId), in npdm);

            Result result;

            KResourceLimit resourceLimit = new(context);

            long applicationRgSize = (long)context.MemoryManager.MemoryRegions[(int)MemoryRegion.Application].Size;

            result = resourceLimit.SetLimitValue(LimitableResource.Memory, applicationRgSize);

            if (result.IsSuccess)
            {
                result = resourceLimit.SetLimitValue(LimitableResource.Thread, 608);
            }

            if (result.IsSuccess)
            {
                result = resourceLimit.SetLimitValue(LimitableResource.Event, 700);
            }

            if (result.IsSuccess)
            {
                result = resourceLimit.SetLimitValue(LimitableResource.TransferMemory, 128);
            }

            if (result.IsSuccess)
            {
                result = resourceLimit.SetLimitValue(LimitableResource.Session, 894);
            }

            if (result != Result.Success)
            {
                Logger.Error?.Print(LogClass.Loader, "Process initialization failed setting resource limit values.");

                return ProcessResult.Failed;
            }

            KProcess process = new(context, allowCodeMemoryForJit);

            // NOTE: This field doesn't exists one firmware pre-5.0.0, a workaround have to be found.
            MemoryRegion memoryRegion = (MemoryRegion)(npdm.Acid.Flags >> 2 & 0xf);
            if (memoryRegion > MemoryRegion.NvServices)
            {
                Logger.Error?.Print(LogClass.Loader, "Process initialization failed due to invalid ACID flags.");

                return ProcessResult.Failed;
            }

            string displayVersion;

            if (metaLoader.GetProgramId() > 0x0100000000007FFF)
            {
                displayVersion = applicationControlProperties.Value.DisplayVersionString.ToString();
            }
            else
            {
                displayVersion = device.System.ContentManager.GetCurrentFirmwareVersion()?.VersionString ?? string.Empty;
            }

            var processContextFactory = new ArmProcessContextFactory(
                context.Device.System.TickSource,
                context.Device.Gpu,
                $"{programId:x16}",
                displayVersion,
                diskCacheEnabled,
                codeStart,
                codeSize);

            result = process.Initialize(
                creationInfo,
                MemoryMarshal.Cast<byte, uint>(npdm.KernelCapabilityData),
                resourceLimit,
                memoryRegion,
                processContextFactory);

            if (result != Result.Success)
            {
                Logger.Error?.Print(LogClass.Loader, $"Process initialization returned error \"{result}\".");

                return ProcessResult.Failed;
            }

            for (int index = 0; index < executables.Length; index++)
            {
                Logger.Info?.Print(LogClass.Loader, $"Loading image {index} at 0x{nsoBase[index]:x16}...");

                result = LoadIntoMemory(process, executables[index], nsoBase[index]);
                if (result != Result.Success)
                {
                    Logger.Error?.Print(LogClass.Loader, $"Process initialization returned error \"{result}\".");

                    return ProcessResult.Failed;
                }
            }

            process.DefaultCpuCore = meta.DefaultCpuId;

            context.Processes.TryAdd(process.Pid, process);

            // Keep the build ids because the tamper machine uses them to know which process to associate a
            // tamper to and also keep the starting address of each executable inside a process because some
            // memory modifications are relative to this address.
            ProcessTamperInfo tamperInfo = new(
                process,
                buildIds,
                nsoBase,
                process.MemoryManager.HeapRegionStart,
                process.MemoryManager.AliasRegionStart,
                process.MemoryManager.CodeRegionStart);

            // Once everything is loaded, we can load cheats.
            device.Configuration.VirtualFileSystem.ModLoader.LoadCheats(programId, tamperInfo, device.TamperMachine);

            ProcessResult processResult = new(
                metaLoader,
                applicationControlProperties,
                diskCacheEnabled,
                allowCodeMemoryForJit,
                processContextFactory.DiskCacheLoadState,
                process.Pid,
                meta.MainThreadPriority,
                meta.MainThreadStackSize,
                device.System.State.DesiredTitleLanguage);

            // Register everything in arp service.
            device.System.ServiceTable.ArpWriter.AcquireRegistrar(out IRegistrar registrar);
            registrar.SetApplicationControlProperty(MemoryMarshal.Cast<byte, Horizon.Sdk.Ns.ApplicationControlProperty>(applicationControlProperties.ByteSpan)[0]);
            // TODO: Handle Version and StorageId when it will be needed.
            registrar.SetApplicationLaunchProperty(new ApplicationLaunchProperty()
            {
                ApplicationId = new Horizon.Sdk.Ncm.ApplicationId(programId),
                Version = 0x00,
                Storage = Horizon.Sdk.Ncm.StorageId.BuiltInUser,
                PatchStorage = Horizon.Sdk.Ncm.StorageId.None,
                ApplicationKind = ApplicationKind.Application,
            });

            device.System.ServiceTable.ArpReader.GetApplicationInstanceId(out ulong applicationInstanceId, process.Pid);
            device.System.ServiceTable.ArpWriter.AcquireApplicationProcessPropertyUpdater(out IUpdater updater, applicationInstanceId);
            updater.SetApplicationProcessProperty(process.Pid, new ApplicationProcessProperty() { ProgramIndex = programIndex });

            return processResult;
        }

        public static Result LoadIntoMemory(KProcess process, IExecutable image, ulong baseAddress)
        {
            ulong textStart = baseAddress + image.TextOffset;
            ulong roStart = baseAddress + image.RoOffset;
            ulong dataStart = baseAddress + image.DataOffset;
            ulong bssStart = baseAddress + image.BssOffset;

            ulong end = dataStart + (ulong)image.Data.Length;

            if (image.BssSize != 0)
            {
                end = bssStart + image.BssSize;
            }

            process.CpuMemory.Write(textStart, image.Text);
            process.CpuMemory.Write(roStart, image.Ro);
            process.CpuMemory.Write(dataStart, image.Data);

            process.CpuMemory.Fill(bssStart, image.BssSize, 0);

            Result SetProcessMemoryPermission(ulong address, ulong size, KMemoryPermission permission)
            {
                if (size == 0)
                {
                    return Result.Success;
                }

                size = BitUtils.AlignUp<ulong>(size, KPageTableBase.PageSize);

                return process.MemoryManager.SetProcessMemoryPermission(address, size, permission);
            }

            Result result = SetProcessMemoryPermission(textStart, (ulong)image.Text.Length, KMemoryPermission.ReadAndExecute);
            if (result != Result.Success)
            {
                return result;
            }

            result = SetProcessMemoryPermission(roStart, (ulong)image.Ro.Length, KMemoryPermission.Read);
            if (result != Result.Success)
            {
                return result;
            }

            return SetProcessMemoryPermission(dataStart, end - dataStart, KMemoryPermission.ReadAndWrite);
        }
    }
}
