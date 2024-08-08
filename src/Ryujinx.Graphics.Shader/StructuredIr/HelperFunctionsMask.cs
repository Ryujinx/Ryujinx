using System;

namespace Ryujinx.Graphics.Shader.StructuredIr
{
    [Flags]
    enum HelperFunctionsMask
    {
        MultiplyHighS32 = 1 << 2,
        MultiplyHighU32 = 1 << 3,

        FindLSB = 1 << 5,
        FindMSBS32 = 1 << 6,
        FindMSBU32 = 1 << 7,

        SwizzleAdd = 1 << 10,
        FSI = 1 << 11,

        Precise = 1 << 13
    }
}
