using System;

internal sealed class ConsoleUi
{
    public void WriteHeader(string title)
    {
        Console.WriteLine("============================================================");
        Console.WriteLine(title);
        Console.WriteLine("============================================================");
    }

    public void Info(string msg) => Console.WriteLine(msg);
    public void Warn(string msg) => Console.WriteLine("WARN: " + msg);
    public void Error(string msg) => Console.WriteLine("ERROR: " + msg);

    public string? Prompt(string label)
    {
        Console.Write(label);
        return Console.ReadLine();
    }

    public void Write(string s) => Console.Write(s);
    public void WriteLine(string s = "") => Console.WriteLine(s);

    public void PrintHelp(AppConfig cfg)
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  " + cfg.HelpCommand + "   show this help");
        Console.WriteLine("  " + cfg.ResetCommand + "  clear conversation history");
        Console.WriteLine("  " + cfg.ExitCommand + "   quit");
    }
}
