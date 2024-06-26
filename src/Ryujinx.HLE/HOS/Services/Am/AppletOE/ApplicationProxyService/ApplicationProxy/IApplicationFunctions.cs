using LibHac.Account;
using LibHac.Fs;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.HLE.HOS.Services.Am.AppletAE.Storage;
using Ryujinx.HLE.HOS.Services.Am.AppletOE.ApplicationProxyService.ApplicationProxy.Types;
using Ryujinx.HLE.HOS.Services.Sdb.Pdm.QueryService;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.Horizon.Common;
using System;
using System.Numerics;
using System.Threading;
using AccountUid = Ryujinx.HLE.HOS.Services.Account.Acc.UserId;
using ApplicationId = LibHac.Ncm.ApplicationId;

namespace Ryujinx.HLE.HOS.Services.Am.AppletOE.ApplicationProxyService.ApplicationProxy
{
    class IApplicationFunctions : IpcService
    {
        private long _defaultSaveDataSize = 200000000;
        private long _defaultJournalSaveDataSize = 200000000;

        private readonly KEvent _gpuErrorDetectedSystemEvent;
        private readonly KEvent _friendInvitationStorageChannelEvent;
        private readonly KEvent _notificationStorageChannelEvent;
        private readonly KEvent _healthWarningDisappearedSystemEvent;

        private int _gpuErrorDetectedSystemEventHandle;
        private int _friendInvitationStorageChannelEventHandle;
        private int _notificationStorageChannelEventHandle;
        private int _healthWarningDisappearedSystemEventHandle;

        private bool _gamePlayRecordingState;

        private int _jitLoaded;

        private readonly LibHac.HorizonClient _horizon;

        public IApplicationFunctions(Horizon system)
        {
            // TODO: Find where they are signaled.
            _gpuErrorDetectedSystemEvent = new KEvent(system.KernelContext);
            _friendInvitationStorageChannelEvent = new KEvent(system.KernelContext);
            _notificationStorageChannelEvent = new KEvent(system.KernelContext);
            _healthWarningDisappearedSystemEvent = new KEvent(system.KernelContext);

            _horizon = system.LibHacHorizonManager.AmClient;
        }

        [CommandCmif(1)]
        // PopLaunchParameter(LaunchParameterKind kind) -> object<nn::am::service::IStorage>
        public ResultCode PopLaunchParameter(ServiceCtx context)
        {
            LaunchParameterKind kind = (LaunchParameterKind)context.RequestData.ReadUInt32();

            byte[] storageData;

            switch (kind)
            {
                case LaunchParameterKind.UserChannel:
                    storageData = context.Device.Configuration.UserChannelPersistence.Pop();
                    break;
                case LaunchParameterKind.PreselectedUser:
                    // Only the first 0x18 bytes of the Data seems to be actually used.
                    storageData = StorageHelper.MakeLaunchParams(context.Device.System.AccountManager.LastOpenedUser);
                    break;
                case LaunchParameterKind.Unknown:
                    throw new NotImplementedException("Unknown LaunchParameterKind.");
                default:
                    return ResultCode.ObjectInvalid;
            }

            if (storageData == null)
            {
                return ResultCode.NotAvailable;
            }

            MakeObject(context, new AppletAE.IStorage(storageData));

            return ResultCode.Success;
        }

        [CommandCmif(12)] // 4.0.0+
        // CreateApplicationAndRequestToStart(u64 title_id)
        public ResultCode CreateApplicationAndRequestToStart(ServiceCtx context)
        {
            ulong titleId = context.RequestData.ReadUInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { titleId });

            if (titleId == 0)
            {
                context.Device.UIHandler.ExecuteProgram(context.Device, ProgramSpecifyKind.RestartProgram, titleId);
            }
            else
            {
                throw new NotImplementedException();
            }

            return ResultCode.Success;
        }

        [CommandCmif(20)]
        // EnsureSaveData(nn::account::Uid) -> u64
        public ResultCode EnsureSaveData(ServiceCtx context)
        {
            Uid userId = context.RequestData.ReadStruct<AccountUid>().ToLibHacUid();

            // Mask out the low nibble of the program ID to get the application ID
            ApplicationId applicationId = new(context.Device.Processes.ActiveApplication.ProgramId & ~0xFul);

            ApplicationControlProperty nacp = context.Device.Processes.ActiveApplication.ApplicationControlProperties;

            LibHac.HorizonClient hos = context.Device.System.LibHacHorizonManager.AmClient;
            LibHac.Result result = hos.Fs.EnsureApplicationSaveData(out long requiredSize, applicationId, in nacp, in userId);

            context.ResponseData.Write(requiredSize);

            return (ResultCode)result.Value;
        }

        [CommandCmif(21)]
        // GetDesiredLanguage() -> nn::settings::LanguageCode
        public ResultCode GetDesiredLanguage(ServiceCtx context)
        {
            // This seems to be calling ns:am GetApplicationDesiredLanguage followed by ConvertApplicationLanguageToLanguageCode
            // Calls are from a IReadOnlyApplicationControlDataInterface object
            // ConvertApplicationLanguageToLanguageCode compares language code strings and returns the index
            // TODO: When above calls are implemented, switch to using ns:am

            long desiredLanguageCode = context.Device.System.State.DesiredLanguageCode;
            int supportedLanguages = (int)context.Device.Processes.ActiveApplication.ApplicationControlProperties.SupportedLanguageFlag;
            int firstSupported = BitOperations.TrailingZeroCount(supportedLanguages);

            if (firstSupported > (int)TitleLanguage.BrazilianPortuguese)
            {
                Logger.Warning?.Print(LogClass.ServiceAm, "Application has zero supported languages");

                context.ResponseData.Write(desiredLanguageCode);

                return ResultCode.Success;
            }

            // If desired language is not supported by application, use first supported language from TitleLanguage.
            // TODO: In the future, a GUI could enable user-specified search priority
            if (((1 << (int)context.Device.System.State.DesiredTitleLanguage) & supportedLanguages) == 0)
            {
                SystemLanguage newLanguage = Enum.Parse<SystemLanguage>(Enum.GetName(typeof(TitleLanguage), firstSupported));
                desiredLanguageCode = SystemStateMgr.GetLanguageCode((int)newLanguage);

                Logger.Info?.Print(LogClass.ServiceAm, $"Application doesn't support configured language. Using {newLanguage}");
            }

            context.ResponseData.Write(desiredLanguageCode);

            return ResultCode.Success;
        }

        [CommandCmif(22)]
        // SetTerminateResult(u32)
        public ResultCode SetTerminateResult(ServiceCtx context)
        {
            LibHac.Result result = new(context.RequestData.ReadUInt32());

            Logger.Info?.Print(LogClass.ServiceAm, $"Result = 0x{result.Value:x8} ({result.ToStringWithName()}).");

            return ResultCode.Success;
        }

        [CommandCmif(23)]
        // GetDisplayVersion() -> nn::oe::DisplayVersion
        public ResultCode GetDisplayVersion(ServiceCtx context)
        {
            // If an NACP isn't found, the buffer will be all '\0' which seems to be the correct implementation.
            context.ResponseData.Write(context.Device.Processes.ActiveApplication.ApplicationControlProperties.DisplayVersion);

            return ResultCode.Success;
        }

        [CommandCmif(25)] // 3.0.0+
        // ExtendSaveData(u8 save_data_type, nn::account::Uid, s64 save_size, s64 journal_size) -> u64 result_code
        public ResultCode ExtendSaveData(ServiceCtx context)
        {
            SaveDataType saveDataType = (SaveDataType)context.RequestData.ReadUInt64();
            Uid userId = context.RequestData.ReadStruct<Uid>();
            long saveDataSize = context.RequestData.ReadInt64();
            long journalSize = context.RequestData.ReadInt64();

            // NOTE: Service calls nn::fs::ExtendApplicationSaveData.
            //       Since LibHac currently doesn't support this method, we can stub it for now.

            _defaultSaveDataSize = saveDataSize;
            _defaultJournalSaveDataSize = journalSize;

            context.ResponseData.Write((uint)ResultCode.Success);

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { saveDataType, userId, saveDataSize, journalSize });

            return ResultCode.Success;
        }

        [CommandCmif(26)] // 3.0.0+
        // GetSaveDataSize(u8 save_data_type, nn::account::Uid) -> (s64 save_size, s64 journal_size)
        public ResultCode GetSaveDataSize(ServiceCtx context)
        {
            SaveDataType saveDataType = (SaveDataType)context.RequestData.ReadUInt64();
            Uid userId = context.RequestData.ReadStruct<Uid>();

            // NOTE: Service calls nn::fs::FindSaveDataWithFilter with SaveDataType = 1 hardcoded.
            //       Then it calls nn::fs::GetSaveDataAvailableSize and nn::fs::GetSaveDataJournalSize to get the sizes.
            //       Since LibHac currently doesn't support the 2 last methods, we can hardcode the values to 200mb.

            context.ResponseData.Write(_defaultSaveDataSize);
            context.ResponseData.Write(_defaultJournalSaveDataSize);

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { saveDataType, userId });

            return ResultCode.Success;
        }

        [CommandCmif(27)] // 5.0.0+
        // CreateCacheStorage(u16 index, s64 save_size, s64 journal_size) -> (u32 storageTarget, u64 requiredSize)
        public ResultCode CreateCacheStorage(ServiceCtx context)
        {
            ushort index = (ushort)context.RequestData.ReadUInt64();
            long saveSize = context.RequestData.ReadInt64();
            long journalSize = context.RequestData.ReadInt64();

            // Mask out the low nibble of the program ID to get the application ID
            ApplicationId applicationId = new(context.Device.Processes.ActiveApplication.ProgramId & ~0xFul);

            ApplicationControlProperty nacp = context.Device.Processes.ActiveApplication.ApplicationControlProperties;

            LibHac.Result result = _horizon.Fs.CreateApplicationCacheStorage(out long requiredSize,
                out CacheStorageTargetMedia storageTarget, applicationId, in nacp, index, saveSize, journalSize);

            if (result.IsFailure())
            {
                return (ResultCode)result.Value;
            }

            context.ResponseData.Write((ulong)storageTarget);
            context.ResponseData.Write(requiredSize);

            return ResultCode.Success;
        }

        [CommandCmif(28)] // 11.0.0+
        // GetSaveDataSizeMax() -> (s64 save_size_max, s64 journal_size_max)
        public ResultCode GetSaveDataSizeMax(ServiceCtx context)
        {
            // NOTE: We are currently using a stub for GetSaveDataSize() which returns the default values.
            //       For this method we shouldn't return anything lower than that, but since we aren't interacting
            //       with fs to get the actual sizes, we return the default values here as well.
            //       This also helps in case ExtendSaveData() has been executed and the default values were modified.

            context.ResponseData.Write(_defaultSaveDataSize);
            context.ResponseData.Write(_defaultJournalSaveDataSize);

            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [CommandCmif(29)] // 11.0.0+
        // GetCacheStorageMax() -> (s32 cache_storage_index_max, s64 cache_storage_data_and_journal_size_max)
        public ResultCode GetCacheStorageMax(ServiceCtx context)
        {
            ApplicationControlProperty nacp = context.Device.Processes.ActiveApplication.ApplicationControlProperties;

            context.ResponseData.Write((Int32)nacp.CacheStorageIndexMax);
            context.ResponseData.Write(0);
            context.ResponseData.Write(nacp.CacheStorageDataAndJournalSizeMax);

            return ResultCode.Success;
        }

        [CommandCmif(30)]
        // BeginBlockingHomeButtonShortAndLongPressed()
        public ResultCode BeginBlockingHomeButtonShortAndLongPressed(ServiceCtx context)
        {
            // NOTE: This set two internal fields at offsets 0x89 and 0x8B to value 1 then it signals an internal event.

            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [CommandCmif(31)]
        // EndBlockingHomeButtonShortAndLongPressed()
        public ResultCode EndBlockingHomeButtonShortAndLongPressed(ServiceCtx context)
        {
            // NOTE: This set two internal fields at offsets 0x89 and 0x8B to value 0 then it signals an internal event.

            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [CommandCmif(32)] // 2.0.0+
        // BeginBlockingHomeButton(u64 nano_second)
        public ResultCode BeginBlockingHomeButton(ServiceCtx context)
        {
            ulong nanoSeconds = context.RequestData.ReadUInt64();

            // NOTE: This set two internal fields at offsets 0x89 to value 1 and 0x90 to value of "nanoSeconds" then it signals an internal event.

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { nanoSeconds });

            return ResultCode.Success;
        }

        [CommandCmif(33)] // 2.0.0+
        // EndBlockingHomeButton()
        public ResultCode EndBlockingHomeButton(ServiceCtx context)
        {
            // NOTE: This set two internal fields at offsets 0x89 and 0x90 to value 0 then it signals an internal event.

            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [CommandCmif(40)]
        // NotifyRunning() -> b8
        public ResultCode NotifyRunning(ServiceCtx context)
        {
            context.ResponseData.Write(true);

            return ResultCode.Success;
        }

        [CommandCmif(50)] // 2.0.0+
        // GetPseudoDeviceId() -> nn::util::Uuid
        public ResultCode GetPseudoDeviceId(ServiceCtx context)
        {
            context.ResponseData.Write(0L);
            context.ResponseData.Write(0L);

            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [CommandCmif(60)] // 2.0.0+
        // SetMediaPlaybackStateForApplication(bool enabled)
        public ResultCode SetMediaPlaybackStateForApplication(ServiceCtx context)
        {
            bool enabled = context.RequestData.ReadBoolean();

            // NOTE: Service stores the "enabled" value in a private field, when enabled is false, it stores nn::os::GetSystemTick() too.

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { enabled });

            return ResultCode.Success;
        }

        [CommandCmif(65)] // 3.0.0+
        // IsGamePlayRecordingSupported() -> u8
        public ResultCode IsGamePlayRecordingSupported(ServiceCtx context)
        {
            context.ResponseData.Write(_gamePlayRecordingState);

            return ResultCode.Success;
        }

        [CommandCmif(66)] // 3.0.0+
        // InitializeGamePlayRecording(u64, handle<copy>)
        public ResultCode InitializeGamePlayRecording(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [CommandCmif(67)] // 3.0.0+
        // SetGamePlayRecordingState(u32)
        public ResultCode SetGamePlayRecordingState(ServiceCtx context)
        {
            _gamePlayRecordingState = context.RequestData.ReadInt32() != 0;

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { _gamePlayRecordingState });

            return ResultCode.Success;
        }

        [CommandCmif(90)] // 4.0.0+
        // EnableApplicationCrashReport(u8)
        public ResultCode EnableApplicationCrashReport(ServiceCtx context)
        {
            bool applicationCrashReportEnabled = context.RequestData.ReadBoolean();

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { applicationCrashReportEnabled });

            return ResultCode.Success;
        }

        [CommandCmif(100)] // 5.0.0+
        // InitializeApplicationCopyrightFrameBuffer(s32 width, s32 height, handle<copy, transfer_memory> transfer_memory, u64 transfer_memory_size)
        public ResultCode InitializeApplicationCopyrightFrameBuffer(ServiceCtx context)
        {
            int width = context.RequestData.ReadInt32();
            int height = context.RequestData.ReadInt32();
            ulong transferMemorySize = context.RequestData.ReadUInt64();
            int transferMemoryHandle = context.Request.HandleDesc.ToCopy[0];
            ulong transferMemoryAddress = context.Process.HandleTable.GetObject<KTransferMemory>(transferMemoryHandle).Address;

            ResultCode resultCode = ResultCode.InvalidParameters;

            if (((transferMemorySize & 0x3FFFF) == 0) && width <= 1280 && height <= 720)
            {
                resultCode = InitializeApplicationCopyrightFrameBufferImpl(transferMemoryAddress, transferMemorySize, width, height);
            }

            if (transferMemoryHandle != 0)
            {
                context.Device.System.KernelContext.Syscall.CloseHandle(transferMemoryHandle);
            }

            return resultCode;
        }

        private ResultCode InitializeApplicationCopyrightFrameBufferImpl(ulong transferMemoryAddress, ulong transferMemorySize, int width, int height)
        {
            if ((transferMemorySize & 0x3FFFF) != 0)
            {
                return ResultCode.InvalidParameters;
            }

            ResultCode resultCode;

            // if (_copyrightBuffer == null)
            {
                // TODO: Initialize buffer and object.

                Logger.Stub?.PrintStub(LogClass.ServiceAm, new { transferMemoryAddress, transferMemorySize, width, height });

                resultCode = ResultCode.Success;
            }

            return resultCode;
        }

        [CommandCmif(101)] // 5.0.0+
        // SetApplicationCopyrightImage(buffer<bytes, 0x45> frame_buffer, s32 x, s32 y, s32 width, s32 height, s32 window_origin_mode)
        public ResultCode SetApplicationCopyrightImage(ServiceCtx context)
        {
            ulong frameBufferPos = context.Request.SendBuff[0].Position;
            ulong frameBufferSize = context.Request.SendBuff[0].Size;
            int x = context.RequestData.ReadInt32();
            int y = context.RequestData.ReadInt32();
            int width = context.RequestData.ReadInt32();
            int height = context.RequestData.ReadInt32();
            uint windowOriginMode = context.RequestData.ReadUInt32();

            ResultCode resultCode = ResultCode.InvalidParameters;

            if (((y | x) >= 0) && width >= 1 && height >= 1)
            {
                ResultCode result = SetApplicationCopyrightImageImpl(x, y, width, height, frameBufferPos, frameBufferSize, windowOriginMode);

                if (result != ResultCode.Success)
                {
                    resultCode = result;
                }
                else
                {
                    resultCode = ResultCode.Success;
                }
            }

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { frameBufferPos, frameBufferSize, x, y, width, height, windowOriginMode });

            return resultCode;
        }

        private ResultCode SetApplicationCopyrightImageImpl(int x, int y, int width, int height, ulong frameBufferPos, ulong frameBufferSize, uint windowOriginMode)
        {
            /*
            if (_copyrightBuffer == null)
            {
                return ResultCode.NullCopyrightObject;
            }
            */

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { x, y, width, height, frameBufferPos, frameBufferSize, windowOriginMode });

            return ResultCode.Success;
        }

        [CommandCmif(102)] // 5.0.0+
        // SetApplicationCopyrightVisibility(bool visible)
        public ResultCode SetApplicationCopyrightVisibility(ServiceCtx context)
        {
            bool visible = context.RequestData.ReadBoolean();

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { visible });

            // NOTE: It sets an internal field and return ResultCode.Success in all case.

            return ResultCode.Success;
        }

        [CommandCmif(110)] // 5.0.0+
        // QueryApplicationPlayStatistics(buffer<bytes, 5> title_id_list) -> (buffer<bytes, 6> entries, s32 entries_count)
        public ResultCode QueryApplicationPlayStatistics(ServiceCtx context)
        {
            // TODO: Call pdm:qry cmd 13 when IPC call between services will be implemented.
            return (ResultCode)QueryPlayStatisticsManager.GetPlayStatistics(context);
        }

        [CommandCmif(111)] // 6.0.0+
        // QueryApplicationPlayStatisticsByUid(nn::account::Uid, buffer<bytes, 5> title_id_list) -> (buffer<bytes, 6> entries, s32 entries_count)
        public ResultCode QueryApplicationPlayStatisticsByUid(ServiceCtx context)
        {
            // TODO: Call pdm:qry cmd 16 when IPC call between services will be implemented.
            return (ResultCode)QueryPlayStatisticsManager.GetPlayStatistics(context, true);
        }

        [CommandCmif(120)] // 5.0.0+
        // ExecuteProgram(ProgramSpecifyKind kind, u64 value)
        public ResultCode ExecuteProgram(ServiceCtx context)
        {
            ProgramSpecifyKind kind = (ProgramSpecifyKind)context.RequestData.ReadUInt32();

            // padding
            context.RequestData.ReadUInt32();

            ulong value = context.RequestData.ReadUInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { kind, value });

            context.Device.UIHandler.ExecuteProgram(context.Device, kind, value);

            return ResultCode.Success;
        }

        [CommandCmif(121)] // 5.0.0+
        // ClearUserChannel()
        public ResultCode ClearUserChannel(ServiceCtx context)
        {
            context.Device.Configuration.UserChannelPersistence.Clear();

            return ResultCode.Success;
        }

        [CommandCmif(122)] // 5.0.0+
        // UnpopToUserChannel(object<nn::am::service::IStorage> input_storage)
        public ResultCode UnpopToUserChannel(ServiceCtx context)
        {
            AppletAE.IStorage data = GetObject<AppletAE.IStorage>(context, 0);

            context.Device.Configuration.UserChannelPersistence.Push(data.Data);

            return ResultCode.Success;
        }

        [CommandCmif(123)] // 5.0.0+
        // GetPreviousProgramIndex() -> s32 program_index
        public ResultCode GetPreviousProgramIndex(ServiceCtx context)
        {
            int previousProgramIndex = context.Device.Configuration.UserChannelPersistence.PreviousIndex;

            context.ResponseData.Write(previousProgramIndex);

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { previousProgramIndex });

            return ResultCode.Success;
        }

        [CommandCmif(130)] // 8.0.0+
        // GetGpuErrorDetectedSystemEvent() -> handle<copy>
        public ResultCode GetGpuErrorDetectedSystemEvent(ServiceCtx context)
        {
            if (_gpuErrorDetectedSystemEventHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(_gpuErrorDetectedSystemEvent.ReadableEvent, out _gpuErrorDetectedSystemEventHandle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_gpuErrorDetectedSystemEventHandle);

            // NOTE: This is used by "sdk" NSO during applet-application initialization.
            //       A separate thread is setup where event-waiting is handled.
            //       When the Event is signaled, official sw will assert.

            return ResultCode.Success;
        }

        [CommandCmif(140)] // 9.0.0+
        // GetFriendInvitationStorageChannelEvent() -> handle<copy>
        public ResultCode GetFriendInvitationStorageChannelEvent(ServiceCtx context)
        {
            if (_friendInvitationStorageChannelEventHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(_friendInvitationStorageChannelEvent.ReadableEvent, out _friendInvitationStorageChannelEventHandle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_friendInvitationStorageChannelEventHandle);

            return ResultCode.Success;
        }

        [CommandCmif(141)] // 9.0.0+
        // TryPopFromFriendInvitationStorageChannel() -> object<nn::am::service::IStorage>
        public ResultCode TryPopFromFriendInvitationStorageChannel(ServiceCtx context)
        {
            // NOTE: IStorage are pushed in the channel with IApplicationAccessor PushToFriendInvitationStorageChannel
            //       If _friendInvitationStorageChannelEvent is signaled, the event is cleared.
            //       If an IStorage is available, returns it with ResultCode.Success.
            //       If not, just returns ResultCode.NotAvailable. Since we don't support friend feature for now, it's fine to do the same.

            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.NotAvailable;
        }

        [CommandCmif(150)] // 9.0.0+
        // GetNotificationStorageChannelEvent() -> handle<copy>
        public ResultCode GetNotificationStorageChannelEvent(ServiceCtx context)
        {
            if (_notificationStorageChannelEventHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(_notificationStorageChannelEvent.ReadableEvent, out _notificationStorageChannelEventHandle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_notificationStorageChannelEventHandle);

            return ResultCode.Success;
        }

        [CommandCmif(160)] // 9.0.0+
        // GetHealthWarningDisappearedSystemEvent() -> handle<copy>
        public ResultCode GetHealthWarningDisappearedSystemEvent(ServiceCtx context)
        {
            if (_healthWarningDisappearedSystemEventHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(_healthWarningDisappearedSystemEvent.ReadableEvent, out _healthWarningDisappearedSystemEventHandle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_healthWarningDisappearedSystemEventHandle);

            return ResultCode.Success;
        }

        [CommandCmif(1001)] // 10.0.0+
        // PrepareForJit()
        public ResultCode PrepareForJit(ServiceCtx context)
        {
            if (Interlocked.Exchange(ref _jitLoaded, 1) == 0)
            {
                string jitPath = context.Device.System.ContentManager.GetInstalledContentPath(0x010000000000003B, StorageId.BuiltInSystem, NcaContentType.Program);
                string filePath = FileSystem.VirtualFileSystem.SwitchPathToSystemPath(jitPath);

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    throw new InvalidSystemResourceException("JIT (010000000000003B) system title not found! The JIT will not work, provide the system archive to fix this error. (See https://github.com/Ryujinx/Ryujinx#requirements for more information)");
                }

                context.Device.LoadNca(filePath);

                // FIXME: Most likely not how this should be done?
                while (!context.Device.System.SmRegistry.IsServiceRegistered("jit:u"))
                {
                    context.Device.System.SmRegistry.WaitForServiceRegistration();
                }
            }

            return ResultCode.Success;
        }
    }
}
