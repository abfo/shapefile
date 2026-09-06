/* ------------------------------------------------------------------------
 * (c)copyright Robert Ellison and contributors
 * Provided under the ms-PL license, see LICENSE.txt
 * https://github.com/abfo/shapefile
 * ------------------------------------------------------------------------ */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;

namespace Catfood.Shapefile.UnitTests
{
    [TestClass]
    public class ExtendedShapeUnitTests
    {
        private static readonly ShapeType[] Types = { ShapeType.PointM, ShapeType.PointZ,
            ShapeType.MultiPointM, ShapeType.MultiPointZ, ShapeType.PolyLineM, ShapeType.PolyLineZ,
            ShapeType.PolygonM, ShapeType.PolygonZ, ShapeType.MultiPatch };

        [TestMethod]
        public void FactoryReadsEveryExtendedTypeWithAndWithoutMeasures()
        {
            var metadata = new StringDictionary { { "name", "extended" } };
            using (var table = new DataTable())
            {
                table.Columns.Add("name");
                table.Rows.Add("extended");
                using (var reader = table.CreateDataReader())
                {
                    reader.Read();
                    foreach (ShapeType type in Types)
                    foreach (bool measures in new[] { false, true })
                    {
                        if (type == ShapeType.PointM && !measures) continue;
                        byte[] record = CreateRecord(type, measures);
                        Shape shape = ShapeFactory.ParseShape(record, metadata, reader, BoundingBoxConvention.YUp);
                        Assert.AreEqual("Shape" + type, shape.GetType().Name);
                        Assert.AreEqual(type, shape.Type);
                        Assert.AreEqual(7, shape.RecordNumber);
                        Assert.AreEqual("extended", shape.GetMetadata("name"));
                        Assert.AreSame(reader, shape.DataRecord);
                        Assert.AreEqual(measures, Property<bool>(shape, "HasM"));

                        if (shape is ShapePointM point)
                        {
                            Assert.AreEqual(new PointD(-4, 2), point.Point);
                            if (measures) Assert.AreEqual(-1e39, point.M);
                            else Assert.IsTrue(double.IsNaN(point.M));
                            if (point is ShapePointZ z) Assert.AreEqual(12.5, z.Z);
                        }
                        else
                        {
                            RectangleD bounds = Property<RectangleD>(shape, "BoundingBox");
                            Assert.AreEqual(-4.0, bounds.Left);
                            Assert.AreEqual(6.0, bounds.Right);
                            Assert.AreEqual(9.0, bounds.Top);
                            Assert.AreEqual(2.0, bounds.Bottom);
                            Shape legacy = ShapeFactory.ParseShape(record, metadata, reader);
                            Assert.AreEqual(2.0, Property<RectangleD>(legacy, "BoundingBox").Top);
                            Assert.AreEqual(9.0, Property<RectangleD>(legacy, "BoundingBox").Bottom);
                            PointD[] points;
                            if (shape is ShapeMultiPoint multi) points = multi.Points;
                            else
                            {
                                var parts = Property<System.Collections.Generic.List<PointD[]>>(shape, "Parts");
                                Assert.AreEqual(type == ShapeType.MultiPatch ? 6 : 2, parts.Count);
                                Assert.IsTrue(parts.All(part => part.Length == 5));
                                points = parts.SelectMany(part => part).ToArray();
                            }
                            for (int i = 0; i < points.Length; i++)
                                Assert.AreEqual(Vertices[i % 5], points[i]);
                            CollectionAssert.AreEqual(measures ? Measures(points.Length) : new double[0],
                                Property<ICollection>(shape, "M"));
                            if (measures)
                            {
                                Assert.AreEqual(1.5, Property<double>(shape, "Mmin"));
                                Assert.AreEqual(points.Length - 0.5, Property<double>(shape, "Mmax"));
                            }
                            else
                            {
                                Assert.IsTrue(double.IsNaN(Property<double>(shape, "Mmin")));
                                Assert.IsTrue(double.IsNaN(Property<double>(shape, "Mmax")));
                            }
                            if (HasZ(type))
                            {
                                CollectionAssert.AreEqual(Elevations(points.Length), Property<ICollection>(shape, "Z"));
                                Assert.AreEqual(10.0, Property<double>(shape, "Zmin"));
                                Assert.AreEqual(10.0 + points.Length - 1, Property<double>(shape, "Zmax"));
                            }
                            if (shape is ShapeMultiPatch patch)
                                CollectionAssert.AreEqual(Enum.GetValues(typeof(MultiPatchPartType)), patch.PartTypes);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void TruncatedAndTrailingDataAreRejectedExceptCompleteOptionalMeasures()
        {
            foreach (ShapeType type in Types)
            {
                byte[] full = CreateRecord(type, true);
                int omittedLength = type == ShapeType.PointM ? -1 : CreateRecord(type, false).Length;
                for (int length = 12; length < full.Length; length += 2)
                {
                    if (length == omittedLength) continue;
                    byte[] truncated = full.Take(length).ToArray();
                    SetLength(truncated);
                    Assert.ThrowsException<InvalidOperationException>(() => ShapeFactory.ParseShape(truncated, null, null),
                        type + " length " + length);
                }
                byte[] trailing = full.Concat(new byte[8]).ToArray();
                SetLength(trailing);
                Assert.ThrowsException<InvalidOperationException>(() => ShapeFactory.ParseShape(trailing, null, null));
            }
        }

        [TestMethod]
        public void InvalidCountsAndPartIndexesAreRejected()
        {
            foreach (ShapeType type in Types.Where(type => !IsPoint(type)))
            {
                foreach (int count in new[] { -1, int.MaxValue, 268435456 })
                {
                    byte[] record = CreateRecord(type, true);
                    SetInt(record, IsMultiPoint(type) ? 44 : 48, count);
                    Assert.ThrowsException<InvalidOperationException>(() => ShapeFactory.ParseShape(record, null, null));
                    if (!IsMultiPoint(type))
                    {
                        record = CreateRecord(type, true);
                        SetInt(record, 44, count);
                        Assert.ThrowsException<InvalidOperationException>(() => ShapeFactory.ParseShape(record, null, null));
                    }
                }
                if (IsMultiPoint(type)) continue;
                foreach (var edit in new[] { new[] { 52, -1 }, new[] { 52, 1 }, new[] { 56, 0 },
                    new[] { 56, -1 }, new[] { 56, int.MaxValue }, new[] { 56, type == ShapeType.MultiPatch ? 30 : 10 } })
                {
                    byte[] record = CreateRecord(type, true);
                    SetInt(record, edit[0], edit[1]);
                    Assert.ThrowsException<InvalidOperationException>(() => ShapeFactory.ParseShape(record, null, null));
                }
            }
            foreach (int partType in new[] { -1, 6 })
            {
                byte[] record = CreateRecord(ShapeType.MultiPatch, true);
                SetInt(record, 52 + 6 * 4, partType);
                Assert.ThrowsException<InvalidOperationException>(() => ShapeFactory.ParseShape(record, null, null));
            }
        }

        [TestMethod]
        public void EmptyCollectionsAndNoDataMeasuresArePreserved()
        {
            foreach (ShapeType type in Types.Where(type => !IsPoint(type)))
            foreach (bool measures in new[] { false, true })
            {
                Shape shape = ShapeFactory.ParseShape(CreateRecord(type, measures, true), null, null);
                Assert.AreEqual(measures, Property<bool>(shape, "HasM"));
                Assert.AreEqual(0, Property<ICollection>(shape, "M").Count);
                if (HasZ(type)) Assert.AreEqual(0, Property<ICollection>(shape, "Z").Count);
                Assert.AreEqual(0, Property<ICollection>(shape, IsMultiPoint(type) ? "Points" : "Parts").Count);
            }
            byte[] record = CreateRecord(ShapeType.PolyLineM, true);
            for (int offset = 52 + 8 + 160; offset < record.Length; offset += 8)
                Buffer.BlockCopy(BitConverter.GetBytes(-1e39), 0, record, offset, 8);
            var line = (ShapePolyLineM)ShapeFactory.ParseShape(record, null, null);
            Assert.IsTrue(line.HasM);
            Assert.AreEqual(-1e39, line.Mmin);
            Assert.AreEqual(-1e39, line.Mmax);
            Assert.IsTrue(line.M.All(value => value == -1e39));
        }

        [TestMethod]
        public void ExtendedShapesEnumerateFromFilesWithNullRecordsAndOptionalIndexes()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ExtendedShapes-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            try
            {
                foreach (ShapeType type in Types)
                foreach (bool index in new[] { false, true })
                {
                    string path = Path.Combine(directory, type + ".shp");
                    byte[] record = CreateRecord(type, type == ShapeType.PointM);
                    SetBigInt(record, 0, 1);
                    byte[] nullRecord = new byte[12];
                    SetBigInt(nullRecord, 0, 2);
                    SetLength(nullRecord);
                    byte[] header = new byte[100];
                    SetBigInt(header, 0, 9994);
                    SetBigInt(header, 24, (100 + record.Length + nullRecord.Length) / 2);
                    SetInt(header, 28, 1000);
                    SetInt(header, 32, (int)type);
                    File.WriteAllBytes(path, header.Concat(record).Concat(nullRecord).ToArray());
                    if (index)
                    {
                        SetBigInt(header, 24, 58);
                        byte[] entries = new byte[16];
                        SetBigInt(entries, 0, 50);
                        SetBigInt(entries, 4, (record.Length - 8) / 2);
                        SetBigInt(entries, 8, (100 + record.Length) / 2);
                        SetBigInt(entries, 12, 2);
                        File.WriteAllBytes(Path.ChangeExtension(path, "shx"), header.Concat(entries).ToArray());
                    }
                    using (var file = new Shapefile(path, BoundingBoxConvention.YUp))
                    {
                        Assert.AreEqual(2, file.Count);
                        Shape[] shapes = file.ToArray();
                        Assert.AreEqual(type, shapes[0].Type);
                        Assert.AreEqual(ShapeType.Null, shapes[1].Type);
                        Assert.AreEqual(2, shapes[1].RecordNumber);
                        if (!IsPoint(type)) Assert.AreEqual(9.0, Property<RectangleD>(shapes[0], "BoundingBox").Top);
                        using (var enumerator = file.GetEnumerator())
                        {
                            Assert.IsTrue(enumerator.MoveNext());
                            enumerator.Reset();
                            Assert.IsTrue(enumerator.MoveNext());
                            Assert.AreEqual(type, enumerator.Current.Type);
                        }
                    }
                }
            }
            finally { Directory.Delete(directory, true); }
        }

        // Reflection keeps shared assertions on the public API identical for each shape family.
        private static T Property<T>(Shape shape, string name) => (T)shape.GetType().GetProperty(name).GetValue(shape);
        private static bool IsPoint(ShapeType type) => type == ShapeType.PointM || type == ShapeType.PointZ;
        private static bool IsMultiPoint(ShapeType type) => type == ShapeType.MultiPointM || type == ShapeType.MultiPointZ;
        private static bool HasZ(ShapeType type) => type == ShapeType.PointZ || type == ShapeType.MultiPointZ ||
            type == ShapeType.PolyLineZ || type == ShapeType.PolygonZ || type == ShapeType.MultiPatch;
        private static readonly PointD[] Vertices = { new PointD(-4, 2), new PointD(-4, 9), new PointD(6, 9),
            new PointD(6, 2), new PointD(-4, 2) };
        private static double[] Measures(int count) => Enumerable.Range(0, count).Select(i => i == 0 ? -1e39 : i + 0.5).ToArray();
        private static double[] Elevations(int count) => Enumerable.Range(0, count).Select(i => 10.0 + i).ToArray();

        private static byte[] CreateRecord(ShapeType type, bool measures, bool empty = false)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new byte[8]);
                writer.Write((int)type);
                if (IsPoint(type))
                {
                    writer.Write(-4.0); writer.Write(2.0);
                    if (HasZ(type)) writer.Write(12.5);
                    if (measures) writer.Write(-1e39);
                }
                else
                {
                    int parts = empty ? 0 : type == ShapeType.MultiPatch ? 6 : 2;
                    int count = parts * 5;
                    writer.Write(-4.0); writer.Write(2.0); writer.Write(6.0); writer.Write(9.0);
                    if (!IsMultiPoint(type)) writer.Write(parts);
                    writer.Write(count);
                    if (!IsMultiPoint(type))
                        for (int i = 0; i < parts; i++) writer.Write(i * 5);
                    if (type == ShapeType.MultiPatch)
                        for (int i = 0; i < parts; i++) writer.Write(i);
                    for (int i = 0; i < count; i++)
                    {
                        writer.Write(Vertices[i % 5].X); writer.Write(Vertices[i % 5].Y);
                    }
                    if (HasZ(type))
                    {
                        writer.Write(10.0); writer.Write(10.0 + count - 1);
                        foreach (double z in Elevations(count)) writer.Write(z);
                    }
                    if (measures)
                    {
                        writer.Write(1.5); writer.Write(count - 0.5);
                        foreach (double m in Measures(count)) writer.Write(m);
                    }
                }
                byte[] record = stream.ToArray();
                SetBigInt(record, 0, 7);
                SetLength(record);
                return record;
            }
        }

        private static void SetLength(byte[] record) => SetBigInt(record, 4, (record.Length - 8) / 2);
        private static void SetInt(byte[] bytes, int offset, int value)
        {
            for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (8 * i));
        }
        private static void SetBigInt(byte[] bytes, int offset, int value)
        {
            for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (24 - 8 * i));
        }
    }
}
