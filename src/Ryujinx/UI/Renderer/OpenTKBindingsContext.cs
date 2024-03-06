using OpenTK;
using System;

namespace Ryujinx.Ava.UI.Renderer
{
    internal class OpenTKBindingsContext : IBindingsContext
    {
        private readonly Func<string, IntPtr> _getProcAddress;

        public OpenTKBindingsContext(Func<string, IntPtr> getProcAddress)
        {
            _getProcAddress = getProcAddress;
        }

        public IntPtr GetProcAddress(string procName)
        {
            return _getProcAddress(procName);
        }
    }
}
