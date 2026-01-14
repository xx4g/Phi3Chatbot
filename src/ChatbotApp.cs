using System;

internal sealed class ChatbotApp
{
    private readonly AppConfig _cfg;
    private readonly ConsoleUi _ui;
    private readonly ModelDownloader _downloader;

    public ChatbotApp(AppConfig cfg)
    {
        _cfg = cfg;
        _ui = new ConsoleUi();
        _downloader = new ModelDownloader(_cfg, _ui);
    }

    public int Run(string[] args)
    {
        _ui.WriteHeader(_cfg.AppTitle);

        var modelRoot = _downloader.EnsureModelAvailable();

        using var runtime = new Phi3Runtime(_cfg, _ui, modelRoot);
        var chat = new ChatSession(_cfg, _ui, runtime);
        chat.Run();

        return 0;
    }
}
