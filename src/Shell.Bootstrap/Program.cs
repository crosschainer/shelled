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
    private const string AutoShutdownEnvironmentVariable = "SHELL_TEST_AUTOSHUTDOWN";
    
    private static ShellCoreService? _shellService;
    private static Process? _uiHostProcess;
    private static bool _shutdownRequested = false;
    private static int _restartAttempts = 0;

    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("Shelled Bootstrap starting...");
            ShellLogger.Info(nameof(Program), $"Bootstrap starting with args: {string.Join(' ', args)}");

            var options = BootstrapCommandLineOptions.Parse(args);

            if (options.PanicRequested)
            {
                ShellLogger.Info(nameof(Program), "Panic command requested via CLI.");
                return HandlePanicCommand();
            }

            // Check for safe mode (Alt key held during startup)
            if (IsSafeModeRequested())
            {
                Console.WriteLine("Safe mode detected - starting Explorer instead");
                ShellLogger.Warn(nameof(Program), "Safe mode detected, starting Explorer instead of Shelled.");
                StartExplorer();
                return 0;
            }

            // Choose between dev/test mode and full shell replacement
            if (ShellConfiguration.IsDevMode || ShellConfiguration.IsTestMode)
            {
                Console.WriteLine("Dev/Test mode detected - running alongside Explorer");
                ShellLogger.Info(nameof(Program), "Dev/Test mode detected; running in development mode.");
                return await RunDevelopmentMode();
            }

            // Normal shell replacement mode (no dev/test flags)
            return await RunShellMode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bootstrap failed: {ex.Message}");
            ShellLogger.Error(nameof(Program), "Bootstrap failed unexpectedly.", ex);
            
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

    private static int HandlePanicCommand()
    {
        Console.WriteLine("Panic command invoked - attempting to restore Explorer shell and launch Explorer.");
        ShellLogger.Warn(nameof(Program), "Panic command invoked.");

        try
        {
            var result = ShellRegistration.TryRestoreExplorerShell();
            Console.WriteLine($"Shell registry restore result: {result}");
            ShellLogger.Info(nameof(Program), $"Shell registry restore result: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restore Explorer shell value: {ex.Message}");
        }

        try
        {
            StartExplorer();
            Console.WriteLine("Explorer launched. You can sign out or close this window once the desktop is ready.");
            ShellLogger.Info(nameof(Program), "Explorer launched from panic handler.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start Explorer during panic command: {ex.Message}");
            ShellLogger.Error(nameof(Program), "Failed to start Explorer during panic command.", ex);
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
        return ShellConfiguration.IsTestMode;
    }

    private static bool IsAutoShutdownRequested()
    {
        return Environment.GetEnvironmentVariable(AutoShutdownEnvironmentVariable) == "1";
    }

    /// <summary>
    /// Terminate any running explorer.exe processes. Used at startup in full shell mode.
    /// </summary>
    private static void KillExistingExplorerProcesses()
    {
        try
        {
            var processes = Process.GetProcessesByName("explorer");
            if (processes.Length == 0)
            {
                return;
            }

            Console.WriteLine($"Killing {processes.Length} existing explorer.exe process(es)...");
            ShellLogger.Warn(nameof(Program), $"Killing {processes.Length} existing explorer.exe process(es) in shell mode.");

            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    ShellLogger.Error(nameof(Program), $"Failed to kill explorer.exe with PID {process.Id}.", ex);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(Program), "Error while attempting to kill explorer.exe processes.", ex);
        }
    }

    private static async Task<int> RunDevelopmentMode()
    {
        Console.WriteLine("Running in development mode alongside Explorer");
        ShellLogger.Info(nameof(Program), "RunDevelopmentMode starting.");
        
        // Start shell service
        _shellService = new ShellCoreService();
        await _shellService.StartAsync();

        // In test mode the service does not host a UI.
        // Only in that case do we start an external UI Host process.
        if (IsTestMode())
        {
            await StartUiHost();
        }

        if (IsAutoShutdownRequested())
        {
            Console.WriteLine("Auto-shutdown test mode enabled; will exit after a short delay.");
            ShellLogger.Info(nameof(Program), "Auto-shutdown test mode enabled in development mode.");
            await Task.Delay(2000);
            _shutdownRequested = true;
        }
        
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
                ShellLogger.Warn(nameof(Program), "UI Host exited in development mode; attempting restart.");
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
        ShellLogger.Info(nameof(Program), "RunShellMode starting.");

        // In full shell mode we take over from Explorer, so terminate any
        // existing explorer.exe shell instances once. Windows may restart it
        // later depending on system settings, but we avoid aggressive guards
        // that can cause lag.
        KillExistingExplorerProcesses();
        
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

        // In shell mode the service hosts the UI itself.
        // External UI Host is only used in test-mode scenarios.
        if (IsTestMode())
        {
            await StartUiHost();
        }

        if (IsAutoShutdownRequested())
        {
            Console.WriteLine("Auto-shutdown test mode enabled; will exit after a short delay.");
            ShellLogger.Info(nameof(Program), "Auto-shutdown test mode enabled in shell mode.");
            await Task.Delay(2000);
            _shutdownRequested = true;
        }
        
        // Monitor UI Host and restart if needed
        while (!_shutdownRequested)
        {
            if (_uiHostProcess?.HasExited == true)
            {
                Console.WriteLine("UI Host exited, attempting restart...");
                ShellLogger.Warn(nameof(Program), "UI Host exited in shell mode; attempting restart.");
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
            ShellLogger.Info(nameof(Program), $"Starting UI Host: {uiHostPath}");
            
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
            ShellLogger.Info(nameof(Program), $"UI Host started with PID: {_uiHostProcess.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start UI Host: {ex.Message}");
            ShellLogger.Error(nameof(Program), "Failed to start UI Host.", ex);
            throw;
        }
    }

    private static async Task RestartUiHost()
    {
        if (_restartAttempts >= MaxRestartAttempts)
        {
            Console.WriteLine($"Max restart attempts ({MaxRestartAttempts}) reached. Giving up.");
            ShellLogger.Error(nameof(Program), $"Max restart attempts ({MaxRestartAttempts}) reached. Giving up.");
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
            ShellLogger.Error(nameof(Program), $"Restart attempt {_restartAttempts} failed.", ex);
        }
    }

    private static string? FindUiHostExecutable()
    {
        // Look for UI Host executable in common locations
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var searchPaths = new[]
        {
            // Same directory as bootstrap
            Path.Combine(baseDirectory, UiHostExecutableName),

            // Development layout: shell bootstrap and UI host built from the same solution
            Path.Combine(baseDirectory, "..", "..", "..", "..", "Shell.Bridge.WebView", "bin", "Debug", "net8.0-windows", UiHostExecutableName),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "Shell.Bridge.WebView", "bin", "Release", "net8.0-windows", UiHostExecutableName)
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
            ShellLogger.Error(nameof(Program), "Failed to start Explorer.", ex);
            throw;
        }
    }

    private static async Task Shutdown()
    {
        Console.WriteLine("Shutting down...");
        ShellLogger.Info(nameof(Program), "Bootstrap shutdown initiated.");
        
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
                    ShellLogger.Warn(nameof(Program), "UI Host did not exit gracefully; killing process.");
                    _uiHostProcess.Kill();
                }
                
                _uiHostProcess.Dispose();
                _uiHostProcess = null;
            }
            
            // Stop shell service
            if (_shellService != null)
            {
                Console.WriteLine("Stopping Shell Service...");
                ShellLogger.Info(nameof(Program), "Stopping Shell Service.");
                await _shellService.StopAsync();
                _shellService.Dispose();
                _shellService = null;
            }
            
            Console.WriteLine("Shutdown complete");
            ShellLogger.Info(nameof(Program), "Bootstrap shutdown complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during shutdown: {ex.Message}");
            ShellLogger.Error(nameof(Program), "Error during bootstrap shutdown.", ex);
        }
    }
}
