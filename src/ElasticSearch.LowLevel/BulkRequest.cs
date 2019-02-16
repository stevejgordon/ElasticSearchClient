using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace ElasticSearch.LowLevel
{
    public class BulkRequest : IDisposable
    {
        private bool disposed = false;              

        private static readonly byte[] _newLineBytes = new byte[] { (byte)'\n' };
        private static readonly byte[] _bulkApiIndexActionTemplateWithoutId;
        private static readonly int _endBytesLength;

        private static readonly MediaTypeWithQualityHeaderValue _applicationJsonHeader = new MediaTypeWithQualityHeaderValue("application/json");
        private static readonly StringWithQualityHeaderValue _gzipHeader = new StringWithQualityHeaderValue("gzip");
        private static readonly StringWithQualityHeaderValue _deflateHeader = new StringWithQualityHeaderValue("deflate");
        private static readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;

        static BulkRequest()
        {
            ReadOnlySpan<char> startBytes = "{\"index\":{\"_id\":\"".AsSpan();
            ReadOnlySpan<char> endBytes = "\"}}".AsSpan();                      

            _bulkApiIndexActionTemplateWithoutId = new byte[startBytes.Length + ApplicationConstants.GuidLength + endBytes.Length];

            Encoding.UTF8.GetBytes(startBytes, _bulkApiIndexActionTemplateWithoutId.AsSpan());
            _endBytesLength = Encoding.UTF8.GetBytes(endBytes, _bulkApiIndexActionTemplateWithoutId.AsSpan(_bulkApiIndexActionTemplateWithoutId.Length - endBytes.Length));
        }

        private MemoryStream contentStream;
        private bool _contentAttached = false;        
        private byte[] _streamBuffer;

        public void AttachContentStream(IList<BulkIndexItem> itemsToIndex)
        {
            var bytesNeeded = 0;

            foreach (var item in itemsToIndex)
            {
                bytesNeeded += _bulkApiIndexActionTemplateWithoutId.Length + ApplicationConstants.GuidLength;
                bytesNeeded += item.Data.Length;
            }

            _streamBuffer = _arrayPool.Rent(bytesNeeded); // this is higher than we need as the actual bytes will be compressed

            contentStream = new MemoryStream(_streamBuffer);

            // TODO - Refactor code duplication between these overloads.

            Span<byte> indexAction = stackalloc byte[_bulkApiIndexActionTemplateWithoutId.Length];

            using (GZipStream gzipStream = new GZipStream(contentStream, CompressionMode.Compress, true))
            {

                foreach (var item in itemsToIndex)
                {
                    _bulkApiIndexActionTemplateWithoutId.CopyTo(indexAction);

                    item.Id.Span.CopyTo(indexAction.Slice(_bulkApiIndexActionTemplateWithoutId.Length - ApplicationConstants.GuidLength - _endBytesLength));

                    gzipStream.Write(indexAction);

                    gzipStream.Write(_newLineBytes.AsSpan());

                    gzipStream.Write(item.Data.Span);

                    gzipStream.Write(_newLineBytes.AsSpan());
                }
            }

            _contentAttached = true;
        }

        public void AttachContentStream(IList<(ReadOnlyMemory<char> Id, ReadOnlyMemory<byte> Data)> itemsToIndex)
        {
            var bytesNeeded = 0;

            foreach (var (_, Data) in itemsToIndex)
            {
                bytesNeeded += _bulkApiIndexActionTemplateWithoutId.Length + ApplicationConstants.GuidLength;
                bytesNeeded += Data.Length;
            }

            _streamBuffer = _arrayPool.Rent(bytesNeeded);

            contentStream = new MemoryStream(_streamBuffer);

            Span<byte> indexAction = stackalloc byte[_bulkApiIndexActionTemplateWithoutId.Length];

            using (GZipStream gzipStream = new GZipStream(contentStream, CompressionMode.Compress, true))
            {
                foreach (var (Id, Data) in itemsToIndex)
                {
                    _bulkApiIndexActionTemplateWithoutId.CopyTo(indexAction);

                    Encoding.UTF8.GetBytes(Id.Span, indexAction.Slice(_bulkApiIndexActionTemplateWithoutId.Length - ApplicationConstants.GuidLength - _endBytesLength));

                    gzipStream.Write(indexAction);

                    gzipStream.Write(_newLineBytes.AsSpan());

                    gzipStream.Write(Data.Span);

                    gzipStream.Write(_newLineBytes.AsSpan());
                }               
            }         

            _contentAttached = true;
        }             

        public HttpRequestMessage GetHttpRequestMessage()
        {
            if (!_contentAttached)
            {
                // throw?
            }

            contentStream.Seek(0, SeekOrigin.Begin);

            // hard coded URL for now but we'll need to accept parameters? Or place the URL piece into the client which issues the request
            var request = new HttpRequestMessage(HttpMethod.Put, "http://localhost:9200/test/type1/_bulk")
            {
                Content = new StreamContent(contentStream)
            };

            request.Headers.Accept.Add(_applicationJsonHeader);
            request.Headers.AcceptEncoding.Add(_gzipHeader);
            request.Headers.AcceptEncoding.Add(_deflateHeader);

            request.Content.Headers.Add("Content-Type", "application/x-ndjson");
            request.Content.Headers.Add("Content-Encoding", "gzip");
            
            return request;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (_streamBuffer != null)
                    _arrayPool.Return(_streamBuffer);
            }

            disposed = true;
        }
    }
}
