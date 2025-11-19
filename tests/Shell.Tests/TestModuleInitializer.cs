using System;
using System.Runtime.CompilerServices;

namespace Shell.Tests;

internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Ensure all Shell.Tests run with dangerous operations disabled.
        // This makes adapters like ProcessLauncherWin32, TrayHostWin32, and
        // HotkeyRegistryWin32 operate in safe "test mode" regardless of
        // how the tests are invoked.
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
        Environment.SetEnvironmentVariable("SHELL_DEV_MODE", "1");
    }
}

