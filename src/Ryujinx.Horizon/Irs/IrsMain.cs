namespace Ryujinx.Horizon.Irs
{
    class IrsMain : IService
    {
        public static void Main(ServiceTable serviceTable)
        {
            IrsIpcServer ipcServer = new();

            ipcServer.Initialize();

            serviceTable.SignalServiceReady();

            ipcServer.ServiceRequests();
            ipcServer.Shutdown();
        }
    }
}
