using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Interfaces;
using Shell.Core.Models;

namespace Shell.Tests;

/// <summary>
/// Tests for ShellCore window management
/// </summary>
public class ShellCoreWindowTests
{
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly MockSystemEventHandler _systemEventHandler;
    private readonly EventPublisher _eventPublisher;
    private readonly Shell.Core.ShellCore _shellCore;
    private readonly List<ShellEvent> _capturedEvents;

    public ShellCoreWindowTests()
    {
        _windowSystem = new MockWindowSystem();
        _processLauncher = new MockProcessLauncher();
        _trayHost = new MockTrayHost();
        _hotkeyRegistry = new MockHotkeyRegistry();
        _systemEventHandler = new MockSystemEventHandler();
        _eventPublisher = new EventPublisher();
        _capturedEvents = new List<ShellEvent>();

        // Subscribe to all events for testing
        _eventPublisher.Subscribe<WindowCreatedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<WindowDestroyedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<WindowStateChangedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<WindowFocusChangedEvent>(e => _capturedEvents.Add(e));

        _shellCore = new Shell.Core.ShellCore(_windowSystem, _processLauncher, _trayHost, _hotkeyRegistry, _systemEventHandler, _eventPublisher);
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