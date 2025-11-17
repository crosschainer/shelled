using Shell.Core;
using Shell.Core.Interfaces;
using Shell.Core.Models;

namespace Shell.Tests;

/// <summary>
/// Mock window system for testing
/// </summary>
public class MockWindowSystem : IWindowSystem
{
    private readonly Dictionary<IntPtr, ShellWindow> _windows = new();
    
    public event Action<IntPtr>? WindowCreated;
    public event Action<IntPtr>? WindowDestroyed;
    public event Action<IntPtr>? WindowActivated;
    public event Action<IntPtr>? WindowUpdated;

    public IEnumerable<ShellWindow> EnumWindows() => _windows.Values;

    public ShellWindow? GetWindowInfo(IntPtr hwnd) => _windows.TryGetValue(hwnd, out var window) ? window : null;

    public bool IsTopLevelWindow(IntPtr hwnd) => _windows.ContainsKey(hwnd);

    public bool IsVisible(IntPtr hwnd) => _windows.TryGetValue(hwnd, out var window) && window.IsVisible;

    public void SetForegroundWindow(IntPtr hwnd) { /* Mock implementation */ }

    public void ShowWindow(IntPtr hwnd, WindowState state)
    {
        if (_windows.TryGetValue(hwnd, out var window))
        {
            window.State = state;
            window.IsVisible = state != WindowState.Hidden;
        }
    }

    // Test helper methods
    public void SimulateWindowCreated(ShellWindow window)
    {
        _windows[window.Handle] = window;
        WindowCreated?.Invoke(window.Handle);
    }

    public void SimulateWindowDestroyed(IntPtr hwnd)
    {
        _windows.Remove(hwnd);
        WindowDestroyed?.Invoke(hwnd);
    }

    public void SimulateWindowActivated(IntPtr hwnd)
    {
        WindowActivated?.Invoke(hwnd);
    }
}

/// <summary>
/// Mock process launcher for testing
/// </summary>
public class MockProcessLauncher : IProcessLauncher
{
    public Task<int> LaunchAppAsync(string appIdOrPath) => Task.FromResult(1234);

    public IEnumerable<ProcessInfo> GetRunningProcesses() => Enumerable.Empty<ProcessInfo>();
}

/// <summary>
/// Mock tray host for testing
/// </summary>
public class MockTrayHost : ITrayHost
{
    private readonly Dictionary<string, TrayIcon> _trayIcons = new();

    public event Action<TrayIcon>? TrayIconAdded;
    public event Action<TrayIcon>? TrayIconUpdated;
    public event Action<string>? TrayIconRemoved;
    public event Action<string, TrayClickType>? TrayIconClicked;
    public event Action<string, TrayBalloonInfo>? TrayBalloonShown;
    public event Action<string, string>? TrayMenuItemClicked;

    public IEnumerable<TrayIcon> GetTrayIcons() => _trayIcons.Values;

    public void ShowBalloonNotification(string trayIconId, string title, string text, TrayBalloonIcon icon = TrayBalloonIcon.None, int timeoutMs = 5000)
    {
        // Mock implementation - just update the tray icon's balloon info
        if (_trayIcons.TryGetValue(trayIconId, out var trayIcon))
        {
            var balloonInfo = new TrayBalloonInfo
            {
                Title = title,
                Text = text,
                Icon = icon,
                TimeoutMs = timeoutMs,
                ShowTime = DateTime.UtcNow
            };
            trayIcon.BalloonInfo = balloonInfo;
            TrayBalloonShown?.Invoke(trayIconId, balloonInfo);
        }
    }

    public void UpdateTrayIconMenu(string trayIconId, TrayMenu menu)
    {
        // Mock implementation - just update the tray icon's menu
        if (_trayIcons.TryGetValue(trayIconId, out var trayIcon))
        {
            trayIcon.Menu = menu;
        }
    }

    // Test helper methods
    public void SimulateTrayIconAdded(TrayIcon trayIcon)
    {
        _trayIcons[trayIcon.Id] = trayIcon;
        TrayIconAdded?.Invoke(trayIcon);
    }

    public void SimulateTrayIconUpdated(TrayIcon trayIcon)
    {
        TrayIconUpdated?.Invoke(trayIcon);
    }

    public void SimulateTrayIconRemoved(string trayIconId)
    {
        _trayIcons.Remove(trayIconId);
        TrayIconRemoved?.Invoke(trayIconId);
    }

    public void SimulateTrayIconClicked(string trayIconId, TrayClickType clickType)
    {
        TrayIconClicked?.Invoke(trayIconId, clickType);
    }

    public void SimulateTrayMenuItemClicked(string trayIconId, string menuItemId)
    {
        TrayMenuItemClicked?.Invoke(trayIconId, menuItemId);
    }
}

/// <summary>
/// Mock hotkey registry for testing
/// </summary>
public class MockHotkeyRegistry : IHotkeyRegistry
{
    public event Action<string>? HotkeyPressed;

    public bool RegisterHotkey(string id, int modifiers, int virtualKey) => true;

    public bool UnregisterHotkey(string id) => true;

    // Test helper method
    public void TriggerHotkey(string id)
    {
        HotkeyPressed?.Invoke(id);
    }
}

/// <summary>
/// Mock system event handler for testing
/// </summary>
public class MockSystemEventHandler : ISystemEventHandler
{
    public event Action<SystemEventType, SystemEventArgs>? SystemEventOccurred;

    public bool IsListening { get; private set; }

    public void StartListening()
    {
        IsListening = true;
    }

    public void StopListening()
    {
        IsListening = false;
    }

    // Test helper method
    public void TriggerSystemEvent(SystemEventType eventType, SystemEventArgs? eventArgs = null)
    {
        eventArgs ??= new SystemEventArgs();
        SystemEventOccurred?.Invoke(eventType, eventArgs);
    }
}