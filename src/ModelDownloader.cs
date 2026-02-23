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
    _cfg = cfg; _ui = ui;
  }
  public string EnsureModelAvailable(AccelKind accel)
  {
    Directory.CreateDirectory(_cfg.CacheDir);
    var sub = SelectRemoteSubfolder(accel);
    if (sub == null)
      throw new InvalidOperationException("Could not find a compatible subfolder in the repo.");

    var local = Path.Combine(_cfg.CacheDir, SanitizeFolderName(sub));
    if (IsUsableModelFolder(local))
    {
      _ui.Info("Using cached model: " + local);
      return local;
    }

    Directory.CreateDirectory(local);
    _ui.Info("Downloading model files…");
    DownloadFolder(sub, local);
    if (!IsUsableModelFolder(local))
      throw new InvalidOperationException("Download finished, but required files are missing.");
    _ui.Info("Model ready: " + local);
    return local;
  }
  private bool IsUsableModelFolder(string folder)
  {
    if (!Directory.Exists(folder)) return false;
    foreach (var f in _cfg.BaseFiles)
    {
      var p = Path.Combine(folder, f);
      if (!File.Exists(p)) return false;
    }
    var genai = Path.Combine(folder, "genai_config.json");
    var onnxName = ReadModelFilenameFromGenAiConfig(genai);
    if (string.IsNullOrWhiteSpace(onnxName)) return false;
    return File.Exists(Path.Combine(folder, onnxName));
  }
  private string? SelectRemoteSubfolder(AccelKind accel)
  {
    using var http = CreateHttpClient();
    var candidates = _cfg.GetCandidates(accel);
    foreach (var sub in candidates)
    {
      var testUrl = _cfg.RepoBase + sub + "genai_config.json";
      if (UrlExists(http, testUrl)) return sub;
    }
    var rootTest = _cfg.RepoBase + "genai_config.json";
    if (UrlExists(http, rootTest)) return "";
    return null;
  }
  private void DownloadFolder(string subfolder, string localFolder)
  {
    using var http = CreateHttpClient();
    foreach (var f in _cfg.BaseFiles)
    {
      var url = _cfg.RepoBase + subfolder + f;
      var dst = Path.Combine(localFolder, f);
      DownloadFileWithProgress(http, url, dst, required: true);
    }
    var genaiPath = Path.Combine(localFolder, "genai_config.json");
    var onnxName = ReadModelFilenameFromGenAiConfig(genaiPath);
    if (string.IsNullOrWhiteSpace(onnxName))
      throw new InvalidOperationException("Could not read model decoder filename from genai_config.json.");
    DownloadFileWithProgress(http, _cfg.RepoBase + subfolder + onnxName, Path.Combine(localFolder, onnxName), required: true);
    var onnxDataName = onnxName + ".data";
    DownloadFileWithProgress(http, _cfg.RepoBase + subfolder + onnxDataName, Path.Combine(localFolder, onnxDataName), required: false);
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
      if (!doc.RootElement.TryGetProperty("model", out var model)) return "";
      if (!model.TryGetProperty("decoder", out var decoder)) return "";
      if (!decoder.TryGetProperty("filename", out var fn)) return "";
      return fn.GetString() ?? "";
    }
    catch { return ""; }
  }
  private void DownloadFileWithProgress(HttpClient http, string url, string dst, bool required)
  {
    if (File.Exists(dst)) return;
    var tmp = dst + ".part";
    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
    try
    {
      using (var req = new HttpRequestMessage(HttpMethod.Get, url))
      using (var resp = http.Send(req, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None))
      {
        if (!resp.IsSuccessStatusCode)
        {
          if (required) throw new HttpRequestException("HTTP " + (int)resp.StatusCode + " for " + url);
          return;
        }
        var total = resp.Content.Headers.ContentLength;
        using (var src = resp.Content.ReadAsStream())
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
          var buf = new byte[_cfg.DownloadChunkBytes];
          long got = 0; int n; var fileName = Path.GetFileName(dst);
          var lastShown = DateTime.UtcNow;
          while ((n = src.Read(buf, 0, buf.Length)) > 0)
          {
            fs.Write(buf, 0, n); got += n; var now = DateTime.UtcNow;
            if ((now - lastShown).TotalMilliseconds >= 200)
            {
              lastShown = now;
              if (total.HasValue && total.Value > 0)
              {
                var pct = (double)got * 100.0 / total.Value; _ui.Write("\r" + fileName + " " + pct.ToString("0.0") + "%");
              }
              else { _ui.Write("\r" + fileName + " " + (got / (1024 * 1024)).ToString() + " MB"); }
            }
          }
          fs.Flush(true); _ui.Write("\r" + fileName + " done\n");
        }
      }
      SafeAtomicMove(tmp, dst);
    }
    catch { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } throw; }
  }
  private static void SafeAtomicMove(string tmp, string dst)
  {
    const int tries = 20, delayMs = 100;
    for (int i = 0; i < tries; i++)
    {
      try { if (File.Exists(dst)) File.Delete(dst); File.Move(tmp, dst); return; }
      catch (IOException) { if (i == tries - 1) throw; Thread.Sleep(delayMs); }
      catch (UnauthorizedAccessException) { if (i == tries - 1) throw; Thread.Sleep(delayMs); }
    }
  }
  private static bool UrlExists(HttpClient http, string url)
  {
    try { using var req = new HttpRequestMessage(HttpMethod.Get, url); using var resp = http.Send(req, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None); return resp.IsSuccessStatusCode; }
    catch { return false; }
  }
  private static string SanitizeFolderName(string s)
  {
    if (string.IsNullOrWhiteSpace(s)) return "root";
    s = s.Trim().Trim('/').Replace('/', '_').Replace('\\', '_');
    foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
    return s;
  }
  private static HttpClient CreateHttpClient()
  {
    var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
    var http = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromHours(3) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Phi3Chatbot/1.0");
    return http;
  }
}
