using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;

namespace Catfood.Shapefile.UnitTests
{
    [TestClass]
    public class BoundingBoxConventionUnitTests
    {
        [DataTestMethod]
        [DataRow(ShapeType.MultiPoint)]
        [DataRow(ShapeType.PolyLine)]
        [DataRow(ShapeType.PolyLineM)]
        [DataRow(ShapeType.Polygon)]
        public void ShapeBoundsRespectConventionWithoutChangingData(ShapeType type)
        {
            foreach (var ys in new[] { new[] { 2.0, 9.0 }, new[] { -9.0, -2.0 }, new[] { -3.0, -3.0 } })
            {
                byte[] record = CreateRecord(type, ys[0], ys[1]);
                var metadata = new StringDictionary { { "name", "test" } };
                Shape legacy = ShapeFactory.ParseShape(record, metadata, null);
                Shape yUp = ShapeFactory.ParseShape(record, metadata, null, BoundingBoxConvention.YUp);
                AssertBounds(GetBounds(legacy), -4, ys[0], 6, ys[1]);
                AssertBounds(GetBounds(yUp), -4, ys[1], 6, ys[0]);
                // Reading repeatedly must not swap the stored edges back and forth.
                AssertBounds(GetBounds(yUp), -4, ys[1], 6, ys[0]);
                CollectionAssert.AreEqual(GetPoints(legacy), GetPoints(yUp));
                Assert.AreEqual("test", yUp.GetMetadata("name"));
                Assert.AreEqual(legacy.RecordNumber, yUp.RecordNumber);
                Assert.AreEqual(legacy.Type, yUp.Type);
                if (type == ShapeType.PolyLineM)
                    CollectionAssert.AreEqual(((ShapePolyLineM)legacy).M, ((ShapePolyLineM)yUp).M);
            }
        }

        [DataTestMethod]
        [DataRow("PAN_water_areas_dcw.shp")]
        [DataRow("PAN_water_lines_dcw.shp")]
        public void FileAndEnumeratedBoundsUseIndependentConventions(string filename)
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "TestData", filename);
            using (var legacy = new Shapefile(path))
            using (var yUp = new Shapefile(path, Shapefile.ConnectionStringTemplateJet, BoundingBoxConvention.YUp))
            using (var configured = new Shapefile())
            {
                configured.BoundingBoxConvention = BoundingBoxConvention.YUp;
                configured.Open(path);
                var expected = legacy.BoundingBox;
                AssertBounds(yUp.BoundingBox, expected.Left, expected.Bottom, expected.Right, expected.Top);
                Assert.AreEqual(yUp.BoundingBox, configured.BoundingBox);
                Assert.AreEqual(BoundingBoxConvention.Legacy, legacy.BoundingBoxConvention);
                Assert.ThrowsException<InvalidOperationException>(() => yUp.BoundingBoxConvention = BoundingBoxConvention.Legacy);
                Assert.AreEqual(legacy.Count, yUp.Count);
                using (var a = legacy.GetEnumerator())
                using (var b = yUp.GetEnumerator())
                {
                    while (a.MoveNext())
                    {
                        Assert.IsTrue(b.MoveNext());
                        Shape first = a.Current;
                        Shape second = b.Current;
                        var bounds = GetBounds(first);
                        AssertBounds(GetBounds(second), bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
                        CollectionAssert.AreEqual(GetPoints(first), GetPoints(second));
                        foreach (string name in first.GetMetadataNames())
                            Assert.AreEqual(first.GetMetadata(name), second.GetMetadata(name));
                    }
                    Assert.IsFalse(b.MoveNext());
                    b.Reset();
                    Assert.IsTrue(b.MoveNext());
                    Assert.IsTrue(GetBounds(b.Current).Top >= GetBounds(b.Current).Bottom);
                }
            }
        }

        [TestMethod]
        public void ConfigurationAndRectangleConstructorPreserveContracts()
        {
            using (var file = new Shapefile(null, BoundingBoxConvention.YUp))
            {
                Assert.AreEqual(BoundingBoxConvention.YUp, file.BoundingBoxConvention);
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => file.BoundingBoxConvention = (BoundingBoxConvention)99);
                Assert.AreEqual(BoundingBoxConvention.YUp, file.BoundingBoxConvention);
                Assert.ThrowsException<InvalidOperationException>(() => { var bounds = file.BoundingBox; });
                file.Dispose();
                Assert.ThrowsException<ObjectDisposedException>(() => file.BoundingBoxConvention = BoundingBoxConvention.Legacy);
            }
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Shapefile(null, (BoundingBoxConvention)99));
            AssertBounds(new RectangleD(-4, 2, 6, 9), -4, 2, 6, 9);
        }

        private static RectangleD GetBounds(Shape shape)
        {
            if (shape is ShapeMultiPoint) return ((ShapeMultiPoint)shape).BoundingBox;
            if (shape is ShapePolygon) return ((ShapePolygon)shape).BoundingBox;
            return ((ShapePolyLine)shape).BoundingBox;
        }

        private static PointD[] GetPoints(Shape shape)
        {
            if (shape is ShapeMultiPoint) return ((ShapeMultiPoint)shape).Points;
            var parts = shape is ShapePolygon ? ((ShapePolygon)shape).Parts : ((ShapePolyLine)shape).Parts;
            return System.Linq.Enumerable.ToArray(System.Linq.Enumerable.SelectMany(parts, part => part));
        }

        private static void AssertBounds(RectangleD actual, double left, double top, double right, double bottom)
        {
            Assert.AreEqual(left, actual.Left);
            Assert.AreEqual(top, actual.Top);
            Assert.AreEqual(right, actual.Right);
            Assert.AreEqual(bottom, actual.Bottom);
        }

        private static byte[] CreateRecord(ShapeType type, double ymin, double ymax)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new byte[] { 0, 0, 0, 1 }); // Big-endian record number.
                writer.Write(0); // Content length, filled below.
                writer.Write((int)type);
                writer.Write(-4.0); writer.Write(ymin); writer.Write(6.0); writer.Write(ymax);
                if (type != ShapeType.MultiPoint) writer.Write(1); // One part.
                writer.Write(5);
                if (type != ShapeType.MultiPoint) writer.Write(0); // First point index.
                foreach (var point in new[] { new PointD(-4, ymin), new PointD(-4, ymax),
                    new PointD(6, ymax), new PointD(6, ymin), new PointD(-4, ymin) })
                {
                    writer.Write(point.X); writer.Write(point.Y);
                }
                if (type == ShapeType.PolyLineM)
                {
                    writer.Write(0.0); writer.Write(4.0);
                    for (int i = 0; i < 5; i++) writer.Write((double)i);
                }
                byte[] bytes = stream.ToArray();
                int words = (bytes.Length - 8) / 2;
                for (int i = 0; i < 4; i++) bytes[4 + i] = (byte)(words >> (24 - 8 * i));
                return bytes;
            }
        }
    }
}
