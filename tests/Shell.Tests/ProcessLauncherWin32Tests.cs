using Shell.Adapters.Win32;
using Shell.Core;

namespace Shell.Tests;

/// <summary>
/// Tests for ProcessLauncherWin32 adapter
/// </summary>
public class ProcessLauncherWin32Tests : IDisposable
{
    public ProcessLauncherWin32Tests()
    {
        // Ensure we're in test mode for safety
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
    }

    [Fact]
    public async Task LaunchAppAsync_InTestMode_ReturnsFakeProcessId()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
        var launcher = new ProcessLauncherWin32();
        
        // Verify test mode is active
        Assert.True(ShellConfiguration.IsTestMode, "Test mode should be active");
        Assert.True(ShellConfiguration.DisableDangerousOperations, "Dangerous operations should be disabled");

        // Act
        var processId = await launcher.LaunchAppAsync("notepad.exe");

        // Assert
        Assert.Equal(12345, processId); // Should return fake process ID in test mode
        
        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }

    [Fact]
    public async Task LaunchAppAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var launcher = new ProcessLauncherWin32();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => launcher.LaunchAppAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => launcher.LaunchAppAsync("   "));
        await Assert.ThrowsAsync<ArgumentException>(() => launcher.LaunchAppAsync(null!));
    }

    [Fact]
    public void GetRunningProcesses_ReturnsProcessList()
    {
        // Arrange
        var launcher = new ProcessLauncherWin32();

        // Act
        var processes = launcher.GetRunningProcesses();

        // Assert
        Assert.NotNull(processes);
        // Should have at least some processes (including the current test process)
        Assert.NotEmpty(processes);
        
        // Check that we have valid process information
        var processList = processes.ToList();
        Assert.All(processList, p => 
        {
            Assert.True(p.ProcessId > 0);
            Assert.NotNull(p.ProcessName);
            Assert.NotEmpty(p.ProcessName);
        });
    }

    [Fact]
    public void GetRunningProcesses_ContainsCurrentProcess()
    {
        // Arrange
        var launcher = new ProcessLauncherWin32();
        var currentProcessId = Environment.ProcessId;

        // Act
        var processes = launcher.GetRunningProcesses();

        // Assert
        var currentProcess = processes.FirstOrDefault(p => p.ProcessId == currentProcessId);
        Assert.NotNull(currentProcess);
        Assert.NotEmpty(currentProcess.ProcessName);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }
}