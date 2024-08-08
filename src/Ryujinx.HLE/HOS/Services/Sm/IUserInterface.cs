using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.HOS.Kernel.Ipc;
using Ryujinx.HLE.HOS.Services.Apm;
using Ryujinx.Horizon.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Sm
{
    partial class IUserInterface : IpcService
    {
        private static readonly Dictionary<string, Type> _services;

        private readonly SmRegistry _registry;
        private readonly ServerBase _commonServer;

        private bool _isInitialized;

        public IUserInterface(KernelContext context, SmRegistry registry)
        {
            _commonServer = new ServerBase(context, "CommonServer");
            _registry = registry;
        }

        static IUserInterface()
        {
            _services = typeof(IUserInterface).Assembly.GetTypes()
                .SelectMany(type => type.GetCustomAttributes(typeof(ServiceAttribute), true)
                .Select(service => (((ServiceAttribute)service).Name, type)))
                .ToDictionary(service => service.Name, service => service.type);
        }

        [CommandCmif(0)]
        [CommandTipc(0)] // 12.0.0+
        // Initialize(pid, u64 reserved)
        public ResultCode Initialize(ServiceCtx context)
        {
            _isInitialized = true;

            return ResultCode.Success;
        }

        [CommandTipc(1)] // 12.0.0+
        // GetService(ServiceName name) -> handle<move, session>
        public ResultCode GetServiceTipc(ServiceCtx context)
        {
            context.Response.HandleDesc = IpcHandleDesc.MakeMove(0);

            return GetService(context);
        }

        [CommandCmif(1)]
        public ResultCode GetService(ServiceCtx context)
        {
            if (!_isInitialized)
            {
                return ResultCode.NotInitialized;
            }

            string name = ReadName(context);

            if (name == string.Empty)
            {
                return ResultCode.InvalidName;
            }

            KSession session = new(context.Device.System.KernelContext);

            if (_registry.TryGetService(name, out KPort port))
            {
                Result result = port.EnqueueIncomingSession(session.ServerSession);

                if (result != Result.Success)
                {
                    throw new InvalidOperationException($"Session enqueue on port returned error \"{result}\".");
                }

                if (context.Process.HandleTable.GenerateHandle(session.ClientSession, out int handle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }

                session.ClientSession.DecrementReferenceCount();

                context.Response.HandleDesc = IpcHandleDesc.MakeMove(handle);
            }
            else
            {
                if (_services.TryGetValue(name, out Type type))
                {
                    ServiceAttribute serviceAttribute = (ServiceAttribute)type.GetCustomAttributes(typeof(ServiceAttribute)).First(service => ((ServiceAttribute)service).Name == name);

                    IpcService service = GetServiceInstance(type, context, serviceAttribute.Parameter);

                    service.TrySetServer(_commonServer);
                    service.Server.AddSessionObj(session.ServerSession, service);
                }
                else
                {
                    if (context.Device.Configuration.IgnoreMissingServices)
                    {
                        Logger.Warning?.Print(LogClass.Service, $"Missing service {name} ignored");
                    }
                    else
                    {
                        throw new NotImplementedException(name);
                    }
                }

                if (context.Process.HandleTable.GenerateHandle(session.ClientSession, out int handle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }

                session.ServerSession.DecrementReferenceCount();
                session.ClientSession.DecrementReferenceCount();

                context.Response.HandleDesc = IpcHandleDesc.MakeMove(handle);
            }

            return ResultCode.Success;
        }

        [CommandCmif(2)]
        // RegisterService(ServiceName name, u8 isLight, u32 maxHandles) -> handle<move, port>
        public ResultCode RegisterServiceCmif(ServiceCtx context)
        {
            if (!_isInitialized)
            {
                return ResultCode.NotInitialized;
            }

            long namePosition = context.RequestData.BaseStream.Position;

            string name = ReadName(context);

            context.RequestData.BaseStream.Seek(namePosition + 8, SeekOrigin.Begin);

            bool isLight = (context.RequestData.ReadInt32() & 1) != 0;

            int maxSessions = context.RequestData.ReadInt32();

            return RegisterService(context, name, isLight, maxSessions);
        }

        [CommandTipc(2)] // 12.0.0+
        // RegisterService(ServiceName name, u32 maxHandles, u8 isLight) -> handle<move, port>
        public ResultCode RegisterServiceTipc(ServiceCtx context)
        {
            if (!_isInitialized)
            {
                context.Response.HandleDesc = IpcHandleDesc.MakeMove(0);

                return ResultCode.NotInitialized;
            }

            long namePosition = context.RequestData.BaseStream.Position;

            string name = ReadName(context);

            context.RequestData.BaseStream.Seek(namePosition + 8, SeekOrigin.Begin);

            int maxSessions = context.RequestData.ReadInt32();

            bool isLight = (context.RequestData.ReadInt32() & 1) != 0;

            return RegisterService(context, name, isLight, maxSessions);
        }

        private ResultCode RegisterService(ServiceCtx context, string name, bool isLight, int maxSessions)
        {
            if (string.IsNullOrEmpty(name))
            {
                return ResultCode.InvalidName;
            }

            Logger.Debug?.Print(LogClass.ServiceSm, $"Register \"{name}\".");

            KPort port = new(context.Device.System.KernelContext, maxSessions, isLight, null);

            if (!_registry.TryRegister(name, port))
            {
                return ResultCode.AlreadyRegistered;
            }

            if (context.Process.HandleTable.GenerateHandle(port.ServerPort, out int handle) != Result.Success)
            {
                throw new InvalidOperationException("Out of handles!");
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeMove(handle);

            return ResultCode.Success;
        }

        [CommandCmif(3)]
        [CommandTipc(3)] // 12.0.0+
        // UnregisterService(ServiceName name)
        public ResultCode UnregisterService(ServiceCtx context)
        {
            if (!_isInitialized)
            {
                return ResultCode.NotInitialized;
            }

            long namePosition = context.RequestData.BaseStream.Position;

            string name = ReadName(context);

            context.RequestData.BaseStream.Seek(namePosition + 8, SeekOrigin.Begin);

#pragma warning disable IDE0059 // Remove unnecessary value assignment
            bool isLight = (context.RequestData.ReadInt32() & 1) != 0;
            int maxSessions = context.RequestData.ReadInt32();
#pragma warning restore IDE0059

            if (string.IsNullOrEmpty(name))
            {
                return ResultCode.InvalidName;
            }

            if (!_registry.Unregister(name))
            {
                return ResultCode.NotRegistered;
            }

            return ResultCode.Success;
        }

        private static string ReadName(ServiceCtx context)
        {
            StringBuilder nameBuilder = new();

            for (int index = 0; index < 8 &&
                context.RequestData.BaseStream.Position <
                context.RequestData.BaseStream.Length; index++)
            {
                byte chr = context.RequestData.ReadByte();

                if (chr >= 0x20 && chr < 0x7f)
                {
                    nameBuilder.Append((char)chr);
                }
            }

            return nameBuilder.ToString();
        }

        public override void DestroyAtExit()
        {
            _commonServer.Dispose();

            base.DestroyAtExit();
        }
    }
}
