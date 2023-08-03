using Ryujinx.Graphics.GAL;

namespace Ryujinx.Graphics.Metal
{
    public class Program : IProgram
    {
        public void Dispose()
        {
            return;
        }

        public ProgramLinkStatus CheckProgramLink(bool blocking)
        {
            return ProgramLinkStatus.Failure;
        }

        public byte[] GetBinary()
        {
            return new byte[] {};
        }
    }
}
