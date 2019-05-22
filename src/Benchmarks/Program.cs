using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ElasticSearch.LowLevel;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace Benchmarks
{
    public class Program
    {
        public static void Main(string[] args) => _ = BenchmarkRunner.Run<BulkResponseParserBenchmarks>();
    }

    [MemoryDiagnoser]
    public class BulkResponseParserBenchmarks
    {
        public Stream _errorStream = new MemoryStream();
        public Stream _successStream = new MemoryStream();

        [GlobalSetup]
        public void Setup()
        {
            using (var fs = File.OpenRead(AppDomain.CurrentDomain.BaseDirectory + "/FailedResponseSample.txt"))
            {
                fs.CopyTo(_errorStream);
            }

            using (var fs = File.OpenRead(AppDomain.CurrentDomain.BaseDirectory + "/SuccessResponseSample.txt"))
            {
                fs.CopyTo(_successStream);
            }
        }

        public IEnumerable<Stream> Streams()
        {
            yield return _errorStream;
            yield return _successStream;
        }

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(Streams))]
        public async Task ParseResponse(Stream stream)
        {
            stream.Position = 0;

            await BulkResponseParser.FromStreamAsync(stream);
        }
    }

    [MemoryDiagnoser]
    public class BulkRequestBenchmarks
    {
        List<(ReadOnlyMemory<char>, ReadOnlyMemory<byte>)> ToProcess;

        [GlobalSetup]
        public void Setup()
        {
            var bytaData = Encoding.UTF8.GetBytes("{\"Title\":\"A second book title\"}");
            var failingByteData = Encoding.UTF8.GetBytes("{\"Title\":\"A second book title\""); // missing trailing brace

            ToProcess = new List<(ReadOnlyMemory<char>, ReadOnlyMemory<byte>)>();

            for (int i = 0; i < 127; i++)
            {
                ToProcess.Add((Guid.NewGuid().ToString().AsMemory(), bytaData.AsMemory()));
            }

            ToProcess.Add((Guid.NewGuid().ToString().AsMemory(), failingByteData.AsMemory()));

            /// just for ContentStreamManualTest

            ReadOnlySpan<char> startBytes = "{\"index\":{\"_id\":\"".AsSpan();
            ReadOnlySpan<char> endBytes = "\"}}".AsSpan();

            _bulkApiIndexActionTemplateWithoutId = new byte[startBytes.Length + GuidLength + endBytes.Length];

            Encoding.UTF8.GetBytes(startBytes, _bulkApiIndexActionTemplateWithoutId.AsSpan());
            _endBytesLength = Encoding.UTF8.GetBytes(endBytes, _bulkApiIndexActionTemplateWithoutId.AsSpan(_bulkApiIndexActionTemplateWithoutId.Length - endBytes.Length));
        }

        [Benchmark]
        public void CreateRequest()
        {
           using (var bulkRequest = new BulkRequest())
           {
               bulkRequest.AttachContentStream(ToProcess);
               var requestMessage = bulkRequest.GetHttpRequestMessage();
           }
        }

        private const int GuidLength = 36;

        private byte[] _newLineBytes = new byte[] { (byte)'\n' };
        private byte[] _bulkApiIndexActionTemplateWithoutId;
        private int _endBytesLength;

        private static ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;

        private MemoryStream contentStream;
        private byte[] _streamBuffer;

        [Benchmark]
        public void ContentStreamManualTest()
        {
            contentStream = null;
            _streamBuffer = null;

            // this is a manual test to see what is allocated during the build up of the gzipped stream, not including any HttpRequestMessage overhead.

            var bytesNeeded = 0;

            foreach (var (_, Data) in ToProcess)
            {
                bytesNeeded += _bulkApiIndexActionTemplateWithoutId.Length + GuidLength;
                bytesNeeded += Data.Length;
            }

            _streamBuffer = _arrayPool.Rent(bytesNeeded);

            contentStream = new MemoryStream(_streamBuffer);

            Span<byte> indexAction = stackalloc byte[_bulkApiIndexActionTemplateWithoutId.Length];

            using (GZipStream gzipStream = new GZipStream(contentStream, CompressionMode.Compress, true))
            {
                foreach (var (Id, Data) in ToProcess)
                {
                    _bulkApiIndexActionTemplateWithoutId.CopyTo(indexAction);

                    Encoding.UTF8.GetBytes(Id.Span, indexAction.Slice(_bulkApiIndexActionTemplateWithoutId.Length - GuidLength - _endBytesLength));

                    gzipStream.Write(indexAction);

                    gzipStream.Write(_newLineBytes.AsSpan());

                    gzipStream.Write(Data.Span);

                    gzipStream.Write(_newLineBytes.AsSpan());
                }
            }

            _arrayPool.Return(_streamBuffer);
        }
    }
}
