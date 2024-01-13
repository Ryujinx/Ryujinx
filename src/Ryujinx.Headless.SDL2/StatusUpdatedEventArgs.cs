using System;

namespace Ryujinx.Headless.SDL2
{
    class StatusUpdatedEventArgs : EventArgs
    {
        public string VSyncMode;
        public string DockedMode;
        public string AspectRatio;
        public string GameStatus;
        public string FifoStatus;
        public string GpuName;

        public StatusUpdatedEventArgs(string vSyncMode, string dockedMode, string aspectRatio, string gameStatus, string fifoStatus, string gpuName)
        {
            VSyncMode = vSyncMode;
            DockedMode = dockedMode;
            AspectRatio = aspectRatio;
            GameStatus = gameStatus;
            FifoStatus = fifoStatus;
            GpuName = gpuName;
        }
    }
}
