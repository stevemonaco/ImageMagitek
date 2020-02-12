using ImageMagitek.Codec;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.UnitTests
{
    [TestFixture]
    public class BroadcastListTests
    {
        [TestCase(new int[] { 5 }, new int[] { 5 })]
        [TestCase(new int[] { 1, 2, 4, 5 }, new int[] { 1, 2, 4, 5 })]
        public void Add_AsExpected(int[] items, int[] expected)
        {
            var list = new BroadcastList<int>();

            foreach (var item in items)
                list.Add(item);

            CollectionAssert.AreEqual(expected, list);
        }

        [TestCase(new int[] { 5 }, 0, 5)]
        [TestCase(new int[] { 5 }, 3, 5)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, 0, 1)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, 2, 3)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, 4, 5)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, 5, 1)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, 7, 3)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, 9, 5)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, 102, 3)]
        public void Index_PositiveIndex_ReturnsExpected(int [] items, int index, int expected)
        {
            var list = new BroadcastList<int>(items);
            var actual = list[index];

            Assert.AreEqual(expected, actual);
        }

        [TestCase(new int[] { 5 }, -1, 5)]
        [TestCase(new int[] { 5 }, -3, 5)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, -1, 5)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, -5, 1)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, -8, 3)]
        [TestCase(new int[] { 1, 2, 3, 4, 5 }, -14, 2)]
        public void Index_NegativeIndex_ReturnsExpected(int[] items, int index, int expected)
        {
            var list = new BroadcastList<int>(items);
            var actual = list[index];

            Assert.AreEqual(expected, actual);
        }
    }
}
