using System;
using System.Text;
using Microsoft.ML.OnnxRuntimeGenAI;

internal sealed class ChatSession
{
    private readonly AppConfig _cfg;
    private readonly ConsoleUi _ui;
    private readonly Phi3Runtime _rt;
    private readonly ChatHistory _history;

    public ChatSession(AppConfig cfg, ConsoleUi ui, Phi3Runtime rt)
    {
        _cfg = cfg;
        _ui = ui;
        _rt = rt;
        _history = new ChatHistory();
        _history.AddSystem(_cfg.SystemPrompt);
    }

    public void Run()
    {
        _ui.Info("Type a message. " + _cfg.HelpCommand + " for commands.");
        _ui.WriteLine();

        while (true)
        {
            var input = _ui.Prompt("> ");
            if (input == null) break;

            input = input.Trim();
            if (input.Length == 0) continue;

            if (input.Equals(_cfg.ExitCommand, StringComparison.OrdinalIgnoreCase))
                break;

            if (input.Equals(_cfg.HelpCommand, StringComparison.OrdinalIgnoreCase))
            {
                _ui.PrintHelp(_cfg);
                continue;
            }

            if (input.Equals(_cfg.ResetCommand, StringComparison.OrdinalIgnoreCase))
            {
                _history.Clear();
                _history.AddSystem(_cfg.SystemPrompt);
                _ui.Info("History cleared.");
                continue;
            }

            _history.AddUser(input);
            _history.TrimToTokenLimit(_rt, _cfg.MaxHistoryTokens);

            var prompt = _history.BuildPrompt();

            _ui.WriteLine();
            var reply = GenerateOneTurnStreaming(prompt);
            reply = CleanPhi3Artifacts(reply);
            _history.AddAssistant(reply);

            _ui.WriteLine();
            _ui.WriteLine();
        }
    }

    private string GenerateOneTurnStreaming(string prompt)
    {
        // Encode prompt (batch size 1)
        var seqs = _rt.Tokenizer.Encode(prompt);
        if (seqs.NumSequences == 0)
            return "";

        ReadOnlySpan<int> inputIds = seqs[0];
        int inputLen = inputIds.Length;

        int maxLen = inputLen + Math.Max(1, _cfg.MaxNewTokens);

        var gp = new GeneratorParams(_rt.Model);
        gp.SetSearchOption("max_length", maxLen);
        gp.SetSearchOption("temperature", _cfg.Temperature);
        gp.SetSearchOption("top_p", _cfg.TopP);
        try { gp.SetSearchOption("do_sample", true); } catch { }

        using var generator = new Generator(_rt.Model, gp);

        // Important: append the prompt tokens
        generator.AppendTokens(inputIds);

        // We decode ONLY generated tokens by slicing off the prompt length.
        string lastGenText = "";
        var assistantOut = new StringBuilder();

        while (!generator.IsDone())
        {
            generator.GenerateNextToken();

            ReadOnlySpan<int> outSeq = generator.GetSequence(0);

            // Safety: if something weird happens, don't crash
            if (outSeq.Length < inputLen)
                continue;

            ReadOnlySpan<int> genOnly = outSeq.Slice(inputLen);
            var genText = _rt.Tokenizer.Decode(genOnly);

            // Stop if the model starts emitting new role tokens (rare, but possible)
            int cut = FindRoleToken(genText);
            if (cut >= 0)
                genText = genText.Substring(0, cut);

            // Stream delta of generated-only text
            var delta = DiffSuffix(lastGenText, genText);
            lastGenText = genText;

            if (delta.Length > 0)
            {
                // Remove wrapper immediately so it never shows up while streaming
                delta = delta.Replace("- [response]:", "", StringComparison.OrdinalIgnoreCase);

                _ui.Write(delta);
                assistantOut.Append(delta);
            }

            if (cut >= 0)
                break;
        }

        return assistantOut.ToString().Trim();
    }

    private static int FindRoleToken(string s)
    {
        int a = s.IndexOf("<|system|>", StringComparison.Ordinal);
        int b = s.IndexOf("<|user|>", StringComparison.Ordinal);
        int c = s.IndexOf("<|assistant|>", StringComparison.Ordinal);

        int cut = -1;
        if (a >= 0) cut = a;
        if (b >= 0) cut = (cut < 0) ? b : Math.Min(cut, b);
        if (c >= 0) cut = (cut < 0) ? c : Math.Min(cut, c);
        return cut;
    }

    private static string DiffSuffix(string prev, string now)
    {
        if (string.IsNullOrEmpty(prev)) return now;
        if (now.StartsWith(prev, StringComparison.Ordinal)) return now.Substring(prev.Length);

        int n = Math.Min(prev.Length, now.Length);
        int i = 0;
        while (i < n && prev[i] == now[i]) i++;
        return now.Substring(i);
    }

    private static string CleanPhi3Artifacts(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";

        s = s.Replace("- [response]:", "", StringComparison.OrdinalIgnoreCase).Trim();

        if (s.StartsWith("[response]:", StringComparison.OrdinalIgnoreCase))
            s = s.Substring("[response]:".Length).Trim();
        if (s.StartsWith("response:", StringComparison.OrdinalIgnoreCase))
            s = s.Substring("response:".Length).Trim();

        // If role tokens leak, cut them off
        int cut = FindAnyRoleToken(s);
        if (cut >= 0) s = s.Substring(0, cut).Trim();

        return s;
    }

    private static int FindAnyRoleToken(string s)
    {
        int a = s.IndexOf("<|system|>", StringComparison.Ordinal);
        int b = s.IndexOf("<|user|>", StringComparison.Ordinal);
        int c = s.IndexOf("<|assistant|>", StringComparison.Ordinal);

        int cut = -1;
        if (a >= 0) cut = a;
        if (b >= 0) cut = (cut < 0) ? b : Math.Min(cut, b);
        if (c >= 0) cut = (cut < 0) ? c : Math.Min(cut, c);
        return cut;
    }
}
