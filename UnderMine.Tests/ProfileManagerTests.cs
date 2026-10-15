using UnderMine.Models;
using Xunit;

namespace UnderMine.Tests
{
    public class ProfileManagerTests
    {
        [Fact]
        public void EncryptDecrypt_RoundTrips()
        {
            const string secret = "hunter2-\u00fc";

            var cipher = ProfileManager.Encrypt(secret);
            Assert.NotEqual(secret, cipher);
            Assert.Equal(secret, ProfileManager.Decrypt(cipher));
        }

        [Fact]
        public void EncryptDecrypt_HandlesEmptyStrings()
        {
            Assert.Equal(string.Empty, ProfileManager.Encrypt(string.Empty));
            Assert.Equal(string.Empty, ProfileManager.Decrypt(string.Empty));
        }

        [Fact]
        public void Decrypt_ReturnsEmptyForInvalidCipher()
        {
            Assert.Equal(string.Empty, ProfileManager.Decrypt("not-base64"));
        }
    }
}
