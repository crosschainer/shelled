using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Interfaces;
using Shell.Core.Models;
using Xunit;

namespace Shell.Tests;

/// <summary>
/// Integration tests for bridge functionality using a fake shell core
/// These tests verify that the core can provide data in the format expected by the bridge
/// </summary>
public class BridgeIntegrationTests : IDisposable
{
    private readonly ShellCore _shellCore;
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly MockSystemEventHandler _systemEventHandler;
    private readonly FakeEventPublisher _eventPublisher;

    public BridgeIntegrationTests()
    {
        _windowSystem = new MockWindowSystem();
        _processLauncher = new MockProcessLauncher();
        _trayHost = new MockTrayHost();
        _hotkeyRegistry = new MockHotkeyRegistry();
        _systemEventHandler = new MockSystemEventHandler();
        _eventPublisher = new FakeEventPublisher();
        
        _shellCore = new ShellCore(
            _windowSystem,
            _processLauncher,
            _trayHost,
            _hotkeyRegistry,
            _systemEventHandler,
            _eventPublisher);
    }

    [Fact]
    public void ShellCore_GetState_ReturnsCorrectWindowData()
    {
        // Arrange
        var window1 = new ShellWindow
        {
            Handle = new IntPtr(123),
            Title = "Test Window 1",
            ProcessId = 1001,
            WorkspaceId = "workspace1",
            State = WindowState.Normal,
            IsVisible = true,
            AppId = "test-app-1",
            ClassName = "TestClass1"
        };
        
        var window2 = new ShellWindow
        {
            Handle = new IntPtr(456),
            Title = "Test Window 2",
            ProcessId = 1002,
            WorkspaceId = "workspace2",
            State = WindowState.Minimized,
            IsVisible = false,
            AppId = "test-app-2",
            ClassName = "TestClass2"
        };

        // Simulate windows being created
        _windowSystem.SimulateWindowCreated(window1);
        _windowSystem.SimulateWindowCreated(window2);

        // Act
        var state = _shellCore.GetState();

        // Assert
        Assert.NotNull(state);
        Assert.Equal(2, state.Windows.Count);
        Assert.True(state.Windows.ContainsKey(new IntPtr(123)));
        Assert.True(state.Windows.ContainsKey(new IntPtr(456)));
        Assert.Equal("Test Window 1", state.Windows[new IntPtr(123)].Title);
        Assert.Equal("Test Window 2", state.Windows[new IntPtr(456)].Title);
    }

    [Fact]
    public void ShellCore_GetState_ReturnsCorrectWorkspaceData()
    {
        // Arrange - Create workspaces through the core
        _shellCore.CreateWorkspace("workspace1", "Main");
        _shellCore.CreateWorkspace("workspace2", "Development");
        _shellCore.SwitchWorkspace("workspace1"); // Make workspace1 active

        // Act
        var state = _shellCore.GetState();

        // Assert
        Assert.NotNull(state);
        Assert.Equal(3, state.Workspaces.Count); // Including default workspace
        Assert.True(state.Workspaces.ContainsKey("workspace1"));
        Assert.True(state.Workspaces.ContainsKey("workspace2"));
        Assert.Equal("Main", state.Workspaces["workspace1"].Name);
        Assert.Equal("Development", state.Workspaces["workspace2"].Name);
        Assert.Equal("workspace1", state.ActiveWorkspaceId);
    }

    [Fact]
    public void ShellCore_GetState_ReturnsCorrectTrayData()
    {
        // Arrange
        var trayIcon = new TrayIcon
        {
            Id = "test-tray-1",
            Tooltip = "Test Tray Icon",
            ProcessId = 2001,
            IsVisible = true,
            IconData = new byte[] { 1, 2, 3, 4 }
        };

        // Simulate tray icon being added
        _trayHost.SimulateTrayIconAdded(trayIcon);

        // Act
        var state = _shellCore.GetState();

        // Assert
        Assert.NotNull(state);
        Assert.Single(state.TrayIcons);
        Assert.True(state.TrayIcons.ContainsKey("test-tray-1"));
        Assert.Equal("Test Tray Icon", state.TrayIcons["test-tray-1"].Tooltip);
        Assert.Equal(2001, state.TrayIcons["test-tray-1"].ProcessId);
    }

    [Fact]
    public async Task ShellCore_LaunchApp_ReturnsProcessId()
    {
        // Arrange
        const string appPath = "notepad.exe";

        // Act
        var processId = await _shellCore.LaunchAppAsync(appPath);

        // Assert
        Assert.True(processId > 0);
    }

    [Fact]
    public void ShellCore_FocusWindow_UpdatesFocusedWindow()
    {
        // Arrange
        var hwnd = new IntPtr(789);
        var window = new ShellWindow { Handle = hwnd, Title = "Focus Test", WorkspaceId = "default" };
        _windowSystem.SimulateWindowCreated(window);

        // Act
        _shellCore.FocusWindow(hwnd);

        // Assert
        var state = _shellCore.GetState();
        Assert.Equal(hwnd, state.FocusedWindowHandle);
    }

    [Fact]
    public void ShellCore_SwitchWorkspace_UpdatesActiveWorkspace()
    {
        // Arrange
        const string workspaceId = "test-workspace";
        _shellCore.CreateWorkspace(workspaceId, "Test Workspace");

        // Act
        _shellCore.SwitchWorkspace(workspaceId);

        // Assert
        var state = _shellCore.GetState();
        Assert.Equal(workspaceId, state.ActiveWorkspaceId);
    }

    [Fact]
    public void ShellCore_WindowCreated_PublishesEvent()
    {
        // Arrange
        var window = new ShellWindow
        {
            Handle = new IntPtr(999),
            Title = "Event Test Window",
            ProcessId = 3001,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true,
            AppId = "event-test-app",
            ClassName = "EventTestClass"
        };

        WindowCreatedEvent? publishedEvent = null;
        _eventPublisher.Subscribe<WindowCreatedEvent>(e => publishedEvent = e);

        // Act
        _windowSystem.SimulateWindowCreated(window);

        // Assert
        Assert.NotNull(publishedEvent);
        Assert.Equal(new IntPtr(999), publishedEvent.Window.Handle);
        Assert.Equal("Event Test Window", publishedEvent.Window.Title);
    }

    [Fact]
    public void ShellCore_WindowDestroyed_PublishesEvent()
    {
        // Arrange
        var hwnd = new IntPtr(888);
        var window = new ShellWindow { Handle = hwnd, Title = "Test", WorkspaceId = "default" };
        _windowSystem.SimulateWindowCreated(window);

        WindowDestroyedEvent? publishedEvent = null;
        _eventPublisher.Subscribe<WindowDestroyedEvent>(e => publishedEvent = e);

        // Act
        _windowSystem.SimulateWindowDestroyed(hwnd);

        // Assert
        Assert.NotNull(publishedEvent);
        Assert.Equal(hwnd, publishedEvent.WindowHandle);
    }

    [Fact]
    public void ShellCore_WorkspaceSwitched_PublishesEvent()
    {
        // Arrange
        const string workspaceId = "test-workspace";
        _shellCore.CreateWorkspace(workspaceId, "Test Workspace");

        WorkspaceSwitchedEvent? publishedEvent = null;
        _eventPublisher.Subscribe<WorkspaceSwitchedEvent>(e => publishedEvent = e);

        // Act
        _shellCore.SwitchWorkspace(workspaceId);

        // Assert
        Assert.NotNull(publishedEvent);
        Assert.Equal("default", publishedEvent.PreviousWorkspaceId);
        Assert.Equal(workspaceId, publishedEvent.NewWorkspaceId);
    }

    public void Dispose()
    {
        _shellCore?.Dispose();
    }
}

/// <summary>
/// Fake EventPublisher for testing
/// </summary>
public class FakeEventPublisher : IEventPublisher
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    public void Subscribe<T>(Action<T> handler) where T : class
    {
        var eventType = typeof(T);
        if (!_subscribers.ContainsKey(eventType))
        {
            _subscribers[eventType] = new List<Delegate>();
        }
        _subscribers[eventType].Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler) where T : class
    {
        var eventType = typeof(T);
        if (_subscribers.ContainsKey(eventType))
        {
            _subscribers[eventType].Remove(handler);
        }
    }

    public void Publish<T>(T eventData) where T : class
    {
        var eventType = typeof(T);
        if (_subscribers.ContainsKey(eventType))
        {
            foreach (var handler in _subscribers[eventType].Cast<Action<T>>())
            {
                handler(eventData);
            }
        }
    }
}