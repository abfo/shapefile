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
    /// <summary>A Shapefile MultiPatch surface with Z coordinates and optional measures.</summary>
    public class ShapeMultiPatch : Shape
    {
        private RectangleD _boundingBox;

        /// <summary>Creates a Shapefile MultiPatch shape from a record.</summary>
        /// <param name="recordNumber">The record number in the Shapefile.</param>
        /// <param name="metadata">Metadata about the shape (optional).</param>
        /// <param name="dataRecord">IDataRecord associated with the metadata.</param>
        /// <param name="shapeData">The shape record, including its eight-byte header.</param>
        /// <exception cref="ArgumentNullException">Thrown if shapeData is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the record layout is invalid.</exception>
        protected internal ShapeMultiPatch(int recordNumber, StringDictionary metadata, IDataRecord dataRecord, byte[] shapeData)
            : base(ShapeType.MultiPatch, recordNumber, metadata, dataRecord)
        {
            ShapeRecordData data = ShapeRecordData.Parse(shapeData, true, true, true);
            _boundingBox = data.BoundingBox;
            Parts = data.Parts;
            PartTypes = data.PartTypes;
            Zmin = data.Zmin;
            Zmax = data.Zmax;
            Z = data.Z;
            HasM = data.HasM;
            Mmin = data.Mmin;
            Mmax = data.Mmax;
            M = data.M;
        }

        /// <summary>Gets the XY bounding box using the configured convention.</summary>
        public RectangleD BoundingBox
        {
            get { return _boundingBox.WithConvention(BoundingBoxConvention); }
        }

        /// <summary>Surface parts in file order, each containing an array of XY vertices.</summary>
        public List<PointD[]> Parts { get; protected set; }

        /// <summary>The surface type for each entry in Parts, in the same order.</summary>
        public MultiPatchPartType[] PartTypes { get; protected set; }

        /// <summary>Minimum Z value.</summary>
        public double Zmin { get; protected set; }

        /// <summary>Maximum Z value.</summary>
        public double Zmax { get; protected set; }

        /// <summary>Z values in point order, concatenated across parts.</summary>
        public List<double> Z { get; protected set; }

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
