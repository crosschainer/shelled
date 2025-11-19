using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Shell.Core;

namespace Shell.Bootstrap;

/// <summary>
/// Handles Winlogon shell registration in a safe, test-friendly way.
/// </summary>
public static class ShellRegistration
{
    private const string WinlogonKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";
    private const string ShellValueName = "Shell";

    /// <summary>
    /// Attempts to register Shelled as the Winlogon shell.
    /// In test/dev mode this call is skipped to prevent registry modifications.
    /// </summary>
    public static ShellRegistrationResult TryRegisterShelledShell(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be null or empty", nameof(executablePath));
        }

        if (ShellConfiguration.DisableDangerousOperations)
        {
            Console.WriteLine("Shell registration skipped because dangerous operations are disabled.");
            return ShellRegistrationResult.SkippedTestMode;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Shell registration skipped because this is not a Windows environment.");
            return ShellRegistrationResult.SkippedPlatform;
        }

        return UpdateShellValue(executablePath);
    }

    /// <summary>
    /// Attempts to restore explorer.exe as the Winlogon shell.
    /// </summary>
    public static ShellRegistrationResult TryRestoreExplorerShell()
    {
        if (ShellConfiguration.DisableDangerousOperations)
        {
            Console.WriteLine("Restoring explorer.exe skipped because dangerous operations are disabled.");
            return ShellRegistrationResult.SkippedTestMode;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Restoring explorer.exe skipped because this is not a Windows environment.");
            return ShellRegistrationResult.SkippedPlatform;
        }

        return UpdateShellValue("explorer.exe");
    }

    [SupportedOSPlatform("windows")]
    private static ShellRegistrationResult UpdateShellValue(string newShell)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(WinlogonKeyPath, writable: true) ??
                            Registry.CurrentUser.CreateSubKey(WinlogonKeyPath, writable: true);

            if (key == null)
            {
                Console.WriteLine("Unable to open or create the Winlogon registry key.");
                return ShellRegistrationResult.Failed;
            }

            var currentValue = key.GetValue(ShellValueName) as string;
            if (string.Equals(currentValue, newShell, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Shell value already matches the requested value. No action taken.");
                return ShellRegistrationResult.NoOp;
            }

            key.SetValue(ShellValueName, newShell, RegistryValueKind.String);
            Console.WriteLine($"Updated Winlogon Shell value to '{newShell}'.");
            return ShellRegistrationResult.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update Winlogon Shell value: {ex.Message}");
            return ShellRegistrationResult.Failed;
        }
    }
}

/// <summary>
/// Result of a shell registration attempt.
/// </summary>
public enum ShellRegistrationResult
{
    Success,
    NoOp,
    SkippedTestMode,
    SkippedPlatform,
    Failed
}
