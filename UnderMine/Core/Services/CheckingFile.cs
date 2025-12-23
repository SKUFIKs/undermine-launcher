using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace UnderMine.Core.Services
{
    public class CheckingFile
    {
        public static async Task<bool> VerifyFolderIntegrityByZipHash(string folderPath, string expectedHash)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(folderPath)) return false;

                    string actualHash = GetFolderChecksum(folderPath);

                    string reportPath = Path.Combine(Directory.GetParent(folderPath).FullName, $"DEBUG_{Path.GetFileName(folderPath)}_hash.json");
                    string json = "{\n" +
                                 $"  \"folder\": \"{Path.GetFileName(folderPath)}\",\n" +
                                 $"  \"expected\": \"{expectedHash}\",\n" +
                                 $"  \"actual\": \"{actualHash}\",\n" +
                                 $"  \"match\": {actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase).ToString().ToLower()}\n" +
                                 "}";
                    File.WriteAllText(reportPath, json);

                    return actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return false;
                }
            });
        }

        private static string GetFolderChecksum(string folderPath)
        {
           
            var files = Directory.GetFiles(folderPath, "*.jar", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase) 
                .ToList();

            if (files.Count == 0) return "empty";

            using (var sha1 = SHA1.Create())
            {
                foreach (var file in files)
                {
                    
                    string relativePath = Path.GetRelativePath(folderPath, file).Replace("\\", "/");
                    byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath.ToLower()); 
                    sha1.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);


                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            sha1.TransformBlock(buffer, 0, bytesRead, null, 0);
                        }
                    }
                }

                sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                return BitConverter.ToString(sha1.Hash).Replace("-", "").ToLower();
            }
        }
    }
}