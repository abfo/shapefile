using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Catfood.Shapefile;

namespace Catfood.Shapefile.UnitTests
{
    [TestClass]
    public class EndianBitConverterUnitTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToInt32ArgumentNullException()
        {
            EndianBitConverter.ToInt32(null, 0, ProvidedOrder.Big);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ToInt32ShortArrayException()
        {
            EndianBitConverter.ToInt32(new byte[1], 0, ProvidedOrder.Big);
        }

        [TestMethod]
        public void ToInt32LittleEndianSuccess()
        {
            Assert.AreEqual(42, EndianBitConverter.ToInt32(new byte[] { 42, 0, 0, 0 }, 0, ProvidedOrder.Little));
        }

        [TestMethod]
        public void ToInt32BigEndianSuccess()
        {
            Assert.AreEqual(42, EndianBitConverter.ToInt32(new byte[] { 0, 0, 0, 42 }, 0, ProvidedOrder.Big));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToDoubleArgumentNullException()
        {
            EndianBitConverter.ToDouble(null, 0, ProvidedOrder.Big);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ToDoublehortArrayException()
        {
            EndianBitConverter.ToDouble(new byte[1], 0, ProvidedOrder.Big);
        }

        [TestMethod]
        public void ToDoubleLittleEndianSuccess()
        {
            Assert.AreEqual(3.141592, EndianBitConverter.ToDouble(new byte[] { 122, 0, 139, 252, 250, 33, 9, 64 }, 0, ProvidedOrder.Little));
        }

        [TestMethod]
        public void ToDoubleBigEndianSuccess()
        {
            Assert.AreEqual(3.141592, EndianBitConverter.ToDouble(new byte[] { 64, 9, 33, 250, 252, 139, 0, 122 }, 0, ProvidedOrder.Big));
        }
    }
}
