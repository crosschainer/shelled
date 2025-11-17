using Shell.Core;
using Shell.Core.Events;
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

    public IEnumerable<TrayIcon> GetTrayIcons() => _trayIcons.Values;

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
}

/// <summary>
/// Mock hotkey registry for testing
/// </summary>
public class MockHotkeyRegistry : IHotkeyRegistry
{
    public event Action<string>? HotkeyPressed;

    public bool RegisterHotkey(string id, int modifiers, int virtualKey) => true;

    public bool UnregisterHotkey(string id) => true;
}

/// <summary>
/// Tests for ShellCore window management
/// </summary>
public class ShellCoreWindowTests
{
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly EventPublisher _eventPublisher;
    private readonly Shell.Core.ShellCore _shellCore;
    private readonly List<ShellEvent> _capturedEvents;

    public ShellCoreWindowTests()
    {
        _windowSystem = new MockWindowSystem();
        _processLauncher = new MockProcessLauncher();
        _trayHost = new MockTrayHost();
        _hotkeyRegistry = new MockHotkeyRegistry();
        _eventPublisher = new EventPublisher();
        _capturedEvents = new List<ShellEvent>();

        // Subscribe to all events for testing
        _eventPublisher.Subscribe<WindowCreatedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<WindowDestroyedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<WindowStateChangedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<WindowFocusChangedEvent>(e => _capturedEvents.Add(e));

        _shellCore = new Shell.Core.ShellCore(_windowSystem, _processLauncher, _trayHost, _hotkeyRegistry, _eventPublisher);
    }

    [Fact]
    public void WindowCreated_UpdatesStateAndEmitsEvent()
    {
        // Arrange
        var windowHandle = new IntPtr(12345);
        var window = new ShellWindow
        {
            Handle = windowHandle,
            Title = "Test Window",
            ProcessId = 1234,
            WorkspaceId = "default"
        };

        // Act
        _windowSystem.SimulateWindowCreated(window);

        // Assert
        var state = _shellCore.GetState();
        Assert.True(state.Windows.ContainsKey(windowHandle));
        Assert.Equal("Test Window", state.Windows[windowHandle].Title);
        
        // Check that the window was added to the default workspace
        var defaultWorkspace = state.Workspaces["default"];
        Assert.Contains(windowHandle, defaultWorkspace.WindowHandles);

        // Check that event was emitted
        var createdEvent = _capturedEvents.OfType<WindowCreatedEvent>().FirstOrDefault();
        Assert.NotNull(createdEvent);
        Assert.Equal(windowHandle, createdEvent.Window.Handle);
    }

    [Fact]
    public void WindowDestroyed_RemovesFromStateAndEmitsEvent()
    {
        // Arrange
        var windowHandle = new IntPtr(12345);
        var window = new ShellWindow
        {
            Handle = windowHandle,
            Title = "Test Window",
            ProcessId = 1234,
            WorkspaceId = "default"
        };

        _windowSystem.SimulateWindowCreated(window);
        _capturedEvents.Clear(); // Clear creation events

        // Act
        _windowSystem.SimulateWindowDestroyed(windowHandle);

        // Assert
        var state = _shellCore.GetState();
        Assert.False(state.Windows.ContainsKey(windowHandle));
        
        // Check that the window was removed from the workspace
        var defaultWorkspace = state.Workspaces["default"];
        Assert.DoesNotContain(windowHandle, defaultWorkspace.WindowHandles);

        // Check that event was emitted
        var destroyedEvent = _capturedEvents.OfType<WindowDestroyedEvent>().FirstOrDefault();
        Assert.NotNull(destroyedEvent);
        Assert.Equal(windowHandle, destroyedEvent.WindowHandle);
        Assert.Equal("default", destroyedEvent.WorkspaceId);
    }

    [Fact]
    public void FocusWindow_UpdatesFocusedWindowAndEmitsEvent()
    {
        // Arrange
        var windowHandle = new IntPtr(12345);
        var window = new ShellWindow
        {
            Handle = windowHandle,
            Title = "Test Window",
            ProcessId = 1234,
            WorkspaceId = "default"
        };

        _windowSystem.SimulateWindowCreated(window);
        _capturedEvents.Clear();

        // Act
        _shellCore.FocusWindow(windowHandle);

        // Assert
        var state = _shellCore.GetState();
        Assert.Equal(windowHandle, state.FocusedWindowHandle);

        // Check that event was emitted
        var focusEvent = _capturedEvents.OfType<WindowFocusChangedEvent>().FirstOrDefault();
        Assert.NotNull(focusEvent);
        Assert.Equal(IntPtr.Zero, focusEvent.PreviousWindowHandle);
        Assert.Equal(windowHandle, focusEvent.NewWindowHandle);
    }

    [Fact]
    public void FocusNonExistentWindow_DoesNothing()
    {
        // Arrange
        var nonExistentHandle = new IntPtr(99999);

        // Act
        _shellCore.FocusWindow(nonExistentHandle);

        // Assert
        var state = _shellCore.GetState();
        Assert.Equal(IntPtr.Zero, state.FocusedWindowHandle);
        Assert.Empty(_capturedEvents.OfType<WindowFocusChangedEvent>());
    }
}