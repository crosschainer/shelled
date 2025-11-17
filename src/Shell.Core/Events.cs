using Shell.Core.Models;
using Shell.Core.Interfaces;

namespace Shell.Core.Events;

/// <summary>
/// Base class for all shell domain events
/// </summary>
public abstract class ShellEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string EventId { get; } = Guid.NewGuid().ToString();
}

// Window Events

/// <summary>
/// Event fired when a new window is created and tracked by the shell
/// </summary>
public class WindowCreatedEvent : ShellEvent
{
    public ShellWindow Window { get; }

    public WindowCreatedEvent(ShellWindow window)
    {
        Window = window;
    }
}

/// <summary>
/// Event fired when a tracked window is destroyed
/// </summary>
public class WindowDestroyedEvent : ShellEvent
{
    public IntPtr WindowHandle { get; }
    public string WorkspaceId { get; }

    public WindowDestroyedEvent(IntPtr windowHandle, string workspaceId)
    {
        WindowHandle = windowHandle;
        WorkspaceId = workspaceId;
    }
}

/// <summary>
/// Event fired when a window's state changes (minimized, maximized, etc.)
/// </summary>
public class WindowStateChangedEvent : ShellEvent
{
    public IntPtr WindowHandle { get; }
    public WindowState OldState { get; }
    public WindowState NewState { get; }

    public WindowStateChangedEvent(IntPtr windowHandle, WindowState oldState, WindowState newState)
    {
        WindowHandle = windowHandle;
        OldState = oldState;
        NewState = newState;
    }
}

/// <summary>
/// Event fired when a window's properties are updated (title, visibility, etc.)
/// </summary>
public class WindowUpdatedEvent : ShellEvent
{
    public ShellWindow Window { get; }

    public WindowUpdatedEvent(ShellWindow window)
    {
        Window = window;
    }
}

/// <summary>
/// Event fired when window focus changes
/// </summary>
public class WindowFocusChangedEvent : ShellEvent
{
    public IntPtr PreviousWindowHandle { get; }
    public IntPtr NewWindowHandle { get; }

    public WindowFocusChangedEvent(IntPtr previousWindowHandle, IntPtr newWindowHandle)
    {
        PreviousWindowHandle = previousWindowHandle;
        NewWindowHandle = newWindowHandle;
    }
}

// Workspace Events

/// <summary>
/// Event fired when the active workspace changes
/// </summary>
public class WorkspaceSwitchedEvent : ShellEvent
{
    public string PreviousWorkspaceId { get; }
    public string NewWorkspaceId { get; }

    public WorkspaceSwitchedEvent(string previousWorkspaceId, string newWorkspaceId)
    {
        PreviousWorkspaceId = previousWorkspaceId;
        NewWorkspaceId = newWorkspaceId;
    }
}

/// <summary>
/// Event fired when a workspace is created
/// </summary>
public class WorkspaceCreatedEvent : ShellEvent
{
    public Workspace Workspace { get; }

    public WorkspaceCreatedEvent(Workspace workspace)
    {
        Workspace = workspace;
    }
}

/// <summary>
/// Event fired when a workspace is updated (name change, window assignments, etc.)
/// </summary>
public class WorkspaceUpdatedEvent : ShellEvent
{
    public Workspace Workspace { get; }

    public WorkspaceUpdatedEvent(Workspace workspace)
    {
        Workspace = workspace;
    }
}

/// <summary>
/// Event fired when a window is moved to a different workspace
/// </summary>
public class WindowMovedToWorkspaceEvent : ShellEvent
{
    public IntPtr WindowHandle { get; }
    public string PreviousWorkspaceId { get; }
    public string NewWorkspaceId { get; }

    public WindowMovedToWorkspaceEvent(IntPtr windowHandle, string previousWorkspaceId, string newWorkspaceId)
    {
        WindowHandle = windowHandle;
        PreviousWorkspaceId = previousWorkspaceId;
        NewWorkspaceId = newWorkspaceId;
    }
}

// Tray Icon Events

/// <summary>
/// Event fired when a new tray icon is added
/// </summary>
public class TrayIconAddedEvent : ShellEvent
{
    public TrayIcon TrayIcon { get; }

    public TrayIconAddedEvent(TrayIcon trayIcon)
    {
        TrayIcon = trayIcon;
    }
}

/// <summary>
/// Event fired when a tray icon is updated
/// </summary>
public class TrayIconUpdatedEvent : ShellEvent
{
    public TrayIcon TrayIcon { get; }

    public TrayIconUpdatedEvent(TrayIcon trayIcon)
    {
        TrayIcon = trayIcon;
    }
}

/// <summary>
/// Event fired when a tray icon is removed
/// </summary>
public class TrayIconRemovedEvent : ShellEvent
{
    public string TrayIconId { get; }
    public int ProcessId { get; }

    public TrayIconRemovedEvent(string trayIconId, int processId)
    {
        TrayIconId = trayIconId;
        ProcessId = processId;
    }
}

/// <summary>
/// Event fired when a tray icon is clicked or interacted with
/// </summary>
public class TrayIconClickedEvent : ShellEvent
{
    public string TrayIconId { get; }
    public TrayClickType ClickType { get; }

    public TrayIconClickedEvent(string trayIconId, TrayClickType clickType)
    {
        TrayIconId = trayIconId;
        ClickType = clickType;
    }
}

