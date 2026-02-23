using System;

internal enum AccelKind { Cpu, DirectML }

internal static class HardwareSelector
{
    public static AccelKind SelectAccelViaDropdown()
    {
        Console.WriteLine("Select hardware backend:");
        Console.WriteLine("  1) CPU");
        Console.WriteLine("  2) GPU (DirectML)");
        Console.Write("Choice [1-2]: ");
        while (true)
        {
            var key = Console.ReadKey(intercept: true).KeyChar;
            if (key == '1') { Console.WriteLine("1"); return AccelKind.Cpu; }
            if (key == '2') { Console.WriteLine("2"); return AccelKind.DirectML; }
        }
    }
}
