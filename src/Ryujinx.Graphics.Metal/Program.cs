using Ryujinx.Graphics.GAL;

namespace Ryujinx.Graphics.Metal
{
    class Program : IProgram
    {
        public ProgramLinkStatus CheckProgramLink(bool blocking)
        {
            return ProgramLinkStatus.Failure;
        }

        public byte[] GetBinary()
        {
            return ""u8.ToArray();
        }

        public void Dispose()
        {
            return;
        }
    }
}
