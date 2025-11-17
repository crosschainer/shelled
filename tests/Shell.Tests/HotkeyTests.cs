using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Interfaces;
using Shell.Core.Models;
using Xunit;

namespace Shell.Tests;

/// <summary>
/// Unit tests for hotkey functionality
/// </summary>
public class HotkeyTests
{
    private ShellCore CreateTestShellCore()
    {
        var windowSystem = new MockWindowSystem();
        var processLauncher = new MockProcessLauncher();
        var trayHost = new MockTrayHost();
        var hotkeyRegistry = new MockHotkeyRegistry();
        var systemEventHandler = new MockSystemEventHandler();
        var eventPublisher = new EventPublisher();

        return new ShellCore(windowSystem, processLauncher, trayHost, hotkeyRegistry, systemEventHandler, eventPublisher);
    }

    [Fact]
    public void RegisterHotkey_ShouldReturnTrue_WhenSuccessful()
    {
        // Arrange
        var shellCore = CreateTestShellCore();

        // Act
        var result = shellCore.RegisterHotkey("test-hotkey", HotkeyModifiers.Win, 0x20); // Win + Space

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void UnregisterHotkey_ShouldReturnTrue_WhenSuccessful()
    {
        // Arrange
        var shellCore = CreateTestShellCore();
        shellCore.RegisterHotkey("test-hotkey", HotkeyModifiers.Win, 0x20);

        // Act
        var result = shellCore.UnregisterHotkey("test-hotkey");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HotkeyPressed_ShouldPublishEvent_WhenHotkeyIsTriggered()
    {
        // Arrange
        var windowSystem = new MockWindowSystem();
        var processLauncher = new MockProcessLauncher();
        var trayHost = new MockTrayHost();
        var hotkeyRegistry = new MockHotkeyRegistry();
        var systemEventHandler = new MockSystemEventHandler();
        var eventPublisher = new EventPublisher();
        var capturedEvents = new List<ShellEvent>();
        eventPublisher.Subscribe<HotkeyPressedEvent>(e => capturedEvents.Add(e));

        var shellCore = new ShellCore(windowSystem, processLauncher, trayHost, hotkeyRegistry, systemEventHandler, eventPublisher);

        // Act
        hotkeyRegistry.TriggerHotkey("test-hotkey");

        // Assert
        var hotkeyEvents = capturedEvents.OfType<HotkeyPressedEvent>().ToList();
        Assert.Single(hotkeyEvents);
        Assert.Equal("test-hotkey", hotkeyEvents[0].HotkeyId);
    }

    [Fact]
    public void RegisterHotkey_WithDifferentModifiers_ShouldWork()
    {
        // Arrange
        var shellCore = CreateTestShellCore();

        // Act & Assert
        Assert.True(shellCore.RegisterHotkey("alt-f4", HotkeyModifiers.Alt, 0x73)); // Alt + F4
        Assert.True(shellCore.RegisterHotkey("ctrl-shift-esc", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x1B)); // Ctrl + Shift + Esc
        Assert.True(shellCore.RegisterHotkey("win-r", HotkeyModifiers.Win, 0x52)); // Win + R
    }

    [Fact]
    public void RegisterHotkey_WithEmptyId_ShouldThrowArgumentException()
    {
        // Arrange
        var shellCore = CreateTestShellCore();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => shellCore.RegisterHotkey("", HotkeyModifiers.Win, 0x20));
        Assert.Throws<ArgumentException>(() => shellCore.RegisterHotkey(null!, HotkeyModifiers.Win, 0x20));
    }

    [Fact]
    public void HotkeyModifiers_ShouldHaveCorrectValues()
    {
        // Assert - verify the enum values match Win32 constants
        Assert.Equal(0x0000, (int)HotkeyModifiers.None);
        Assert.Equal(0x0001, (int)HotkeyModifiers.Alt);
        Assert.Equal(0x0002, (int)HotkeyModifiers.Control);
        Assert.Equal(0x0004, (int)HotkeyModifiers.Shift);
        Assert.Equal(0x0008, (int)HotkeyModifiers.Win);
    }

    [Fact]
    public void HotkeyModifiers_ShouldSupportFlags()
    {
        // Arrange & Act
        var combined = HotkeyModifiers.Control | HotkeyModifiers.Shift;

        // Assert
        Assert.Equal(0x0006, (int)combined); // 0x0002 | 0x0004
        Assert.True(combined.HasFlag(HotkeyModifiers.Control));
        Assert.True(combined.HasFlag(HotkeyModifiers.Shift));
        Assert.False(combined.HasFlag(HotkeyModifiers.Alt));
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromHotkeyEvents()
    {
        // Arrange
        var windowSystem = new MockWindowSystem();
        var processLauncher = new MockProcessLauncher();
        var trayHost = new MockTrayHost();
        var hotkeyRegistry = new MockHotkeyRegistry();
        var systemEventHandler = new MockSystemEventHandler();
        var eventPublisher = new EventPublisher();
        var capturedEvents = new List<ShellEvent>();
        eventPublisher.Subscribe<HotkeyPressedEvent>(e => capturedEvents.Add(e));

        var shellCore = new ShellCore(windowSystem, processLauncher, trayHost, hotkeyRegistry, systemEventHandler, eventPublisher);

        // Act
        shellCore.Dispose();

        // Trigger hotkey after disposal - should not publish events
        hotkeyRegistry.TriggerHotkey("test-hotkey");

        // Assert
        var hotkeyEvents = capturedEvents.OfType<HotkeyPressedEvent>().ToList();
        Assert.Empty(hotkeyEvents);
    }
}