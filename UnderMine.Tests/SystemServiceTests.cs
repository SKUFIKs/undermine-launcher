using System.Threading.Tasks;
using UnderMine.Services;
using Xunit;

namespace UnderMine.Tests
{
    public class SystemServiceTests
    {
        [Fact]
        public void GetStorageDirectory_NotEmpty()
        {
            var svc = new SystemService();
            var path = svc.GetStorageDirectory();
            Assert.False(string.IsNullOrWhiteSpace(path.Value));
        }

        [Fact]
        public async Task InitializeMemorySliderAsync_ReturnsValues()
        {
            var svc = new SystemService();
            var vals = await svc.InitializeMemorySliderAsync();
            Assert.True(vals.Step >= 0);
            Assert.Equal(5, vals.Labels.Count);
        }
    }
}