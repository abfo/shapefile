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
    /// <summary>A Shapefile PointZ shape with an optional measure.</summary>
    public class ShapePointZ : ShapePointM
    {
        /// <summary>Creates a Shapefile PointZ shape from a record.</summary>
        /// <param name="recordNumber">The record number in the Shapefile.</param>
        /// <param name="metadata">Metadata about the shape (optional).</param>
        /// <param name="dataRecord">IDataRecord associated with the metadata.</param>
        /// <param name="shapeData">The shape record, including its eight-byte header.</param>
        /// <exception cref="ArgumentNullException">Thrown if shapeData is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the record layout is invalid.</exception>
        protected internal ShapePointZ(int recordNumber, StringDictionary metadata, IDataRecord dataRecord, byte[] shapeData)
            : this(recordNumber, metadata, dataRecord, ShapeRecordData.ParsePoint(shapeData, true))
        {
        }

        private ShapePointZ(int recordNumber, StringDictionary metadata, IDataRecord dataRecord, ShapeRecordData data)
            : base(ShapeType.PointZ, recordNumber, metadata, dataRecord, data)
        {
            Z = data.Z[0];
        }

        /// <summary>The Z coordinate.</summary>
        public double Z { get; protected set; }
    }
}
