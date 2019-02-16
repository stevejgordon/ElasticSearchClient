using ElasticSearch.LowLevel;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ElasticSearchClient.SampleConsumer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // begin - setup some sample data

            var bytaData = Encoding.UTF8.GetBytes("{\"Title\":\"A second book title\"}");
            var failingByteData = Encoding.UTF8.GetBytes("{\"Title\":\"A second book title\""); // missing trailing brace

            var toProcess = new List<(ReadOnlyMemory<char>, ReadOnlyMemory<byte>)>();

            for (int i = 0; i < 127; i++)
            {
                toProcess.Add((Guid.NewGuid().ToString().AsMemory(), bytaData.AsMemory()));
            }

            toProcess.Add((Guid.NewGuid().ToString().AsMemory(), failingByteData.AsMemory()));

            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            // end - setup some sample data

            Console.WriteLine("Ready!!");
            Console.ReadKey();

            var client = new HttpClient(handler);

            using (var request = new BulkRequest())
            {
                request.AttachContentStream(toProcess);

                var response = await client.SendAsync(request.GetHttpRequestMessage(), HttpCompletionOption.ResponseHeadersRead);

                if (response.IsSuccessStatusCode)
                {
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        var (success, errors) = await BulkResponseParser.FromStreamAsync(contentStream);
                    }
                }
            }

            Console.WriteLine("Done!!");
            Console.ReadKey();
        }
    }
}
