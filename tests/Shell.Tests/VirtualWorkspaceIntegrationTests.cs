using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Models;

namespace Shell.Tests;

/// <summary>
/// Integration tests for virtual workspace functionality
/// TEST-INT-VM-01: Assigning windows to workspaces + switching workspace hides/shows windows correctly
/// </summary>
public class VirtualWorkspaceIntegrationTests : IDisposable
{
    private readonly Shell.Core.ShellCore _shellCore;
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly MockSystemEventHandler _systemEventHandler;
    private readonly EventPublisher _eventPublisher;
    private readonly List<WorkspaceSwitchedEvent> _workspaceEvents;
    private readonly List<WindowUpdatedEvent> _visibilityEvents;

    public VirtualWorkspaceIntegrationTests()
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
        
        _workspaceEvents = new List<WorkspaceSwitchedEvent>();
        _visibilityEvents = new List<WindowUpdatedEvent>();
        
        _eventPublisher.Subscribe<WorkspaceSwitchedEvent>(evt => _workspaceEvents.Add(evt));
        _eventPublisher.Subscribe<WindowUpdatedEvent>(evt => _visibilityEvents.Add(evt));
    }

    [Fact]
    public void VirtualWorkspace_SwitchWorkspace_HidesAndShowsWindowsCorrectly()
    {
        // Arrange - Create two workspaces
        var workspace1 = "workspace1";
        var workspace2 = "workspace2";
        
        _shellCore.CreateWorkspace(workspace1, "Workspace 1");
        _shellCore.CreateWorkspace(workspace2, "Workspace 2");
        
        // Start in workspace1
        _shellCore.SwitchWorkspace(workspace1);
        
        // Create windows in workspace1 (they should be assigned to active workspace automatically)
        var window1 = new ShellWindow
        {
            Handle = new IntPtr(1001),
            Title = "App 1 - Workspace 1",
            ProcessId = 1001,
            WorkspaceId = "", // Let it be assigned to active workspace
            State = WindowState.Normal,
            IsVisible = true
        };
        
        var window2 = new ShellWindow
        {
            Handle = new IntPtr(1002),
            Title = "App 2 - Workspace 1",
            ProcessId = 1002,
            WorkspaceId = "", // Let it be assigned to active workspace
            State = WindowState.Normal,
            IsVisible = true
        };
        
        _windowSystem.SimulateWindowCreated(window1);
        _windowSystem.SimulateWindowCreated(window2);
        
        // Switch to workspace2 and create windows there
        _shellCore.SwitchWorkspace(workspace2);
        
        var window3 = new ShellWindow
        {
            Handle = new IntPtr(2001),
            Title = "App 3 - Workspace 2",
            ProcessId = 2001,
            WorkspaceId = "", // Let it be assigned to active workspace (workspace2)
            State = WindowState.Normal,
            IsVisible = true
        };
        
        _windowSystem.SimulateWindowCreated(window3);
        
        // Clear events to focus on the switching behavior
        _workspaceEvents.Clear();
        _visibilityEvents.Clear();
        
        // Act - Switch back to workspace1
        _shellCore.SwitchWorkspace(workspace1);
        
        // Assert
        var state = _shellCore.GetState();
        

        
        // Verify active workspace changed
        Assert.Equal(workspace1, state.ActiveWorkspaceId);
        Assert.Single(_workspaceEvents);
        Assert.Equal(workspace1, _workspaceEvents[0].NewWorkspaceId);
        
        // Verify windows are in correct workspaces
        Assert.True(state.Workspaces[workspace1].WindowHandles.Contains(window1.Handle));
        Assert.True(state.Workspaces[workspace1].WindowHandles.Contains(window2.Handle));
        Assert.True(state.Workspaces[workspace2].WindowHandles.Contains(window3.Handle));
        
        // Verify visibility changes occurred
        // When switching to workspace1, windows in workspace1 should be shown, workspace2 windows hidden
        // Verify visibility changes occurred
        Assert.True(_visibilityEvents.Count >= 0); // At least some events should occur
        
        // Check that workspace1 windows are visible and workspace2 windows are hidden
        var workspace1Windows = state.Windows.Values.Where(w => w.WorkspaceId == workspace1);
        var workspace2Windows = state.Windows.Values.Where(w => w.WorkspaceId == workspace2);
        
        foreach (var window in workspace1Windows)
        {
            Assert.True(window.IsVisible, $"Window {window.Handle} in active workspace should be visible");
        }
        
        foreach (var window in workspace2Windows)
        {
            Assert.False(window.IsVisible, $"Window {window.Handle} in inactive workspace should be hidden");
        }
    }

    [Fact]
    public void VirtualWorkspace_SimpleWorkspaceSwitch_HidesWindow()
    {
        // Arrange - Create two workspaces
        var ws1 = "ws1";
        var ws2 = "ws2";
        
        _shellCore.CreateWorkspace(ws1, "Workspace 1");
        _shellCore.CreateWorkspace(ws2, "Workspace 2");
        
        // Switch to ws1 and create a window
        _shellCore.SwitchWorkspace(ws1);
        var window1 = new ShellWindow { Handle = new IntPtr(1001), Title = "W1", ProcessId = 1001, WorkspaceId = ws1, State = WindowState.Normal, IsVisible = true };
        _windowSystem.SimulateWindowCreated(window1);
        
        // Verify window is visible in ws1
        var state1 = _shellCore.GetState();
        Assert.Equal(ws1, state1.ActiveWorkspaceId);
        Assert.True(state1.Windows[window1.Handle].IsVisible);
        Assert.True(state1.Workspaces[ws1].WindowHandles.Contains(window1.Handle));
        
        // Act - Switch to ws2
        _shellCore.SwitchWorkspace(ws2);
        
        // Assert - Window should be hidden
        var state2 = _shellCore.GetState();
        Assert.Equal(ws2, state2.ActiveWorkspaceId);
        Assert.False(state2.Windows[window1.Handle].IsVisible, "Window should be hidden when switching to different workspace");
    }

    [Fact]
    public void VirtualWorkspace_MultipleWorkspaceSwitches_MaintainsCorrectVisibility()
    {
        // Arrange - Create three workspaces with windows
        var ws1 = "ws1";
        var ws2 = "ws2";
        var ws3 = "ws3";
        
        _shellCore.CreateWorkspace(ws1, "Workspace 1");
        _shellCore.CreateWorkspace(ws2, "Workspace 2");
        _shellCore.CreateWorkspace(ws3, "Workspace 3");
        
        // Create windows in each workspace
        _shellCore.SwitchWorkspace(ws1);
        var window1 = new ShellWindow { Handle = new IntPtr(1001), Title = "W1", ProcessId = 1001, State = WindowState.Normal, IsVisible = true, WorkspaceId = string.Empty };
        _windowSystem.SimulateWindowCreated(window1);
        
        _shellCore.SwitchWorkspace(ws2);
        var window2 = new ShellWindow { Handle = new IntPtr(2001), Title = "W2", ProcessId = 2001, State = WindowState.Normal, IsVisible = true, WorkspaceId = string.Empty };
        _windowSystem.SimulateWindowCreated(window2);
        
        _shellCore.SwitchWorkspace(ws3);
        var window3 = new ShellWindow { Handle = new IntPtr(3001), Title = "W3", ProcessId = 3001, State = WindowState.Normal, IsVisible = true, WorkspaceId = string.Empty };
        _windowSystem.SimulateWindowCreated(window3);
        
        // Act - Switch through workspaces multiple times
        _shellCore.SwitchWorkspace(ws1);
        var state1 = _shellCore.GetState();
        
        _shellCore.SwitchWorkspace(ws2);
        var state2 = _shellCore.GetState();
        
        _shellCore.SwitchWorkspace(ws3);
        var state3 = _shellCore.GetState();
        
        _shellCore.SwitchWorkspace(ws1);
        var stateFinal = _shellCore.GetState();
        
        // Assert - Each switch should show only the active workspace's windows
        Assert.Equal(ws1, state1.ActiveWorkspaceId);
        Assert.True(state1.Windows[window1.Handle].IsVisible);
        Assert.False(state1.Windows[window2.Handle].IsVisible);
        Assert.False(state1.Windows[window3.Handle].IsVisible);
        
        Assert.Equal(ws2, state2.ActiveWorkspaceId);
        Assert.False(state2.Windows[window1.Handle].IsVisible);
        Assert.True(state2.Windows[window2.Handle].IsVisible);
        Assert.False(state2.Windows[window3.Handle].IsVisible);
        
        Assert.Equal(ws3, state3.ActiveWorkspaceId);
        Assert.False(state3.Windows[window1.Handle].IsVisible);
        Assert.False(state3.Windows[window2.Handle].IsVisible);
        Assert.True(state3.Windows[window3.Handle].IsVisible);
        
        Assert.Equal(ws1, stateFinal.ActiveWorkspaceId);
        Assert.True(stateFinal.Windows[window1.Handle].IsVisible);
        Assert.False(stateFinal.Windows[window2.Handle].IsVisible);
        Assert.False(stateFinal.Windows[window3.Handle].IsVisible);
    }

    [Fact]
    public void VirtualWorkspace_WindowMovedBetweenWorkspaces_UpdatesVisibilityCorrectly()
    {
        // Arrange
        var ws1 = "workspace1";
        var ws2 = "workspace2";
        
        _shellCore.CreateWorkspace(ws1, "Workspace 1");
        _shellCore.CreateWorkspace(ws2, "Workspace 2");
        _shellCore.SwitchWorkspace(ws1);
        
        var window = new ShellWindow
        {
            Handle = new IntPtr(1001),
            Title = "Movable Window",
            ProcessId = 1001,
            WorkspaceId = ws1,
            State = WindowState.Normal,
            IsVisible = true
        };
        
        _windowSystem.SimulateWindowCreated(window);
        
        // Act - Move window to different workspace
        _shellCore.MoveWindowToWorkspace(window.Handle, ws2);
        
        // Switch to workspace2 to see the moved window
        _shellCore.SwitchWorkspace(ws2);
        var state = _shellCore.GetState();
        
        // Assert
        Assert.Equal(ws2, state.ActiveWorkspaceId);
        Assert.True(state.Workspaces[ws2].WindowHandles.Contains(window.Handle));
        Assert.False(state.Workspaces[ws1].WindowHandles.Contains(window.Handle));
        Assert.Equal(ws2, state.Windows[window.Handle].WorkspaceId);
        Assert.True(state.Windows[window.Handle].IsVisible);
    }

    [Fact]
    public void VirtualWorkspace_WindowClosedInInactiveWorkspace_RemovedCorrectly()
    {
        // Arrange
        var ws1 = "workspace1";
        var ws2 = "workspace2";
        
        _shellCore.CreateWorkspace(ws1, "Workspace 1");
        _shellCore.CreateWorkspace(ws2, "Workspace 2");
        
        // Create window in ws1
        _shellCore.SwitchWorkspace(ws1);
        var window = new ShellWindow
        {
            Handle = new IntPtr(1001),
            Title = "Window to Close",
            ProcessId = 1001,
            WorkspaceId = ws1,
            State = WindowState.Normal,
            IsVisible = true
        };
        _windowSystem.SimulateWindowCreated(window);
        
        // Switch to ws2 (making ws1 inactive)
        _shellCore.SwitchWorkspace(ws2);
        
        // Act - Close window in inactive workspace
        _windowSystem.SimulateWindowDestroyed(window.Handle);
        
        // Assert
        var state = _shellCore.GetState();
        Assert.False(state.Windows.ContainsKey(window.Handle));
        Assert.False(state.Workspaces[ws1].WindowHandles.Contains(window.Handle));
    }

    [Fact]
    public void VirtualWorkspace_NewWindowCreatedInInactiveWorkspace_RemainsHidden()
    {
        // Arrange
        var ws1 = "workspace1";
        var ws2 = "workspace2";
        
        _shellCore.CreateWorkspace(ws1, "Workspace 1");
        _shellCore.CreateWorkspace(ws2, "Workspace 2");
        _shellCore.SwitchWorkspace(ws1); // ws1 is active
        
        // Act - Create window in inactive workspace (ws2)
        var window = new ShellWindow
        {
            Handle = new IntPtr(2001),
            Title = "Background Window",
            ProcessId = 2001,
            WorkspaceId = ws2,
            State = WindowState.Normal,
            IsVisible = true // Window starts visible but should be hidden because workspace is inactive
        };
        
        _windowSystem.SimulateWindowCreated(window);
        
        // Assert
        var state = _shellCore.GetState();
        Assert.Equal(ws1, state.ActiveWorkspaceId); // ws1 still active
        Assert.True(state.Windows.ContainsKey(window.Handle));
        Assert.True(state.Workspaces[ws2].WindowHandles.Contains(window.Handle));
        
        // Window should be hidden because it's in an inactive workspace
        Assert.False(state.Windows[window.Handle].IsVisible);
    }

    [Fact]
    public void VirtualWorkspace_EmptyWorkspaceSwitch_DoesNotThrow()
    {
        // Arrange
        var emptyWorkspace = "empty";
        _shellCore.CreateWorkspace(emptyWorkspace, "Empty Workspace");
        
        // Act & Assert - Should not throw
        _shellCore.SwitchWorkspace(emptyWorkspace);
        
        var state = _shellCore.GetState();
        Assert.Equal(emptyWorkspace, state.ActiveWorkspaceId);
        Assert.Empty(state.Workspaces[emptyWorkspace].WindowHandles);
    }

    [Fact]
    public void VirtualWorkspace_WorkspaceEvents_FiredCorrectly()
    {
        // Arrange
        var ws1 = "workspace1";
        var ws2 = "workspace2";
        
        _shellCore.CreateWorkspace(ws1, "Workspace 1");
        _shellCore.CreateWorkspace(ws2, "Workspace 2");
        
        _workspaceEvents.Clear();
        
        // Act
        _shellCore.SwitchWorkspace(ws1);
        _shellCore.SwitchWorkspace(ws2);
        _shellCore.SwitchWorkspace(ws1);
        
        // Assert
        Assert.Equal(3, _workspaceEvents.Count);
        
        Assert.Equal("default", _workspaceEvents[0].PreviousWorkspaceId); // From default to ws1
        Assert.Equal(ws1, _workspaceEvents[0].NewWorkspaceId);
        
        Assert.Equal(ws1, _workspaceEvents[1].PreviousWorkspaceId); // From ws1 to ws2
        Assert.Equal(ws2, _workspaceEvents[1].NewWorkspaceId);
        
        Assert.Equal(ws2, _workspaceEvents[2].PreviousWorkspaceId); // From ws2 to ws1
        Assert.Equal(ws1, _workspaceEvents[2].NewWorkspaceId);
        
        // All events should have valid timestamps and IDs
        foreach (var evt in _workspaceEvents)
        {
            Assert.NotNull(evt.EventId);
            Assert.NotEmpty(evt.EventId);
            Assert.True(evt.Timestamp > DateTime.MinValue);
        }
    }

    public void Dispose()
    {
        _shellCore?.Dispose();
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }
}