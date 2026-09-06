/* ------------------------------------------------------------------------
 * (c)copyright Robert Ellison and contributors
 * Provided under the ms-PL license, see LICENSE.txt
 * https://github.com/abfo/shapefile
 * ------------------------------------------------------------------------ */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;

namespace Catfood.Shapefile
{
    /// <summary>A Shapefile MultiPointZ shape with optional measures.</summary>
    public class ShapeMultiPointZ : ShapeMultiPointM
    {
        /// <summary>Creates a Shapefile MultiPointZ shape from a record.</summary>
        /// <param name="recordNumber">The record number in the Shapefile.</param>
        /// <param name="metadata">Metadata about the shape (optional).</param>
        /// <param name="dataRecord">IDataRecord associated with the metadata.</param>
        /// <param name="shapeData">The shape record, including its eight-byte header.</param>
        /// <exception cref="ArgumentNullException">Thrown if shapeData is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the record layout is invalid.</exception>
        protected internal ShapeMultiPointZ(int recordNumber, StringDictionary metadata, IDataRecord dataRecord, byte[] shapeData)
            : this(recordNumber, metadata, dataRecord, ShapeRecordData.Parse(shapeData, false, true))
        {
        }

        private ShapeMultiPointZ(int recordNumber, StringDictionary metadata, IDataRecord dataRecord, ShapeRecordData data)
            : base(ShapeType.MultiPointZ, recordNumber, metadata, dataRecord, data)
        {
            Zmin = data.Zmin;
            Zmax = data.Zmax;
            Z = data.Z;
        }

        /// <summary>Minimum Z value.</summary>
        public double Zmin { get; protected set; }

        /// <summary>Maximum Z value.</summary>
        public double Zmax { get; protected set; }

        /// <summary>Z values in point order, concatenated across parts.</summary>
        public List<double> Z { get; protected set; }
    }
}
