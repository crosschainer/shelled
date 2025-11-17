using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Shell.Core;
using Shell.Core.Interfaces;

namespace Shell.Adapters.Win32;

/// <summary>
/// Win32 implementation of the process launcher interface
/// </summary>
public class ProcessLauncherWin32 : IProcessLauncher
{
    public async Task<int> LaunchAppAsync(string appIdOrPath)
    {
        if (string.IsNullOrWhiteSpace(appIdOrPath))
            throw new ArgumentException("App ID or path cannot be empty", nameof(appIdOrPath));

        if (ShellConfiguration.DisableDangerousOperations)
        {
            if (ShellConfiguration.VerboseLogging)
            {
                Console.WriteLine($"LaunchApp blocked in safe mode: {appIdOrPath}");
            }
            // Return a fake process ID in test mode
            return 12345;
        }

        try
        {
            ProcessStartInfo startInfo;

            // Check if it's a file path
            if (File.Exists(appIdOrPath) || Path.IsPathRooted(appIdOrPath))
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = appIdOrPath,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
            }
            else
            {
                // Treat as app name/command - use shell execute to find it
                startInfo = new ProcessStartInfo
                {
                    FileName = appIdOrPath,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
            }

            var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start process: {appIdOrPath}");
            }

            // Wait a bit for the process to initialize
            await Task.Delay(100);

            return process.Id;
        }
        catch (Exception ex)
        {
            if (ShellConfiguration.VerboseLogging)
            {
                Console.WriteLine($"Error launching app '{appIdOrPath}': {ex.Message}");
            }
            throw new InvalidOperationException($"Failed to launch app: {appIdOrPath}", ex);
        }
    }

    public IEnumerable<ProcessInfo> GetRunningProcesses()
    {
        try
        {
            var processes = Process.GetProcesses();
            return processes.Select(p =>
            {
                try
                {
                    return new ProcessInfo
                    {
                        ProcessId = p.Id,
                        ProcessName = p.ProcessName,
                        ExecutablePath = GetProcessExecutablePath(p),
                        StartTime = p.StartTime
                    };
                }
                catch
                {
                    // Some processes may not be accessible
                    return new ProcessInfo
                    {
                        ProcessId = p.Id,
                        ProcessName = p.ProcessName,
                        ExecutablePath = string.Empty,
                        StartTime = DateTime.MinValue
                    };
                }
            }).Where(pi => !string.IsNullOrEmpty(pi.ProcessName));
        }
        catch (Exception ex)
        {
            if (ShellConfiguration.VerboseLogging)
            {
                Console.WriteLine($"Error getting running processes: {ex.Message}");
            }
            return Enumerable.Empty<ProcessInfo>();
        }
    }

    private static string GetProcessExecutablePath(Process process)
    {
        try
        {
            // Try to get the main module filename
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            // Access denied or other error
            return string.Empty;
        }
    }
}