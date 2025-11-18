using System.Runtime.InteropServices;
using Shell.Bootstrap;

namespace Shell.Bootstrap.Tests;

public class ShellRegistrationTests
{
    [Fact]
    public void TryRegisterShelledShell_WhenTestModeEnabled_SkipsRegistryWrites()
    {
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");

        try
        {
            var result = ShellRegistration.TryRegisterShelledShell("C:\\Shelled\\myshell-bootstrap.exe");
            Assert.Equal(ShellRegistrationResult.SkippedTestMode, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
        }
    }

    [Fact]
    public void TryRestoreExplorerShell_WhenTestModeEnabled_SkipsRegistryWrites()
    {
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");

        try
        {
            var result = ShellRegistration.TryRestoreExplorerShell();
            Assert.Equal(ShellRegistrationResult.SkippedTestMode, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);
        }
    }

    [Fact]
    public void TryRegisterShelledShell_WhenNotWindows_SkipsForPlatform()
    {
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", null);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Avoid touching the registry during local Windows runs.
            return;
        }

        var result = ShellRegistration.TryRegisterShelledShell("/tmp/shelled/myshell-bootstrap.exe");
        Assert.Equal(ShellRegistrationResult.SkippedPlatform, result);
    }
}
