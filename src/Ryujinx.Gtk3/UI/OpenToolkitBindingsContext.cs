using SPB.Graphics;
using System;

namespace Ryujinx.UI
{
    public class OpenToolkitBindingsContext : OpenTK.IBindingsContext
    {
        private readonly IBindingsContext _bindingContext;

        public OpenToolkitBindingsContext(IBindingsContext bindingsContext)
        {
            _bindingContext = bindingsContext;
        }

        public IntPtr GetProcAddress(string procName)
        {
            return _bindingContext.GetProcAddress(procName);
        }
    }
}
