using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Catfood.Shapefile.UnitTests
{
    [TestClass]
    public class MetadataUnitTests
    {
        private string _directory;
        private string _path;

        [TestInitialize]
        public void CreateFixture()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ShapefileTests-" + Guid.NewGuid());
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "long shapefile name.shp");
            WriteShapes();
            WriteMetadata(3);
        }

        [TestCleanup]
        public void DeleteFixture()
        {
            Directory.Delete(_directory, true);
        }

        [TestMethod]
        public void LongFilenameAndDeletedRowsPreserveShapeMetadataAlignment()
        {
            using (var shapefile = new Shapefile(_path))
            {
                var shapes = shapefile.ToList();
                Assert.AreEqual(3, shapes.Count);
                for (int i = 0; i < shapes.Count; i++)
                {
                    Assert.AreEqual(i + 1, shapes[i].RecordNumber);
                    Assert.AreEqual("row" + (i + 1), shapes[i].GetMetadata("name"));
                    Assert.AreEqual(i + 1.0, ((ShapePoint)shapes[i]).Point.X);
                }
                Assert.AreEqual("", shapes[2].GetMetadata("amount"));
            }
        }

        [TestMethod]
        public void RawMetadataProvidesTypedValuesWithoutDictionary()
        {
            using (var shapefile = new Shapefile(_path) { RawMetadataOnly = true })
            using (var enumerator = shapefile.GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                var shape = enumerator.Current;
                Assert.IsNull(shape.GetMetadataNames());
                Assert.IsNull(shape.GetMetadata("name"));
                var record = shape.DataRecord;
                Assert.AreEqual(2, record.FieldCount);
                Assert.AreEqual("row1", record.GetString(record.GetOrdinal("NAME")));
                Assert.AreEqual(12.5m, Convert.ToDecimal(record.GetValue(record.GetOrdinal("AMOUNT"))));
                Assert.IsTrue(enumerator.MoveNext());
                Assert.IsTrue(enumerator.MoveNext());
                Assert.IsTrue(enumerator.Current.DataRecord.IsDBNull(1));
                Assert.IsFalse(enumerator.MoveNext());
                Assert.IsFalse(enumerator.MoveNext());
            }
        }

        [TestMethod]
        public void ResetAndRepeatedEnumerationStartAtFirstRecord()
        {
            using (var shapefile = new Shapefile(_path))
            {
                using (var enumerator = shapefile.GetEnumerator())
                {
                    Assert.IsTrue(enumerator.MoveNext());
                    Assert.IsTrue(enumerator.MoveNext());
                    enumerator.Reset();
                    Assert.IsTrue(enumerator.MoveNext());
                    Assert.AreEqual("row1", enumerator.Current.GetMetadata("name"));
                    while (enumerator.MoveNext()) { }
                    enumerator.Reset();
                    Assert.IsTrue(enumerator.MoveNext());
                    Assert.AreEqual(1, enumerator.Current.RecordNumber);
                }
                Assert.AreEqual(3, shapefile.ToList().Count);
                Assert.AreEqual(3, shapefile.ToList().Count);
            }
        }

        [TestMethod]
        public void MissingMetadataRowThrowsInsteadOfReusingPreviousRow()
        {
            WriteMetadata(2);
            using (var shapefile = new Shapefile(_path))
            using (var enumerator = shapefile.GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.IsTrue(enumerator.MoveNext());
                Assert.ThrowsException<InvalidOperationException>(() => enumerator.MoveNext());
            }
        }

        [TestMethod]
        public void EarlyEnumerationDisposalReleasesFiles()
        {
            using (var shapefile = new Shapefile(_path))
            {
                foreach (var shape in shapefile) { break; }
            }
            foreach (string extension in new[] { "shp", "shx", "dbf" })
            {
                using (File.Open(Path.ChangeExtension(_path, extension), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
        }

        [TestMethod]
        public void DisposingShapefileClosesActiveEnumerators()
        {
            var shapefile = new Shapefile(_path);
            var enumerator = shapefile.GetEnumerator();
            Assert.IsTrue(enumerator.MoveNext());
            shapefile.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => enumerator.MoveNext());
            enumerator.Dispose();
            using (File.Open(Path.ChangeExtension(_path, "dbf"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }

        [TestMethod]
        public void MalformedMetadataOpenReleasesFilesAndAllowsRetry()
        {
            File.WriteAllBytes(Path.ChangeExtension(_path, "dbf"), new byte[0]);
            using (var shapefile = new Shapefile())
            {
                bool failed = false;
                try { shapefile.Open(_path); }
                catch (Exception) { failed = true; }
                Assert.IsTrue(failed, "An empty DBF must fail to open.");
                foreach (string extension in new[] { "shp", "shx", "dbf" })
                {
                    using (File.Open(Path.ChangeExtension(_path, extension), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                }
                WriteMetadata(3);
                shapefile.Open(_path);
                Assert.AreEqual(3, shapefile.ToList().Count);
            }
        }

        [TestMethod]
        public void HeaderEncodingAndLeadingSpacesArePreserved()
        {
            string dbfPath = Path.ChangeExtension(_path, "dbf");
            var bytes = File.ReadAllBytes(dbfPath);
            bytes[29] = 0x03; // Windows-1252 language driver
            new byte[] { 32, 99, 97, 102, 233, 32, 32, 32, 32, 32 }.CopyTo(bytes, 98);
            File.WriteAllBytes(dbfPath, bytes);
            using (var shapefile = new Shapefile(_path))
            using (var enumerator = shapefile.GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(" café", enumerator.Current.GetMetadata("name"));
            }
        }

        [DataTestMethod]
        [DataRow("PAN_water_areas_dcw.shp", 30, "country")]
        [DataRow("PAN_water_lines_dcw.shp", 757, "name_0")]
        public void EnumeratesAllSampleShapes(string filename, int expectedCount, string countryField)
        {
            using (var shapefile = new Shapefile(Path.Combine(AppContext.BaseDirectory, "TestData", filename)))
            {
                int count = 0;
                foreach (var shape in shapefile)
                {
                    Assert.AreEqual(++count, shape.RecordNumber);
                    Assert.AreEqual("Panama", shape.GetMetadata(countryField));
                }
                Assert.AreEqual(expectedCount, count);
            }
        }

        private void WriteShapes()
        {
            using (var shp = new BinaryWriter(File.Create(_path)))
            using (var shx = new BinaryWriter(File.Create(Path.ChangeExtension(_path, "shx"))))
            {
                WriteHeader(shp, 92);
                WriteHeader(shx, 62);
                for (int i = 0; i < 3; i++)
                {
                    WriteBigEndian(shx, 50 + i * 14);
                    WriteBigEndian(shx, 10);
                    WriteBigEndian(shp, i + 1);
                    WriteBigEndian(shp, 10);
                    shp.Write(1); // Point
                    shp.Write(i + 1.0);
                    shp.Write(0.0);
                }
            }
        }

        private static void WriteHeader(BinaryWriter writer, int length)
        {
            WriteBigEndian(writer, 9994);
            writer.Write(new byte[20]);
            WriteBigEndian(writer, length);
            writer.Write(1000);
            writer.Write(1);
            writer.Write(1.0);
            writer.Write(0.0);
            writer.Write(3.0);
            writer.Write(0.0);
            writer.Write(new byte[32]);
        }

        private static void WriteBigEndian(BinaryWriter writer, int value)
        {
            writer.Write(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
        }

        private void WriteMetadata(int count)
        {
            using (var writer = new BinaryWriter(File.Create(Path.ChangeExtension(_path, "dbf"))))
            {
                writer.Write(new byte[] { 3, 126, 9, 6 });
                writer.Write(count);
                writer.Write((short)97);
                writer.Write((short)19);
                writer.Write(new byte[20]);
                WriteColumn(writer, "NAME", 'C', 10, 0);
                WriteColumn(writer, "AMOUNT", 'N', 8, 2);
                writer.Write((byte)13);
                for (int i = 0; i < count; i++)
                {
                    writer.Write((byte)(i == 1 ? '*' : ' '));
                    writer.Write(Encoding.ASCII.GetBytes(("row" + (i + 1)).PadRight(10)));
                    writer.Write(Encoding.ASCII.GetBytes((i == 2 ? "" : "12.50").PadLeft(8)));
                }
                writer.Write((byte)26);
            }
        }

        private static void WriteColumn(BinaryWriter writer, string name, char type, byte length, byte decimals)
        {
            var descriptor = new byte[32];
            Encoding.ASCII.GetBytes(name).CopyTo(descriptor, 0);
            descriptor[11] = (byte)type;
            descriptor[16] = length;
            descriptor[17] = decimals;
            writer.Write(descriptor);
        }
    }
}
