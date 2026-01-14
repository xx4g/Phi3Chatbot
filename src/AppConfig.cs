using System;
using System.IO;

internal sealed class AppConfig
{
    public string AppTitle { get; init; } = "Phi-3 Mini Instruct ONNX Chatbot";

    public string RepoBase { get; init; } =
        "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/resolve/main/";

    // Candidate subfolders inside the repo. First one that contains genai_config.json wins.
    public string[] CandidateSubfolders { get; init; } = new[]
    {
        "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/",
        "cpu_and_mobile/cpu-int4-rtn-block-32/",
        "directml/directml-int4-awq-block-128/",
        "cuda/cuda-fp16/",
        "cuda/cuda-int4-rtn-block-32/",
    };

    // Files typically required to load tokenizer + config.
    // The model .onnx filename is discovered from genai_config.json at runtime.
    public string[] BaseFiles { get; init; } = new[]
    {
        "genai_config.json",
        "config.json",
        "tokenizer.json",
        "tokenizer_config.json",
        "special_tokens_map.json",
    };

    // Often present (download if available)
    public string[] ExtraFiles { get; init; } = new[]
    {
        "added_tokens.json",
        "tokenizer.model",
        "vocab.json",
        "merges.txt",
        "configuration_phi3.py",
    };

    public string CacheDir { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Phi3Chatbot", "model");

    public int DownloadChunkBytes { get; init; } = 1 << 20;

    public string ExitCommand { get; init; } = "/exit";
    public string ResetCommand { get; init; } = "/reset";
    public string HelpCommand { get; init; } = "/help";

    // Generation
    public int MaxNewTokens { get; init; } = 256;
    public float Temperature { get; init; } = 0.7f;
    public float TopP { get; init; } = 0.9f;

    // History trimming (token-based)
    public int MaxHistoryTokens { get; init; } = 3000;

    // Prompt format
    public string SystemPrompt { get; init; } =
        "You are a helpful assistant.";

    public static AppConfig Default() => new AppConfig();
}
