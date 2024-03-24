using LibHac.Tools.FsSystem;
using Ryujinx.Common;
using Ryujinx.Cpu;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.Horizon.Common;
using Ryujinx.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Ryujinx.HLE.HOS.Services.Ro
{
    [Service("ldr:ro")]
    [Service("ro:1")] // 7.0.0+
    class IRoInterface : DisposableIpcService
    {
        private const bool EnableOpt = true;

        private const int MaxNrr = 0x40;
        private const int MaxNro = 0x40;
        private const int MaxMapRetries = 0x200;
        private const int GuardPagesSize = 0x4000;

        private const uint NrrMagic = 0x3052524E;
        private const uint NroMagic = 0x304F524E;

        private readonly List<NrrInfo> _nrrInfos;
        private readonly List<NroInfo> _nroInfos;
        private readonly List<NroInfo> _nroInfosUnloaded;

        private KProcess _owner;
        private IVirtualMemoryManager _ownerMm;

        public IRoInterface(ServiceCtx context)
        {
            _nrrInfos = new List<NrrInfo>(MaxNrr);
            _nroInfos = new List<NroInfo>(MaxNro);
            _nroInfosUnloaded = new List<NroInfo>(MaxNro);

            _owner = null;
            _ownerMm = null;
        }

        private ResultCode ParseNrr(out NrrInfo nrrInfo, ServiceCtx context, ulong nrrAddress, ulong nrrSize)
        {
            nrrInfo = null;

            if (nrrSize == 0 || nrrAddress + nrrSize <= nrrAddress || (nrrSize & 0xFFF) != 0)
            {
                return ResultCode.InvalidSize;
            }
            else if ((nrrAddress & 0xFFF) != 0)
            {
                return ResultCode.InvalidAddress;
            }

            NrrHeader header = _owner.CpuMemory.Read<NrrHeader>(nrrAddress);

            if (header.Magic != NrrMagic)
            {
                return ResultCode.InvalidNrr;
            }
            else if (header.Size != nrrSize)
            {
                return ResultCode.InvalidSize;
            }

            List<byte[]> hashes = new();

            for (int i = 0; i < header.HashesCount; i++)
            {
                byte[] hash = new byte[0x20];

                _owner.CpuMemory.Read(nrrAddress + header.HashesOffset + (uint)(i * 0x20), hash);

                hashes.Add(hash);
            }

            nrrInfo = new NrrInfo(nrrAddress, header, hashes);

            return ResultCode.Success;
        }

        public bool IsNroHashPresent(byte[] nroHash)
        {
            foreach (NrrInfo info in _nrrInfos)
            {
                foreach (byte[] hash in info.Hashes)
                {
                    if (hash.SequenceEqual(nroHash))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsNroLoaded(byte[] nroHash)
        {
            foreach (NroInfo info in _nroInfos)
            {
                if (info.Hash.SequenceEqual(nroHash))
                {
                    return true;
                }
            }

            return false;
        }

        public ResultCode ParseNro(out NroInfo res, ServiceCtx context, ulong nroAddress, ulong nroSize, ulong bssAddress, ulong bssSize)
        {
            res = null;

            if (_nroInfos.Count >= MaxNro)
            {
                return ResultCode.TooManyNro;
            }
            else if (nroSize == 0 || nroAddress + nroSize <= nroAddress || (nroSize & 0xFFF) != 0)
            {
                return ResultCode.InvalidSize;
            }
            else if (bssSize != 0 && bssAddress + bssSize <= bssAddress)
            {
                return ResultCode.InvalidSize;
            }
            else if ((nroAddress & 0xFFF) != 0)
            {
                return ResultCode.InvalidAddress;
            }

            uint magic = _owner.CpuMemory.Read<uint>(nroAddress + 0x10);
            uint nroFileSize = _owner.CpuMemory.Read<uint>(nroAddress + 0x18);

            if (magic != NroMagic || nroSize != nroFileSize)
            {
                return ResultCode.InvalidNro;
            }

            byte[] nroData = new byte[nroSize];

            _owner.CpuMemory.Read(nroAddress, nroData);

            MemoryStream stream = new(nroData);

            byte[] nroHash = SHA256.HashData(stream);

            if (!IsNroHashPresent(nroHash))
            {
                return ResultCode.NotRegistered;
            }

            if (IsNroLoaded(nroHash))
            {
                return ResultCode.AlreadyLoaded;
            }

            stream.Position = 0;

            NroExecutable nro = new(stream.AsStorage(), nroAddress, bssAddress);

            // Check if everything is page align.
            if ((nro.Text.Length & 0xFFF) != 0 || (nro.Ro.Length & 0xFFF) != 0 ||
                (nro.Data.Length & 0xFFF) != 0 || (nro.BssSize & 0xFFF) != 0)
            {
                return ResultCode.InvalidNro;
            }

            // Check if everything is contiguous.
            if (nro.RoOffset != nro.TextOffset + nro.Text.Length ||
                nro.DataOffset != nro.RoOffset + nro.Ro.Length ||
                nroFileSize != nro.DataOffset + nro.Data.Length)
            {
                return ResultCode.InvalidNro;
            }

            // Check the bss size match.
            if ((ulong)nro.BssSize != bssSize)
            {
                return ResultCode.InvalidNro;
            }

            uint totalSize = (uint)nro.Text.Length + (uint)nro.Ro.Length + (uint)nro.Data.Length + nro.BssSize;

            // Apply patches
            context.Device.FileSystem.ModLoader.ApplyNroPatches(nro);

            res = new NroInfo(
                nro,
                nroHash,
                nroAddress,
                nroSize,
                bssAddress,
                bssSize,
                (ulong)totalSize);

            return ResultCode.Success;
        }

        private ResultCode MapNro(KProcess process, NroInfo info, MapType mapType, out ulong nroMappedAddress)
        {
            KPageTableBase memMgr = process.MemoryManager;

            int retryCount = 0;
            int maxMapRetries = mapType != MapType.Within ? MaxMapRetries : 1;

            nroMappedAddress = 0;

            while (retryCount++ < maxMapRetries)
            {
                ResultCode result = MapCodeMemoryInProcess(process, info, mapType, out nroMappedAddress);

                if (result != ResultCode.Success)
                {
                    return result;
                }

                if (info.BssSize > 0)
                {
                    Result bssMappingResult = memMgr.MapProcessCodeMemory(nroMappedAddress + info.NroSize, info.BssAddress, info.BssSize);

                    if (bssMappingResult == KernelResult.InvalidMemState)
                    {
                        memMgr.UnmapProcessCodeMemory(nroMappedAddress + info.NroSize, info.BssAddress, info.BssSize);
                        memMgr.UnmapProcessCodeMemory(nroMappedAddress, info.NroAddress, info.NroSize);

                        continue;
                    }
                    else if (bssMappingResult != Result.Success)
                    {
                        memMgr.UnmapProcessCodeMemory(nroMappedAddress + info.NroSize, info.BssAddress, info.BssSize);
                        memMgr.UnmapProcessCodeMemory(nroMappedAddress, info.NroAddress, info.NroSize);

                        return (ResultCode)bssMappingResult.ErrorCode;
                    }
                }

                if (CanAddGuardRegionsInProcess(process, nroMappedAddress, info.TotalSize))
                {
                    return ResultCode.Success;
                }
            }

            return ResultCode.InsufficientAddressSpace;
        }

        private bool CanAddGuardRegionsInProcess(KProcess process, ulong baseAddress, ulong size)
        {
            KPageTableBase memMgr = process.MemoryManager;

            KMemoryInfo memInfo = memMgr.QueryMemory(baseAddress - 1);

            if (memInfo.State == MemoryState.Unmapped && baseAddress - GuardPagesSize >= memInfo.Address)
            {
                memInfo = memMgr.QueryMemory(baseAddress + size);

                if (memInfo.State == MemoryState.Unmapped)
                {
                    return baseAddress + size + GuardPagesSize <= memInfo.Address + memInfo.Size;
                }
            }
            return false;
        }

        private static bool NotOverlapsWith(ulong newAddress, ulong newSize, ulong oldAddress, ulong oldSize)
        {
            return newAddress + newSize <= oldAddress || oldAddress + oldSize <= newAddress;
        }

        private ResultCode MapCodeMemoryInProcess(KProcess process, NroInfo info, MapType mapType, out ulong targetAddress)
        {
            KPageTableBase memMgr = process.MemoryManager;

            int retryCount = 0;
            int maxMapRetries = mapType != MapType.Within ? MaxMapRetries : 1;

            targetAddress = 0;

            while (retryCount++ < maxMapRetries)
            {
                if (mapType == MapType.Default)
                {
                    while (true)
                    {
                        ulong addressSpacePageLimit = (memMgr.GetAddrSpaceSize() - info.NroSize) >> 12;
                        ulong randomOffset = (ulong)(uint)Random.Shared.Next(0, (int)addressSpacePageLimit) << 12;

                        targetAddress = memMgr.GetAddrSpaceBaseAddr() + randomOffset;

                        if (memMgr.InsideAddrSpace(targetAddress, info.NroSize) &&
                            !memMgr.InsideHeapRegion(targetAddress, info.NroSize) &&
                            !memMgr.InsideAliasRegion(targetAddress, info.NroSize))
                        {
                            break;
                        }
                    }
                }
                else if (mapType == MapType.Between)
                {
                    while (true)
                    {
                        while (true)
                        {
                            ulong addressSpacePageLimit = (memMgr.GetAddrSpaceSize() - info.NroSize) >> 12;
                            ulong randomOffset = (ulong)(uint)Random.Shared.Next(0, (int)addressSpacePageLimit) << 12;

                            targetAddress = memMgr.GetAddrSpaceBaseAddr() + randomOffset;

                            if (memMgr.InsideAddrSpace(targetAddress, info.NroSize) &&
                                !memMgr.InsideHeapRegion(targetAddress, info.NroSize) &&
                                !memMgr.InsideAliasRegion(targetAddress, info.NroSize))
                            {
                                break;
                            }
                        }

                        ulong targetAddressTmp = targetAddress; // CS1628.
                        if (_nroInfosUnloaded.TrueForAll((infoU) => NotOverlapsWith(targetAddressTmp, info.NroSize + info.BssSize, infoU.NroMappedAddress, infoU.NroSize + infoU.BssSize)))
                        {
                            break;
                        }
                    }
                }
                else /* if (mapType == MapType.Within) */
                {
                    NroInfo infoU = _nroInfosUnloaded.Find((infoU) => infoU.Hash.SequenceEqual(info.Hash));

                    targetAddress = infoU.NroMappedAddress;
                }

                Result result = memMgr.MapProcessCodeMemory(targetAddress, info.NroAddress, info.NroSize);

                if (result == KernelResult.InvalidMemState)
                {
                    continue;
                }
                else if (result != Result.Success)
                {
                    return (ResultCode)result.ErrorCode;
                }

                if (!CanAddGuardRegionsInProcess(process, targetAddress, info.NroSize))
                {
                    continue;
                }

                return ResultCode.Success;
            }

            return ResultCode.InsufficientAddressSpace;
        }

        private Result SetNroMemoryPermissions(KProcess process, IExecutable relocatableObject, ulong baseAddress)
        {
            ulong textStart = baseAddress + relocatableObject.TextOffset;
            ulong roStart = baseAddress + relocatableObject.RoOffset;
            ulong dataStart = baseAddress + relocatableObject.DataOffset;

            ulong bssStart = dataStart + (ulong)relocatableObject.Data.Length;

            ulong bssEnd = BitUtils.AlignUp<ulong>(bssStart + relocatableObject.BssSize, KPageTableBase.PageSize);

            process.CpuMemory.Write(textStart, relocatableObject.Text);
            process.CpuMemory.Write(roStart, relocatableObject.Ro);
            process.CpuMemory.Write(dataStart, relocatableObject.Data);

            MemoryHelper.FillWithZeros(process.CpuMemory, bssStart, (int)(bssEnd - bssStart));

            Result result;

            result = process.MemoryManager.SetProcessMemoryPermission(textStart, roStart - textStart, KMemoryPermission.ReadAndExecute);

            if (result != Result.Success)
            {
                return result;
            }

            result = process.MemoryManager.SetProcessMemoryPermission(roStart, dataStart - roStart, KMemoryPermission.Read);

            if (result != Result.Success)
            {
                return result;
            }

            return process.MemoryManager.SetProcessMemoryPermission(dataStart, bssEnd - dataStart, KMemoryPermission.ReadAndWrite);
        }

        private ResultCode RemoveNrrInfo(ulong nrrAddress)
        {
            foreach (NrrInfo info in _nrrInfos)
            {
                if (info.NrrAddress == nrrAddress)
                {
                    _nrrInfos.Remove(info);

                    return ResultCode.Success;
                }
            }

            return ResultCode.NotLoaded;
        }

        private ResultCode RemoveNroInfo(ulong nroMappedAddress)
        {
            foreach (NroInfo info in _nroInfos)
            {
                if (info.NroMappedAddress == nroMappedAddress)
                {
                    if (EnableOpt)
                    {
                        _nroInfosUnloaded.Add(info);
                    }
                    _nroInfos.Remove(info);

                    return UnmapNroFromInfo(info, clearRejitQueueOnly: EnableOpt);
                }
            }

            return ResultCode.NotLoaded;
        }

        private ResultCode UnmapNroFromInfo(NroInfo info, bool clearRejitQueueOnly = false)
        {
            ulong textSize = (ulong)info.Executable.Text.Length;
            ulong roSize = (ulong)info.Executable.Ro.Length;
            ulong dataSize = (ulong)info.Executable.Data.Length;
            ulong bssSize = (ulong)info.Executable.BssSize;

            Result result = Result.Success;

            if (info.Executable.BssSize != 0)
            {
                result = _owner.MemoryManager.UnmapProcessCodeMemory(
                    info.NroMappedAddress + textSize + roSize + dataSize,
                    info.Executable.BssAddress,
                    bssSize, clearRejitQueueOnly);
            }

            if (result == Result.Success)
            {
                result = _owner.MemoryManager.UnmapProcessCodeMemory(
                    info.NroMappedAddress + textSize + roSize,
                    info.Executable.SourceAddress + textSize + roSize,
                    dataSize, clearRejitQueueOnly);

                if (result == Result.Success)
                {
                    result = _owner.MemoryManager.UnmapProcessCodeMemory(
                        info.NroMappedAddress,
                        info.Executable.SourceAddress,
                        textSize + roSize, clearRejitQueueOnly);
                }
            }

            return (ResultCode)result.ErrorCode;
        }

        private ResultCode IsInitialized(ulong pid)
        {
            if (_owner != null && _owner.Pid == pid)
            {
                return ResultCode.Success;
            }

            return ResultCode.InvalidProcess;
        }

        private enum MapType { Default, Between, Within }

        [CommandCmif(0)]
        // LoadNro(u64, u64, u64, u64, u64, pid) -> u64
        public ResultCode LoadNro(ServiceCtx context)
        {
            ResultCode result = IsInitialized(_owner.Pid);

            // Zero
            context.RequestData.ReadUInt64();

            ulong nroHeapAddress = context.RequestData.ReadUInt64();
            ulong nroSize = context.RequestData.ReadUInt64();
            ulong bssHeapAddress = context.RequestData.ReadUInt64();
            ulong bssSize = context.RequestData.ReadUInt64();

            ulong nroMappedAddress = 0;

            if (result == ResultCode.Success)
            {

                result = ParseNro(out NroInfo info, context, nroHeapAddress, nroSize, bssHeapAddress, bssSize);

                if (result == ResultCode.Success)
                {
                    MapType mapType = MapType.Default;

                    if (_nroInfosUnloaded.Count != 0)
                    {
                        mapType = _nroInfosUnloaded.Exists((infoU) => infoU.Hash.SequenceEqual(info.Hash)) // One/zero match.
                            ? MapType.Within
                            : MapType.Between;
                    }

                    result = MapNro(_owner, info, mapType, out nroMappedAddress);

                    if (mapType == MapType.Between)
                    {
                        if (result != ResultCode.Success)
                        {
                            _nroInfosUnloaded.Clear();

                            mapType = MapType.Default;

                            result = MapNro(_owner, info, mapType, out nroMappedAddress);
                        }
                    }
                    else if (mapType == MapType.Within)
                    {
                        _nroInfosUnloaded.RemoveAll((infoU) => infoU.Hash.SequenceEqual(info.Hash)); // One match.

                        if (result != ResultCode.Success)
                        {
                            mapType = MapType.Between;

                            result = MapNro(_owner, info, mapType, out nroMappedAddress);

                            if (result != ResultCode.Success)
                            {
                                _nroInfosUnloaded.Clear();

                                mapType = MapType.Default;

                                result = MapNro(_owner, info, mapType, out nroMappedAddress);
                            }
                        }
                    }

                    if (result == ResultCode.Success)
                    {
                        result = (ResultCode)SetNroMemoryPermissions(_owner, info.Executable, nroMappedAddress).ErrorCode;

                        if (result == ResultCode.Success)
                        {
                            info.NroMappedAddress = nroMappedAddress;

                            _nroInfos.Add(info);
                        }
                    }
                }
            }

            context.ResponseData.Write(nroMappedAddress);

            return result;
        }

        [CommandCmif(1)]
        // UnloadNro(u64, u64, pid)
        public ResultCode UnloadNro(ServiceCtx context)
        {
            ResultCode result = IsInitialized(_owner.Pid);

            // Zero
            context.RequestData.ReadUInt64();

            ulong nroMappedAddress = context.RequestData.ReadUInt64();

            if (result == ResultCode.Success)
            {
                if ((nroMappedAddress & 0xFFF) != 0)
                {
                    return ResultCode.InvalidAddress;
                }

                result = RemoveNroInfo(nroMappedAddress);
            }

            return result;
        }

        [CommandCmif(2)]
        // LoadNrr(u64, u64, u64, pid)
        public ResultCode LoadNrr(ServiceCtx context)
        {
            ResultCode result = IsInitialized(_owner.Pid);

            // pid placeholder, zero
            context.RequestData.ReadUInt64();

            ulong nrrAddress = context.RequestData.ReadUInt64();
            ulong nrrSize = context.RequestData.ReadUInt64();

            if (result == ResultCode.Success)
            {
                result = ParseNrr(out NrrInfo info, context, nrrAddress, nrrSize);

                if (result == ResultCode.Success)
                {
                    if (_nrrInfos.Count >= MaxNrr)
                    {
                        result = ResultCode.NotLoaded;
                    }
                    else
                    {
                        _nrrInfos.Add(info);
                    }
                }
            }

            return result;
        }

        [CommandCmif(3)]
        // UnloadNrr(u64, u64, pid)
        public ResultCode UnloadNrr(ServiceCtx context)
        {
            ResultCode result = IsInitialized(_owner.Pid);

            // pid placeholder, zero
            context.RequestData.ReadUInt64();

            ulong nrrHeapAddress = context.RequestData.ReadUInt64();

            if (result == ResultCode.Success)
            {
                if ((nrrHeapAddress & 0xFFF) != 0)
                {
                    return ResultCode.InvalidAddress;
                }

                result = RemoveNrrInfo(nrrHeapAddress);
            }

            return result;
        }

        [CommandCmif(4)]
        // Initialize(u64, pid, KObject)
        public ResultCode Initialize(ServiceCtx context)
        {
            if (_owner != null)
            {
                return ResultCode.InvalidSession;
            }

            int processHandle = context.Request.HandleDesc.ToCopy[0];
            _owner = context.Process.HandleTable.GetKProcess(processHandle);
            _ownerMm = _owner?.CpuMemory;
            context.Device.System.KernelContext.Syscall.CloseHandle(processHandle);

            if (_ownerMm is IRefCounted rc)
            {
                rc.IncrementReferenceCount();
            }

            return ResultCode.Success;
        }

        [CommandCmif(10)]
        // LoadNrr2(u64, u64, u64, pid)
        public ResultCode LoadNrr2(ServiceCtx context)
        {
            context.Device.System.KernelContext.Syscall.CloseHandle(context.Request.HandleDesc.ToCopy[0]);

            return LoadNrr(context);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (NroInfo info in _nroInfos)
                {
                    UnmapNroFromInfo(info);
                }

                _nroInfos.Clear();
                _nroInfosUnloaded.Clear();

                if (_ownerMm is IRefCounted rc)
                {
                    rc.DecrementReferenceCount();
                }
            }
        }
    }
}
