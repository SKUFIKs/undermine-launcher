using UnderMine.Core.Game;
using Xunit;

namespace UnderMine.Tests
{
    public class JavaRuntimeServiceTests
    {
        [Theory]
        [InlineData("openjdk version \"25.0.1\" 2025-10-21 LTS", 25)]
        [InlineData("openjdk version \"17.0.9\" 2023-10-17", 17)]
        [InlineData("openjdk version \"21.0.2\" 2024-01-16", 21)]
        [InlineData("java version \"1.8.0_402\"", 8)]
        [InlineData("", 0)]
        [InlineData("command not found", 0)]
        public void ParseMajorVersion_ReadsMajorFromVersionOutput(string output, int expected)
        {
            Assert.Equal(expected, JavaRuntimeService.ParseMajorVersion(output));
        }
    }
}
