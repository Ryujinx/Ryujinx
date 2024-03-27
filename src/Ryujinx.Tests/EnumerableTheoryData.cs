using System.Collections.Generic;
using Xunit;

namespace Ryujinx.Tests
{
    public class EnumerableTheoryData<T> : TheoryData<T>
    {
        public EnumerableTheoryData(IEnumerable<T> data)
        {
            foreach (T item in data)
            {
                Add(item);
            }
        }
    }
}
