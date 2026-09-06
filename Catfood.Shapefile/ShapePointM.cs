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
    /// <summary>A Shapefile PointM shape.</summary>
    public class ShapePointM : ShapePoint
    {
        /// <summary>Creates a Shapefile PointM shape from a record.</summary>
        /// <param name="recordNumber">The record number in the Shapefile.</param>
        /// <param name="metadata">Metadata about the shape (optional).</param>
        /// <param name="dataRecord">IDataRecord associated with the metadata.</param>
        /// <param name="shapeData">The shape record, including its eight-byte header.</param>
        /// <exception cref="ArgumentNullException">Thrown if shapeData is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the record layout is invalid.</exception>
        protected internal ShapePointM(int recordNumber, StringDictionary metadata, IDataRecord dataRecord, byte[] shapeData)
            : this(ShapeType.PointM, recordNumber, metadata, dataRecord, ShapeRecordData.ParsePoint(shapeData, false))
        {
        }

        internal ShapePointM(ShapeType type, int recordNumber, StringDictionary metadata, IDataRecord dataRecord,
            ShapeRecordData data) : base(type, recordNumber, metadata, dataRecord, data)
        {
            HasM = data.HasM;
            M = HasM ? data.M[0] : double.NaN;
        }

        /// <summary>Whether a measure is present, including an ESRI no-data value.</summary>
        public bool HasM { get; protected set; }

        /// <summary>
        /// The measure, or NaN when absent. Values below -1e38 are ESRI no-data and are preserved unchanged.
        /// </summary>
        public double M { get; protected set; }
    }
}
