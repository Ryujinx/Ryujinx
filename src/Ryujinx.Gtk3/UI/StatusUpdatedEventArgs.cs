using System;

namespace Ryujinx.UI
{
    public class StatusUpdatedEventArgs : EventArgs
    {
        public string PresentIntervalState;
        public float Volume;
        public string DockedMode;
        public string AspectRatio;
        public string GameStatus;
        public string FifoStatus;
        public string GpuName;
        public string GpuBackend;

        public StatusUpdatedEventArgs(string presentIntervalState, float volume, string gpuBackend, string dockedMode, string aspectRatio, string gameStatus, string fifoStatus, string gpuName)
        {
            PresentIntervalState = presentIntervalState;
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
