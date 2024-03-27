using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Time
{
    public class TimeZoneRuleTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x4000, Unsafe.SizeOf<TimeZoneRule>());
        }
    }
}
