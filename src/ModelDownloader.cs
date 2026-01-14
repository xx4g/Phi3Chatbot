using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

internal sealed class ModelDownloader
{
    private readonly AppConfig _cfg;
    private readonly ConsoleUi _ui;

    public ModelDownloader(AppConfig cfg, ConsoleUi ui)
    {
        _cfg = cfg;
        _ui = ui;
    }

    public string EnsureModelAvailable()
    {
        Directory.CreateDirectory(_cfg.CacheDir);

        var existing = FindFirstUsableLocalFolder();
        if (existing != null)
        {
            _ui.Info("Using cached model: " + existing);
            return existing;
        }

        _ui.Info("Selecting model variant from Hugging Face…");
        var sub = SelectRemoteSubfolder();
        if (sub == null)
            throw new InvalidOperationException("Could not find a compatible subfolder in the repo.");

        var local = Path.Combine(_cfg.CacheDir, SanitizeFolderName(sub));
        Directory.CreateDirectory(local);

        _ui.Info("Downloading model files…");
        DownloadFolder(sub, local);

        if (!HasAllBaseFiles(local))
            throw new InvalidOperationException("Download finished, but required base files are missing.");

        // Verify ONNX referenced by genai_config.json exists locally
        var onnxName = ReadModelFilenameFromGenAiConfig(Path.Combine(local, "genai_config.json"));
        if (string.IsNullOrWhiteSpace(onnxName))
            throw new InvalidOperationException("genai_config.json did not contain a model decoder filename.");

        var onnxPath = Path.Combine(local, onnxName);
        if (!File.Exists(onnxPath))
            throw new InvalidOperationException("Model ONNX file missing: " + onnxName);

        _ui.Info("Model ready: " + local);
        return local;
    }

    private string? FindFirstUsableLocalFolder()
    {
        if (!Directory.Exists(_cfg.CacheDir)) return null;

        if (IsUsableModelFolder(_cfg.CacheDir))
            return _cfg.CacheDir;

        foreach (var d in Directory.GetDirectories(_cfg.CacheDir))
        {
            if (IsUsableModelFolder(d))
                return d;
        }
        return null;
    }

    private bool IsUsableModelFolder(string folder)
    {
        if (!HasAllBaseFiles(folder)) return false;

        var genai = Path.Combine(folder, "genai_config.json");
        var onnxName = ReadModelFilenameFromGenAiConfig(genai);
        if (string.IsNullOrWhiteSpace(onnxName)) return false;

        return File.Exists(Path.Combine(folder, onnxName));
    }

    private bool HasAllBaseFiles(string folder)
    {
        foreach (var f in _cfg.BaseFiles)
        {
            var p = Path.Combine(folder, f);
            if (!File.Exists(p)) return false;
        }
        return true;
    }

    private string? SelectRemoteSubfolder()
    {
        using var http = CreateHttpClient();

        foreach (var sub in _cfg.CandidateSubfolders)
        {
            var testUrl = _cfg.RepoBase + sub + "genai_config.json";
            if (UrlExists(http, testUrl))
                return sub;
        }

        // fall back: try root
        var rootTest = _cfg.RepoBase + "genai_config.json";
        if (UrlExists(http, rootTest))
            return "";

        return null;
    }

    private void DownloadFolder(string subfolder, string localFolder)
    {
        using var http = CreateHttpClient();

        // 1) Download base files (must exist)
        foreach (var f in _cfg.BaseFiles)
        {
            var url = _cfg.RepoBase + subfolder + f;
            var dst = Path.Combine(localFolder, f);
            DownloadFileWithProgress(http, url, dst, required: true);
        }

        // 2) Read genai_config.json to know the ONNX filename
        var genaiPath = Path.Combine(localFolder, "genai_config.json");
        var onnxName = ReadModelFilenameFromGenAiConfig(genaiPath);
        if (string.IsNullOrWhiteSpace(onnxName))
            throw new InvalidOperationException("Could not read model decoder filename from genai_config.json.");

        // 3) Download ONNX file + its external data if present
        DownloadFileWithProgress(http, _cfg.RepoBase + subfolder + onnxName, Path.Combine(localFolder, onnxName), required: true);

        // External data file is commonly "<onnx>.data"
        var onnxDataName = onnxName + ".data";
        DownloadFileWithProgress(http, _cfg.RepoBase + subfolder + onnxDataName, Path.Combine(localFolder, onnxDataName), required: false);

        // 4) Try extra files (optional)
        foreach (var f in _cfg.ExtraFiles)
        {
            var url = _cfg.RepoBase + subfolder + f;
            var dst = Path.Combine(localFolder, f);
            DownloadFileWithProgress(http, url, dst, required: false);
        }
    }

    private static string ReadModelFilenameFromGenAiConfig(string genaiConfigPath)
    {
        try
        {
            var json = File.ReadAllText(genaiConfigPath, Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);

            // Path: model.decoder.filename
            if (!doc.RootElement.TryGetProperty("model", out var model)) return "";
            if (!model.TryGetProperty("decoder", out var decoder)) return "";
            if (!decoder.TryGetProperty("filename", out var fn)) return "";
            return fn.GetString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private void DownloadFileWithProgress(HttpClient http, string url, string dst, bool required)
    {
        if (File.Exists(dst))
            return;

        var tmp = dst + ".part";

        // Ensure parent dir exists
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

        try
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            using (var resp = http.Send(req, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None))
            {
                if (!resp.IsSuccessStatusCode)
                {
                    if (required)
                        throw new HttpRequestException("HTTP " + (int)resp.StatusCode + " for " + url);
                    return;
                }

                var total = resp.Content.Headers.ContentLength;

                using (var src = resp.Content.ReadAsStream())
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    var buf = new byte[_cfg.DownloadChunkBytes];
                    long got = 0;
                    int n;

                    var fileName = Path.GetFileName(dst);
                    var lastShown = DateTime.UtcNow;

                    while ((n = src.Read(buf, 0, buf.Length)) > 0)
                    {
                        fs.Write(buf, 0, n);
                        got += n;

                        var now = DateTime.UtcNow;
                        if ((now - lastShown).TotalMilliseconds >= 200)
                        {
                            lastShown = now;
                            if (total.HasValue && total.Value > 0)
                            {
                                var pct = (double)got * 100.0 / total.Value;
                                _ui.Write("\r" + fileName + "  " + pct.ToString("0.0") + "%");
                            }
                            else
                            {
                                _ui.Write("\r" + fileName + "  " + (got / (1024 * 1024)).ToString() + " MB");
                            }
                        }
                    }

                    fs.Flush(true);
                    _ui.Write("\r" + fileName + "  done\n");
                }
            }

            // Move into place AFTER streams are closed (prevents Windows file-lock issues)
            SafeAtomicMove(tmp, dst);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            throw;
        }
    }


    private static void SafeAtomicMove(string tmp, string dst)
    {
        // Retry move a few times (AV scanners / indexing can briefly lock new files)
        const int tries = 20;
        const int delayMs = 100;

        for (int i = 0; i < tries; i++)
        {
            try
            {
                if (File.Exists(dst))
                {
                    // Replace if target exists (rare)
                    File.Delete(dst);
                }

                File.Move(tmp, dst);
                return;
            }
            catch (IOException)
            {
                if (i == tries - 1) throw;
                Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException)
            {
                if (i == tries - 1) throw;
                Thread.Sleep(delayMs);
            }
        }
    }

private static bool UrlExists
(HttpClient http, string url)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = http.Send(req, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeFolderName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "root";
        s = s.Trim().Trim('/').Replace('/', '_').Replace('\\', '_');
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };

        var http = new HttpClient(handler, disposeHandler: true);
        http.Timeout = TimeSpan.FromHours(3);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Phi3Chatbot/1.0");
        return http;
    }

}