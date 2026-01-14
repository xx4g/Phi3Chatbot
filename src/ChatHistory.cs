using System;
using System.Collections.Generic;
using System.Text;

internal sealed class ChatHistory
{
    private readonly List<(string Role, string Text)> _msgs = new();
    public IReadOnlyList<(string Role, string Text)> Messages => _msgs;

    public void Clear() => _msgs.Clear();

    public void AddSystem(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_msgs.Count == 0 || _msgs[0].Role != "system")
            _msgs.Insert(0, ("system", text));
        else
            _msgs[0] = ("system", text);
    }

    public void AddUser(string text) => _msgs.Add(("user", text));
    public void AddAssistant(string text) => _msgs.Add(("assistant", text));

    public string BuildPrompt()
    {
        // Phi-3 chat template: <|system|>, <|user|>, <|assistant|>
        // This prevents the model from inventing "System:"/"User:" transcript lines.
        var sb = new StringBuilder();

        // system first (optional)
        if (_msgs.Count > 0 && _msgs[0].Role == "system")
        {
            sb.Append("<|system|>\n");
            sb.Append(_msgs[0].Text ?? "");
            sb.Append("\n");
        }

        // rest
        for (int i = 0; i < _msgs.Count; i++)
        {
            var m = _msgs[i];
            if (m.Role == "system") continue;

            if (m.Role == "user")
            {
                sb.Append("<|user|>\n");
                sb.Append(m.Text ?? "");
                sb.Append("\n");
            }
            else
            {
                sb.Append("<|assistant|>\n");
                sb.Append(m.Text ?? "");
                sb.Append("\n");
            }
        }

        // Ask for assistant continuation
        sb.Append("<|assistant|>\n");
        return sb.ToString();
    }

    public void TrimToTokenLimit(Phi3Runtime rt, int maxTokens)
    {
        if (maxTokens < 256) maxTokens = 256;

        while (_msgs.Count > 1)
        {
            var prompt = BuildPrompt();

            var seqs = rt.Tokenizer.Encode(prompt);
            int tokLen = 0;
            if (seqs.NumSequences > 0)
                tokLen = seqs[0].Length;

            if (tokLen <= maxTokens) return;

            int start = (_msgs.Count > 0 && _msgs[0].Role == "system") ? 1 : 0;
            if (start >= _msgs.Count) break;

            _msgs.RemoveAt(start);
        }
    }
}
