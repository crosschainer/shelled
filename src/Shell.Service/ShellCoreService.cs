using System.Diagnostics;
using Shell.Adapters.Win32;
using Shell.Bridge.WebView;
using Shell.Core;
using Shell.Core.Interfaces;

namespace Shell.Service;

/// <summary>
/// Main service that orchestrates all shell components and manages their lifecycle
/// </summary>
public class ShellCoreService : IShellCoreService
{
    private readonly object _stateLock = new();
    private ServiceState _state = ServiceState.Stopped;
    private bool _disposed = false;

    // Core components
    private ShellCore? _shellCore;
    private IEventPublisher? _eventPublisher;

    // Win32 adapters
    private WindowSystemWin32? _windowSystem;
    private ProcessLauncherWin32? _processLauncher;
    private TrayHostWin32? _trayHost;
    private HotkeyRegistryWin32? _hotkeyRegistry;
    private SystemEventHandlerWin32? _systemEventHandler;

    // UI Host
    private ShellUiHostForm? _uiHostForm;
    private Thread? _uiHostThread;
    private CancellationTokenSource? _uiHostCancellation;

    public ServiceState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                if (_state != value)
                {
                    _state = value;
                    StateChanged?.Invoke(value);
                }
            }
        }
    }

    public event Action<ServiceState>? StateChanged;
    public event Action<Exception?>? UiHostCrashed;

    public async Task StartAsync()
    {
        if (State != ServiceState.Stopped)
        {
            throw new InvalidOperationException($"Cannot start service in state {State}");
        }

        State = ServiceState.Starting;

        try
        {
            // Initialize event publisher first
            _eventPublisher = new EventPublisher();

            // Initialize Win32 adapters
            _windowSystem = new WindowSystemWin32();
            _processLauncher = new ProcessLauncherWin32();
            _trayHost = new TrayHostWin32();
            _hotkeyRegistry = new HotkeyRegistryWin32();
            _systemEventHandler = new SystemEventHandlerWin32();

            // Initialize shell core with all dependencies
            _shellCore = new ShellCore(
                _windowSystem,
                _processLauncher,
                _trayHost,
                _hotkeyRegistry,
                _systemEventHandler,
                _eventPublisher);

            // Start system event handler
            _systemEventHandler.StartListening();

            // Start UI Host
            await StartUiHostAsync();

            State = ServiceState.Running;
        }
        catch (Exception ex)
        {
            State = ServiceState.Failed;
            await CleanupAsync();
            throw new InvalidOperationException("Failed to start shell core service", ex);
        }
    }

    public async Task StopAsync()
    {
        if (State == ServiceState.Stopped || State == ServiceState.Stopping)
        {
            return;
        }

        State = ServiceState.Stopping;

        try
        {
            await CleanupAsync();
            State = ServiceState.Stopped;
        }
        catch (Exception ex)
        {
            State = ServiceState.Failed;
            throw new InvalidOperationException("Failed to stop shell core service", ex);
        }
    }

    public async Task RestartUiHostAsync()
    {
        if (State != ServiceState.Running)
        {
            throw new InvalidOperationException($"Cannot restart UI host in state {State}");
        }

        try
        {
            await StopUiHostAsync();
            await StartUiHostAsync();
        }
        catch (Exception ex)
        {
            UiHostCrashed?.Invoke(ex);
            throw;
        }
    }

    public ShellCore? GetShellCore()
    {
        return _shellCore;
    }

    private async Task StartUiHostAsync()
    {
        if (_shellCore == null || _eventPublisher == null)
        {
            throw new InvalidOperationException("Shell core not initialized");
        }

        _uiHostCancellation = new CancellationTokenSource();

        // Start UI Host in a separate thread to avoid blocking
        _uiHostThread = new Thread(() =>
        {
            try
            {
                // Create and run the UI Host form
                _uiHostForm = new ShellUiHostForm(_shellCore, _eventPublisher);
                
                // Handle form closed event
                _uiHostForm.FormClosed += (sender, e) =>
                {
                    if (!_uiHostCancellation.Token.IsCancellationRequested)
                    {
                        // UI Host closed unexpectedly
                        UiHostCrashed?.Invoke(null);
                    }
                };

                // Run the message loop
                System.Windows.Forms.Application.Run(_uiHostForm);
            }
            catch (Exception ex)
            {
                if (!_uiHostCancellation.Token.IsCancellationRequested)
                {
                    UiHostCrashed?.Invoke(ex);
                }
            }
        })
        {
            IsBackground = false,
            Name = "ShellUiHost"
        };

        _uiHostThread.SetApartmentState(ApartmentState.STA);
        _uiHostThread.Start();

        // Give the UI Host a moment to start
        await Task.Delay(500);
    }

    private async Task StopUiHostAsync()
    {
        if (_uiHostCancellation != null)
        {
            _uiHostCancellation.Cancel();
        }

        if (_uiHostForm != null)
        {
            try
            {
                // Close the form on the UI thread
                if (_uiHostForm.InvokeRequired)
                {
                    _uiHostForm.Invoke(new Action(() => _uiHostForm.Close()));
                }
                else
                {
                    _uiHostForm.Close();
                }
            }
            catch (Exception)
            {
                // Ignore exceptions during shutdown
            }
        }

        if (_uiHostThread != null && _uiHostThread.IsAlive)
        {
            // Wait for the thread to finish
            if (!_uiHostThread.Join(TimeSpan.FromSeconds(5)))
            {
                // Force abort if it doesn't finish gracefully
                try
                {
                    _uiHostThread.Interrupt();
                }
                catch (Exception)
                {
                    // Ignore exceptions during forced shutdown
                }
            }
        }

        _uiHostForm?.Dispose();
        _uiHostForm = null;
        _uiHostThread = null;
        _uiHostCancellation?.Dispose();
        _uiHostCancellation = null;

        await Task.CompletedTask;
    }

    private async Task CleanupAsync()
    {
        // Stop UI Host first
        await StopUiHostAsync();

        // Stop system event handler
        _systemEventHandler?.StopListening();

        // Dispose all components in reverse order of initialization
        _shellCore?.Dispose();
        _systemEventHandler?.Dispose();
        _hotkeyRegistry?.Dispose();
        _trayHost?.Dispose();
        _windowSystem?.Dispose();

        // Clear references
        _shellCore = null;
        _systemEventHandler = null;
        _hotkeyRegistry = null;
        _trayHost = null;
        _processLauncher = null;
        _windowSystem = null;
        _eventPublisher = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Ignore exceptions during disposal
        }

        _disposed = true;
    }
}