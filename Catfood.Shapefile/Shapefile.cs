/* ------------------------------------------------------------------------
 * (c)copyright Robert Ellison and contributors 
 * Provided under the ms-PL license, see LICENSE.txt
 * https://github.com/abfo/shapefile
 * ------------------------------------------------------------------------ */

using System;
using System.Collections.Generic;
using System.IO;

namespace Catfood.Shapefile
{
    /// <summary>
    /// Provides a readonly IEnumerable interface to an ERSI Shapefile.
    /// NOTE - has not been designed to be thread safe
    /// </summary>
    /// <remarks>
    /// See the ESRI Shapefile specification at http://www.esri.com/library/whitepapers/pdfs/shapefile.pdf
    /// </remarks>
    public class Shapefile : IDisposable, IEnumerable<Shape>
    {
        private const string MainPathExtension = "shp";
        private const string IndexPathExtension = "shx";
        private const string DbasePathExtension = "dbf";

        private bool _disposed;
        private bool _opened;
        private bool _rawMetadataOnly;
        private BoundingBoxConvention _boundingBoxConvention = BoundingBoxConvention.YUp;
        private int _count;
        private RectangleD _boundingBox;
        private ShapeType _type;
        private string _shapefileMainPath;
        private string _shapefileIndexPath;
        private string _shapefileDbasePath;
        private FileStream _mainStream;
        private FileStream _indexStream;
        private Header _mainHeader;
        private Header _indexHeader;
        private List<long> _recordOffsets;
        private readonly HashSet<ShapeFileEnumerator> _enumerators = new HashSet<ShapeFileEnumerator>();

        /// <summary>
        /// Create a new Shapefile object.
        /// </summary>
        public Shapefile()
        { }

        /// <summary>
        /// Open a Shapefile. Only the main file (.shp) is required.
        /// The index (.shx) and dBASE table (.dbf) are optional. Companion files
        /// must all have the same filename (i.e. shapes.shp, shapes.shx and shapes.dbf). Set path
        /// to any one of these three filenames to open the Shapefile. Without a .dbf file, metadata access throws InvalidOperationException.
        /// </summary>
        /// <param name="path">Path to the .shp, .shx or .dbf file for this Shapefile</param>
        /// <exception cref="ObjectDisposedException">Thrown if the Shapefile has been disposed</exception>
        /// <exception cref="ArgumentException">Thrown if the path parameter is empty</exception>
        /// <exception cref="FileNotFoundException">Thrown if the main .shp file is not found</exception>
        public Shapefile(string path)
        {
            if (path != null) Open(path);
        }

        /// <summary>Creates and opens a shapefile using the selected bounding-box convention.</summary>
        /// <param name="path">Path to the .shp, .shx or .dbf file, or null to open later.</param>
        /// <param name="boundingBoxConvention">Mapping of Y extents to Top and Bottom.</param>
        public Shapefile(string path, BoundingBoxConvention boundingBoxConvention)
        {
            BoundingBoxConvention = boundingBoxConvention;
            if (path != null) Open(path);
        }

        /// <summary>
        /// Open a Shapefile. Only the main file (.shp) is required.
        /// The index (.shx) and dBASE table (.dbf) are optional. Companion files
        /// must all have the same filename (i.e. shapes.shp, shapes.shx and shapes.dbf). Set path
        /// to any one of these three filenames to open the Shapefile. Without a .dbf file, metadata access throws InvalidOperationException.
        /// </summary>
        /// <param name="path">Path to the .shp, .shx or .dbf file for this Shapefile</param>
        /// <exception cref="ObjectDisposedException">Thrown if the Shapefile has been disposed</exception>
        /// <exception cref="ArgumentNullException">Thrown if the path parameter is null</exception>
        /// <exception cref="ArgumentException">Thrown if the path parameter is empty</exception>
        /// <exception cref="FileNotFoundException">Thrown if the main .shp file is not found</exception>
        /// <exception cref="InvalidOperationException">Thrown if an error occurs parsing file headers</exception>
        public void Open(string path)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Shapefile");
            }
            if (_opened)
            {
                throw new InvalidOperationException("Shapefile is already open.");
            }

            if (path == null)
            {
                throw new ArgumentNullException("path");
            }
            if (path.Length <= 0)
            {
                throw new ArgumentException("path parameter is empty", "path");
            }

            _shapefileMainPath = Path.ChangeExtension(path, MainPathExtension);
            _shapefileIndexPath = Path.ChangeExtension(path, IndexPathExtension);
            _shapefileDbasePath = Path.ChangeExtension(path, DbasePathExtension);

            if (!File.Exists(_shapefileMainPath))
            {
                throw new FileNotFoundException("Shapefile main file not found", _shapefileMainPath);
            }
            if (!File.Exists(_shapefileDbasePath))
            {
                _shapefileDbasePath = null;
            }

            try
            {
                _mainStream = File.Open(_shapefileMainPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (File.Exists(_shapefileIndexPath))
                    _indexStream = File.Open(_shapefileIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (_mainStream.Length < Header.HeaderLength)
                {
                    throw new InvalidOperationException("Shapefile main file does not contain a valid header");
                }

                if (_indexStream != null && _indexStream.Length < Header.HeaderLength)
                {
                    throw new InvalidOperationException("Shapefile index file does not contain a valid header");
                }

                // read in and parse the headers
                byte[] headerBytes = new byte[Header.HeaderLength];
                _mainStream.Read(headerBytes, 0, Header.HeaderLength);
                _mainHeader = new Header(headerBytes);
                if (_indexStream != null)
                {
                    _indexStream.Read(headerBytes, 0, Header.HeaderLength);
                    _indexHeader = new Header(headerBytes);
                }

                // set properties from the main header
                _type = _mainHeader.ShapeType;
                _boundingBox = new RectangleD(_mainHeader.XMin, _mainHeader.YMin, _mainHeader.XMax, _mainHeader.YMax);

                if (_indexStream != null)
                {
                    // Each index record contains four 16-bit words.
                    _count = (_indexHeader.FileLength - (Header.HeaderLength / 2)) / 4;
                }
                else
                {
                    _recordOffsets = ReadRecordOffsets();
                    _count = _recordOffsets.Count;
                }

                // open the metadata database
                if (_shapefileDbasePath != null)
                {
                    using (ShapeFileEnumerator.OpenMetadata(_shapefileDbasePath)) { }
                }

                _opened = true;
            }
            catch
            {
                _mainStream?.Dispose();
                _indexStream?.Dispose();
                _mainStream = null;
                _indexStream = null;
                throw;
            }
        }

        private List<long> ReadRecordOffsets()
        {
            long fileLength = (long)_mainHeader.FileLength * 2;
            if (fileLength < Header.HeaderLength || fileLength != _mainStream.Length)
                throw new InvalidOperationException("Shapefile main file length does not match its header.");

            var offsets = new List<long>();
            var recordHeader = new byte[8];
            long offset = Header.HeaderLength;
            while (offset < fileLength)
            {
                _mainStream.Seek(offset, SeekOrigin.Begin);
                if (fileLength - offset < recordHeader.Length ||
                    _mainStream.Read(recordHeader, 0, recordHeader.Length) != recordHeader.Length)
                    throw new InvalidOperationException("Shapefile contains a truncated record header.");

                int contentLength = EndianBitConverter.ToInt32(recordHeader, 4, ProvidedOrder.Big);
                long nextOffset = offset + 8 + (long)contentLength * 2;
                if (contentLength < 2 || nextOffset > fileLength || (long)contentLength * 2 > int.MaxValue - 8)
                    throw new InvalidOperationException("Shapefile contains an invalid record length.");

                offsets.Add(offset);
                offset = nextOffset;
            }
            return offsets;
        }

        /// <summary>
        /// Close the Shapefile. Equivalent to calling Dispose().
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// If true then only the IDataRecord (DataRecord) property is available to access metadata for each shape.
        /// If flase (the default) then metadata is also parsed into a string dictionary (use GetMetadataNames() and
        /// GetMetadata() to access)
        /// </summary>
        public bool RawMetadataOnly
        {
            get { return _rawMetadataOnly; }
            set { _rawMetadataOnly = value; }
        }

        /// <summary>
        /// Gets or sets the convention for file and shape bounding boxes. Defaults to YUp
        /// (Top = YMax, Bottom = YMin). Legacy uses Top = YMin, Bottom = YMax.
        /// Set before opening the file; point coordinates are unaffected.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The shapefile has been disposed.</exception>
        /// <exception cref="InvalidOperationException">The shapefile is already open.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The convention is not supported.</exception>
        public BoundingBoxConvention BoundingBoxConvention
        {
            get { return _boundingBoxConvention; }
            set
            {
                if (_disposed) throw new ObjectDisposedException("Shapefile");
                if (_opened) throw new InvalidOperationException("Set the bounding-box convention before opening the shapefile.");
                if (value != BoundingBoxConvention.Legacy && value != BoundingBoxConvention.YUp)
                    throw new ArgumentOutOfRangeException("value");
                _boundingBoxConvention = value;
            }
        }

        /// <summary>
        /// Gets the number of shapes in the Shapefile
        /// </summary>
        public int Count
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException("Shapefile");
                if (!_opened) throw new InvalidOperationException("Shapefile not open.");

                return _count;
            }
        }

        /// <summary>
        /// Gets the bounding box for the Shapefile
        /// </summary>
        public RectangleD BoundingBox
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException("Shapefile");
                if (!_opened) throw new InvalidOperationException("Shapefile not open.");

                return _boundingBox.WithConvention(_boundingBoxConvention);
            }

        }

        /// <summary>
        /// Gets the ShapeType of the Shapefile
        /// </summary>
        public ShapeType Type
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException("Shapefile");
                if (!_opened) throw new InvalidOperationException("Shapefile not open.");

                return _type;
            }
        }

        #region IDisposable Members

        /// <summary />
        ~Shapefile()
        {
            Dispose(false);
        }

        /// <summary>
        /// Dispose the Shapefile and free all resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool canDisposeManagedResources)
        {
            if (!_disposed)
            {
                if (canDisposeManagedResources)
                {
                    if (_mainStream != null)
                    {
                        _mainStream.Close();
                        _mainStream = null;
                    }

                    if (_indexStream != null)
                    {
                        _indexStream.Close();
                        _indexStream = null;
                    }

                    foreach (var enumerator in new List<ShapeFileEnumerator>(_enumerators))
                    {
                        enumerator.Dispose();
                    }
                }

                _disposed = true;
                _opened = false;
            }
        }


        /// <summary>
        /// Get the IEnumerator for this Shapefile
        /// </summary>
        /// <returns>IEnumerator</returns>
        public IEnumerator<Shape> GetEnumerator()
        {

            if (_disposed) throw new ObjectDisposedException("Shapefile");
            if (!_opened) throw new InvalidOperationException("Shapefile not open.");

            var enumerator = new ShapeFileEnumerator(_shapefileDbasePath, _rawMetadataOnly, _mainStream,
                _indexStream, _recordOffsets, _count, _boundingBoxConvention, disposed => _enumerators.Remove(disposed));
            _enumerators.Add(enumerator);
            return enumerator;
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
