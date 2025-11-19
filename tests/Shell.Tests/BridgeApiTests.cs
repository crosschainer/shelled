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
/// Tests for the bridge API methods that would be called from JavaScript
/// These tests simulate the UI-to-Core communication without requiring WebView2
/// </summary>
public class BridgeApiTests : IDisposable
{
    private readonly ShellCore _shellCore;
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly MockSystemEventHandler _systemEventHandler;
    private readonly FakeEventPublisher _eventPublisher;
    private readonly FakeWebView _webView;
    private readonly BridgeApiWrapper _bridgeApi;

    public BridgeApiTests()
    {
        _windowSystem = new MockWindowSystem();
        _processLauncher = new MockProcessLauncher();
        _trayHost = new MockTrayHost();
        _hotkeyRegistry = new MockHotkeyRegistry();
        _systemEventHandler = new MockSystemEventHandler();
        _eventPublisher = new FakeEventPublisher();
        _webView = new FakeWebView();
        
        _shellCore = new ShellCore(
            _windowSystem,
            _processLauncher,
            _trayHost,
            _hotkeyRegistry,
            _systemEventHandler,
            _eventPublisher);
            
        _bridgeApi = new BridgeApiWrapper(_shellCore, _webView, _eventPublisher);
    }

    [Fact]
    public void BridgeApi_FocusWindow_WithValidHwnd_CallsShellCoreFocusWindow()
    {
        // Arrange
        var hwnd = new IntPtr(12345);
        var window = new ShellWindow 
        { 
            Handle = hwnd, 
            Title = "Test Window", 
            WorkspaceId = "default" 
        };
        _windowSystem.SimulateWindowCreated(window);

        // Act
        var result = _bridgeApi.FocusWindow("12345");

        // Assert
        Assert.True(result);
        var state = _shellCore.GetState();
        Assert.Equal(hwnd, state.FocusedWindowHandle);
    }

    [Fact]
    public void BridgeApi_FocusWindow_WithInvalidHwnd_ReturnsFalse()
    {
        // Act
        var result = _bridgeApi.FocusWindow("99999");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void BridgeApi_FocusWindow_WithNonNumericHwnd_ReturnsFalse()
    {
        // Act
        var result = _bridgeApi.FocusWindow("not-a-number");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void BridgeApi_SwitchWorkspace_WithValidWorkspace_CallsShellCoreSwitchWorkspace()
    {
        // Arrange
        const string workspaceId = "test-workspace";
        _shellCore.CreateWorkspace(workspaceId, "Test Workspace");

        // Act
        var result = _bridgeApi.SwitchWorkspace(workspaceId);

        // Assert
        Assert.True(result);
        var state = _shellCore.GetState();
        Assert.Equal(workspaceId, state.ActiveWorkspaceId);
    }

    [Fact]
    public void BridgeApi_SwitchWorkspace_WithInvalidWorkspace_ReturnsFalse()
    {
        // Act
        var result = _bridgeApi.SwitchWorkspace("non-existent-workspace");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BridgeApi_LaunchApp_WithValidApp_CallsShellCoreLaunchApp()
    {
        // Arrange
        const string appPath = "notepad.exe";

        // Act
        var result = await _bridgeApi.LaunchApp(appPath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task BridgeApi_LaunchApp_WithEmptyApp_ReturnsFalse()
    {
        // Act
        var result = await _bridgeApi.LaunchApp("");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void BridgeApi_ListWindowsJson_ReturnsValidJson()
    {
        // Arrange
        var window1 = new ShellWindow
        {
            Handle = new IntPtr(123),
            Title = "Window 1",
            ProcessId = 1001,
            WorkspaceId = "default",
            State = WindowState.Normal,
            IsVisible = true,
            AppId = "app1",
            ClassName = "Class1"
        };
        
        var window2 = new ShellWindow
        {
            Handle = new IntPtr(456),
            Title = "Window 2",
            ProcessId = 1002,
            WorkspaceId = "workspace2",
            State = WindowState.Minimized,
            IsVisible = false,
            AppId = "app2",
            ClassName = "Class2"
        };

        _windowSystem.SimulateWindowCreated(window1);
        _windowSystem.SimulateWindowCreated(window2);

        // Act
        var json = _bridgeApi.ListWindowsJson();

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        
        // Verify JSON contains expected data
        Assert.Contains("123", json);
        Assert.Contains("Window 1", json);
        Assert.Contains("456", json);
        Assert.Contains("Window 2", json);
        Assert.Contains("default", json);
        Assert.Contains("workspace2", json);
    }

    [Fact]
    public void BridgeApi_ListWorkspacesJson_ReturnsValidJson()
    {
        // Arrange
        _shellCore.CreateWorkspace("workspace1", "Main Workspace");
        _shellCore.CreateWorkspace("workspace2", "Development");

        // Act
        var json = _bridgeApi.ListWorkspacesJson();

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        
        // Verify JSON contains expected data
        Assert.Contains("workspace1", json);
        Assert.Contains("Main Workspace", json);
        Assert.Contains("workspace2", json);
        Assert.Contains("Development", json);
        Assert.Contains("default", json); // Default workspace should exist
    }

    [Fact]
    public void BridgeApi_GetTrayIconsJson_ReturnsValidJson()
    {
        // Arrange
        var trayIcon = new TrayIcon
        {
            Id = "test-tray",
            Tooltip = "Test Tray Icon",
            ProcessId = 2001,
            IsVisible = true,
            IconData = new byte[] { 1, 2, 3, 4 }
        };

        _trayHost.SimulateTrayIconAdded(trayIcon);

        // Act
        var json = _bridgeApi.GetTrayIconsJson();

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        
        // Verify JSON contains expected data
        Assert.Contains("test-tray", json);
        Assert.Contains("Test Tray Icon", json);
        Assert.Contains("2001", json);
    }

    [Fact]
    public void BridgeApi_EventForwarding_WindowCreated_SendsMessageToWebView()
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

        // Act
        _windowSystem.SimulateWindowCreated(window);

        // Assert
        Assert.Single(_webView.SentMessages);
        var message = _webView.SentMessages.First();
        Assert.Contains("windowCreated", message);
        Assert.Contains("999", message);
        Assert.Contains("Event Test Window", message);
    }

    [Fact]
    public void BridgeApi_EventForwarding_WindowDestroyed_SendsMessageToWebView()
    {
        // Arrange
        var hwnd = new IntPtr(888);
        var window = new ShellWindow { Handle = hwnd, Title = "Test", WorkspaceId = "default" };
        _windowSystem.SimulateWindowCreated(window);
        
        // Clear messages from window creation
        _webView.ClearMessages();

        // Act
        _windowSystem.SimulateWindowDestroyed(hwnd);

        // Assert
        Assert.Single(_webView.SentMessages);
        var message = _webView.SentMessages.First();
        Assert.Contains("windowDestroyed", message);
        Assert.Contains("888", message);
    }

    [Fact]
    public void BridgeApi_EventForwarding_WorkspaceSwitched_SendsMessageToWebView()
    {
        // Arrange
        const string workspaceId = "test-workspace";
        _shellCore.CreateWorkspace(workspaceId, "Test Workspace");

        // Act
        _shellCore.SwitchWorkspace(workspaceId);

        // Assert
        Assert.Single(_webView.SentMessages);
        var message = _webView.SentMessages.First();
        Assert.Contains("workspaceSwitched", message);
        Assert.Contains("default", message); // Previous workspace
        Assert.Contains(workspaceId, message); // New workspace
    }

    public void Dispose()
    {
        _shellCore?.Dispose();
        _bridgeApi?.Dispose();
    }
}

/// <summary>
/// Wrapper around ShellApi that simulates the bridge functionality without WebView2 dependencies
/// </summary>
public class BridgeApiWrapper : IDisposable
{
    private readonly ShellCore _shellCore;
    private readonly FakeWebView _webView;
    private readonly IEventPublisher _eventPublisher;

    public BridgeApiWrapper(ShellCore shellCore, FakeWebView webView, IEventPublisher eventPublisher)
    {
        _shellCore = shellCore ?? throw new ArgumentNullException(nameof(shellCore));
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        
        // Subscribe to events and forward them to the web view
        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _eventPublisher.Subscribe<WindowCreatedEvent>(OnWindowCreated);
        _eventPublisher.Subscribe<WindowDestroyedEvent>(OnWindowDestroyed);
        _eventPublisher.Subscribe<WindowFocusChangedEvent>(OnWindowFocusChanged);
        _eventPublisher.Subscribe<WorkspaceSwitchedEvent>(OnWorkspaceSwitched);
        _eventPublisher.Subscribe<TrayIconAddedEvent>(OnTrayIconAdded);
        _eventPublisher.Subscribe<TrayIconRemovedEvent>(OnTrayIconRemoved);
    }

    public bool FocusWindow(string hwndString)
    {
        if (string.IsNullOrWhiteSpace(hwndString))
            return false;

        if (!IntPtr.TryParse(hwndString, out var hwnd))
            return false;

        // Check if the window exists in the shell state
        var state = _shellCore.GetState();
        if (!state.Windows.ContainsKey(hwnd))
            return false;

        _shellCore.FocusWindow(hwnd);
        return true;
    }

    public bool SwitchWorkspace(string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
            return false;

        // Check if the workspace exists in the shell state
        var state = _shellCore.GetState();
        if (!state.Workspaces.ContainsKey(workspaceId))
            return false;

        _shellCore.SwitchWorkspace(workspaceId);
        return true;
    }

    public async Task<bool> LaunchApp(string appIdOrPath)
    {
        if (string.IsNullOrWhiteSpace(appIdOrPath))
            return false;

        try
        {
            await _shellCore.LaunchAppAsync(appIdOrPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string ListWindowsJson()
    {
        var state = _shellCore.GetState();
        var windows = state.Windows.Values.Select(w => new
        {
            hwnd = w.Handle.ToString(),
            title = w.Title,
            processId = w.ProcessId,
            workspaceId = w.WorkspaceId,
            state = w.State.ToString(),
            isVisible = w.IsVisible,
            appId = w.AppId,
            className = w.ClassName
        });

        return System.Text.Json.JsonSerializer.Serialize(windows);
    }

    public string ListWorkspacesJson()
    {
        var state = _shellCore.GetState();
        var workspaces = state.Workspaces.Values.Select(w => new
        {
            id = w.Id,
            name = w.Name,
            isActive = w.IsActive
        });

        return System.Text.Json.JsonSerializer.Serialize(workspaces);
    }

    public string GetTrayIconsJson()
    {
        var state = _shellCore.GetState();
        var trayIcons = state.TrayIcons.Values.Select(t => new
        {
            id = t.Id,
            tooltip = t.Tooltip,
            processId = t.ProcessId,
            isVisible = t.IsVisible,
            iconData = t.IconData != null ? Convert.ToBase64String(t.IconData) : null
        });

        return System.Text.Json.JsonSerializer.Serialize(trayIcons);
    }

    private void OnWindowCreated(WindowCreatedEvent e)
    {
        var message = new
        {
            type = "windowCreated",
            data = new
            {
                hwnd = e.Window.Handle.ToString(),
                title = e.Window.Title,
                processId = e.Window.ProcessId,
                workspaceId = e.Window.WorkspaceId,
                state = e.Window.State.ToString(),
                isVisible = e.Window.IsVisible,
                appId = e.Window.AppId,
                className = e.Window.ClassName
            },
            timestamp = DateTime.UtcNow.ToString("O")
        };

        var json = System.Text.Json.JsonSerializer.Serialize(message);
        _webView.PostWebMessageAsString(json);
    }

    private void OnWindowDestroyed(WindowDestroyedEvent e)
    {
        var message = new
        {
            type = "windowDestroyed",
            data = new { hwnd = e.WindowHandle.ToString() },
            timestamp = DateTime.UtcNow.ToString("O")
        };

        var json = System.Text.Json.JsonSerializer.Serialize(message);
        _webView.PostWebMessageAsString(json);
    }

    private void OnWindowFocusChanged(WindowFocusChangedEvent e)
    {
        var message = new
        {
            type = "windowFocusChanged",
            data = new 
            { 
                previousHwnd = e.PreviousWindowHandle.ToString(),
                newHwnd = e.NewWindowHandle.ToString()
            },
            timestamp = DateTime.UtcNow.ToString("O")
        };

        var json = System.Text.Json.JsonSerializer.Serialize(message);
        _webView.PostWebMessageAsString(json);
    }

    private void OnWorkspaceSwitched(WorkspaceSwitchedEvent e)
    {
        var message = new
        {
            type = "workspaceSwitched",
            data = new
            {
                previousWorkspaceId = e.PreviousWorkspaceId,
                newWorkspaceId = e.NewWorkspaceId
            },
            timestamp = DateTime.UtcNow.ToString("O")
        };

        var json = System.Text.Json.JsonSerializer.Serialize(message);
        _webView.PostWebMessageAsString(json);
    }

    private void OnTrayIconAdded(TrayIconAddedEvent e)
    {
        var message = new
        {
            type = "trayIconAdded",
            data = new
            {
                id = e.TrayIcon.Id,
                tooltip = e.TrayIcon.Tooltip,
                processId = e.TrayIcon.ProcessId,
                isVisible = e.TrayIcon.IsVisible,
                iconData = e.TrayIcon.IconData != null ? Convert.ToBase64String(e.TrayIcon.IconData) : null
            },
            timestamp = DateTime.UtcNow.ToString("O")
        };

        var json = System.Text.Json.JsonSerializer.Serialize(message);
        _webView.PostWebMessageAsString(json);
    }

    private void OnTrayIconRemoved(TrayIconRemovedEvent e)
    {
        var message = new
        {
            type = "trayIconRemoved",
            data = new { id = e.TrayIconId },
            timestamp = DateTime.UtcNow.ToString("O")
        };

        var json = System.Text.Json.JsonSerializer.Serialize(message);
        _webView.PostWebMessageAsString(json);
    }

    public void Dispose()
    {
        // Unsubscribe from events
        _eventPublisher.Unsubscribe<WindowCreatedEvent>(OnWindowCreated);
        _eventPublisher.Unsubscribe<WindowDestroyedEvent>(OnWindowDestroyed);
        _eventPublisher.Unsubscribe<WindowFocusChangedEvent>(OnWindowFocusChanged);
        _eventPublisher.Unsubscribe<WorkspaceSwitchedEvent>(OnWorkspaceSwitched);
        _eventPublisher.Unsubscribe<TrayIconAddedEvent>(OnTrayIconAdded);
        _eventPublisher.Unsubscribe<TrayIconRemovedEvent>(OnTrayIconRemoved);
    }
}

/// <summary>
/// Fake WebView2 implementation for testing
/// </summary>
public class FakeWebView
{
    private readonly List<string> _sentMessages = new();

    public IReadOnlyList<string> SentMessages => _sentMessages.AsReadOnly();

    public void PostWebMessageAsString(string webMessageAsString)
    {
        _sentMessages.Add(webMessageAsString);
    }

    public void ClearMessages()
    {
        _sentMessages.Clear();
    }
}