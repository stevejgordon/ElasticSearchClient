using Xunit;

namespace ElasticSearch.LowLevel.Test
{
    public class BulkRequestTests
    {
        [Fact]
        public void Should_DisposeSuccessfully_WhenNoContent()
        {
            using (var request = new BulkRequest()) { } // Should not throw
        }
    }
}
