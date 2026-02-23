using System;
internal sealed class ChatbotApp
{
  private readonly AppConfig _cfg;
  private readonly ConsoleUi _ui;
  private readonly ModelDownloader _downloader;
  private readonly AccelKind _accel;
  public ChatbotApp(AppConfig cfg, AccelKind accel)
  {
    _cfg = cfg; _accel = accel; _ui = new ConsoleUi(); _downloader = new ModelDownloader(_cfg, _ui);
  }
  public int Run(string[] args)
  {
    _ui.WriteHeader(_cfg.AppTitle + " (" + _accel + ")");
    var modelRoot = _downloader.EnsureModelAvailable(_accel);
    using var runtime = new Phi3Runtime(_cfg, _ui, modelRoot, _accel);
    var chat = new ChatSession(_cfg, _ui, runtime);
    chat.Run();
    return 0;
  }
}
