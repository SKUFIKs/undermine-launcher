using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using Newtonsoft.Json.Linq;
using UnderMine.Core.server_1;
using Microsoft.VSDiagnostics;

[SimpleJob(RuntimeMoniker.Net70)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[CPUUsageDiagnoser]
public class ModLoaderBenchmarks
{
    private string _tempDir;
    private HttpListener _listener;
    private string _manifestUrl;
    private int _modCount = 5;
    [GlobalSetup]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "modloader_bench_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        // Создаём тестовые моды (файлы в файловой системе), которые будет отдавать локальный HTTP сервер
        for (int i = 0; i < _modCount; i++)
        {
            var filePath = Path.Combine(_tempDir, $"mod{i}.jar");
            // 200 KB file
            var data = new byte[200 * 1024];
            // fill with pseudo-random content to avoid extreme compression or optimizations
            var rnd = new Random(i);
            rnd.NextBytes(data);
            File.WriteAllBytes(filePath, data);
        }

        // Запускаем локальный HTTP сервер
        _listener = new HttpListener();
        string prefix = "http://127.0.0.1:5001/";
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        Task.Run(() => HandleRequests());
        // Генерируем манифест, который указывает на наши файлы
        var manifest = new JObject();
        var arr = new JArray();
        for (int i = 0; i < _modCount; i++)
        {
            arr.Add(new JObject { ["Name"] = $"mod{i}.jar", ["Url"] = $"{prefix}files/mod{i}.jar", ["Hash"] = "" });
        }

        manifest["Mods"] = arr;
        manifest["AllowedUserMods"] = new JArray();
        File.WriteAllText(Path.Combine(_tempDir, "manifest.json"), manifest.ToString());
        _manifestUrl = prefix + "manifest";
    }

    private async Task HandleRequests()
    {
        while (_listener.IsListening)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                var req = ctx.Request;
                var res = ctx.Response;
                if (req.RawUrl.StartsWith("/manifest"))
                {
                    var manifestPath = Path.Combine(_tempDir, "manifest.json");
                    var bytes = File.ReadAllBytes(manifestPath);
                    res.ContentType = "application/json";
                    res.ContentLength64 = bytes.Length;
                    await res.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    res.Close();
                }
                else if (req.RawUrl.StartsWith("/files/"))
                {
                    var name = req.RawUrl.Substring("/files/".Length);
                    var path = Path.Combine(_tempDir, name);
                    if (File.Exists(path))
                    {
                        using var fs = File.OpenRead(path);
                        res.ContentLength64 = fs.Length;
                        res.ContentType = "application/java-archive";
                        await fs.CopyToAsync(res.OutputStream);
                    }
                    else
                    {
                        res.StatusCode = 404;
                    }

                    res.Close();
                }
                else
                {
                    res.StatusCode = 404;
                    res.Close();
                }
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
            // ignore
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
            }
        }
        catch
        {
        }

        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    [Benchmark]
    public async Task RunModLoader()
    {
        var modsDir = Path.Combine(Path.GetTempPath(), "modloader_output_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(modsDir);
        var loader = new ModLoader();
        await loader.InstallOrUpdateMods(modsDir, _manifestUrl);
    }
}