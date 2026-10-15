using System.Net;
using UnderMine.Services;
using Xunit;

namespace UnderMine.Tests
{
    public class ProgressServiceTests
    {
        [Fact]
        public void GetHttpStatusMessage_ReturnsExpectedString()
        {
            Assert.Equal("Bad request (400)", ProgressService.GetHttpStatusMessage(HttpStatusCode.BadRequest));
            Assert.Equal("Authorization error (401)", ProgressService.GetHttpStatusMessage(HttpStatusCode.Unauthorized));
            Assert.Equal("Error 999", ProgressService.GetHttpStatusMessage((HttpStatusCode)999));
        }
    }
}
