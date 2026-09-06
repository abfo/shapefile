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
    /// <summary>A Shapefile PolygonM shape.</summary>
    public class ShapePolygonM : ShapePolygon
    {
        /// <summary>Creates a Shapefile PolygonM shape from a record.</summary>
        /// <param name="recordNumber">The record number in the Shapefile.</param>
        /// <param name="metadata">Metadata about the shape (optional).</param>
        /// <param name="dataRecord">IDataRecord associated with the metadata.</param>
        /// <param name="shapeData">The shape record, including its eight-byte header.</param>
        /// <exception cref="ArgumentNullException">Thrown if shapeData is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the record layout is invalid.</exception>
        protected internal ShapePolygonM(int recordNumber, StringDictionary metadata, IDataRecord dataRecord, byte[] shapeData)
            : this(ShapeType.PolygonM, recordNumber, metadata, dataRecord, ShapeRecordData.Parse(shapeData, true, false))
        {
        }

        internal ShapePolygonM(ShapeType type, int recordNumber, StringDictionary metadata, IDataRecord dataRecord,
            ShapeRecordData data) : base(type, recordNumber, metadata, dataRecord, data)
        {
            HasM = data.HasM;
            Mmin = data.Mmin;
            Mmax = data.Mmax;
            M = data.M;
        }

        /// <summary>Whether the optional measure block is present, including blocks containing no-data values.</summary>
        public bool HasM { get; protected set; }

        /// <summary>Minimum measure, or NaN when the measure block is absent.</summary>
        public double Mmin { get; protected set; }

        /// <summary>Maximum measure, or NaN when the measure block is absent.</summary>
        public double Mmax { get; protected set; }

        /// <summary>
        /// Measures in point order, concatenated across parts. Empty when absent.
        /// Values below -1e38 represent ESRI no-data and are preserved unchanged.
        /// </summary>
        public List<double> M { get; protected set; }
    }
}
