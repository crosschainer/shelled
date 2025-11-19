using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Interfaces;
using Shell.Core.Models;

namespace Shell.Tests;

/// <summary>
/// Tests for ShellCore tray icon management
/// </summary>
public class ShellCoreTrayTests
{
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly MockSystemEventHandler _systemEventHandler;
    private readonly EventPublisher _eventPublisher;
    private readonly Shell.Core.ShellCore _shellCore;
    private readonly List<ShellEvent> _capturedEvents;

    public ShellCoreTrayTests()
    {
        _windowSystem = new MockWindowSystem();
        _processLauncher = new MockProcessLauncher();
        _trayHost = new MockTrayHost();
        _hotkeyRegistry = new MockHotkeyRegistry();
        _systemEventHandler = new MockSystemEventHandler();
        _eventPublisher = new EventPublisher();
        _capturedEvents = new List<ShellEvent>();

        // Subscribe to tray events for testing
        _eventPublisher.Subscribe<TrayIconAddedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<TrayIconUpdatedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<TrayIconRemovedEvent>(e => _capturedEvents.Add(e));
        _eventPublisher.Subscribe<TrayIconClickedEvent>(e => _capturedEvents.Add(e));

        _shellCore = new Shell.Core.ShellCore(_windowSystem, _processLauncher, _trayHost, _hotkeyRegistry, _systemEventHandler, _eventPublisher);
    }

    [Fact]
    public void TrayIconAdded_UpdatesStateAndEmitsEvent()
    {
        // Arrange
        var trayIcon = new TrayIcon
        {
            Id = "test-icon-1",
            ProcessId = 1234,
            Tooltip = "Test Application",
            IsVisible = true
        };

        // Act
        _trayHost.SimulateTrayIconAdded(trayIcon);

        // Assert
        var state = _shellCore.GetState();
        Assert.True(state.TrayIcons.ContainsKey("test-icon-1"));
        Assert.Equal("Test Application", state.TrayIcons["test-icon-1"].Tooltip);
        Assert.Equal(1234, state.TrayIcons["test-icon-1"].ProcessId);

        // Check that event was emitted
        var addedEvent = _capturedEvents.OfType<TrayIconAddedEvent>().FirstOrDefault();
        Assert.NotNull(addedEvent);
        Assert.Equal("test-icon-1", addedEvent.TrayIcon.Id);
        Assert.Equal("Test Application", addedEvent.TrayIcon.Tooltip);
    }

    [Fact]
    public void TrayIconUpdated_UpdatesStateAndEmitsEvent()
    {
        // Arrange
        var originalIcon = new TrayIcon
        {
            Id = "test-icon-1",
            ProcessId = 1234,
            Tooltip = "Original Tooltip",
            IsVisible = true
        };
        _trayHost.SimulateTrayIconAdded(originalIcon);
        _capturedEvents.Clear();

        var updatedIcon = new TrayIcon
        {
            Id = "test-icon-1",
            ProcessId = 1234,
            Tooltip = "Updated Tooltip",
            IsVisible = true
        };

        // Act
        _trayHost.SimulateTrayIconUpdated(updatedIcon);

        // Assert
        var state = _shellCore.GetState();
        Assert.Equal("Updated Tooltip", state.TrayIcons["test-icon-1"].Tooltip);

        // Check that event was emitted
        var updatedEvent = _capturedEvents.OfType<TrayIconUpdatedEvent>().FirstOrDefault();
        Assert.NotNull(updatedEvent);
        Assert.Equal("test-icon-1", updatedEvent.TrayIcon.Id);
        Assert.Equal("Updated Tooltip", updatedEvent.TrayIcon.Tooltip);
    }

    [Fact]
    public void TrayIconUpdated_NonExistentIcon_DoesNothing()
    {
        // Arrange
        var nonExistentIcon = new TrayIcon
        {
            Id = "non-existent",
            ProcessId = 1234,
            Tooltip = "Should not be added",
            IsVisible = true
        };

        // Act
        _trayHost.SimulateTrayIconUpdated(nonExistentIcon);

        // Assert
        var state = _shellCore.GetState();
        Assert.False(state.TrayIcons.ContainsKey("non-existent"));

        // No event should be emitted
        Assert.Empty(_capturedEvents.OfType<TrayIconUpdatedEvent>());
    }

    [Fact]
    public void TrayIconRemoved_RemovesFromStateAndEmitsEvent()
    {
        // Arrange
        var trayIcon = new TrayIcon
        {
            Id = "test-icon-1",
            ProcessId = 1234,
            Tooltip = "Test Application",
            IsVisible = true
        };
        _trayHost.SimulateTrayIconAdded(trayIcon);
        _capturedEvents.Clear();

        // Act
        _trayHost.SimulateTrayIconRemoved("test-icon-1");

        // Assert
        var state = _shellCore.GetState();
        Assert.False(state.TrayIcons.ContainsKey("test-icon-1"));

        // Check that event was emitted
        var removedEvent = _capturedEvents.OfType<TrayIconRemovedEvent>().FirstOrDefault();
        Assert.NotNull(removedEvent);
        Assert.Equal("test-icon-1", removedEvent.TrayIconId);
        Assert.Equal(1234, removedEvent.ProcessId);
    }

    [Fact]
    public void TrayIconRemoved_NonExistentIcon_DoesNothing()
    {
        // Act
        _trayHost.SimulateTrayIconRemoved("non-existent");

        // Assert
        // No event should be emitted
        Assert.Empty(_capturedEvents.OfType<TrayIconRemovedEvent>());
    }

    [Fact]
    public void TrayIconClicked_EmitsEvent()
    {
        // Arrange
        var trayIcon = new TrayIcon
        {
            Id = "test-icon-1",
            ProcessId = 1234,
            Tooltip = "Test Application",
            IsVisible = true
        };
        _trayHost.SimulateTrayIconAdded(trayIcon);
        _capturedEvents.Clear();

        // Act
        _trayHost.SimulateTrayIconClicked("test-icon-1", TrayClickType.LeftClick);

        // Assert
        var clickedEvent = _capturedEvents.OfType<TrayIconClickedEvent>().FirstOrDefault();
        Assert.NotNull(clickedEvent);
        Assert.Equal("test-icon-1", clickedEvent.TrayIconId);
        Assert.Equal(TrayClickType.LeftClick, clickedEvent.ClickType);
    }

    [Fact]
    public void MultipleTrayIcons_ManagedCorrectly()
    {
        // Arrange
        var icon1 = new TrayIcon
        {
            Id = "icon-1",
            ProcessId = 1001,
            Tooltip = "Application 1",
            IsVisible = true
        };

        var icon2 = new TrayIcon
        {
            Id = "icon-2",
            ProcessId = 1002,
            Tooltip = "Application 2",
            IsVisible = true
        };

        var icon3 = new TrayIcon
        {
            Id = "icon-3",
            ProcessId = 1003,
            Tooltip = "Application 3",
            IsVisible = false
        };

        // Act
        _trayHost.SimulateTrayIconAdded(icon1);
        _trayHost.SimulateTrayIconAdded(icon2);
        _trayHost.SimulateTrayIconAdded(icon3);

        // Assert
        var state = _shellCore.GetState();
        Assert.Equal(3, state.TrayIcons.Count);
        Assert.True(state.TrayIcons.ContainsKey("icon-1"));
        Assert.True(state.TrayIcons.ContainsKey("icon-2"));
        Assert.True(state.TrayIcons.ContainsKey("icon-3"));

        // Check visibility states
        Assert.True(state.TrayIcons["icon-1"].IsVisible);
        Assert.True(state.TrayIcons["icon-2"].IsVisible);
        Assert.False(state.TrayIcons["icon-3"].IsVisible);

        // Remove one icon
        _trayHost.SimulateTrayIconRemoved("icon-2");
        state = _shellCore.GetState();
        Assert.Equal(2, state.TrayIcons.Count);
        Assert.False(state.TrayIcons.ContainsKey("icon-2"));
    }

    [Fact]
    public void InitializeWithExistingTrayIcons_LoadsCorrectly()
    {
        // This test verifies that existing tray icons are loaded during initialization
        // We need to create a new ShellCore instance with pre-existing tray icons

        // Arrange
        var existingIcon = new TrayIcon
        {
            Id = "existing-icon",
            ProcessId = 5555,
            Tooltip = "Existing Application",
            IsVisible = true
        };

        var trayHostWithExisting = new MockTrayHost();
        trayHostWithExisting.SimulateTrayIconAdded(existingIcon); // Add before ShellCore initialization

        // Act - Create new ShellCore instance
        var newShellCore = new Shell.Core.ShellCore(_windowSystem, _processLauncher, trayHostWithExisting, _hotkeyRegistry, _systemEventHandler, _eventPublisher);

        // Assert
        var state = newShellCore.GetState();
        Assert.True(state.TrayIcons.ContainsKey("existing-icon"));
        Assert.Equal("Existing Application", state.TrayIcons["existing-icon"].Tooltip);
        Assert.Equal(5555, state.TrayIcons["existing-icon"].ProcessId);

        newShellCore.Dispose();
    }
}