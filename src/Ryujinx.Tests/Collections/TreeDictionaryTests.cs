using Ryujinx.Common.Collections;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Collections
{
    public class TreeDictionaryTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TreeDictionaryTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void EnsureAddIntegrity()
        {
            TreeDictionary<int, int> dictionary = new();

            Assert.Equal(0, dictionary.Count);

            dictionary.Add(2, 7);
            dictionary.Add(1, 4);
            dictionary.Add(10, 2);
            dictionary.Add(4, 1);
            dictionary.Add(3, 2);
            dictionary.Add(11, 2);
            dictionary.Add(5, 2);

            Assert.Equal(7, dictionary.Count);

            List<KeyValuePair<int, int>> list = dictionary.AsLevelOrderList();

            /*
             *  Tree Should Look as Follows After Rotations
             *
             *        2
             *    1        4
             *           3    10
             *              5    11
             *
             */

            Assert.Equal(dictionary.Count, list.Count);
            Assert.Equal(2, list[0].Key);
            Assert.Equal(1, list[1].Key);
            Assert.Equal(4, list[2].Key);
            Assert.Equal(3, list[3].Key);
            Assert.Equal(10, list[4].Key);
            Assert.Equal(5, list[5].Key);
            Assert.Equal(11, list[6].Key);
        }

        [Fact]
        public void EnsureRemoveIntegrity()
        {
            TreeDictionary<int, int> dictionary = new();

            Assert.Equal(0, dictionary.Count);

            dictionary.Add(2, 7);
            dictionary.Add(1, 4);
            dictionary.Add(10, 2);
            dictionary.Add(4, 1);
            dictionary.Add(3, 2);
            dictionary.Add(11, 2);
            dictionary.Add(5, 2);
            dictionary.Add(7, 2);
            dictionary.Add(9, 2);
            dictionary.Add(8, 2);
            dictionary.Add(13, 2);
            dictionary.Add(24, 2);
            dictionary.Add(6, 2);
            Assert.Equal(13, dictionary.Count);

            List<KeyValuePair<int, int>> list = dictionary.AsLevelOrderList();

            /*
             *  Tree Should Look as Follows After Rotations
             *
             *              4
             *      2               10
             *  1      3       7         13
             *              5      9  11    24
             *                6  8
             */

            foreach (KeyValuePair<int, int> node in list)
            {
                _testOutputHelper.WriteLine($"{node.Key} -> {node.Value}");
            }
            Assert.Equal(dictionary.Count, list.Count);
            Assert.Equal(4, list[0].Key);
            Assert.Equal(2, list[1].Key);
            Assert.Equal(10, list[2].Key);
            Assert.Equal(1, list[3].Key);
            Assert.Equal(3, list[4].Key);
            Assert.Equal(7, list[5].Key);
            Assert.Equal(13, list[6].Key);
            Assert.Equal(5, list[7].Key);
            Assert.Equal(9, list[8].Key);
            Assert.Equal(11, list[9].Key);
            Assert.Equal(24, list[10].Key);
            Assert.Equal(6, list[11].Key);
            Assert.Equal(8, list[12].Key);

            list.Clear();

            dictionary.Remove(7);

            /*
             *  Tree Should Look as Follows After Removal
             *
             *              4
             *      2               10
             *  1      3       6         13
             *              5      9  11    24
             *                  8
             */

            list = dictionary.AsLevelOrderList();
            foreach (KeyValuePair<int, int> node in list)
            {
                _testOutputHelper.WriteLine($"{node.Key} -> {node.Value}");
            }
            Assert.Equal(4, list[0].Key);
            Assert.Equal(2, list[1].Key);
            Assert.Equal(10, list[2].Key);
            Assert.Equal(1, list[3].Key);
            Assert.Equal(3, list[4].Key);
            Assert.Equal(6, list[5].Key);
            Assert.Equal(13, list[6].Key);
            Assert.Equal(5, list[7].Key);
            Assert.Equal(9, list[8].Key);
            Assert.Equal(11, list[9].Key);
            Assert.Equal(24, list[10].Key);
            Assert.Equal(8, list[11].Key);

            list.Clear();

            dictionary.Remove(10);

            list = dictionary.AsLevelOrderList();
            /*
             *  Tree Should Look as Follows After Removal
             *
             *              4
             *      2               9
             *  1      3       6         13
             *              5      8  11    24
             *
             */
            foreach (KeyValuePair<int, int> node in list)
            {
                _testOutputHelper.WriteLine($"{node.Key} -> {node.Value}");
            }
            Assert.Equal(4, list[0].Key);
            Assert.Equal(2, list[1].Key);
            Assert.Equal(9, list[2].Key);
            Assert.Equal(1, list[3].Key);
            Assert.Equal(3, list[4].Key);
            Assert.Equal(6, list[5].Key);
            Assert.Equal(13, list[6].Key);
            Assert.Equal(5, list[7].Key);
            Assert.Equal(8, list[8].Key);
            Assert.Equal(11, list[9].Key);
            Assert.Equal(24, list[10].Key);
        }

        [Fact]
        public void EnsureOverwriteIntegrity()
        {
            TreeDictionary<int, int> dictionary = new();

            Assert.Equal(0, dictionary.Count);

            dictionary.Add(2, 7);
            dictionary.Add(1, 4);
            dictionary.Add(10, 2);
            dictionary.Add(4, 1);
            dictionary.Add(3, 2);
            dictionary.Add(11, 2);
            dictionary.Add(5, 2);
            dictionary.Add(7, 2);
            dictionary.Add(9, 2);
            dictionary.Add(8, 2);
            dictionary.Add(13, 2);
            dictionary.Add(24, 2);
            dictionary.Add(6, 2);
            Assert.Equal(13, dictionary.Count);

            List<KeyValuePair<int, int>> list = dictionary.AsLevelOrderList();

            foreach (KeyValuePair<int, int> node in list)
            {
                _testOutputHelper.WriteLine($"{node.Key} -> {node.Value}");
            }

            /*
             *  Tree Should Look as Follows After Rotations
             *
             *              4
             *      2               10
             *  1      3       7         13
             *              5      9  11    24
             *                6  8
             */

            Assert.Equal(list.Count, dictionary.Count);
            Assert.Equal(4, list[0].Key);
            Assert.Equal(2, list[1].Key);
            Assert.Equal(10, list[2].Key);
            Assert.Equal(1, list[3].Key);
            Assert.Equal(3, list[4].Key);
            Assert.Equal(7, list[5].Key);
            Assert.Equal(13, list[6].Key);
            Assert.Equal(5, list[7].Key);
            Assert.Equal(9, list[8].Key);
            Assert.Equal(11, list[9].Key);
            Assert.Equal(24, list[10].Key);
            Assert.Equal(6, list[11].Key);
            Assert.Equal(8, list[12].Key);

            Assert.Equal(2, list[4].Value);

            dictionary.Add(3, 4);

            list = dictionary.AsLevelOrderList();

            Assert.Equal(4, list[4].Value);


            // Assure that none of the nodes locations have been modified.
            Assert.Equal(4, list[0].Key);
            Assert.Equal(2, list[1].Key);
            Assert.Equal(10, list[2].Key);
            Assert.Equal(1, list[3].Key);
            Assert.Equal(3, list[4].Key);
            Assert.Equal(7, list[5].Key);
            Assert.Equal(13, list[6].Key);
            Assert.Equal(5, list[7].Key);
            Assert.Equal(9, list[8].Key);
            Assert.Equal(11, list[9].Key);
            Assert.Equal(24, list[10].Key);
            Assert.Equal(6, list[11].Key);
            Assert.Equal(8, list[12].Key);
        }
    }
}
