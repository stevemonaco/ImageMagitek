﻿using System.Drawing;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.UnitTests.Helpers;
using NUnit.Framework;

namespace ImageMagitek.UnitTests
{
    [TestFixture]
    public class ElementCopierTests
    {
        ScatteredArranger _sourceIndexed;
        DataFile _df;

        [OneTimeSetUp]
        public void Setup()
        {
            _df = new DataFile("sourceDataFile", new MemoryStream());
            _sourceIndexed = new ScatteredArranger("source", PixelColorType.Indexed, ArrangerLayout.Tiled, 6, 6, 8, 8);

            for (int y = 0; y < _sourceIndexed.ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < _sourceIndexed.ArrangerElementSize.Width; x++)
                {
                    if (_sourceIndexed.GetElement(x, y) is ArrangerElement element)
                    {
                        element = element.WithTarget(_df, new FileBitAddress(x * y), new Snes3bppCodec(8, 8), null);
                        _sourceIndexed.SetElement(element, x, y);
                    }
                }
            }
        }

        //[Test]
        //public void CopyElements_ValidIndexedToIndexed_ReturnsTrue()
        //{
        //    ScatteredArranger dest = new ScatteredArranger("dest", PixelColorType.Indexed, ArrangerLayout.Tiled, 4, 4, 8, 8);

        //    ElementCopier.CopyElements(sourceIndexed, dest, new Point(2, 2), new Point(0, 0), 4, 4);
        //    var sourceItems = sourceIndexed.EnumerateElements(2, 2, 4, 4).ToList();
        //    var destItems = dest.EnumerateElements().ToList();

        //    CollectionAssert.AreEqual(sourceItems, destItems, new ElementWithoutLocationComparer());
        //}

        //public void CanCopyElements_IndexedToDirect_ReturnsFalse()
        //{
        //    ScatteredArranger dest = new ScatteredArranger("dest", ArrangerLayout.Tiled, 4, 4, 8, 8);

        //    var actual = ElementCopier.CanCopyElements(sourceIndexed, dest, new Point(2, 2), new Point(0, 0), 4, 4);

        //    Assert.IsFalse(actual);
        //}
    }
}
