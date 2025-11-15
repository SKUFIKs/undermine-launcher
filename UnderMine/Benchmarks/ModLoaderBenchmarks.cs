using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using Newtonsoft.Json.Linq;
using UnderMine.Core.server_1;

[SimpleJob(RuntimeMoniker.Net70)]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ModLoaderBenchmarks
{
    private string _tempDir;
    private HttpListener _listener;
    private string _manifestUrl;
    private int _modCount = 5;

    private Task _serverTask;
    private CancellationTokenSource _cts;
    private int _port;

    [GlobalSetup]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "modloader_bench_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Создаём тестовые моды (файлы в файловой системе), которые будет отдавать локальный HTTP сервер
        var rnd = new Random(42);
        for (int i = 0; i < _modCount; i++)
        {
            var filePath = Path.Combine(_tempDir, $"mod{i}.jar");
            var data = new byte[200 * 1024];
            rnd.NextBytes(data);
            File.WriteAllBytes(filePath, data);
        }

        // Найдём свободный порт и запустим локальный HTTP сервер
        _port = GetFreePort();
        var prefix = $"http://127.0.0.1:{_port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _listener.Start();

        _cts = new CancellationTokenSource();
        _serverTask = Task.Run(() => HandleRequests(_cts.Token));

        // Генерируем манифест
        var manifest = new JObject();
        var arr = new JArray();
        for (int i = 0; i < _modCount; i++)
        {
            arr.Add(new JObject
            {
                ["Name"] = $"mod{i}.jar",
                ["Url"] = $"{prefix}files/mod{i}.jar",
                ["Hash"] = ""
            });
        }
        manifest["Mods"] = arr;
        manifest["AllowedUserMods"] = new JArray();

        File.WriteAllText(Path.Combine(_tempDir, "manifest.json"), manifest.ToString());
        _manifestUrl = prefix + "manifest";
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task HandleRequests(CancellationToken ct)
    {
        while (_listener != null && _listener.IsListening && !ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(async () =>
                {
                    var req = ctx.Request;
                    var res = ctx.Response;
                    try
                    {
                        if (req.RawUrl.StartsWith("/manifest"))
                        {
                            var manifestPath = Path.Combine(_tempDir, "manifest.json");
                            byte[] bytes = await File.ReadAllBytesAsync(manifestPath).ConfigureAwait(false);
                            res.ContentType = "application/json";
                            res.ContentLength64 = bytes.Length;
                            await res.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                        }
                        else if (req.RawUrl.StartsWith("/files/"))
                        {
                            var name = req.RawUrl.Substring("/files/".Length);
                            var path = Path.Combine(_tempDir, name);
                            if (File.Exists(path))
                            {
                                res.ContentType = "application/java-archive";
                                using var fs = File.OpenRead(path);
                                res.ContentLength64 = fs.Length;
                                await fs.CopyToAsync(res.OutputStream).ConfigureAwait(false);
                            }
                            else
                            {
                                res.StatusCode = 404;
                            }
                        }
                        else
                        {
                            res.StatusCode = 404;
                        }
                    }
                    catch
                    {
                        // ignore individual request failures
                    }
                    finally
                    {
                        try { res.OutputStream.Close(); } catch { }
                        res.Close();
                    }
                }, ct);
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
                // игнорировать остальные ошибки и продолжать, если возможно
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try { _cts?.Cancel(); } catch { }

        try
        {
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
            }
        }
        catch { }

        try { _serverTask?.Wait(500); } catch { }

        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Benchmark]
    public async Task RunModLoader()
    {
        var modsDir = Path.Combine(Path.GetTempPath(), "modloader_output_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(modsDir);
        var loader = new ModLoader();
        await loader.InstallOrUpdateMods(modsDir, _manifestUrl).ConfigureAwait(false);
    }
}
