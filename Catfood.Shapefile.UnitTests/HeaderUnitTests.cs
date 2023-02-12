using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Catfood.Shapefile;

namespace Catfood.Shapefile.UnitTests
{
    [TestClass]
    public class HeaderUnitTests
    {
        static byte[] _goodHeader = new byte[] { 0, 0, 39, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 170, 232, 3, 0, 0, 5, 0, 0, 0, 40, 66, 144, 255, 50, 172, 84, 192, 128, 92, 162, 157, 150, 63, 32, 64, 160, 109, 174, 161, 203, 129, 83, 192, 128, 105, 177, 159, 51, 9, 35, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        static byte[] _badFileCode = new byte[] { 1, 0, 39, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 170, 232, 3, 0, 0, 5, 0, 0, 0, 40, 66, 144, 255, 50, 172, 84, 192, 128, 92, 162, 157, 150, 63, 32, 64, 160, 109, 174, 161, 203, 129, 83, 192, 128, 105, 177, 159, 51, 9, 35, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        static byte[] _badFileVersion = new byte[] { 0, 0, 39, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 170, 232, 4, 0, 0, 5, 0, 0, 0, 40, 66, 144, 255, 50, 172, 84, 192, 128, 92, 162, 157, 150, 63, 32, 64, 160, 109, 174, 161, 203, 129, 83, 192, 128, 105, 177, 159, 51, 9, 35, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void NullHeaderArgumentNullException()
        {
            Header header = new Header(null);
        }

        [ExpectedException(typeof(InvalidOperationException))]
        [TestMethod]
        public void ShortHeaderInvalidOperationException()
        {
            Header header = new Header(new byte[4]);
        }

        [ExpectedException(typeof(InvalidOperationException))]
        [TestMethod]
        public void BadFileCodeInvalidOperationException()
        {
            Header header = new Header(_badFileCode);
        }

        [ExpectedException(typeof(InvalidOperationException))]
        [TestMethod]
        public void BadFileVersionInvalidOperationException()
        {
            Header header = new Header(_badFileVersion);
        }

        [TestMethod]
        public void GoodHeaderSuccess()
        {
            Header header = new Header(_goodHeader);
            Assert.AreEqual(ShapeType.Polygon, header.ShapeType);
            Assert.AreEqual(1000, header.Version);
            Assert.AreEqual(9994, header.FileCode);
            Assert.AreEqual(170, header.FileLength);
            
            Assert.AreEqual(-82.6906126889013, header.XMin, 0.001);
            Assert.AreEqual(8.12419598204565, header.YMin, 0.001);
            Assert.AreEqual(-78.0280536845589, header.XMax, 0.001);
            Assert.AreEqual(9.51797198334384, header.YMax, 0.001);

            Assert.AreEqual(0, header.MMin);
            Assert.AreEqual(0, header.MMax);
            Assert.AreEqual(0, header.ZMin);
            Assert.AreEqual(0, header.ZMax);
        }
    }
}
