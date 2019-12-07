/* ------------------------------------------------------------------------
 * (c)copyright 2009-2019 Robert Ellison and contributors - https://github.com/abfo/shapefile
 * Provided under the ms-PL license, see LICENSE.txt
 * ------------------------------------------------------------------------ */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.IO;
using System.Drawing;
using System.Data.OleDb;

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
        /// <summary>
        /// Jet connection string template
        /// </summary>
        public const string ConnectionStringTemplateJet = @"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Extended Properties=dBase IV";

        /// <summary>
        /// ACE connection string template
        /// </summary>
        public const string ConnectionStringTemplateAce = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={0};Extended Properties=dBase IV";
        
        private const string DbSelectStringTemplate = "SELECT * FROM [{0}]";
        private const string MainPathExtension = "shp";
        private const string IndexPathExtension = "shx";
        private const string DbasePathExtension = "dbf";

        private bool _disposed;
        private bool _opened;
        private bool _rawMetadataOnly;
        private int _count;
        private RectangleD _boundingBox;
        private ShapeType _type;
        private string _shapefileMainPath;
        private string _shapefileIndexPath;
        private string _shapefileDbasePath;
        private string _shapefileTempDbasePath;
        private FileStream _mainStream;
        private FileStream _indexStream;
        private Header _mainHeader;
        private Header _indexHeader;
        private OleDbConnection _dbConnection;
        private string _connectionStringTemplate;
        private string _selectString;

        /// <summary>
        /// Create a new Shapefile object.
        /// </summary>
        public Shapefile()
            : this(null, ConnectionStringTemplateJet) {}

        /// <summary>
        /// Create a new Shapefile object and open a Shapefile. Note that three files are required - 
        /// the main file (.shp), the index file (.shx) and the dBASE table (.dbf). The three files 
        /// must all have the same filename (i.e. shapes.shp, shapes.shx and shapes.dbf). Set path
        /// to any one of these three files to open the Shapefile.
        /// </summary>
        /// <param name="path">Path to the .shp, .shx or .dbf file for this Shapefile</param>
        /// <exception cref="ObjectDisposedException">Thrown if the Shapefile has been disposed</exception>
        /// <exception cref="ArgumentException">Thrown if the path parameter is empty</exception>
        /// <exception cref="FileNotFoundException">Thrown if one of the three required files is not found</exception>
        public Shapefile(string path)
            : this(path, ConnectionStringTemplateJet) {}

        /// <summary>
        /// Create a new Shapefile object and open a Shapefile. Note that three files are required - 
        /// the main file (.shp), the index file (.shx) and the dBASE table (.dbf). The three files 
        /// must all have the same filename (i.e. shapes.shp, shapes.shx and shapes.dbf). Set path
        /// to any one of these three files to open the Shapefile.
        /// </summary>
        /// <param name="path">Path to the .shp, .shx or .dbf file for this Shapefile</param>
        /// <param name="connectionStringTemplate">Connection string template - use Shapefile.ConnectionStringTemplateJet
        /// (the default), Shapefile.ConnectionStringTemplateAce or your own dBASE connection string</param>
        /// <exception cref="ObjectDisposedException">Thrown if the Shapefile has been disposed</exception>
        /// <exception cref="ArgumentNullException">Thrown if the connectionStringTemplate parameter is null</exception>
        /// <exception cref="ArgumentException">Thrown if the path parameter is empty</exception>
        /// <exception cref="FileNotFoundException">Thrown if one of the three required files is not found</exception>
        public Shapefile(string path, string connectionStringTemplate)
        {
            if (connectionStringTemplate == null)
            {
                throw new ArgumentNullException("connectionStringTemplate");
            }

            ConnectionStringTemplate = connectionStringTemplate;

            if (path != null)
            {
                Open(path);
            }
        }

        /// <summary>
        /// Create a new Shapefile object and open a Shapefile. Note that three files are required - 
        /// the main file (.shp), the index file (.shx) and the dBASE table (.dbf). The three files 
        /// must all have the same filename (i.e. shapes.shp, shapes.shx and shapes.dbf). Set path
        /// to any one of these three files to open the Shapefile.
        /// </summary>
        /// <param name="path">Path to the .shp, .shx or .dbf file for this Shapefile</param>
        /// <exception cref="ObjectDisposedException">Thrown if the Shapefile has been disposed</exception>
        /// <exception cref="ArgumentNullException">Thrown if the path parameter is null</exception>
        /// <exception cref="ArgumentException">Thrown if the path parameter is empty</exception>
        /// <exception cref="FileNotFoundException">Thrown if one of the three required files is not found</exception>
        /// <exception cref="InvalidOperationException">Thrown if an error occurs parsing file headers</exception>
        public void Open(string path)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Shapefile");
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
            if (!File.Exists(_shapefileIndexPath))
            {
                throw new FileNotFoundException("Shapefile index file not found", _shapefileIndexPath);
            }
            if (!File.Exists(_shapefileDbasePath))
            {
                throw new FileNotFoundException("Shapefile dBase file not found", _shapefileDbasePath);
            }

            _mainStream = File.Open(_shapefileMainPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _indexStream = File.Open(_shapefileIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (_mainStream.Length < Header.HeaderLength)
            {
                throw new InvalidOperationException("Shapefile main file does not contain a valid header");
            }

            if (_indexStream.Length < Header.HeaderLength)
            {
                throw new InvalidOperationException("Shapefile index file does not contain a valid header");
            }

            // read in and parse the headers
            byte[] headerBytes = new byte[Header.HeaderLength];
            _mainStream.Read(headerBytes, 0, Header.HeaderLength);
            _mainHeader = new Header(headerBytes);
            _indexStream.Read(headerBytes, 0, Header.HeaderLength);
            _indexHeader = new Header(headerBytes);

            // set properties from the main header
            _type = _mainHeader.ShapeType;
            _boundingBox = new RectangleD(_mainHeader.XMin, _mainHeader.YMin, _mainHeader.XMax, _mainHeader.YMax);

            // index header length is in 16-bit words, including the header - number of 
            // shapes is the number of records (each 4 workds long) after subtracting the header bytes
            _count = (_indexHeader.FileLength - (Header.HeaderLength / 2)) / 4;

            // open the metadata database
            OpenDb();

            _opened = true;
        }

        /// <summary>
        /// Close the Shapefile. Equivalent to calling Dispose().
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Gets or sets the connection string template - use Shapefile.ConnectionStringTemplateJet
        /// (the default), Shapefile.ConnectionStringTemplateAce or your own dBASE connection string
        /// </summary>
        public string ConnectionStringTemplate
        {
            get { return _connectionStringTemplate; }
            set { _connectionStringTemplate = value; }
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

                return _boundingBox; 
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

        private void OpenDb()
        {
            // The drivers for DBF files throw an exception if the filename 
            // is longer than 8 characters - in this case create a temp file
            // for the DB
            string safeDbasePath = _shapefileDbasePath;
            if (Path.GetFileNameWithoutExtension(safeDbasePath).Length > 8)
            {
                // create/delete temp file (we just want a safe path)
                string initialTempFile = Path.GetTempFileName();
                try
                {
                    File.Delete(initialTempFile);
                }
                catch { }

                // set the correct extension
                _shapefileTempDbasePath = Path.ChangeExtension(initialTempFile, DbasePathExtension);

                // copy over the DB
                File.Copy(_shapefileDbasePath, _shapefileTempDbasePath, true);
                safeDbasePath = _shapefileTempDbasePath;
            }

            string connectionString = string.Format(ConnectionStringTemplate,
                Path.GetDirectoryName(safeDbasePath));
            _selectString = string.Format(DbSelectStringTemplate,
                Path.GetFileNameWithoutExtension(safeDbasePath));

            _dbConnection = new OleDbConnection(connectionString);
            _dbConnection.Open();
            
        }

        private void CloseDb()
        {

            if (_dbConnection != null)
            {
                _dbConnection.Close();
                _dbConnection = null;
            }

            if (_shapefileTempDbasePath != null)
            {
                if (File.Exists(_shapefileTempDbasePath))
                {
                    try
                    {
                        File.Delete(_shapefileTempDbasePath);
                    }
                    catch { }
                }

                _shapefileTempDbasePath = null;
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

                    CloseDb();
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

            return new ShapeFileEnumerator(_dbConnection, _selectString, _rawMetadataOnly, _mainStream,
                                          _indexStream, _count);
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
