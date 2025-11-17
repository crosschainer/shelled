using Shell.Adapters.Win32;
using Shell.Core;

namespace Shell.Tests;

/// <summary>
/// Tests for WindowSystemWin32 adapter
/// </summary>
public class WindowSystemWin32Tests : IDisposable
{
    private readonly WindowSystemWin32 _windowSystem;

    public WindowSystemWin32Tests()
    {
        // Ensure we're in test mode for safety
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
        _windowSystem = new WindowSystemWin32();
    }

    [Fact]
    public void Constructor_InTestMode_DoesNotSetupHooks()
    {
        // Arrange & Act - constructor already called
        // Assert - no exceptions should be thrown
        Assert.True(ShellConfiguration.IsTestMode);
        Assert.True(ShellConfiguration.DisableDangerousOperations);
    }

    [Fact]
    public void EnumWindows_OnLinux_ReturnsEmptyCollection()
    {
        // Act
        var windows = _windowSystem.EnumWindows();

        // Assert
        Assert.NotNull(windows);
        Assert.Empty(windows); // Should be empty on Linux
    }

    [Fact]
    public void IsTopLevelWindow_OnLinux_ReturnsFalse()
    {
        // Arrange
        var fakeHandle = new IntPtr(12345);

        // Act
        var result = _windowSystem.IsTopLevelWindow(fakeHandle);

        // Assert
        Assert.False(result); // Should always be false on Linux
    }

    [Fact]
    public void GetWindowInfo_OnLinux_ReturnsNull()
    {
        // Arrange
        var fakeHandle = new IntPtr(12345);

        // Act
        var result = _windowSystem.GetWindowInfo(fakeHandle);

        // Assert
        Assert.Null(result); // Should be null on Linux
    }

    [Fact]
    public void ShowWindow_InTestMode_DoesNotThrow()
    {
        // Arrange
        var fakeHandle = new IntPtr(12345);

        // Act & Assert - should not throw
        _windowSystem.ShowWindow(fakeHandle, Shell.Core.Models.WindowState.Normal);
        _windowSystem.ShowWindow(fakeHandle, Shell.Core.Models.WindowState.Hidden);
        _windowSystem.ShowWindow(fakeHandle, Shell.Core.Models.WindowState.Minimized);
        _windowSystem.ShowWindow(fakeHandle, Shell.Core.Models.WindowState.Maximized);
    }

    [Fact]
    public void SetForegroundWindow_InTestMode_DoesNotThrow()
    {
        // Arrange
        var fakeHandle = new IntPtr(12345);

        // Act & Assert - should not throw
        _windowSystem.SetForegroundWindow(fakeHandle);
    }

    [Fact]
    public void IsVisible_OnLinux_ReturnsFalse()
    {
        // Arrange
        var fakeHandle = new IntPtr(12345);

        // Act
        var result = _windowSystem.IsVisible(fakeHandle);

        // Assert
        Assert.False(result); // Should be false on Linux
    }

    [Fact]
    public void Events_CanBeSubscribedTo()
    {
        // Arrange
        var windowCreatedFired = false;
        var windowDestroyedFired = false;
        var windowActivatedFired = false;
        var windowUpdatedFired = false;

        // Act
        _windowSystem.WindowCreated += (hwnd) => windowCreatedFired = true;
        _windowSystem.WindowDestroyed += (hwnd) => windowDestroyedFired = true;
        _windowSystem.WindowActivated += (hwnd) => windowActivatedFired = true;
        _windowSystem.WindowUpdated += (hwnd) => windowUpdatedFired = true;

        // Assert - events should be subscribable without throwing
        Assert.False(windowCreatedFired);
        Assert.False(windowDestroyedFired);
        Assert.False(windowActivatedFired);
        Assert.False(windowUpdatedFired);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Act & Assert - should not throw
        _windowSystem.Dispose();
        
        // Should be safe to call multiple times
        _windowSystem.Dispose();
    }

    public void Dispose()
    {
        _windowSystem?.Dispose();
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }
}