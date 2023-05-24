using SharpMetal;

namespace Ryujinx.Graphics.Metal
{
    static class FormatCapabilities
    {
        public static MTLPixelFormat ConvertToMTLFormat(GAL.Format srcFormat)
        {
            var format = FormatTable.GetFormat(srcFormat);

            return format;
        }
    }
}