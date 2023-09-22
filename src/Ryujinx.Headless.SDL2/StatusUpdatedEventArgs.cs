using System;

namespace Ryujinx.Headless.SDL2
{
    class StatusUpdatedEventArgs : EventArgs
    {
        public string PresentIntervalState;
        public string DockedMode;
        public string AspectRatio;
        public string GameStatus;
        public string FifoStatus;
        public string GpuName;

        public StatusUpdatedEventArgs(string presentIntervalState, string dockedMode, string aspectRatio, string gameStatus, string fifoStatus, string gpuName)
        {
            PresentIntervalState = presentIntervalState;
            DockedMode = dockedMode;
            AspectRatio = aspectRatio;
            GameStatus = gameStatus;
            FifoStatus = fifoStatus;
            GpuName = gpuName;
        }
    }
}
