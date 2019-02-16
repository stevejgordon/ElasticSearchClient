using System;

namespace ElasticSearch.LowLevel
{
    public class BulkIndexItem
    {
        public BulkIndexItem(ReadOnlyMemory<byte> id, ReadOnlyMemory<byte> data)
        {
            // this is very much tied to my sample requirement where the ID is known to be a guid
            if (id.Length != ApplicationConstants.GuidLength)
                throw new ArgumentOutOfRangeException("The length of the ID must match the length of a GUID", nameof(id));

            if (data.Length < 0)
                throw new ArgumentOutOfRangeException("There must be some data to index", nameof(data));

            Id = id;
            Data = data;
        }

        public ReadOnlyMemory<byte> Id { get; }

        public ReadOnlyMemory<byte> Data { get; }
    }
}
