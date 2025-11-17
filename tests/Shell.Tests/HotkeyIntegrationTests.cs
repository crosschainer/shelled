using Shell.Adapters.Win32;
using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Interfaces;
using Shell.Core.Models;
using Xunit;

namespace Shell.Tests;

/// <summary>
/// Integration tests for hotkey functionality with Win32 adapter
/// </summary>
public class HotkeyIntegrationTests
{
    [Fact]
    public void HotkeyRegistryWin32_ShouldImplementInterface()
    {
        // Arrange & Act
        var hotkeyRegistry = new HotkeyRegistryWin32();

        // Assert
        Assert.IsAssignableFrom<IHotkeyRegistry>(hotkeyRegistry);
    }

    [Fact]
    public void HotkeyRegistryWin32_RegisterHotkey_ShouldReturnTrue_InTestMode()
    {
        // Arrange
        var hotkeyRegistry = new HotkeyRegistryWin32();

        // Act
        var result = hotkeyRegistry.RegisterHotkey("test-hotkey", (int)HotkeyModifiers.Win, 0x20); // Win + Space

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HotkeyRegistryWin32_UnregisterHotkey_ShouldReturnTrue_InTestMode()
    {
        // Arrange
        var hotkeyRegistry = new HotkeyRegistryWin32();
        hotkeyRegistry.RegisterHotkey("test-hotkey", (int)HotkeyModifiers.Win, 0x20);

        // Act
        var result = hotkeyRegistry.UnregisterHotkey("test-hotkey");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HotkeyRegistryWin32_RegisterSameHotkeyTwice_ShouldReturnFalseSecondTime()
    {
        // Arrange
        var hotkeyRegistry = new HotkeyRegistryWin32();

        // Act
        var firstResult = hotkeyRegistry.RegisterHotkey("test-hotkey", (int)HotkeyModifiers.Win, 0x20);
        var secondResult = hotkeyRegistry.RegisterHotkey("test-hotkey", (int)HotkeyModifiers.Win, 0x20);

        // Assert
        Assert.True(firstResult);
        Assert.False(secondResult); // Should fail because already registered
    }

    [Fact]
    public void HotkeyRegistryWin32_UnregisterNonExistentHotkey_ShouldReturnFalse()
    {
        // Arrange
        var hotkeyRegistry = new HotkeyRegistryWin32();

        // Act
        var result = hotkeyRegistry.UnregisterHotkey("non-existent-hotkey");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HotkeyRegistryWin32_RegisterMultipleHotkeys_ShouldWork()
    {
        // Arrange
        var hotkeyRegistry = new HotkeyRegistryWin32();

        // Act & Assert
        Assert.True(hotkeyRegistry.RegisterHotkey("hotkey1", (int)HotkeyModifiers.Win, 0x20)); // Win + Space
        Assert.True(hotkeyRegistry.RegisterHotkey("hotkey2", (int)HotkeyModifiers.Alt, 0x73)); // Alt + F4
        Assert.True(hotkeyRegistry.RegisterHotkey("hotkey3", (int)(HotkeyModifiers.Control | HotkeyModifiers.Shift), 0x1B)); // Ctrl + Shift + Esc
    }

    [Fact]
    public void HotkeyRegistryWin32_RegisterAndUnregisterMultipleHotkeys_ShouldWork()
    {
        // Arrange
        var hotkeyRegistry = new HotkeyRegistryWin32();

        // Act - Register multiple hotkeys
        Assert.True(hotkeyRegistry.RegisterHotkey("hotkey1", (int)HotkeyModifiers.Win, 0x20));
        Assert.True(hotkeyRegistry.RegisterHotkey("hotkey2", (int)HotkeyModifiers.Alt, 0x73));
        Assert.True(hotkeyRegistry.RegisterHotkey("hotkey3", (int)(HotkeyModifiers.Control | HotkeyModifiers.Shift), 0x1B));

        // Act - Unregister them
        Assert.True(hotkeyRegistry.UnregisterHotkey("hotkey1"));
        Assert.True(hotkeyRegistry.UnregisterHotkey("hotkey2"));
        Assert.True(hotkeyRegistry.UnregisterHotkey("hotkey3"));

        // Act - Try to unregister again (should fail)
        Assert.False(hotkeyRegistry.UnregisterHotkey("hotkey1"));
        Assert.False(hotkeyRegistry.UnregisterHotkey("hotkey2"));
        Assert.False(hotkeyRegistry.UnregisterHotkey("hotkey3"));
    }

    [Fact]
    public void ShellCore_WithHotkeyRegistryWin32_ShouldIntegrateCorrectly()
    {
        // Arrange
        var windowSystem = new MockWindowSystem();
        var processLauncher = new MockProcessLauncher();
        var trayHost = new MockTrayHost();
        var hotkeyRegistry = new HotkeyRegistryWin32(); // Use real Win32 implementation
        var systemEventHandler = new MockSystemEventHandler();
        var eventPublisher = new EventPublisher();
        var capturedEvents = new List<ShellEvent>();
        eventPublisher.Subscribe<HotkeyPressedEvent>(e => capturedEvents.Add(e));

        var shellCore = new ShellCore(windowSystem, processLauncher, trayHost, hotkeyRegistry, systemEventHandler, eventPublisher);

        // Act
        var registerResult = shellCore.RegisterHotkey("shell-launcher", HotkeyModifiers.Win, 0x20); // Win + Space
        var unregisterResult = shellCore.UnregisterHotkey("shell-launcher");

        // Assert
        Assert.True(registerResult);
        Assert.True(unregisterResult);

        shellCore.Dispose();
    }

    [Fact]
    public void HotkeyRegistryWin32_WithInvalidParameters_ShouldHandleGracefully()
    {
        // Arrange
        var hotkeyRegistry = new HotkeyRegistryWin32();

        // Act & Assert - Empty/null ID should be handled by ShellCore validation
        // But the registry itself should handle other edge cases gracefully
        Assert.True(hotkeyRegistry.RegisterHotkey("valid-id", 0, 0x20)); // No modifiers
        Assert.True(hotkeyRegistry.RegisterHotkey("another-valid-id", (int)HotkeyModifiers.Win, 0)); // No virtual key (edge case)
    }

    [Fact]
    public void HotkeyRegistryWin32_Dispose_ShouldCleanupResources()
    {
        // Arrange
        var hotkeyRegistry = new HotkeyRegistryWin32();
        hotkeyRegistry.RegisterHotkey("test-hotkey", (int)HotkeyModifiers.Win, 0x20);

        // Act
        hotkeyRegistry.Dispose();

        // Assert - After disposal, operations should not crash
        // In test mode, this should work fine since we're not actually using Win32 APIs
        var result = hotkeyRegistry.RegisterHotkey("after-dispose", (int)HotkeyModifiers.Alt, 0x73);
        Assert.True(result); // Should still work in test mode
    }

    [Fact]
    public void HotkeyModifiers_EnumValues_ShouldMatchWin32Constants()
    {
        // Assert - Verify enum values match Win32 MOD_* constants
        Assert.Equal(0x0000, (int)HotkeyModifiers.None);
        Assert.Equal(0x0001, (int)HotkeyModifiers.Alt);     // MOD_ALT
        Assert.Equal(0x0002, (int)HotkeyModifiers.Control); // MOD_CONTROL
        Assert.Equal(0x0004, (int)HotkeyModifiers.Shift);   // MOD_SHIFT
        Assert.Equal(0x0008, (int)HotkeyModifiers.Win);     // MOD_WIN
    }

    [Fact]
    public void HotkeyPressedEvent_ShouldContainCorrectProperties()
    {
        // Arrange
        var hotkeyId = "test-hotkey";
        var modifiers = (int)(HotkeyModifiers.Win | HotkeyModifiers.Shift);
        var virtualKey = 0x20; // Space

        // Act
        var hotkeyEvent = new HotkeyPressedEvent(hotkeyId, modifiers, virtualKey);

        // Assert
        Assert.Equal(hotkeyId, hotkeyEvent.HotkeyId);
        Assert.Equal(modifiers, hotkeyEvent.Modifiers);
        Assert.Equal(virtualKey, hotkeyEvent.VirtualKey);
        Assert.True(hotkeyEvent.Timestamp <= DateTime.UtcNow);
        Assert.True(hotkeyEvent.Timestamp > DateTime.UtcNow.AddSeconds(-1)); // Should be recent
    }
}