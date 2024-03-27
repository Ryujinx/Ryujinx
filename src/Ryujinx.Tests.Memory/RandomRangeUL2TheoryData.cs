using System;
using Xunit;

namespace Ryujinx.Tests.Memory
{
    public class RandomRangeUL2TheoryData : TheoryData<ulong, ulong>
    {
        public RandomRangeUL2TheoryData(ulong from, ulong to, int count)
        {
            byte[] buffer = new byte[8];

            for (int i = 0; i < count; i++)
            {
                ulong[] results = new ulong[2];

                for (int j = 0; j < results.Length; j++)
                {
                    Random.Shared.NextBytes(buffer);
                    // NOTE: The result won't be perfectly random, but it should be random enough for tests
                    results[j] = BitConverter.ToUInt64(buffer) % (to + 1 - from) + from;
                }

                Add(results[0], results[1]);
            }
        }
    }
}
