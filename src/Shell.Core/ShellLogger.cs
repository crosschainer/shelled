using System;
using System.Diagnostics;
using System.IO;

namespace Shell.Core;

/// <summary>
/// Simple file-based logger shared by core, service, bootstrap, and bridge.
/// Designed to be safe in both dev/test and production: failures to log never
/// crash the shell.
/// </summary>
public static class ShellLogger
{
    private static readonly object Sync = new();

    private static string GetLogFilePath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDir = Path.Combine(baseDir, "Shelled", "Logs");
        Directory.CreateDirectory(logDir);

        var fileName = $"shelled-{DateTime.UtcNow:yyyyMMdd}.log";
        return Path.Combine(logDir, fileName);
    }

    public static void Info(string source, string message) => Write("INFO", source, message);

    public static void Warn(string source, string message) => Write("WARN", source, message);

    public static void Error(string source, string message, Exception? ex = null) =>
        Write("ERROR", source, message + (ex != null ? $" :: {ex}" : string.Empty));

    private static void Write(string level, string source, string message)
    {
        try
        {
            var line = $"{DateTime.UtcNow:O} [{level}] [{source}] {message}";
            var path = GetLogFilePath();

            lock (Sync)
            {
                File.AppendAllLines(path, new[] { line });
            }

            Debug.WriteLine(line);
        }
        catch
        {
            // Never throw from logging; this is strictly best-effort.
        }
    }
}

