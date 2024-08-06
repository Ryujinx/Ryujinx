namespace Ryujinx.Horizon.Hid
{
    class HidMain : IService
    {
        public static void Main(ServiceTable serviceTable)
        {
            HidIpcServer ipcServer = new();

            ipcServer.Initialize();

            serviceTable.SignalServiceReady();

            ipcServer.ServiceRequests();
            ipcServer.Shutdown();
        }
    }
}
