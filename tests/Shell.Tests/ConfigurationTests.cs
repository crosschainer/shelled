using Shell.Core;

namespace Shell.Tests;

/// <summary>
/// Tests for ShellConfiguration
/// </summary>
public class ShellConfigurationTests
{
    [Fact]
    public void IsTestMode_WhenEnvVarIs1_ReturnsTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");

        // Act & Assert
        Assert.True(ShellConfiguration.IsTestMode);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }

    [Fact]
    public void IsTestMode_WhenEnvVarIsTrue_ReturnsTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "true");

        // Act & Assert
        Assert.True(ShellConfiguration.IsTestMode);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }

    [Fact]
    public void IsTestMode_WhenEnvVarIsYes_ReturnsTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "yes");

        // Act & Assert
        Assert.True(ShellConfiguration.IsTestMode);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }

    [Fact]
    public void IsTestMode_WhenEnvVarIsNotSet_ReturnsFalse()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);

        // Act & Assert
        Assert.False(ShellConfiguration.IsTestMode);
    }

    [Fact]
    public void IsTestMode_WhenEnvVarIsFalse_ReturnsFalse()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "false");

        // Act & Assert
        Assert.False(ShellConfiguration.IsTestMode);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }

    [Fact]
    public void IsDevMode_WhenEnvVarIs1_ReturnsTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_DEV_MODE", "1");

        // Act & Assert
        Assert.True(ShellConfiguration.IsDevMode);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_DEV_MODE", null);
    }

    [Fact]
    public void IsDevMode_WhenEnvVarIsNotSet_ReturnsFalse()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_DEV_MODE", null);

        // Act & Assert
        Assert.False(ShellConfiguration.IsDevMode);
    }

    [Fact]
    public void DisableDangerousOperations_WhenTestMode_ReturnsTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
        Environment.SetEnvironmentVariable("SHELL_DEV_MODE", null);

        // Act & Assert
        Assert.True(ShellConfiguration.DisableDangerousOperations);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
    }

    [Fact]
    public void DisableDangerousOperations_WhenDevMode_ReturnsTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
        Environment.SetEnvironmentVariable("SHELL_DEV_MODE", "1");

        // Act & Assert
        Assert.True(ShellConfiguration.DisableDangerousOperations);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_DEV_MODE", null);
    }

    [Fact]
    public void DisableDangerousOperations_WhenNeitherModeSet_ReturnsFalse()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
        Environment.SetEnvironmentVariable("SHELL_DEV_MODE", null);

        // Act & Assert
        Assert.False(ShellConfiguration.DisableDangerousOperations);
    }

    [Fact]
    public void LogLevel_WhenNotSet_ReturnsInfo()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_LOG_LEVEL", null);

        // Act & Assert
        Assert.Equal("Info", ShellConfiguration.LogLevel);
    }

    [Fact]
    public void LogLevel_WhenSet_ReturnsValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_LOG_LEVEL", "Debug");

        // Act & Assert
        Assert.Equal("Debug", ShellConfiguration.LogLevel);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_LOG_LEVEL", null);
    }

    [Fact]
    public void VerboseLogging_WhenLogLevelIsDebug_ReturnsTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_LOG_LEVEL", "Debug");

        // Act & Assert
        Assert.True(ShellConfiguration.VerboseLogging);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_LOG_LEVEL", null);
    }

    [Fact]
    public void VerboseLogging_WhenLogLevelIsTrace_ReturnsTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_LOG_LEVEL", "Trace");

        // Act & Assert
        Assert.True(ShellConfiguration.VerboseLogging);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_LOG_LEVEL", null);
    }

    [Fact]
    public void VerboseLogging_WhenLogLevelIsInfo_ReturnsFalse()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SHELL_LOG_LEVEL", "Info");

        // Act & Assert
        Assert.False(ShellConfiguration.VerboseLogging);

        // Cleanup
        Environment.SetEnvironmentVariable("SHELL_LOG_LEVEL", null);
    }
}