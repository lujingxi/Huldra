using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Logging;

public static class Logger
{
    public static void Log(LogLevel level, string message)
    {
        Console.WriteLine($"[{level}] {message}");
    }
}
