using System;
using System.Text;
internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            var accel = HardwareSelector.SelectAccelViaDropdown();
            var cfg = AppConfig.Default();
            var app = new ChatbotApp(cfg, accel);
            return app.Run(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine("FATAL: " + ex);
            return 1;
        }
    }
}
