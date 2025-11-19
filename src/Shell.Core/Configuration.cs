using System;

namespace Shell.Core;

/// <summary>
/// Configuration settings for the shell system
/// </summary>
public static class ShellConfiguration
{
    /// <summary>
    /// Gets whether the shell is running in test mode.
    /// In test mode, dangerous operations like registry modifications and shell registration are disabled.
    /// </summary>
    public static bool IsTestMode => 
        Environment.GetEnvironmentVariable("SHELL_TEST_MODE")?.ToLowerInvariant() is "1" or "true" or "yes";

    /// <summary>
    /// Gets whether the shell is running in development mode.
    /// In dev mode, the shell runs on top of Explorer without replacing it.
    /// </summary>
    public static bool IsDevMode => 
        Environment.GetEnvironmentVariable("SHELL_DEV_MODE")?.ToLowerInvariant() is "1" or "true" or "yes";

    /// <summary>
    /// Gets whether dangerous operations should be disabled.
    /// This includes registry modifications, shell registration, and system-level changes.
    /// </summary>
    public static bool DisableDangerousOperations => IsTestMode || IsDevMode;

    /// <summary>
    /// Gets the log level for the shell system
    /// </summary>
    public static string LogLevel => 
        Environment.GetEnvironmentVariable("SHELL_LOG_LEVEL") ?? "Info";

    /// <summary>
    /// Gets whether verbose logging is enabled
    /// </summary>
    public static bool VerboseLogging => 
        LogLevel.Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
        LogLevel.Equals("Trace", StringComparison.OrdinalIgnoreCase);
}
