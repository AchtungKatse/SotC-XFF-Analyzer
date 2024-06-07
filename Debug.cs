public static class Debug
{
    public enum LogLevel { Debug, Warn, Info, Error, Critical };
    public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Debug;


    // Effectivley stops the entire program from working
    public static void LogCritical(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine($"CRITICAL: {message}");
        Console.ResetColor();
    }

    // Does not work / breaks a function
    public static void LogError(string message)
    {
        if (CurrentLogLevel > LogLevel.Error)
            return;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"ERROR: {message}");
        Console.ResetColor();
    }

    // Not intended but the program will still function
    public static void LogWarn(string message)
    {
        if (CurrentLogLevel > LogLevel.Warn)
            return;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Warn: {message}");
        Console.ResetColor();
    }

    // Good to know
    public static void LogInfo(string message)
    {
        if (CurrentLogLevel > LogLevel.Info)
            return;

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"Info: {message}");
        Console.ResetColor();
    }

    // I'm probably hating my life at this point
    public static void LogDebug(string message)
    {
        if (CurrentLogLevel > LogLevel.Debug)
            return;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Debug: {message}");
        Console.ResetColor();
    }
}