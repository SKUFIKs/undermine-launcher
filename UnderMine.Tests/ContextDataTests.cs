using UnderMine.Models;
using Xunit;

namespace UnderMine.Tests
{
    public class ContextDataTests
    {
        [Fact]
        public void DefaultValues_AreSet()
        {
            var cd = new ContextData();
            Assert.False(string.IsNullOrWhiteSpace(cd.BaseUrl));
            Assert.False(string.IsNullOrWhiteSpace(cd.LogApiKey));
        }
    }
}