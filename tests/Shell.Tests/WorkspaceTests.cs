using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Interfaces;
using Shell.Core.Models;

namespace Shell.Tests;

/// <summary>
/// Tests for ShellCore workspace management
/// </summary>
public class ShellCoreWorkspaceTests
{
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly EventPublisher _eventPublisher;
    private readonly Shell.Core.ShellCore _shellCore;
    private readonly List<ShellEvent> _capturedEvents;

    public ShellCoreWorkspaceTests()
    {
        _windowSystem = new MockWindowSystem();
        _processLauncher = new MockProcessLauncher();
        _trayHost = new MockTrayHost();
        _hotkeyRegistry = new MockHotkeyRegistry();
        _eventPublisher = new EventPublisher();
        _capturedEvents = new List<ShellEvent>();

        // Subscribe to workspace events for testing
        _eventPublisher.Subscribe<WorkspaceSwitchedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<WorkspaceCreatedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<WindowMovedToWorkspaceEvent>(e => _capturedEvents.Add(e));

        _shellCore = new Shell.Core.ShellCore(_windowSystem, _processLauncher, _trayHost, _hotkeyRegistry, _eventPublisher);
    }

    [Fact]
    public void CreateWorkspace_AddsWorkspaceToStateAndEmitsEvent()
    {
        // Act
        _shellCore.CreateWorkspace("workspace1", "Test Workspace");

        // Assert
        var state = _shellCore.GetState();
        Assert.True(state.Workspaces.ContainsKey("workspace1"));
        Assert.Equal("Test Workspace", state.Workspaces["workspace1"].Name);
        Assert.False(state.Workspaces["workspace1"].IsActive);

        // Check that event was emitted
        var createdEvent = _capturedEvents.OfType<WorkspaceCreatedEvent>().FirstOrDefault();
        Assert.NotNull(createdEvent);
        Assert.Equal("workspace1", createdEvent.Workspace.Id);
        Assert.Equal("Test Workspace", createdEvent.Workspace.Name);
    }

    [Fact]
    public void CreateDuplicateWorkspace_DoesNothing()
    {
        // Arrange
        _shellCore.CreateWorkspace("workspace1", "Test Workspace");
        _capturedEvents.Clear();

        // Act
        _shellCore.CreateWorkspace("workspace1", "Duplicate Workspace");

        // Assert
        var state = _shellCore.GetState();
        Assert.Equal("Test Workspace", state.Workspaces["workspace1"].Name); // Original name preserved

        // No new event should be emitted
        Assert.Empty(_capturedEvents.OfType<WorkspaceCreatedEvent>());
    }

    [Fact]
    public void SwitchWorkspace_HidesCurrentWindowsAndShowsNewOnes()
    {
        // Arrange
        _shellCore.CreateWorkspace("workspace1", "Workspace 1");

        // Create windows in default workspace
        var window1Handle = new IntPtr(1001);
        var window1 = new ShellWindow
        {
            Handle = window1Handle,
            Title = "Window 1",
            ProcessId = 1001,
            WorkspaceId = "default"
        };
        _windowSystem.SimulateWindowCreated(window1);

        // Create windows in workspace1
        var window2Handle = new IntPtr(1002);
        var window2 = new ShellWindow
        {
            Handle = window2Handle,
            Title = "Window 2",
            ProcessId = 1002,
            WorkspaceId = "workspace1"
        };
        _windowSystem.SimulateWindowCreated(window2);
        _shellCore.MoveWindowToWorkspace(window2Handle, "workspace1");

        _capturedEvents.Clear();

        // Act
        _shellCore.SwitchWorkspace("workspace1");

        // Assert
        var state = _shellCore.GetState();
        Assert.Equal("workspace1", state.ActiveWorkspaceId);
        Assert.True(state.Workspaces["workspace1"].IsActive);
        Assert.False(state.Workspaces["default"].IsActive);

        // Check window visibility states
        Assert.False(state.Windows[window1Handle].IsVisible); // Hidden because not in active workspace
        Assert.True(state.Windows[window2Handle].IsVisible);  // Visible because in active workspace

        // Check that event was emitted
        var switchedEvent = _capturedEvents.OfType<WorkspaceSwitchedEvent>().FirstOrDefault();
        Assert.NotNull(switchedEvent);
        Assert.Equal("default", switchedEvent.PreviousWorkspaceId);
        Assert.Equal("workspace1", switchedEvent.NewWorkspaceId);
    }

    [Fact]
    public void SwitchToSameWorkspace_DoesNothing()
    {
        // Act
        _shellCore.SwitchWorkspace("default"); // Already active

        // Assert
        Assert.Empty(_capturedEvents.OfType<WorkspaceSwitchedEvent>());
    }

    [Fact]
    public void SwitchToNonExistentWorkspace_DoesNothing()
    {
        // Act
        _shellCore.SwitchWorkspace("nonexistent");

        // Assert
        var state = _shellCore.GetState();
        Assert.Equal("default", state.ActiveWorkspaceId); // Should remain unchanged

        Assert.Empty(_capturedEvents.OfType<WorkspaceSwitchedEvent>());
    }

    [Fact]
    public void MoveWindowToWorkspace_UpdatesWindowAndWorkspaces()
    {
        // Arrange
        _shellCore.CreateWorkspace("workspace1", "Workspace 1");

        var windowHandle = new IntPtr(1001);
        var window = new ShellWindow
        {
            Handle = windowHandle,
            Title = "Test Window",
            ProcessId = 1001,
            WorkspaceId = "default"
        };
        _windowSystem.SimulateWindowCreated(window);
        _capturedEvents.Clear();

        // Act
        _shellCore.MoveWindowToWorkspace(windowHandle, "workspace1");

        // Assert
        var state = _shellCore.GetState();
        
        // Window should be updated
        Assert.Equal("workspace1", state.Windows[windowHandle].WorkspaceId);
        
        // Window should be removed from default workspace
        Assert.DoesNotContain(windowHandle, state.Workspaces["default"].WindowHandles);
        
        // Window should be added to workspace1
        Assert.Contains(windowHandle, state.Workspaces["workspace1"].WindowHandles);

        // Window should be hidden since workspace1 is not active
        Assert.False(state.Windows[windowHandle].IsVisible);

        // Check that event was emitted
        var movedEvent = _capturedEvents.OfType<WindowMovedToWorkspaceEvent>().FirstOrDefault();
        Assert.NotNull(movedEvent);
        Assert.Equal(windowHandle, movedEvent.WindowHandle);
        Assert.Equal("default", movedEvent.PreviousWorkspaceId);
        Assert.Equal("workspace1", movedEvent.NewWorkspaceId);
    }

    [Fact]
    public void MoveWindowToSameWorkspace_DoesNothing()
    {
        // Arrange
        var windowHandle = new IntPtr(1001);
        var window = new ShellWindow
        {
            Handle = windowHandle,
            Title = "Test Window",
            ProcessId = 1001,
            WorkspaceId = "default"
        };
        _windowSystem.SimulateWindowCreated(window);
        _capturedEvents.Clear();

        // Act
        _shellCore.MoveWindowToWorkspace(windowHandle, "default"); // Same workspace

        // Assert
        Assert.Empty(_capturedEvents.OfType<WindowMovedToWorkspaceEvent>());
    }

    [Fact]
    public void MoveNonExistentWindow_DoesNothing()
    {
        // Arrange
        _shellCore.CreateWorkspace("workspace1", "Workspace 1");

        // Act
        _shellCore.MoveWindowToWorkspace(new IntPtr(99999), "workspace1");

        // Assert
        Assert.Empty(_capturedEvents.OfType<WindowMovedToWorkspaceEvent>());
    }

    [Fact]
    public void MoveWindowToNonExistentWorkspace_DoesNothing()
    {
        // Arrange
        var windowHandle = new IntPtr(1001);
        var window = new ShellWindow
        {
            Handle = windowHandle,
            Title = "Test Window",
            ProcessId = 1001,
            WorkspaceId = "default"
        };
        _windowSystem.SimulateWindowCreated(window);
        _capturedEvents.Clear();

        // Act
        _shellCore.MoveWindowToWorkspace(windowHandle, "nonexistent");

        // Assert
        var state = _shellCore.GetState();
        Assert.Equal("default", state.Windows[windowHandle].WorkspaceId); // Should remain unchanged

        Assert.Empty(_capturedEvents.OfType<WindowMovedToWorkspaceEvent>());
    }

    [Fact]
    public void GetVisibleWindows_ReturnsOnlyActiveWorkspaceWindows()
    {
        // Arrange
        _shellCore.CreateWorkspace("workspace1", "Workspace 1");

        // Create window in default workspace
        var window1Handle = new IntPtr(1001);
        var window1 = new ShellWindow
        {
            Handle = window1Handle,
            Title = "Window 1",
            ProcessId = 1001,
            WorkspaceId = "default"
        };
        _windowSystem.SimulateWindowCreated(window1);

        // Create window in workspace1
        var window2Handle = new IntPtr(1002);
        var window2 = new ShellWindow
        {
            Handle = window2Handle,
            Title = "Window 2",
            ProcessId = 1002,
            WorkspaceId = "workspace1"
        };
        _windowSystem.SimulateWindowCreated(window2);
        _shellCore.MoveWindowToWorkspace(window2Handle, "workspace1");

        // Act & Assert - Default workspace is active
        var state = _shellCore.GetState();
        var visibleWindows = state.GetVisibleWindows().ToList();
        
        Assert.Single(visibleWindows);
        Assert.Equal(window1Handle, visibleWindows[0].Handle);

        // Switch to workspace1 and check again
        _shellCore.SwitchWorkspace("workspace1");
        state = _shellCore.GetState();
        visibleWindows = state.GetVisibleWindows().ToList();
        
        Assert.Single(visibleWindows);
        Assert.Equal(window2Handle, visibleWindows[0].Handle);
    }
}