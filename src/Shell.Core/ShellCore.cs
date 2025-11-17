using Shell.Core.Events;
using Shell.Core.Interfaces;
using Shell.Core.Models;

namespace Shell.Core;

/// <summary>
/// Core shell state machine that manages windows, workspaces, and tray icons
/// </summary>
public class ShellCore : IDisposable
{
    private readonly IWindowSystem _windowSystem;
    private readonly IProcessLauncher _processLauncher;
    private readonly ITrayHost _trayHost;
    private readonly IHotkeyRegistry _hotkeyRegistry;
    private readonly IEventPublisher _eventPublisher;
    private readonly ShellState _state;
    private bool _disposed = false;

    public ShellCore(
        IWindowSystem windowSystem,
        IProcessLauncher processLauncher,
        ITrayHost trayHost,
        IHotkeyRegistry hotkeyRegistry,
        IEventPublisher eventPublisher)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _trayHost = trayHost ?? throw new ArgumentNullException(nameof(trayHost));
        _hotkeyRegistry = hotkeyRegistry ?? throw new ArgumentNullException(nameof(hotkeyRegistry));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        
        _state = new ShellState();
        
        SubscribeToAdapterEvents();
        InitializeState();
    }

    /// <summary>
    /// Get a read-only copy of the current shell state
    /// </summary>
    public ShellState GetState()
    {
        // Return a copy to prevent external modification
        return new ShellState
        {
            Windows = new Dictionary<IntPtr, ShellWindow>(_state.Windows),
            Workspaces = new Dictionary<string, Workspace>(_state.Workspaces.ToDictionary(
                kvp => kvp.Key,
                kvp => new Workspace
                {
                    Id = kvp.Value.Id,
                    Name = kvp.Value.Name,
                    WindowHandles = new List<IntPtr>(kvp.Value.WindowHandles),
                    CreatedAt = kvp.Value.CreatedAt,
                    IsActive = kvp.Value.IsActive
                })),
            ActiveWorkspaceId = _state.ActiveWorkspaceId,
            FocusedWindowHandle = _state.FocusedWindowHandle,
            TrayIcons = new Dictionary<string, TrayIcon>(_state.TrayIcons),
            LastUpdated = _state.LastUpdated
        };
    }

    /// <summary>
    /// Launch an application
    /// </summary>
    public async Task<int> LaunchAppAsync(string appIdOrPath)
    {
        return await _processLauncher.LaunchAppAsync(appIdOrPath);
    }

    /// <summary>
    /// Focus a specific window
    /// </summary>
    public void FocusWindow(IntPtr windowHandle)
    {
        if (!_state.Windows.ContainsKey(windowHandle))
            return;

        var previousFocus = _state.FocusedWindowHandle;
        _state.FocusedWindowHandle = windowHandle;
        _state.LastUpdated = DateTime.UtcNow;

        _windowSystem.SetForegroundWindow(windowHandle);
        
        _eventPublisher.Publish(new WindowFocusChangedEvent(previousFocus, windowHandle));
    }

    /// <summary>
    /// Switch to a different workspace
    /// </summary>
    public void SwitchWorkspace(string workspaceId)
    {
        if (!_state.Workspaces.ContainsKey(workspaceId) || _state.ActiveWorkspaceId == workspaceId)
            return;

        var previousWorkspaceId = _state.ActiveWorkspaceId;
        
        // Hide windows from the current workspace
        if (_state.Workspaces.TryGetValue(previousWorkspaceId, out var previousWorkspace))
        {
            previousWorkspace.IsActive = false;
            foreach (var windowHandle in previousWorkspace.WindowHandles)
            {
                if (_state.Windows.TryGetValue(windowHandle, out var window))
                {
                    window.IsVisible = false;
                    _windowSystem.ShowWindow(windowHandle, WindowState.Hidden);
                }
            }
        }

        // Show windows from the new workspace
        if (_state.Workspaces.TryGetValue(workspaceId, out var newWorkspace))
        {
            newWorkspace.IsActive = true;
            foreach (var windowHandle in newWorkspace.WindowHandles)
            {
                if (_state.Windows.TryGetValue(windowHandle, out var window))
                {
                    window.IsVisible = true;
                    _windowSystem.ShowWindow(windowHandle, window.State);
                }
            }
        }

        _state.ActiveWorkspaceId = workspaceId;
        _state.LastUpdated = DateTime.UtcNow;

        _eventPublisher.Publish(new WorkspaceSwitchedEvent(previousWorkspaceId, workspaceId));
    }

    /// <summary>
    /// Create a new workspace
    /// </summary>
    public void CreateWorkspace(string workspaceId, string name)
    {
        if (_state.Workspaces.ContainsKey(workspaceId))
            return;

        var workspace = new Workspace
        {
            Id = workspaceId,
            Name = name,
            IsActive = false
        };

        _state.Workspaces[workspaceId] = workspace;
        _state.LastUpdated = DateTime.UtcNow;

        _eventPublisher.Publish(new WorkspaceCreatedEvent(workspace));
    }

    /// <summary>
    /// Move a window to a different workspace
    /// </summary>
    public void MoveWindowToWorkspace(IntPtr windowHandle, string workspaceId)
    {
        if (!_state.Windows.TryGetValue(windowHandle, out var window) ||
            !_state.Workspaces.ContainsKey(workspaceId))
            return;

        var previousWorkspaceId = window.WorkspaceId;
        if (previousWorkspaceId == workspaceId)
            return;

        // Remove from previous workspace
        if (_state.Workspaces.TryGetValue(previousWorkspaceId, out var previousWorkspace))
        {
            previousWorkspace.WindowHandles.Remove(windowHandle);
        }

        // Add to new workspace
        if (_state.Workspaces.TryGetValue(workspaceId, out var newWorkspace))
        {
            newWorkspace.WindowHandles.Add(windowHandle);
        }

        // Update window
        window.WorkspaceId = workspaceId;
        window.LastUpdated = DateTime.UtcNow;

        // Update visibility based on active workspace
        var shouldBeVisible = workspaceId == _state.ActiveWorkspaceId;
        if (window.IsVisible != shouldBeVisible)
        {
            window.IsVisible = shouldBeVisible;
            _windowSystem.ShowWindow(windowHandle, shouldBeVisible ? window.State : WindowState.Hidden);
        }

        _state.LastUpdated = DateTime.UtcNow;

        _eventPublisher.Publish(new WindowMovedToWorkspaceEvent(windowHandle, previousWorkspaceId, workspaceId));
    }

    private void SubscribeToAdapterEvents()
    {
        _windowSystem.WindowCreated += OnWindowCreated;
        _windowSystem.WindowDestroyed += OnWindowDestroyed;
        _windowSystem.WindowActivated += OnWindowActivated;
        _windowSystem.WindowUpdated += OnWindowUpdated;

        _trayHost.TrayIconAdded += OnTrayIconAdded;
        _trayHost.TrayIconUpdated += OnTrayIconUpdated;
        _trayHost.TrayIconRemoved += OnTrayIconRemoved;
        _trayHost.TrayIconClicked += OnTrayIconClicked;
    }

    private void InitializeState()
    {
        // Initialize with existing windows
        foreach (var window in _windowSystem.EnumWindows())
        {
            AddWindowToState(window);
        }

        // Initialize with existing tray icons
        foreach (var trayIcon in _trayHost.GetTrayIcons())
        {
            _state.TrayIcons[trayIcon.Id] = trayIcon;
        }
    }

    private void OnWindowCreated(IntPtr windowHandle)
    {
        var windowInfo = _windowSystem.GetWindowInfo(windowHandle);
        if (windowInfo == null)
            return;

        AddWindowToState(windowInfo);
        _eventPublisher.Publish(new WindowCreatedEvent(windowInfo));
    }

    private void OnWindowDestroyed(IntPtr windowHandle)
    {
        if (!_state.Windows.TryGetValue(windowHandle, out var window))
            return;

        var workspaceId = window.WorkspaceId;

        // Remove from workspace
        if (_state.Workspaces.TryGetValue(workspaceId, out var workspace))
        {
            workspace.WindowHandles.Remove(windowHandle);
        }

        // Remove from state
        _state.Windows.Remove(windowHandle);

        // Clear focus if this was the focused window
        if (_state.FocusedWindowHandle == windowHandle)
        {
            _state.FocusedWindowHandle = IntPtr.Zero;
        }

        _state.LastUpdated = DateTime.UtcNow;

        _eventPublisher.Publish(new WindowDestroyedEvent(windowHandle, workspaceId));
    }

    private void OnWindowActivated(IntPtr windowHandle)
    {
        if (!_state.Windows.ContainsKey(windowHandle))
            return;

        var previousFocus = _state.FocusedWindowHandle;
        _state.FocusedWindowHandle = windowHandle;
        _state.LastUpdated = DateTime.UtcNow;

        _eventPublisher.Publish(new WindowFocusChangedEvent(previousFocus, windowHandle));
    }

    private void OnWindowUpdated(IntPtr windowHandle)
    {
        var windowInfo = _windowSystem.GetWindowInfo(windowHandle);
        if (windowInfo == null || !_state.Windows.ContainsKey(windowHandle))
            return;

        var existingWindow = _state.Windows[windowHandle];
        var oldState = existingWindow.State;

        // Update the window in state
        _state.Windows[windowHandle] = windowInfo;
        _state.LastUpdated = DateTime.UtcNow;

        // Fire state change event if state changed
        if (oldState != windowInfo.State)
        {
            _eventPublisher.Publish(new WindowStateChangedEvent(windowHandle, oldState, windowInfo.State));
        }

        _eventPublisher.Publish(new WindowUpdatedEvent(windowInfo));
    }

    private void OnTrayIconAdded(TrayIcon trayIcon)
    {
        _state.TrayIcons[trayIcon.Id] = trayIcon;
        _state.LastUpdated = DateTime.UtcNow;

        _eventPublisher.Publish(new TrayIconAddedEvent(trayIcon));
    }

    private void OnTrayIconUpdated(TrayIcon trayIcon)
    {
        if (!_state.TrayIcons.ContainsKey(trayIcon.Id))
            return;

        _state.TrayIcons[trayIcon.Id] = trayIcon;
        _state.LastUpdated = DateTime.UtcNow;

        _eventPublisher.Publish(new TrayIconUpdatedEvent(trayIcon));
    }

    private void OnTrayIconRemoved(string trayIconId)
    {
        if (!_state.TrayIcons.TryGetValue(trayIconId, out var trayIcon))
            return;

        _state.TrayIcons.Remove(trayIconId);
        _state.LastUpdated = DateTime.UtcNow;

        _eventPublisher.Publish(new TrayIconRemovedEvent(trayIconId, trayIcon.ProcessId));
    }

    private void OnTrayIconClicked(string trayIconId, TrayClickType clickType)
    {
        _eventPublisher.Publish(new TrayIconClickedEvent(trayIconId, clickType));
    }

    private void AddWindowToState(ShellWindow window)
    {
        // Assign to active workspace if not already assigned
        if (string.IsNullOrEmpty(window.WorkspaceId))
        {
            window.WorkspaceId = _state.ActiveWorkspaceId;
        }

        // Add to state
        _state.Windows[window.Handle] = window;

        // Add to workspace
        if (_state.Workspaces.TryGetValue(window.WorkspaceId, out var workspace))
        {
            if (!workspace.WindowHandles.Contains(window.Handle))
            {
                workspace.WindowHandles.Add(window.Handle);
            }
        }

        _state.LastUpdated = DateTime.UtcNow;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _windowSystem.WindowCreated -= OnWindowCreated;
        _windowSystem.WindowDestroyed -= OnWindowDestroyed;
        _windowSystem.WindowActivated -= OnWindowActivated;
        _windowSystem.WindowUpdated -= OnWindowUpdated;

        _trayHost.TrayIconAdded -= OnTrayIconAdded;
        _trayHost.TrayIconUpdated -= OnTrayIconUpdated;
        _trayHost.TrayIconRemoved -= OnTrayIconRemoved;
        _trayHost.TrayIconClicked -= OnTrayIconClicked;

        _disposed = true;
    }
}