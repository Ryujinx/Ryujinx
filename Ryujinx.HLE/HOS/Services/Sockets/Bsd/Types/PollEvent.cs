﻿namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd
{
    class PollEvent
    {
        public PollEventData Data;
        public IFileDescriptor FileDescriptor { get; }

        public PollEvent(PollEventData data, IFileDescriptor fileDescriptor)
        {
            Data = data;
            FileDescriptor = fileDescriptor;
        }
    }
}