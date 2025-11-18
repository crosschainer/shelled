using Shell.Adapters.Win32;
using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Models;

namespace Shell.Tests;

/// <summary>
/// Integration tests for ShellCore with real Win32 adapters
/// </summary>
public class ShellCoreIntegrationTests : IDisposable
{
    private readonly Shell.Core.ShellCore _shellCore;
    private readonly WindowSystemWin32 _windowSystem;
    private readonly ProcessLauncherWin32 _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly MockSystemEventHandler _systemEventHandler;
    private readonly EventPublisher _eventPublisher;
    private readonly List<ShellEvent> _capturedEvents;

    public ShellCoreIntegrationTests()
    {
        // Ensure we're in test mode for safety
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
        
        _windowSystem = new WindowSystemWin32();
        _processLauncher = new ProcessLauncherWin32();
        _trayHost = new MockTrayHost();
        _hotkeyRegistry = new MockHotkeyRegistry();
        _systemEventHandler = new MockSystemEventHandler();
        _eventPublisher = new EventPublisher();
        
        _shellCore = new Shell.Core.ShellCore(_windowSystem, _processLauncher, _trayHost, _hotkeyRegistry, _systemEventHandler, _eventPublisher);
        
        _capturedEvents = new List<ShellEvent>();
        _eventPublisher.Subscribe<WorkspaceCreatedEvent>(evt => _capturedEvents.Add(evt));
        _eventPublisher.Subscribe<WorkspaceSwitchedEvent>(evt => _capturedEvents.Add(evt));
    }

    [Fact]
    public void ShellCore_WithRealAdapters_InitializesCorrectly()
    {
        // Assert
        Assert.NotNull(_shellCore);
        var state = _shellCore.GetState();
        Assert.NotNull(state);
        Assert.Equal("default", state.ActiveWorkspaceId);
        Assert.Single(state.Workspaces); // Should have default workspace
    }

    [Fact]
    public async Task ShellCore_LaunchApp_InTestMode_ReturnsProcessId()
    {
        // Act
        var processId = await _shellCore.LaunchAppAsync("notepad.exe");

        // Assert
        Assert.Equal(12345, processId); // Should return fake process ID in test mode
    }

    [Fact]
    public void ShellCore_WindowSystemIntegration_DoesNotThrow()
    {
        // Act & Assert - should not throw even though we're on Linux in test mode
        var exception = Record.Exception(() => 
        {
            var fakeHandle = new IntPtr(12345);
            _shellCore.FocusWindow(fakeHandle);
        });
        Assert.Null(exception);
    }

    [Fact]
    public void ShellCore_CreateWorkspace_AddsWorkspaceToState()
    {
        // Arrange
        _capturedEvents.Clear();

        // Act
        _shellCore.CreateWorkspace("test-workspace", "Test Workspace");

        // Assert
        var workspaceCreatedEvent = _capturedEvents.OfType<WorkspaceCreatedEvent>().FirstOrDefault();
        Assert.NotNull(workspaceCreatedEvent);
        Assert.Equal("test-workspace", workspaceCreatedEvent.Workspace.Id);
        
        // Verify workspace exists in state
        var state = _shellCore.GetState();
        Assert.True(state.Workspaces.ContainsKey("test-workspace"));
    }

    [Fact]
    public void ShellCore_SwitchWorkspace_ChangesActiveWorkspace()
    {
        // Arrange
        _shellCore.CreateWorkspace("test-workspace", "Test Workspace");
        _capturedEvents.Clear();

        // Act
        _shellCore.SwitchWorkspace("test-workspace");

        // Assert
        var workspaceSwitchedEvent = _capturedEvents.OfType<WorkspaceSwitchedEvent>().FirstOrDefault();
        Assert.NotNull(workspaceSwitchedEvent);
        Assert.Equal("default", workspaceSwitchedEvent.PreviousWorkspaceId);
        Assert.Equal("test-workspace", workspaceSwitchedEvent.NewWorkspaceId);
        
        // Verify active workspace changed
        var state = _shellCore.GetState();
        Assert.Equal("test-workspace", state.ActiveWorkspaceId);
    }

    [Fact]
    public void ShellCore_WindowSystemEvents_CanBeSubscribed()
    {
        // Arrange
        var windowCreatedFired = false;
        var windowDestroyedFired = false;
        var windowActivatedFired = false;
        var windowUpdatedFired = false;

        // Act - Subscribe to window system events
        _windowSystem.WindowCreated += (hwnd) => windowCreatedFired = true;
        _windowSystem.WindowDestroyed += (hwnd) => windowDestroyedFired = true;
        _windowSystem.WindowActivated += (hwnd) => windowActivatedFired = true;
        _windowSystem.WindowUpdated += (hwnd) => windowUpdatedFired = true;

        // Assert - Events should be subscribable without throwing
        Assert.False(windowCreatedFired);
        Assert.False(windowDestroyedFired);
        Assert.False(windowActivatedFired);
        Assert.False(windowUpdatedFired);
    }

    [Fact]
    public void ShellCore_State_ReflectsChangesCorrectly()
    {
        // Arrange
        var initialState = _shellCore.GetState();
        var initialWindowCount = initialState.Windows.Count;

        // Act - Create a workspace and switch to it
        _shellCore.CreateWorkspace("integration-test", "Integration Test");
        _shellCore.SwitchWorkspace("integration-test");

        // Assert
        var finalState = _shellCore.GetState();
        Assert.Equal("integration-test", finalState.ActiveWorkspaceId);
        Assert.Equal(2, finalState.Workspaces.Count); // default + integration-test
        Assert.True(finalState.Workspaces.ContainsKey("integration-test"));
        
        // Window count should remain the same (no real windows on Linux)
        Assert.Equal(initialWindowCount, finalState.Windows.Count);
    }

    [Fact]
    public void ShellCore_SafeMode_PreventsActualSystemChanges()
    {
        // Ensure we're in test mode for this test
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
        
        // Assert that we're in safe mode
        Assert.True(ShellConfiguration.IsTestMode);
        Assert.True(ShellConfiguration.DisableDangerousOperations);
        
        // Verify that dangerous operations are disabled
        var fakeHandle = new IntPtr(12345);
        
        // These should not throw but also should not make actual system changes
        var exception1 = Record.Exception(() => _windowSystem.ShowWindow(fakeHandle, WindowState.Normal));
        var exception2 = Record.Exception(() => _windowSystem.SetForegroundWindow(fakeHandle));
        
        Assert.Null(exception1);
        Assert.Null(exception2);
    }

    [Fact]
    public void ShellCore_RealAdapters_WorkWithMockDependencies()
    {
        // This test verifies that real Win32 adapters can work alongside mock dependencies
        // Act & Assert - Should not throw
        var exception = Record.Exception(() =>
        {
            var state = _shellCore.GetState();
            Assert.NotNull(state);
            Assert.NotNull(_windowSystem);
            Assert.NotNull(_processLauncher);
            Assert.NotNull(_trayHost);
            Assert.NotNull(_hotkeyRegistry);
        });
        
        Assert.Null(exception);
    }



    public void Dispose()
    {
        _shellCore?.Dispose();
        _windowSystem?.Dispose();
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }
}