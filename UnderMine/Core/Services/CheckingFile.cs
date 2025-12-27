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
        // Добавлен аргумент reporter
        public static async Task<bool> VerifyFolderIntegrityByZipHash(string folderPath, string expectedHash, ProgressReporter? reporter = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    reporter?.Log($"[Integrity] Starting verification for: {Path.GetFileName(folderPath)}");

                    if (!Directory.Exists(folderPath))
                    {
                        reporter?.Log($"[Integrity] Folder not found: {folderPath}", "WARN");
                        return false;
                    }

                    string actualHash = GetFolderChecksum(folderPath, reporter);

                    bool isMatch = actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);

                    if (isMatch)
                    {
                        reporter?.Log($"[Integrity] Hash verified successfully: {actualHash}");
                    }
                    else
                    {
                        reporter?.Log($"[Integrity] Hash MISMATCH! Expected: {expectedHash}, Actual: {actualHash}", "WARN");
                    }

                    try
                    {
                        string reportPath = Path.Combine(Directory.GetParent(folderPath)?.FullName ?? folderPath, $"DEBUG_{Path.GetFileName(folderPath)}_hash.json");
                        string json = "{\n" +
                                      $"  \"folder\": \"{Path.GetFileName(folderPath)}\",\n" +
                                      $"  \"expected\": \"{expectedHash}\",\n" +
                                      $"  \"actual\": \"{actualHash}\",\n" +
                                      $"  \"match\": {isMatch.ToString().ToLower()}\n" +
                                      "}";
                        File.WriteAllText(reportPath, json);
                        reporter?.Log($"[Integrity] Debug report saved to: {Path.GetFileName(reportPath)}");
                    }
                    catch (Exception reportEx)
                    {
                        reporter?.Log($"[Integrity] Failed to write debug report: {reportEx.Message}", "WARN");
                    }

                    return isMatch;
                }
                catch (Exception ex)
                {
                    reporter?.Log($"[Integrity] Error during verification: {ex.Message}", "ERROR");
                    return false;
                }
            });
        }

        private static string GetFolderChecksum(string folderPath, ProgressReporter? reporter)
        {
            var files = Directory.GetFiles(folderPath, "*.jar", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            reporter?.Log($"[Integrity] Found {files.Count} .jar files in {Path.GetFileName(folderPath)}");

            if (files.Count == 0) return "empty";

            using (var sha1 = SHA1.Create())
            {
                byte[] buffer = new byte[8192];

                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];

                    if (i % 50 == 0 && i > 0)
                    {
                        reporter?.Log($"[Integrity] Hashed {i}/{files.Count} files...");
                    }

                    string relativePath = Path.GetRelativePath(folderPath, file).Replace("\\", "/");
                    byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath.ToLower());
                    sha1.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);

                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
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