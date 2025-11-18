using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Models;

namespace Shell.Tests;

/// <summary>
/// Integration tests for process launcher functionality
/// TEST-INT-PL-01: ShellCore.LaunchApp starts process and creates WindowCreatedEvent
/// </summary>
public class ProcessLauncherIntegrationTests : IDisposable
{
    private readonly Shell.Core.ShellCore _shellCore;
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly MockSystemEventHandler _systemEventHandler;
    private readonly EventPublisher _eventPublisher;
    private readonly List<WindowCreatedEvent> _windowCreatedEvents;

    public ProcessLauncherIntegrationTests()
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
        
        _windowCreatedEvents = new List<WindowCreatedEvent>();
        _eventPublisher.Subscribe<WindowCreatedEvent>(evt => _windowCreatedEvents.Add(evt));
    }

    [Fact]
    public async Task ShellCore_LaunchApp_StartsProcessAndCreatesWindowEvent()
    {
        // Arrange
        var appPath = "notepad.exe";
        var expectedProcessId = 1234;
        
        // Clear any initial events
        _windowCreatedEvents.Clear();

        // Act - Launch the app
        var actualProcessId = await _shellCore.LaunchAppAsync(appPath);

        // Simulate the window creation that would happen after process starts
        var newWindow = new ShellWindow
        {
            Handle = new IntPtr(5001),
            Title = "Untitled - Notepad",
            ProcessId = expectedProcessId,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };

        // Simulate window creation event (this would normally happen automatically via Win32 hooks)
        _windowSystem.SimulateWindowCreated(newWindow);

        // Assert
        Assert.Equal(expectedProcessId, actualProcessId);
        
        // Verify window created event was fired
        Assert.Single(_windowCreatedEvents);
        var windowEvent = _windowCreatedEvents[0];
        Assert.Equal(newWindow.Handle, windowEvent.Window.Handle);
        Assert.Equal(newWindow.Title, windowEvent.Window.Title);
        Assert.Equal(expectedProcessId, windowEvent.Window.ProcessId);
        
        // Verify window is tracked in shell state
        var state = _shellCore.GetState();
        Assert.True(state.Windows.ContainsKey(newWindow.Handle));
        Assert.Equal(newWindow.Title, state.Windows[newWindow.Handle].Title);
    }

    [Fact]
    public async Task ShellCore_LaunchApp_MultipleApps_CreatesMultipleWindowEvents()
    {
        // Arrange
        _windowCreatedEvents.Clear();

        // Act - Launch multiple apps
        var processId1 = await _shellCore.LaunchAppAsync("notepad.exe");
        var processId2 = await _shellCore.LaunchAppAsync("calc.exe");

        // Simulate windows being created for both processes
        var window1 = new ShellWindow
        {
            Handle = new IntPtr(5001),
            Title = "Untitled - Notepad",
            ProcessId = processId1,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };

        var window2 = new ShellWindow
        {
            Handle = new IntPtr(5002),
            Title = "Calculator",
            ProcessId = processId2,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };

        _windowSystem.SimulateWindowCreated(window1);
        _windowSystem.SimulateWindowCreated(window2);

        // Assert
        Assert.Equal(2, _windowCreatedEvents.Count);
        
        // Verify both windows are tracked
        var state = _shellCore.GetState();
        Assert.Equal(2, state.Windows.Count);
        Assert.True(state.Windows.ContainsKey(window1.Handle));
        Assert.True(state.Windows.ContainsKey(window2.Handle));
    }

    [Fact]
    public async Task ShellCore_LaunchApp_InvalidApp_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _shellCore.LaunchAppAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _shellCore.LaunchAppAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _shellCore.LaunchAppAsync("   "));
    }

    [Fact]
    public async Task ShellCore_LaunchApp_WindowCreatedInDifferentWorkspace()
    {
        // Arrange
        var workspaceId = "test-workspace";
        _shellCore.CreateWorkspace(workspaceId, "Test Workspace");
        _shellCore.SwitchWorkspace(workspaceId);
        _windowCreatedEvents.Clear();

        // Act
        var processId = await _shellCore.LaunchAppAsync("notepad.exe");

        // Simulate window creation in the active workspace
        var newWindow = new ShellWindow
        {
            Handle = new IntPtr(5001),
            Title = "Untitled - Notepad",
            ProcessId = processId,
            WorkspaceId = workspaceId, // Window should be in the active workspace
            State = WindowState.Normal,
            IsVisible = true
        };

        _windowSystem.SimulateWindowCreated(newWindow);

        // Assert
        Assert.Single(_windowCreatedEvents);
        var windowEvent = _windowCreatedEvents[0];
        Assert.Equal(workspaceId, windowEvent.Window.WorkspaceId);
        
        // Verify window is in the correct workspace
        var state = _shellCore.GetState();
        Assert.Equal(workspaceId, state.ActiveWorkspaceId);
        Assert.True(state.Workspaces[workspaceId].WindowHandles.Contains(newWindow.Handle));
    }

    [Fact]
    public async Task ShellCore_LaunchApp_WindowCreatedEvent_HasCorrectTimestamp()
    {
        // Arrange
        _windowCreatedEvents.Clear();
        var beforeLaunch = DateTime.UtcNow;

        // Act
        var processId = await _shellCore.LaunchAppAsync("notepad.exe");

        var newWindow = new ShellWindow
        {
            Handle = new IntPtr(5001),
            Title = "Untitled - Notepad",
            ProcessId = processId,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };

        _windowSystem.SimulateWindowCreated(newWindow);
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.Single(_windowCreatedEvents);
        var windowEvent = _windowCreatedEvents[0];
        
        // Event timestamp should be between before launch and after creation
        Assert.True(windowEvent.Timestamp >= beforeLaunch);
        Assert.True(windowEvent.Timestamp <= afterCreation);
        
        // Event should have a unique ID
        Assert.NotNull(windowEvent.EventId);
        Assert.NotEmpty(windowEvent.EventId);
    }

    [Fact]
    public async Task ShellCore_LaunchApp_ProcessIdTracking()
    {
        // Arrange
        _windowCreatedEvents.Clear();

        // Act
        var processId1 = await _shellCore.LaunchAppAsync("notepad.exe");
        var processId2 = await _shellCore.LaunchAppAsync("calc.exe");

        // Assert - Process IDs should be different (mock returns incrementing IDs)
        Assert.NotEqual(processId1, processId2);
        
        // Simulate windows for both processes
        var window1 = new ShellWindow
        {
            Handle = new IntPtr(5001),
            Title = "Notepad",
            ProcessId = processId1,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };

        var window2 = new ShellWindow
        {
            Handle = new IntPtr(5002),
            Title = "Calculator",
            ProcessId = processId2,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true
        };

        _windowSystem.SimulateWindowCreated(window1);
        _windowSystem.SimulateWindowCreated(window2);

        // Verify process IDs are correctly tracked
        Assert.Equal(2, _windowCreatedEvents.Count);
        Assert.Equal(processId1, _windowCreatedEvents[0].Window.ProcessId);
        Assert.Equal(processId2, _windowCreatedEvents[1].Window.ProcessId);
    }

    public void Dispose()
    {
        _shellCore?.Dispose();
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }
}