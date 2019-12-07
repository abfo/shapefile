/* ------------------------------------------------------------------------
 * (c)copyright 2009-2019 Robert Ellison and contributors - https://github.com/abfo/shapefile
 * Provided under the ms-PL license, see LICENSE.txt
 * ------------------------------------------------------------------------ */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.OleDb;
using System.IO;
using System.Text;

namespace Catfood.Shapefile
{
    class ShapeFileEnumerator : IEnumerator<Shape>
    {
        private OleDbCommand _dbCommand;
        private OleDbDataReader _dbReader;
        private int _currentIndex = -1;
        private bool _rawMetadataOnly;
        private FileStream _mainStream;
        private FileStream _indexStream;
        private int _count;

        public ShapeFileEnumerator(OleDbConnection dbConnection, string selectString, bool rawMetadataOnly, FileStream mainStream,
                                   FileStream indexStream, int count)
        {

            _rawMetadataOnly = rawMetadataOnly;
            _mainStream = mainStream;
            _indexStream = indexStream;
            _count = count;
            _dbCommand = new OleDbCommand(selectString, dbConnection);
            _dbReader = _dbCommand.ExecuteReader();
        }


        #region IEnumerator<Shape> Members

        /// <summary>
        /// Gets the current shape in the collection
        /// </summary>
        public Shape Current
        {
            get
            {
                // get the metadata
                StringDictionary metadata = null;
                if (!_rawMetadataOnly)
                {
                    metadata = new StringDictionary();
                    for (int i = 0; i < _dbReader.FieldCount; i++)
                    {
                        metadata.Add(_dbReader.GetName(i),
                            _dbReader.GetValue(i).ToString());
                    }
                }

                // get the index record
                byte[] indexHeaderBytes = new byte[8];
                _indexStream.Seek(Header.HeaderLength + _currentIndex * 8, SeekOrigin.Begin);
                _indexStream.Read(indexHeaderBytes, 0, indexHeaderBytes.Length);
                int contentOffsetInWords = EndianBitConverter.ToInt32(indexHeaderBytes, 0, ProvidedOrder.Big);
                int contentLengthInWords = EndianBitConverter.ToInt32(indexHeaderBytes, 4, ProvidedOrder.Big);

                // get the data chunk from the main file - need to factor in 8 byte record header
                int bytesToRead = (contentLengthInWords * 2) + 8;
                byte[] shapeData = new byte[bytesToRead];
                _mainStream.Seek(contentOffsetInWords * 2, SeekOrigin.Begin);
                _mainStream.Read(shapeData, 0, bytesToRead);

                return ShapeFactory.ParseShape(shapeData, metadata, _dbReader);
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
            _dbReader.Close();
            _dbCommand.Dispose();
        }

        /// <summary>
        /// Move to the next item in the collection (returns false if at the end)
        /// </summary>
        /// <returns>false if there are no more items in the collection</returns>
        public bool MoveNext()
        {

            if (_currentIndex++ < (_count - 1))
            {
                // try to read the next database record
                if (!_dbReader.Read())
                {
                    throw new InvalidOperationException("Metadata database does not contain a record for the next shape");
                }

                return true;
            }
            else
            {
                // reached the last shape
                return false;
            }
        }

        /// <summary>
        /// Reset the enumerator
        /// </summary>
        public void Reset()
        {
            _dbReader.Close();
            _dbReader = _dbCommand.ExecuteReader();
            _currentIndex = -1;
        }

        #endregion
    }
}
