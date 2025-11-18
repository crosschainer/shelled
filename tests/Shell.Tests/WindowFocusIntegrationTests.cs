using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Models;

namespace Shell.Tests;

/// <summary>
/// Integration tests for window focus tracking functionality
/// TEST-INT-WS-03: Core tracks focus changes when user activates another window
/// </summary>
public class WindowFocusIntegrationTests : IDisposable
{
    private readonly Shell.Core.ShellCore _shellCore;
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly MockSystemEventHandler _systemEventHandler;
    private readonly EventPublisher _eventPublisher;
    private readonly List<WindowFocusChangedEvent> _focusEvents;

    public WindowFocusIntegrationTests()
    {
        // Ensure we're in test mode for safety
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
        
        _windowSystem = new MockWindowSystem();
        _processLauncher = new MockProcessLauncher();
        _trayHost = new MockTrayHost();
        _hotkeyRegistry = new MockHotkeyRegistry();
        _systemEventHandler = new MockSystemEventHandler();
        _eventPublisher = new EventPublisher();
        
        _shellCore = new Shell.Core.ShellCore(_windowSystem, _processLauncher, _trayHost, _hotkeyRegistry, _systemEventHandler, _eventPublisher);
        
        _focusEvents = new List<WindowFocusChangedEvent>();
        _eventPublisher.Subscribe<WindowFocusChangedEvent>(evt => _focusEvents.Add(evt));
    }

    [Fact]
    public void Core_TracksWindowFocusChanges_WhenWindowActivated()
    {
        // Arrange
        var window1Handle = new IntPtr(1001);
        var window2Handle = new IntPtr(1002);
        
        var window1 = new ShellWindow
        {
            Handle = window1Handle,
            Title = "Test Window 1",
            ProcessId = 1001,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };
        
        var window2 = new ShellWindow
        {
            Handle = window2Handle,
            Title = "Test Window 2",
            ProcessId = 1002,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };

        // Create windows in the system
        _windowSystem.SimulateWindowCreated(window1);
        _windowSystem.SimulateWindowCreated(window2);
        
        // Clear any events from window creation
        _focusEvents.Clear();
        
        // Verify initial state - no focused window
        var initialState = _shellCore.GetState();
        Assert.Equal(IntPtr.Zero, initialState.FocusedWindowHandle);

        // Act - Simulate window activation events
        _windowSystem.SimulateWindowActivated(window1Handle);
        
        // Assert - First activation
        Assert.Single(_focusEvents);
        var firstEvent = _focusEvents[0];
        Assert.Equal(IntPtr.Zero, firstEvent.PreviousWindowHandle); // No previous focus
        Assert.Equal(window1Handle, firstEvent.NewWindowHandle);
        
        // Verify state updated
        var stateAfterFirst = _shellCore.GetState();
        Assert.Equal(window1Handle, stateAfterFirst.FocusedWindowHandle);

        // Act - Activate second window
        _windowSystem.SimulateWindowActivated(window2Handle);
        
        // Assert - Second activation
        Assert.Equal(2, _focusEvents.Count);
        var secondEvent = _focusEvents[1];
        Assert.Equal(window1Handle, secondEvent.PreviousWindowHandle);
        Assert.Equal(window2Handle, secondEvent.NewWindowHandle);
        
        // Verify final state
        var finalState = _shellCore.GetState();
        Assert.Equal(window2Handle, finalState.FocusedWindowHandle);
    }

    [Fact]
    public void Core_IgnoresActivationOfUnknownWindows()
    {
        // Arrange
        var unknownWindowHandle = new IntPtr(9999);
        
        // Act - Try to activate a window that doesn't exist in our state
        _windowSystem.SimulateWindowActivated(unknownWindowHandle);
        
        // Assert - No focus events should be fired
        Assert.Empty(_focusEvents);
        
        // State should remain unchanged
        var state = _shellCore.GetState();
        Assert.Equal(IntPtr.Zero, state.FocusedWindowHandle);
    }

    [Fact]
    public void Core_HandlesMultipleRapidFocusChanges()
    {
        // Arrange
        var window1Handle = new IntPtr(1001);
        var window2Handle = new IntPtr(1002);
        var window3Handle = new IntPtr(1003);
        
        var window1 = new ShellWindow
        {
            Handle = window1Handle,
            Title = "Window 1",
            ProcessId = 1001,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };
        
        var window2 = new ShellWindow
        {
            Handle = window2Handle,
            Title = "Window 2",
            ProcessId = 1002,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };
        
        var window3 = new ShellWindow
        {
            Handle = window3Handle,
            Title = "Window 3",
            ProcessId = 1003,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };

        // Create windows
        _windowSystem.SimulateWindowCreated(window1);
        _windowSystem.SimulateWindowCreated(window2);
        _windowSystem.SimulateWindowCreated(window3);
        _focusEvents.Clear();

        // Act - Rapid focus changes
        _windowSystem.SimulateWindowActivated(window1Handle);
        _windowSystem.SimulateWindowActivated(window2Handle);
        _windowSystem.SimulateWindowActivated(window3Handle);
        _windowSystem.SimulateWindowActivated(window1Handle); // Back to first

        // Assert - All focus changes tracked
        Assert.Equal(4, _focusEvents.Count);
        
        // Verify sequence
        Assert.Equal(IntPtr.Zero, _focusEvents[0].PreviousWindowHandle);
        Assert.Equal(window1Handle, _focusEvents[0].NewWindowHandle);
        
        Assert.Equal(window1Handle, _focusEvents[1].PreviousWindowHandle);
        Assert.Equal(window2Handle, _focusEvents[1].NewWindowHandle);
        
        Assert.Equal(window2Handle, _focusEvents[2].PreviousWindowHandle);
        Assert.Equal(window3Handle, _focusEvents[2].NewWindowHandle);
        
        Assert.Equal(window3Handle, _focusEvents[3].PreviousWindowHandle);
        Assert.Equal(window1Handle, _focusEvents[3].NewWindowHandle);
        
        // Final state should be window1
        var finalState = _shellCore.GetState();
        Assert.Equal(window1Handle, finalState.FocusedWindowHandle);
    }

    [Fact]
    public void Core_FocusEvents_ContainCorrectTimestamps()
    {
        // Arrange
        var windowHandle = new IntPtr(1001);
        var window = new ShellWindow
        {
            Handle = windowHandle,
            Title = "Test Window",
            ProcessId = 1001,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };

        _windowSystem.SimulateWindowCreated(window);
        _focusEvents.Clear();
        
        var beforeActivation = DateTime.UtcNow;

        // Act
        _windowSystem.SimulateWindowActivated(windowHandle);
        
        var afterActivation = DateTime.UtcNow;

        // Assert
        Assert.Single(_focusEvents);
        var focusEvent = _focusEvents[0];
        
        // Event timestamp should be between before and after
        Assert.True(focusEvent.Timestamp >= beforeActivation);
        Assert.True(focusEvent.Timestamp <= afterActivation);
        
        // Event should have a unique ID
        Assert.NotNull(focusEvent.EventId);
        Assert.NotEmpty(focusEvent.EventId);
    }

    public void Dispose()
    {
        _shellCore?.Dispose();
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }
}