using System;

namespace Ryujinx.Ava.UI.Models
{
    internal class StatusInitEventArgs : EventArgs
    {
        public string GpuBackend { get; }
        public string GpuName { get; }

        public StatusInitEventArgs(string gpuBackend, string gpuName)
        {
            GpuBackend = gpuBackend;
            GpuName = gpuName;
        }
    }
}
