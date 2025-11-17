namespace Shell.Core.Models;

/// <summary>
/// Represents a window managed by the shell
/// </summary>
public class ShellWindow
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string WorkspaceId { get; set; } = "default";
    public WindowState State { get; set; } = WindowState.Normal;
    public bool IsVisible { get; set; } = true;
    public string AppId { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Window state enumeration
/// </summary>
public enum WindowState
{
    Normal,
    Minimized,
    Maximized,
    Hidden
}

/// <summary>
/// Represents a virtual workspace containing windows
/// </summary>
public class Workspace
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<IntPtr> WindowHandles { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = false;
}

/// <summary>
/// Represents a system tray icon
/// </summary>
public class TrayIcon
{
    public string Id { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string Tooltip { get; set; } = string.Empty;
    public byte[]? IconData { get; set; }
    public IntPtr IconHandle { get; set; } = IntPtr.Zero;
    public bool IsVisible { get; set; } = true;
    public TrayMenu? Menu { get; set; }
    public TrayBalloonInfo? BalloonInfo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a tray icon context menu
/// </summary>
public class TrayMenu
{
    public List<TrayMenuItem> Items { get; set; } = new();
}

/// <summary>
/// Represents a tray menu item
/// </summary>
public class TrayMenuItem
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsChecked { get; set; } = false;
    public bool IsSeparator { get; set; } = false;
    public List<TrayMenuItem> SubItems { get; set; } = new();
}

/// <summary>
/// Represents balloon notification information for a tray icon
/// </summary>
public class TrayBalloonInfo
{
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public TrayBalloonIcon Icon { get; set; } = TrayBalloonIcon.None;
    public int TimeoutMs { get; set; } = 5000;
    public DateTime? ShowTime { get; set; }
}

/// <summary>
/// Types of balloon notification icons
/// </summary>
public enum TrayBalloonIcon
{
    None,
    Info,
    Warning,
    Error
}

/// <summary>
/// Central state of the shell system
/// </summary>
public class ShellState
{
    public Dictionary<IntPtr, ShellWindow> Windows { get; set; } = new();
    public Dictionary<string, Workspace> Workspaces { get; set; } = new();
    public string ActiveWorkspaceId { get; set; } = "default";
    public IntPtr FocusedWindowHandle { get; set; } = IntPtr.Zero;
    public Dictionary<string, TrayIcon> TrayIcons { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public ShellState()
    {
        // Initialize with default workspace
        Workspaces["default"] = new Workspace 
        { 
            Id = "default", 
            Name = "Default", 
            IsActive = true 
        };
    }

    /// <summary>
    /// Get the currently active workspace
    /// </summary>
    public Workspace? GetActiveWorkspace()
    {
        return Workspaces.TryGetValue(ActiveWorkspaceId, out var workspace) ? workspace : null;
    }

    /// <summary>
    /// Get all windows in the specified workspace
    /// </summary>
    public IEnumerable<ShellWindow> GetWindowsInWorkspace(string workspaceId)
    {
        if (!Workspaces.TryGetValue(workspaceId, out var workspace))
            return Enumerable.Empty<ShellWindow>();

        return workspace.WindowHandles
            .Where(handle => Windows.ContainsKey(handle))
            .Select(handle => Windows[handle]);
    }

    /// <summary>
    /// Get all visible windows in the active workspace
    /// </summary>
    public IEnumerable<ShellWindow> GetVisibleWindows()
    {
        return GetWindowsInWorkspace(ActiveWorkspaceId)
            .Where(w => w.IsVisible && w.State != WindowState.Hidden);
    }
}
