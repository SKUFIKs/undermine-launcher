using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UnderMine.Core.Services
{
    public class FileLoader
    {
        private readonly HttpClient _httpClient;

        public FileLoader(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task DownloadFileAsync(string url, string path, CancellationToken ct, Action<double>? onProgress = null)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, ct);
                totalRead += read;

                if (totalBytes.HasValue && onProgress != null)
                {
                    double progress = (double)totalRead / totalBytes.Value * 100;
                    onProgress(progress);
                }
            }
        }
    }
}