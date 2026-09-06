/* ------------------------------------------------------------------------
 * (c)copyright Robert Ellison and contributors
 * Provided under the ms-PL license, see LICENSE.txt
 * https://github.com/abfo/shapefile
 * ------------------------------------------------------------------------ */

using System;
using System.Collections.Generic;

namespace Catfood.Shapefile
{
    // Shared binary layout for measured and Z records. All offsets include the
    // eight-byte record header. Validate sizes before allocating or reading arrays.
    internal sealed class ShapeRecordData
    {
        internal PointD Point;
        internal RectangleD BoundingBox;
        internal PointD[] Points;
        internal List<PointD[]> Parts;
        internal MultiPatchPartType[] PartTypes;
        internal double Zmin, Zmax;
        internal double Mmin = double.NaN, Mmax = double.NaN;
        internal List<double> Z = new List<double>();
        internal List<double> M = new List<double>();
        internal bool HasM;

        internal static ShapeRecordData ParsePoint(byte[] shapeData, bool hasZ)
        {
            if (shapeData == null) throw new ArgumentNullException("shapeData");
            // PointM requires M. PointZ permits the optional M coordinate to be absent.
            if (hasZ ? shapeData.Length != 36 && shapeData.Length != 44 : shapeData.Length != 36)
                throw new InvalidOperationException("Invalid shape data");

            var result = new ShapeRecordData();
            result.Point = ReadPoint(shapeData, 12);
            if (hasZ) result.Z.Add(ReadDouble(shapeData, 28));
            result.HasM = !hasZ || shapeData.Length == 44;
            if (result.HasM) result.M.Add(ReadDouble(shapeData, hasZ ? 36 : 28));
            return result;
        }

        internal static ShapeRecordData Parse(byte[] shapeData, bool multipart, bool hasZ, bool multiPatch = false)
        {
            if (shapeData == null) throw new ArgumentNullException("shapeData");
            int headerSize = multipart ? 52 : 48;
            if (shapeData.Length < headerSize) throw new InvalidOperationException("Invalid shape data");

            int numParts = multipart ? ReadInt(shapeData, 44) : 0;
            int numPoints = ReadInt(shapeData, multipart ? 48 : 44);
            if (numParts < 0 || numPoints < 0 || (multipart && ((numParts == 0) != (numPoints == 0))))
                throw new InvalidOperationException("Invalid shape counts");

            long pointsOffset = headerSize + (multiPatch ? 8L : 4L) * numParts;
            long xyEnd = pointsOffset + 16L * numPoints;
            long dimensionSize = 16L + 8L * numPoints;
            long requiredSize = xyEnd + (hasZ ? dimensionSize : 0);
            if (shapeData.Length != requiredSize && shapeData.Length != requiredSize + dimensionSize)
                throw new InvalidOperationException("Invalid shape data");

            var result = new ShapeRecordData();
            result.HasM = shapeData.Length != requiredSize;
            result.BoundingBox = new RectangleD(ReadDouble(shapeData, 12), ReadDouble(shapeData, 20),
                ReadDouble(shapeData, 28), ReadDouble(shapeData, 36));

            if (multipart)
            {
                // Validate the full index table before allocating any point arrays.
                int previous = -1;
                for (int i = 0; i < numParts; i++)
                {
                    int start = ReadInt(shapeData, 52 + 4 * i);
                    if (start < 0 || start >= numPoints || (i == 0 && start != 0) || start <= previous)
                        throw new InvalidOperationException("Invalid shape part index");
                    previous = start;
                }
                if (multiPatch)
                {
                    result.PartTypes = new MultiPatchPartType[numParts];
                    for (int i = 0; i < numParts; i++)
                    {
                        int type = ReadInt(shapeData, 52 + 4 * numParts + 4 * i);
                        if (type < 0 || type > 5) throw new InvalidOperationException("Invalid MultiPatch part type");
                        result.PartTypes[i] = (MultiPatchPartType)type;
                    }
                }
                result.Parts = new List<PointD[]>(numParts);
                for (int i = 0; i < numParts; i++)
                {
                    int start = ReadInt(shapeData, 52 + 4 * i);
                    int end = i + 1 == numParts ? numPoints : ReadInt(shapeData, 56 + 4 * i);
                    var points = new PointD[end - start];
                    for (int j = 0; j < points.Length; j++)
                        points[j] = ReadPoint(shapeData, (int)pointsOffset + 16 * (start + j));
                    result.Parts.Add(points);
                }
            }
            else
            {
                result.Points = new PointD[numPoints];
                for (int i = 0; i < numPoints; i++)
                    result.Points[i] = ReadPoint(shapeData, (int)pointsOffset + 16 * i);
            }

            if (hasZ)
                result.Z = ReadDimension(shapeData, (int)xyEnd, numPoints, out result.Zmin, out result.Zmax);
            if (result.HasM)
                result.M = ReadDimension(shapeData, (int)requiredSize, numPoints, out result.Mmin, out result.Mmax);
            return result;
        }

        private static List<double> ReadDimension(byte[] data, int offset, int count, out double min, out double max)
        {
            min = ReadDouble(data, offset);
            max = ReadDouble(data, offset + 8);
            var values = new List<double>(count);
            for (int i = 0; i < count; i++) values.Add(ReadDouble(data, offset + 16 + 8 * i));
            return values;
        }

        private static PointD ReadPoint(byte[] data, int offset)
        {
            return new PointD(ReadDouble(data, offset), ReadDouble(data, offset + 8));
        }

        private static double ReadDouble(byte[] data, int offset)
        {
            return EndianBitConverter.ToDouble(data, offset, ProvidedOrder.Little);
        }

        private static int ReadInt(byte[] data, int offset)
        {
            return EndianBitConverter.ToInt32(data, offset, ProvidedOrder.Little);
        }
    }
}
