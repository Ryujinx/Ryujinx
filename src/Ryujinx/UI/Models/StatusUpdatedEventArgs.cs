using System;

namespace Ryujinx.Ava.UI.Models
{
    internal class StatusUpdatedEventArgs : EventArgs
    {
        public string VSyncMode { get; }
        public string VolumeStatus { get; }
        public string AspectRatio { get; }
        public string DockedMode { get; }
        public string FifoStatus { get; }
        public string GameStatus { get; }

        public StatusUpdatedEventArgs(string vSyncMode, string volumeStatus, string dockedMode, string aspectRatio, string gameStatus, string fifoStatus)
        {
            VSyncMode = vSyncMode;
            VolumeStatus = volumeStatus;
            DockedMode = dockedMode;
            AspectRatio = aspectRatio;
            GameStatus = gameStatus;
            FifoStatus = fifoStatus;
        }
    }
}
