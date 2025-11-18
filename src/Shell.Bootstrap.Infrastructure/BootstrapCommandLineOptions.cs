using System.Collections.Generic;

namespace Shell.Bootstrap;

/// <summary>
/// Simple parser for bootstrap command-line arguments.
/// </summary>
public sealed class BootstrapCommandLineOptions
{
    /// <summary>
    /// Gets a value indicating whether the panic command should be executed.
    /// </summary>
    public bool PanicRequested { get; private set; }

    /// <summary>
    /// Parses the provided command-line arguments.
    /// </summary>
    public static BootstrapCommandLineOptions Parse(IEnumerable<string>? args)
    {
        var options = new BootstrapCommandLineOptions();

        if (args == null)
        {
            return options;
        }

        foreach (var arg in args)
        {
            if (IsPanicSwitch(arg))
            {
                options.PanicRequested = true;
                break;
            }
        }

        return options;
    }

    private static bool IsPanicSwitch(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            return false;
        }

        var trimmed = arg.TrimStart('-', '/');
        return string.Equals(trimmed, "panic", StringComparison.OrdinalIgnoreCase);
    }
}
