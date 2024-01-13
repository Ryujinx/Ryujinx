using System;

namespace Ryujinx.UI
{
    public class StatusUpdatedEventArgs : EventArgs
    {
        public string VSyncMode;
        public float Volume;
        public string DockedMode;
        public string AspectRatio;
        public string GameStatus;
        public string FifoStatus;
        public string GpuName;
        public string GpuBackend;

        public StatusUpdatedEventArgs(string vSyncMode, float volume, string gpuBackend, string dockedMode, string aspectRatio, string gameStatus, string fifoStatus, string gpuName)
        {
            VSyncMode = vSyncMode;
            Volume = volume;
            GpuBackend = gpuBackend;
            DockedMode = dockedMode;
            AspectRatio = aspectRatio;
            GameStatus = gameStatus;
            FifoStatus = fifoStatus;
            GpuName = gpuName;
        }
    }
}
