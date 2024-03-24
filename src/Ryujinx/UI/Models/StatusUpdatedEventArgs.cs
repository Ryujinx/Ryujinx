using System;

namespace Ryujinx.Ava.UI.Models
{
    internal class StatusUpdatedEventArgs : EventArgs
    {
        public bool VSyncEnabled { get; }
        public string VolumeStatus { get; }
        public string AspectRatio { get; }
        public string DockedMode { get; }
        public string FifoStatus { get; }
        public string GameStatus { get; }

        public StatusUpdatedEventArgs(bool vSyncEnabled, string volumeStatus, string dockedMode, string aspectRatio, string gameStatus, string fifoStatus)
        {
            VSyncEnabled = vSyncEnabled;
            VolumeStatus = volumeStatus;
            DockedMode = dockedMode;
            AspectRatio = aspectRatio;
            GameStatus = gameStatus;
            FifoStatus = fifoStatus;
        }
    }
}
