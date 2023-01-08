using System;
using Microsoft.Extensions.Logging;

namespace SideloaderModpackUpdater.Data;

public static class SimpleLogger
{
    public static void LogInfo(string value)
    {
        Console.WriteLine($"[Info] {DateTime.Now}: {value}");
    }
    public static void LogError(string value)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Error] {DateTime.Now}: {value}");
        Console.ResetColor();
    }
}