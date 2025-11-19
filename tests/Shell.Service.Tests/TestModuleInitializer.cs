using System;
using System.Runtime.CompilerServices;

namespace Shell.Service.Tests;

internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Ensure service-related tests run with dangerous operations disabled
        // so they don't accidentally affect the running shell environment.
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
        Environment.SetEnvironmentVariable("SHELL_DEV_MODE", "1");
    }
}

