using System;
using System.Diagnostics;

public static class Logger
{
    public static void Log(string message)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    public static void Log(string message, params object[] args)
    {
        Log(string.Format(message, args));
    }
}
