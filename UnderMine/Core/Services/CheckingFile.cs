using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnderMine.Services.Logger;

namespace UnderMine.Core.Services
{
    public class CheckingFile
    {
        public static async Task<bool> VerifyFolderIntegrityByZipHash(string folderPath, string expectedHash, ProgressReporter? reporter = null)
        {
            if (string.IsNullOrWhiteSpace(expectedHash)) return true;

            return await Task.Run(() =>
            {
                try
                {
                    reporter?.Log($"[Integrity] Проверка папки: {Path.GetFileName(folderPath)}");

                    if (!Directory.Exists(folderPath))
                    {
                        reporter?.Log($"[Integrity] Папка не найдена: {folderPath}", "WARN");
                        return false;
                    }

                    string actualHash = GetFolderChecksum(folderPath, reporter);

                    string cleanExpected = expectedHash.Trim().Replace("\r", "").Replace("\n", "").ToLower();
                    string cleanActual = actualHash.Trim().ToLower();

                    bool isMatch = cleanActual == cleanExpected;

                    if (isMatch)
                    {
                        reporter?.Log($"[Integrity] Успешно: {cleanActual}");
                    }
                    else
                    {
                        reporter?.Log($"[Integrity] MISMATCH! Ожидалось: {cleanExpected}, Получено: {cleanActual}", "WARN");
                    }

                    WriteDebugReport(folderPath, cleanExpected, cleanActual, isMatch, reporter);

                    return isMatch;
                }
                catch (Exception ex)
                {
                    reporter?.Log($"[Integrity] Ошибка: {ex.Message}", "ERROR");
                    return false;
                }
            });
        }

        private static string GetFolderChecksum(string folderPath, ProgressReporter? reporter)
        {
            var files = Directory.GetFiles(folderPath, "*.jar", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0) return "empty";

            using (var sha1 = SHA1.Create())
            {
                byte[] buffer = new byte[8192];

                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];

                    string relativePath = Path.GetRelativePath(folderPath, file).Replace("\\", "/").ToLower();
                    byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
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

        private static void WriteDebugReport(string folderPath, string exp, string act, bool match, ProgressReporter? reporter)
        {
            try
            {
                string reportPath = Path.Combine(Directory.GetParent(folderPath)?.FullName ?? folderPath, $"DEBUG_{Path.GetFileName(folderPath)}_hash.json");
                string json = $"{{\n  \"folder\": \"{Path.GetFileName(folderPath)}\",\n  \"expected\": \"{exp}\",\n  \"actual\": \"{act}\",\n  \"match\": {match.ToString().ToLower()}\n}}";
                File.WriteAllText(reportPath, json);
            }
            catch { /* Игнорируем ошибки записи лога */ }
        }
    }
}