using System;
using System.Runtime.CompilerServices;

namespace Shell.Bootstrap.Tests;

internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Ensure bootstrap-related tests always run with dangerous
        // operations disabled so no registry or shell changes occur.
        Environment.SetEnvironmentVariable("SHELL_TEST_MODE", "1");
        Environment.SetEnvironmentVariable("SHELL_DEV_MODE", "1");
    }
}

