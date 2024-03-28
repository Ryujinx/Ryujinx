using System;

namespace Ryujinx.UI
{
    public class StatusUpdatedEventArgs : EventArgs
    {
        public bool VSyncEnabled;
        public float Volume;
        public string DockedMode;
        public string AspectRatio;
        public string GameStatus;
        public string FifoStatus;
        public string GpuName;
        public string GpuBackend;
        public uint ShaderCount;

        public StatusUpdatedEventArgs(
            bool vSyncEnabled,
            float volume,
            string gpuBackend,
            string dockedMode,
            string aspectRatio,
            string gameStatus,
            string fifoStatus,
            string gpuName,
            uint shaderCount)
        {
            VSyncEnabled = vSyncEnabled;
            Volume = volume;
            GpuBackend = gpuBackend;
            DockedMode = dockedMode;
            AspectRatio = aspectRatio;
            GameStatus = gameStatus;
            FifoStatus = fifoStatus;
            GpuName = gpuName;
            ShaderCount = shaderCount;
        }
    }
}
