using Shell.Core.Models;

namespace Shell.Core.Interfaces;

/// <summary>
/// Types of tray icon clicks
/// </summary>
public enum TrayClickType
{
    LeftClick,
    RightClick,
    DoubleClick
}

/// <summary>
/// Interface for window system operations
/// </summary>
public interface IWindowSystem
{
    /// <summary>
    /// Enumerate all top-level windows
    /// </summary>
    IEnumerable<ShellWindow> EnumWindows();

    /// <summary>
    /// Check if a window handle represents a top-level window
    /// </summary>
    bool IsTopLevelWindow(IntPtr hwnd);

    /// <summary>
    /// Get window information for a specific handle
    /// </summary>
    ShellWindow? GetWindowInfo(IntPtr hwnd);

    /// <summary>
    /// Show or hide a window
    /// </summary>
    void ShowWindow(IntPtr hwnd, WindowState state);

    /// <summary>
    /// Set the foreground window (bring to front and focus)
    /// </summary>
    void SetForegroundWindow(IntPtr hwnd);

    /// <summary>
    /// Check if a window is currently visible
    /// </summary>
    bool IsVisible(IntPtr hwnd);

    /// <summary>
    /// Event fired when a new window is created
    /// </summary>
    event Action<IntPtr>? WindowCreated;

    /// <summary>
    /// Event fired when a window is destroyed
    /// </summary>
    event Action<IntPtr>? WindowDestroyed;

    /// <summary>
    /// Event fired when a window is activated (gains focus)
    /// </summary>
    event Action<IntPtr>? WindowActivated;

    /// <summary>
    /// Event fired when a window's properties change
    /// </summary>
    event Action<IntPtr>? WindowUpdated;
}

/// <summary>
/// Interface for process launching operations
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Launch an application by app ID or path
    /// </summary>
    Task<int> LaunchAppAsync(string appIdOrPath);

    /// <summary>
    /// Get information about currently running processes
    /// </summary>
    IEnumerable<ProcessInfo> GetRunningProcesses();
}

/// <summary>
/// Information about a running process
/// </summary>
public class ProcessInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
}

/// <summary>
/// Interface for system tray operations
/// </summary>
public interface ITrayHost
{
    /// <summary>
    /// Event fired when a tray icon is added
    /// </summary>
    event Action<TrayIcon>? TrayIconAdded;

    /// <summary>
    /// Event fired when a tray icon is updated
    /// </summary>
    event Action<TrayIcon>? TrayIconUpdated;

    /// <summary>
    /// Event fired when a tray icon is removed
    /// </summary>
    event Action<string>? TrayIconRemoved;

    /// <summary>
    /// Event fired when a tray icon is clicked
    /// </summary>
    event Action<string, TrayClickType>? TrayIconClicked;

    /// <summary>
    /// Event fired when a balloon notification is shown for a tray icon
    /// </summary>
    event Action<string, TrayBalloonInfo>? TrayBalloonShown;

    /// <summary>
    /// Get all current tray icons
    /// </summary>
    IEnumerable<TrayIcon> GetTrayIcons();

    /// <summary>
    /// Show a balloon notification for a tray icon
    /// </summary>
    void ShowBalloonNotification(string trayIconId, string title, string text, TrayBalloonIcon icon = TrayBalloonIcon.None, int timeoutMs = 5000);

    /// <summary>
    /// Update a tray icon's menu
    /// </summary>
    void UpdateTrayIconMenu(string trayIconId, TrayMenu menu);

    /// <summary>
    /// Event fired when a tray menu item is clicked
    /// </summary>
    event Action<string, string>? TrayMenuItemClicked; // trayIconId, menuItemId
}

/// <summary>
/// Interface for hotkey registration
/// </summary>
public interface IHotkeyRegistry
{
    /// <summary>
    /// Register a global hotkey
    /// </summary>
    bool RegisterHotkey(string id, int modifiers, int virtualKey);

    /// <summary>
    /// Unregister a global hotkey
    /// </summary>
    bool UnregisterHotkey(string id);

    /// <summary>
    /// Event fired when a registered hotkey is pressed
    /// </summary>
    event Action<string>? HotkeyPressed;
}

/// <summary>
/// Interface for event publishing
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publish an event to all subscribers
    /// </summary>
    void Publish<T>(T eventData) where T : class;

    /// <summary>
    /// Subscribe to events of a specific type
    /// </summary>
    void Subscribe<T>(Action<T> handler) where T : class;

    /// <summary>
    /// Unsubscribe from events of a specific type
    /// </summary>
    void Unsubscribe<T>(Action<T> handler) where T : class;
}

/// <summary>
/// Types of system events
/// </summary>
public enum SystemEventType
{
    QueryEndSession,
    EndSession,
    PowerSuspend,
    PowerResume,
    DisplaySettingsChanged,
    SessionLock,
    SessionUnlock
}

/// <summary>
/// Interface for system event handling
/// </summary>
public interface ISystemEventHandler
{
    /// <summary>
    /// Event fired when a system event occurs
    /// </summary>
    event Action<SystemEventType, SystemEventArgs>? SystemEventOccurred;

    /// <summary>
    /// Start listening for system events
    /// </summary>
    void StartListening();

    /// <summary>
    /// Stop listening for system events
    /// </summary>
    void StopListening();

    /// <summary>
    /// Check if currently listening for events
    /// </summary>
    bool IsListening { get; }
}

/// <summary>
/// Arguments for system events
/// </summary>
public class SystemEventArgs
{
    public bool CanCancel { get; set; } = false;
    public bool Cancel { get; set; } = false;
    public string? Reason { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}