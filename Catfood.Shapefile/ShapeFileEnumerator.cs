/* ------------------------------------------------------------------------
 * (c)copyright Robert Ellison and contributors 
 * Provided under the ms-PL license, see LICENSE.txt
 * https://github.com/abfo/shapefile
 * ------------------------------------------------------------------------ */

using DbfDataReader;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;

namespace Catfood.Shapefile
{
    class ShapeFileEnumerator : IEnumerator<Shape>
    {
        private readonly DbfDataReader.DbfDataReader _dbReader;
        private readonly Action<ShapeFileEnumerator> _onDispose;
        private bool _disposed;
        private int _currentIndex = -1;
        private bool _rawMetadataOnly;
        private FileStream _mainStream;
        private FileStream _indexStream;
        private int _count;
        private readonly List<long> _recordOffsets;
        private readonly BoundingBoxConvention _boundingBoxConvention;

        public ShapeFileEnumerator(string dbfPath, bool rawMetadataOnly, FileStream mainStream,
                                   FileStream indexStream, List<long> recordOffsets, int count, BoundingBoxConvention boundingBoxConvention,
                                   Action<ShapeFileEnumerator> onDispose)
        {

            _rawMetadataOnly = rawMetadataOnly;
            _mainStream = mainStream;
            _indexStream = indexStream;
            _recordOffsets = recordOffsets;
            _count = count;
            _boundingBoxConvention = boundingBoxConvention;
            _dbReader = dbfPath == null ? null : OpenMetadata(dbfPath);
            _onDispose = onDispose;
        }

        internal static DbfDataReader.DbfDataReader OpenMetadata(string path)
        {
            // Own the streams until construction succeeds so malformed DBF/memo headers
            // cannot leave files open inside a partially constructed dependency object.
            var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream memoStream = null;
            try
            {
                foreach (string extension in new[] { "fpt", "FPT", "dbt", "DBT" })
                {
                    string memoPath = Path.ChangeExtension(path, extension);
                    if (File.Exists(memoPath))
                    {
                        memoStream = File.Open(memoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        break;
                    }
                }
                return new DbfDataReader.DbfDataReader(stream, memoStream, new DbfDataReaderOptions
                {
                    // DBF records correspond to shapes by physical position, including deleted rows.
                    SkipDeletedRecords = false,
                    StringTrimming = StringTrimmingOption.TrimEnd
                });
            }
            catch
            {
                memoStream?.Dispose();
                stream.Dispose();
                throw;
            }
        }


        #region IEnumerator<Shape> Members

        /// <summary>
        /// Gets the current shape in the collection
        /// </summary>
        public Shape Current
        {
            get
            {
                ThrowIfDisposed();
                if (_currentIndex < 0 || _currentIndex >= _count)
                    throw new InvalidOperationException("Enumerator is not positioned on a shape.");

                // get the metadata
                StringDictionary metadata = null;
                if (_dbReader != null && !_rawMetadataOnly)
                {
                    metadata = new StringDictionary();
                    for (int i = 0; i < _dbReader.FieldCount; i++)
                    {
                        metadata.Add(_dbReader.GetName(i),
                            _dbReader.GetValue(i)?.ToString() ?? string.Empty);
                    }
                }

                // get the index record
                byte[] indexHeaderBytes = new byte[8];
                long contentOffset;
                if (_indexStream != null)
                {
                    _indexStream.Seek(Header.HeaderLength + (long)_currentIndex * 8, SeekOrigin.Begin);
                    _indexStream.Read(indexHeaderBytes, 0, indexHeaderBytes.Length);
                    contentOffset = (long)EndianBitConverter.ToInt32(indexHeaderBytes, 0, ProvidedOrder.Big) * 2;
                }
                else
                {
                    contentOffset = _recordOffsets[_currentIndex];
                    _mainStream.Seek(contentOffset, SeekOrigin.Begin);
                    _mainStream.Read(indexHeaderBytes, 0, indexHeaderBytes.Length);
                }
                int contentLengthInWords = EndianBitConverter.ToInt32(indexHeaderBytes, 4, ProvidedOrder.Big);

                // get the data chunk from the main file - need to factor in 8 byte record header
                int bytesToRead = (contentLengthInWords * 2) + 8;
                byte[] shapeData = new byte[bytesToRead];
                _mainStream.Seek(contentOffset, SeekOrigin.Begin);
                _mainStream.Read(shapeData, 0, bytesToRead);

                return ShapeFactory.ParseShape(shapeData, metadata, _dbReader, _boundingBoxConvention);
            }
        }

        #endregion

        #region IEnumerator Members

        /// <summary>
        /// Gets the current item in the collection
        /// </summary>
        object System.Collections.IEnumerator.Current
        {
            get
            {
                return this.Current;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _dbReader?.Dispose();
                _disposed = true;
                _onDispose(this);
            }
        }

        /// <summary>
        /// Move to the next item in the collection (returns false if at the end)
        /// </summary>
        /// <returns>false if there are no more items in the collection</returns>
        public bool MoveNext()
        {
            ThrowIfDisposed();
            if (_currentIndex < (_count - 1))
            {
                // try to read the next database record
                if (_dbReader != null && !_dbReader.Read())
                {
                    throw new InvalidOperationException("Metadata database does not contain a record for the next shape");
                }

                _currentIndex++;
                return true;
            }
            else
            {
                // reached the last shape
                _currentIndex = _count;
                return false;
            }
        }

        /// <summary>
        /// Reset the enumerator
        /// </summary>
        public void Reset()
        {
            ThrowIfDisposed();
            _dbReader?.Seek(0);
            _currentIndex = -1;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException("ShapeFileEnumerator");
        }

        #endregion
    }
}
