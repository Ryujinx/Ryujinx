namespace Ryujinx.Horizon.Ptm
{
    class TsMain : IService
    {
        public static void Main(ServiceTable serviceTable)
        {
            TsIpcServer ipcServer = new();

            ipcServer.Initialize();

            serviceTable.SignalServiceReady();

            ipcServer.ServiceRequests();
            ipcServer.Shutdown();
        }
    }
}
