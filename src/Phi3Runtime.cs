using System;
using Microsoft.ML.OnnxRuntimeGenAI;

internal sealed class Phi3Runtime : IDisposable
{
    private readonly AppConfig _cfg;
    private readonly ConsoleUi _ui;

    public string ModelRoot { get; }
    public Model Model { get; }
    public Tokenizer Tokenizer { get; }

    public Phi3Runtime(AppConfig cfg, ConsoleUi ui, string modelRoot)
    {
        _cfg = cfg;
        _ui = ui;
        ModelRoot = modelRoot;

        _ui.Info("Loading model…");
        Model = new Model(modelRoot);
        Tokenizer = new Tokenizer(Model);
        _ui.Info("Model loaded.");
    }

    public void Dispose()
    {
        try { Tokenizer.Dispose(); } catch { }
        try { Model.Dispose(); } catch { }
    }
}
