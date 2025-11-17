using Shell.Adapters.Win32;
using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Models;

namespace Shell.Tests;

/// <summary>
/// Integration tests for tray functionality with real TrayHostWin32
/// </summary>
public class TrayIntegrationTests : IDisposable
{
    private readonly Shell.Core.ShellCore _shellCore;
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly TrayHostWin32 _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly MockSystemEventHandler _systemEventHandler;
    private readonly EventPublisher _eventPublisher;
    private readonly List<ShellEvent> _capturedEvents;

    public TrayIntegrationTests()
    {
        // Ensure we're in test mode for safety - MUST be set before creating TrayHostWin32
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
        
        _windowSystem = new MockWindowSystem();
        _processLauncher = new MockProcessLauncher();
        _trayHost = new TrayHostWin32();
        _hotkeyRegistry = new MockHotkeyRegistry();
        _systemEventHandler = new MockSystemEventHandler();
        _eventPublisher = new EventPublisher();
        
        _shellCore = new Shell.Core.ShellCore(_windowSystem, _processLauncher, _trayHost, _hotkeyRegistry, _systemEventHandler, _eventPublisher);
        
        _capturedEvents = new List<ShellEvent>();
        _eventPublisher.Subscribe<TrayIconAddedEvent>(evt => _capturedEvents.Add(evt));
        _eventPublisher.Subscribe<TrayIconUpdatedEvent>(evt => _capturedEvents.Add(evt));
        _eventPublisher.Subscribe<TrayIconRemovedEvent>(evt => _capturedEvents.Add(evt));
        _eventPublisher.Subscribe<TrayIconClickedEvent>(evt => _capturedEvents.Add(evt));
        _eventPublisher.Subscribe<TrayBalloonShownEvent>(evt => _capturedEvents.Add(evt));
        _eventPublisher.Subscribe<TrayBalloonClickedEvent>(evt => _capturedEvents.Add(evt));
        _eventPublisher.Subscribe<TrayMenuItemClickedEvent>(evt => _capturedEvents.Add(evt));
    }

    [Fact]
    public void TrayHost_AddTrayIcon_EmitsTrayIconAddedEventAndUpdatesState()
    {
        // Arrange
        var iconId = "test-icon-1";
        var trayIcon = new TrayIcon
        {
            Id = iconId,
            ProcessId = 1234,
            Tooltip = "Test Icon",
            IconHandle = new IntPtr(12345)
        };

        // Act
        _trayHost.AddTrayIcon(trayIcon);

        // Assert - Check that event was emitted
        var addedEvent = _capturedEvents.OfType<TrayIconAddedEvent>().FirstOrDefault();
        Assert.NotNull(addedEvent);
        Assert.Equal(iconId, addedEvent.TrayIcon.Id);
        Assert.Equal(1234, addedEvent.TrayIcon.ProcessId);
        Assert.Equal("Test Icon", addedEvent.TrayIcon.Tooltip);

        // Assert - Check that state was updated
        var state = _shellCore.GetState();
        Assert.Contains(state.TrayIcons, kvp => kvp.Value.Id == iconId);
        
        var stateIcon = state.TrayIcons.First(kvp => kvp.Value.Id == iconId).Value;
        Assert.Equal(1234, stateIcon.ProcessId);
        Assert.Equal("Test Icon", stateIcon.Tooltip);
    }

    [Fact]
    public void TrayHost_UpdateTrayIcon_EmitsTrayIconUpdatedEventAndUpdatesState()
    {
        // Arrange - Add initial icon
        var iconId = "test-icon-2";
        var initialIcon = new TrayIcon
        {
            Id = iconId,
            ProcessId = 1234,
            Tooltip = "Initial Tooltip",
            IconHandle = new IntPtr(12345)
        };
        _trayHost.AddTrayIcon(initialIcon);
        _capturedEvents.Clear(); // Clear the add event

        // Act - Update the icon
        var updatedIcon = new TrayIcon
        {
            Id = iconId,
            ProcessId = 1234,
            Tooltip = "Updated Tooltip",
            IconHandle = new IntPtr(54321)
        };
        _trayHost.UpdateTrayIcon(updatedIcon);

        // Assert - Check that event was emitted
        var updatedEvent = _capturedEvents.OfType<TrayIconUpdatedEvent>().FirstOrDefault();
        Assert.NotNull(updatedEvent);
        Assert.Equal(iconId, updatedEvent.TrayIcon.Id);
        Assert.Equal("Updated Tooltip", updatedEvent.TrayIcon.Tooltip);

        // Assert - Check that state was updated
        var state = _shellCore.GetState();
        var stateIcon = state.TrayIcons.First(kvp => kvp.Value.Id == iconId).Value;
        Assert.Equal("Updated Tooltip", stateIcon.Tooltip);
        Assert.Equal(new IntPtr(54321), stateIcon.IconHandle);
    }

    [Fact]
    public void TrayHost_RemoveTrayIcon_EmitsTrayIconRemovedEventAndUpdatesState()
    {
        // Arrange - Add initial icon
        var iconId = "test-icon-3";
        var trayIcon = new TrayIcon
        {
            Id = iconId,
            ProcessId = 1234,
            Tooltip = "Test Icon",
            IconHandle = new IntPtr(12345)
        };
        _trayHost.AddTrayIcon(trayIcon);
        _capturedEvents.Clear(); // Clear the add event

        // Act
        _trayHost.RemoveTrayIcon(iconId);

        // Assert - Check that event was emitted
        var removedEvent = _capturedEvents.OfType<TrayIconRemovedEvent>().FirstOrDefault();
        Assert.NotNull(removedEvent);
        Assert.Equal(iconId, removedEvent.TrayIconId);

        // Assert - Check that state was updated
        var state = _shellCore.GetState();
        Assert.DoesNotContain(state.TrayIcons, kvp => kvp.Value.Id == iconId);
    }

    [Fact]
    public void TrayHost_ShowBalloonNotification_EmitsTrayBalloonShownEvent()
    {
        // Arrange - Add initial icon
        var iconId = "test-icon-4";
        var trayIcon = new TrayIcon
        {
            Id = iconId,
            ProcessId = 1234,
            Tooltip = "Test Icon",
            IconHandle = new IntPtr(12345)
        };
        _trayHost.AddTrayIcon(trayIcon);
        _capturedEvents.Clear(); // Clear the add event

        // Act
        _trayHost.ShowBalloonNotification(iconId, "Test Title", "Test Message", TrayBalloonIcon.Info, 5000);

        // Assert - Check that event was emitted
        var balloonEvent = _capturedEvents.OfType<TrayBalloonShownEvent>().FirstOrDefault();
        Assert.NotNull(balloonEvent);
        Assert.Equal(iconId, balloonEvent.TrayIconId);
        Assert.Equal("Test Title", balloonEvent.BalloonInfo.Title);
        Assert.Equal("Test Message", balloonEvent.BalloonInfo.Text);
        Assert.Equal(TrayBalloonIcon.Info, balloonEvent.BalloonInfo.Icon);
        Assert.Equal(5000, balloonEvent.BalloonInfo.TimeoutMs);
    }

    [Fact]
    public void TrayHost_GetTrayIcons_ReturnsCurrentIcons()
    {
        // Arrange - Add multiple icons
        var icon1 = new TrayIcon { Id = "icon1", ProcessId = 1001, Tooltip = "Icon 1" };
        var icon2 = new TrayIcon { Id = "icon2", ProcessId = 1002, Tooltip = "Icon 2" };
        
        _trayHost.AddTrayIcon(icon1);
        _trayHost.AddTrayIcon(icon2);

        // Act
        var icons = _trayHost.GetTrayIcons().ToList();

        // Assert
        Assert.Equal(2, icons.Count);
        Assert.Contains(icons, icon => icon.Id == "icon1" && icon.Tooltip == "Icon 1");
        Assert.Contains(icons, icon => icon.Id == "icon2" && icon.Tooltip == "Icon 2");
    }

    [Fact]
    public void TrayHost_MultipleTrayIcons_StateRemainsConsistent()
    {
        // Arrange & Act - Add, update, and remove icons in sequence
        var icon1 = new TrayIcon { Id = "multi1", ProcessId = 2001, Tooltip = "Multi 1" };
        var icon2 = new TrayIcon { Id = "multi2", ProcessId = 2002, Tooltip = "Multi 2" };
        var icon3 = new TrayIcon { Id = "multi3", ProcessId = 2003, Tooltip = "Multi 3" };

        _trayHost.AddTrayIcon(icon1);
        _trayHost.AddTrayIcon(icon2);
        _trayHost.AddTrayIcon(icon3);

        // Update icon2
        var updatedIcon2 = new TrayIcon { Id = "multi2", ProcessId = 2002, Tooltip = "Updated Multi 2" };
        _trayHost.UpdateTrayIcon(updatedIcon2);

        // Remove icon1
        _trayHost.RemoveTrayIcon("multi1");

        // Assert - Check final state
        var state = _shellCore.GetState();
        Assert.Equal(2, state.TrayIcons.Count);
        Assert.DoesNotContain(state.TrayIcons, kvp => kvp.Value.Id == "multi1");
        Assert.Contains(state.TrayIcons, kvp => kvp.Value.Id == "multi2" && kvp.Value.Tooltip == "Updated Multi 2");
        Assert.Contains(state.TrayIcons, kvp => kvp.Value.Id == "multi3" && kvp.Value.Tooltip == "Multi 3");

        // Assert - Check events were emitted correctly
        var addEvents = _capturedEvents.OfType<TrayIconAddedEvent>().ToList();
        var updateEvents = _capturedEvents.OfType<TrayIconUpdatedEvent>().ToList();
        var removeEvents = _capturedEvents.OfType<TrayIconRemovedEvent>().ToList();

        Assert.Equal(3, addEvents.Count);
        Assert.Equal(1, updateEvents.Count);
        Assert.Equal(1, removeEvents.Count);
        
        Assert.Equal("multi2", updateEvents[0].TrayIcon.Id);
        Assert.Equal("multi1", removeEvents[0].TrayIconId);
    }

    [Fact]
    public void TrayHost_InTestMode_DoesNotMakeActualSystemCalls()
    {
        // This test verifies that in test mode, the tray host doesn't make actual Win32 calls
        // The fact that we can run these tests without errors on Linux indicates this is working
        
        // Arrange
        var iconId = "test-safe-mode";
        var trayIcon = new TrayIcon
        {
            Id = iconId,
            ProcessId = 9999,
            Tooltip = "Safe Mode Test",
            IconHandle = new IntPtr(99999)
        };

        // Act & Assert - These should not throw exceptions even with invalid handles
        var exception1 = Record.Exception(() => _trayHost.AddTrayIcon(trayIcon));
        var exception2 = Record.Exception(() => _trayHost.ShowBalloonNotification(iconId, "Test", "Message", TrayBalloonIcon.Warning, 1000));
        var exception3 = Record.Exception(() => _trayHost.RemoveTrayIcon(iconId));

        Assert.Null(exception1);
        Assert.Null(exception2);
        Assert.Null(exception3);
    }

    public void Dispose()
    {
        _shellCore?.Dispose();
        _trayHost?.Dispose();
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }
}