using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Interfaces;

namespace Shell.Tests;

/// <summary>
/// Tests for ShellCore system event handling
/// </summary>
public class ShellCoreSystemEventTests
{
    private readonly MockWindowSystem _windowSystem;
    private readonly MockProcessLauncher _processLauncher;
    private readonly MockTrayHost _trayHost;
    private readonly MockHotkeyRegistry _hotkeyRegistry;
    private readonly MockSystemEventHandler _systemEventHandler;
    private readonly EventPublisher _eventPublisher;
    private readonly Shell.Core.ShellCore _shellCore;
    private readonly List<ShellEvent> _capturedEvents;

    public ShellCoreSystemEventTests()
    {
        _windowSystem = new MockWindowSystem();
        _processLauncher = new MockProcessLauncher();
        _trayHost = new MockTrayHost();
        _hotkeyRegistry = new MockHotkeyRegistry();
        _systemEventHandler = new MockSystemEventHandler();
        _eventPublisher = new EventPublisher();
        _capturedEvents = new List<ShellEvent>();

        // Subscribe to system events for testing
        _eventPublisher.Subscribe<SystemEvent>(e => _capturedEvents.Add(e));

        _shellCore = new Shell.Core.ShellCore(_windowSystem, _processLauncher, _trayHost, _hotkeyRegistry, _systemEventHandler, _eventPublisher);
    }

    [Fact]
    public void QueryEndSession_ForwardsEventToSubscribers()
    {
        // Arrange
        var eventArgs = new SystemEventArgs
        {
            CanCancel = true,
            Reason = "User initiated shutdown"
        };

        // Act
        _systemEventHandler.TriggerSystemEvent(SystemEventType.QueryEndSession, eventArgs);

        // Assert
        var systemEvent = _capturedEvents.OfType<SystemEvent>().FirstOrDefault();
        Assert.NotNull(systemEvent);
        Assert.Equal(SystemEventType.QueryEndSession, systemEvent.EventType);
        Assert.Equal("User initiated shutdown", systemEvent.EventArgs.Reason);
        Assert.True(systemEvent.EventArgs.CanCancel);
    }

    [Fact]
    public void EndSession_ForwardsEventToSubscribers()
    {
        // Arrange
        var eventArgs = new SystemEventArgs
        {
            Reason = "System shutdown"
        };

        // Act
        _systemEventHandler.TriggerSystemEvent(SystemEventType.EndSession, eventArgs);

        // Assert
        var systemEvent = _capturedEvents.OfType<SystemEvent>().FirstOrDefault();
        Assert.NotNull(systemEvent);
        Assert.Equal(SystemEventType.EndSession, systemEvent.EventType);
        Assert.Equal("System shutdown", systemEvent.EventArgs.Reason);
    }

    [Fact]
    public void PowerSuspend_ForwardsEventToSubscribers()
    {
        // Act
        _systemEventHandler.TriggerSystemEvent(SystemEventType.PowerSuspend);

        // Assert
        var systemEvent = _capturedEvents.OfType<SystemEvent>().FirstOrDefault();
        Assert.NotNull(systemEvent);
        Assert.Equal(SystemEventType.PowerSuspend, systemEvent.EventType);
    }

    [Fact]
    public void PowerResume_ForwardsEventToSubscribers()
    {
        // Act
        _systemEventHandler.TriggerSystemEvent(SystemEventType.PowerResume);

        // Assert
        var systemEvent = _capturedEvents.OfType<SystemEvent>().FirstOrDefault();
        Assert.NotNull(systemEvent);
        Assert.Equal(SystemEventType.PowerResume, systemEvent.EventType);
    }

    [Fact]
    public void DisplaySettingsChanged_ForwardsEventToSubscribers()
    {
        // Act
        _systemEventHandler.TriggerSystemEvent(SystemEventType.DisplaySettingsChanged);

        // Assert
        var systemEvent = _capturedEvents.OfType<SystemEvent>().FirstOrDefault();
        Assert.NotNull(systemEvent);
        Assert.Equal(SystemEventType.DisplaySettingsChanged, systemEvent.EventType);
    }

    [Fact]
    public void SessionLock_ForwardsEventToSubscribers()
    {
        // Act
        _systemEventHandler.TriggerSystemEvent(SystemEventType.SessionLock);

        // Assert
        var systemEvent = _capturedEvents.OfType<SystemEvent>().FirstOrDefault();
        Assert.NotNull(systemEvent);
        Assert.Equal(SystemEventType.SessionLock, systemEvent.EventType);
    }

    [Fact]
    public void SessionUnlock_ForwardsEventToSubscribers()
    {
        // Act
        _systemEventHandler.TriggerSystemEvent(SystemEventType.SessionUnlock);

        // Assert
        var systemEvent = _capturedEvents.OfType<SystemEvent>().FirstOrDefault();
        Assert.NotNull(systemEvent);
        Assert.Equal(SystemEventType.SessionUnlock, systemEvent.EventType);
    }

    [Fact]
    public void MultipleSystemEvents_AllForwardedCorrectly()
    {
        // Act
        _systemEventHandler.TriggerSystemEvent(SystemEventType.PowerSuspend);
        _systemEventHandler.TriggerSystemEvent(SystemEventType.PowerResume);
        _systemEventHandler.TriggerSystemEvent(SystemEventType.SessionLock);

        // Assert
        var systemEvents = _capturedEvents.OfType<SystemEvent>().ToList();
        Assert.Equal(3, systemEvents.Count);
        
        Assert.Equal(SystemEventType.PowerSuspend, systemEvents[0].EventType);
        Assert.Equal(SystemEventType.PowerResume, systemEvents[1].EventType);
        Assert.Equal(SystemEventType.SessionLock, systemEvents[2].EventType);
    }

    [Fact]
    public void SystemEventHandler_StartStopListening_WorksCorrectly()
    {
        // Arrange
        Assert.False(_systemEventHandler.IsListening);

        // Act & Assert - Start listening
        _systemEventHandler.StartListening();
        Assert.True(_systemEventHandler.IsListening);

        // Act & Assert - Stop listening
        _systemEventHandler.StopListening();
        Assert.False(_systemEventHandler.IsListening);
    }
}