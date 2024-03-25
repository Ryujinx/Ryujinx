using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Memory.Tracking
{
    [Flags]
    public enum RegionFlags
    {
        None = 0,

        /// <summary>
        /// Access to the resource is expected to occasionally be unaligned.
        /// With some memory managers, guest protection must extend into the previous page to cover unaligned access.
        /// If this is not expected, protection is not altered, which can avoid unintended resource dirty/flush.
        /// </summary>
        UnalignedAccess = 1,
    }
}
