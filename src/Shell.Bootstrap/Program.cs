using System.Diagnostics;
using Microsoft.Win32;
using Shell.Service;
using Shell.Core;

namespace Shell.Bootstrap;

/// <summary>
/// Bootstrap executable that replaces explorer.exe as the Windows shell.
/// Starts ShellCoreService and manages UI Host lifecycle with crash recovery.
/// </summary>
internal class Program
{
    private static readonly string UiHostExecutableName = "ShellUiHost.exe";
    private static readonly int RestartDelayMs = 1000;
    private static readonly int MaxRestartAttempts = 5;
    
    private static ShellCoreService? _shellService;
    private static Process? _uiHostProcess;
    private static bool _shutdownRequested = false;
    private static int _restartAttempts = 0;

    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("Shelled Bootstrap starting...");
            
            // Check for safe mode (Alt key held during startup)
            if (IsSafeModeRequested())
            {
                Console.WriteLine("Safe mode detected - starting Explorer instead");
                StartExplorer();
                return 0;
            }

            // Check if we're in test mode
            if (IsTestMode())
            {
                Console.WriteLine("Test mode detected - running in development mode");
                return await RunDevelopmentMode();
            }

            // Normal shell replacement mode
            return await RunShellMode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bootstrap failed: {ex.Message}");
            
            // Try to start Explorer as fallback
            try
            {
                StartExplorer();
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"Fallback to Explorer failed: {fallbackEx.Message}");
            }
            
            return 1;
        }
    }

    private static bool IsSafeModeRequested()
    {
        // Check if Alt key is held during startup
        // In a real implementation, this would check GetAsyncKeyState
        // For now, we'll check for an environment variable for testing
        return Environment.GetEnvironmentVariable("SHELL_SAFE_MODE") == "1";
    }

    private static bool IsTestMode()
    {
        return Environment.GetEnvironmentVariable("SHELL_TEST_MODE") == "1";
    }

    private static async Task<int> RunDevelopmentMode()
    {
        Console.WriteLine("Running in development mode alongside Explorer");
        
        // Start shell service
        _shellService = new ShellCoreService();
        await _shellService.StartAsync();
        
        // Start UI Host
        await StartUiHost();
        
        // Wait for shutdown signal
        Console.WriteLine("Press Ctrl+C to shutdown");
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _shutdownRequested = true;
        };
        
        // Monitor UI Host and restart if needed
        while (!_shutdownRequested)
        {
            if (_uiHostProcess?.HasExited == true)
            {
                Console.WriteLine("UI Host exited, attempting restart...");
                await RestartUiHost();
            }
            
            await Task.Delay(1000);
        }
        
        await Shutdown();
        return 0;
    }

    private static async Task<int> RunShellMode()
    {
        Console.WriteLine("Running as system shell replacement");
        
        // Set up shutdown handlers
        Console.CancelKeyPress += async (_, e) =>
        {
            e.Cancel = true;
            _shutdownRequested = true;
        };
        
        // Handle system shutdown events
        AppDomain.CurrentDomain.ProcessExit += async (_, _) =>
        {
            _shutdownRequested = true;
            await Shutdown();
        };
        
        // Start shell service
        _shellService = new ShellCoreService();
        await _shellService.StartAsync();
        
        // Start UI Host
        await StartUiHost();
        
        // Monitor UI Host and restart if needed
        while (!_shutdownRequested)
        {
            if (_uiHostProcess?.HasExited == true)
            {
                Console.WriteLine("UI Host exited, attempting restart...");
                await RestartUiHost();
            }
            
            await Task.Delay(1000);
        }
        
        await Shutdown();
        return 0;
    }

    private static async Task StartUiHost()
    {
        try
        {
            var uiHostPath = FindUiHostExecutable();
            if (string.IsNullOrEmpty(uiHostPath))
            {
                throw new FileNotFoundException($"Could not find {UiHostExecutableName}");
            }

            Console.WriteLine($"Starting UI Host: {uiHostPath}");
            
            _uiHostProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = uiHostPath,
                    UseShellExecute = false,
                    CreateNoWindow = false
                }
            };
            
            _uiHostProcess.Start();
            _restartAttempts = 0; // Reset restart attempts on successful start
            
            Console.WriteLine($"UI Host started with PID: {_uiHostProcess.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start UI Host: {ex.Message}");
            throw;
        }
    }

    private static async Task RestartUiHost()
    {
        if (_restartAttempts >= MaxRestartAttempts)
        {
            Console.WriteLine($"Max restart attempts ({MaxRestartAttempts}) reached. Giving up.");
            _shutdownRequested = true;
            return;
        }

        _restartAttempts++;
        Console.WriteLine($"Restart attempt {_restartAttempts}/{MaxRestartAttempts}");
        
        await Task.Delay(RestartDelayMs);
        
        try
        {
            await StartUiHost();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Restart attempt {_restartAttempts} failed: {ex.Message}");
        }
    }

    private static string? FindUiHostExecutable()
    {
        // Look for UI Host executable in common locations
        var searchPaths = new[]
        {
            // Same directory as bootstrap
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UiHostExecutableName),
            
            // Relative to bootstrap location
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Shell.Bridge.WebView", UiHostExecutableName),
            
            // Development build output
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Shell.Bridge.WebView", "bin", "Debug", "net8.0-windows", UiHostExecutableName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Shell.Bridge.WebView", "bin", "Release", "net8.0-windows", UiHostExecutableName)
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return null;
    }

    private static void StartExplorer()
    {
        try
        {
            Console.WriteLine("Starting Windows Explorer...");
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start Explorer: {ex.Message}");
            throw;
        }
    }

    private static async Task Shutdown()
    {
        Console.WriteLine("Shutting down...");
        
        try
        {
            // Stop UI Host
            if (_uiHostProcess != null && !_uiHostProcess.HasExited)
            {
                Console.WriteLine("Stopping UI Host...");
                _uiHostProcess.CloseMainWindow();
                
                if (!_uiHostProcess.WaitForExit(5000))
                {
                    Console.WriteLine("UI Host did not exit gracefully, killing process...");
                    _uiHostProcess.Kill();
                }
                
                _uiHostProcess.Dispose();
                _uiHostProcess = null;
            }
            
            // Stop shell service
            if (_shellService != null)
            {
                Console.WriteLine("Stopping Shell Service...");
                await _shellService.StopAsync();
                _shellService.Dispose();
                _shellService = null;
            }
            
            Console.WriteLine("Shutdown complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during shutdown: {ex.Message}");
        }
    }
}
