using System;

namespace ElasticSearch.LowLevel
{
    public class BulkIndexItem
    {
        public BulkIndexItem(ReadOnlyMemory<byte> id, ReadOnlyMemory<byte> data)
        {
            Id = id;
            Data = data;
        }

        public ReadOnlyMemory<byte> Id { get; }

        public ReadOnlyMemory<byte> Data { get; }
    }
}
