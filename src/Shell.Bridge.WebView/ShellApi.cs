using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Interfaces;
using Shell.Core.Models;

namespace Shell.Bridge.WebView;

/// <summary>
/// Bridge API exposed to JavaScript for shell operations
/// </summary>
[ComVisible(true)]
public class ShellApi : IDisposable
{
    private readonly ShellCore _shellCore;
    private readonly CoreWebView2 _webView;
    private readonly IEventPublisher _eventPublisher;
    private bool _disposed = false;

    public ShellApi(ShellCore shellCore, CoreWebView2 webView, IEventPublisher eventPublisher)
    {
        _shellCore = shellCore ?? throw new ArgumentNullException(nameof(shellCore));
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        
        SubscribeToShellEvents();
    }

    /// <summary>
    /// Get all windows as JSON string
    /// </summary>
    public string ListWindowsJson()
    {
        try
        {
            var state = _shellCore.GetState();
            var windows = state.Windows.Values.Select(w => new
            {
                hwnd = w.Handle.ToString(),
                title = w.Title,
                processId = w.ProcessId,
                workspaceId = w.WorkspaceId,
                state = w.State.ToString(),
                isVisible = w.IsVisible,
                appId = w.AppId,
                className = w.ClassName,
                lastUpdated = w.LastUpdated.ToString("O")
            }).ToArray();

            return JsonSerializer.Serialize(windows, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ListWindowsJson: {ex.Message}");
            return "[]";
        }
    }

    /// <summary>
    /// Get all workspaces as JSON string
    /// </summary>
    public string ListWorkspacesJson()
    {
        try
        {
            var state = _shellCore.GetState();
            var workspaces = state.Workspaces.Values.Select(w => new
            {
                id = w.Id,
                name = w.Name,
                windowHandles = w.WindowHandles.Select(h => h.ToString()).ToArray(),
                isActive = w.IsActive,
                createdAt = w.CreatedAt.ToString("O")
            }).ToArray();

            return JsonSerializer.Serialize(workspaces, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ListWorkspacesJson: {ex.Message}");
            return "[]";
        }
    }

    /// <summary>
    /// Get all tray icons as JSON string
    /// </summary>
    public string GetTrayIconsJson()
    {
        try
        {
            var state = _shellCore.GetState();
            var trayIcons = state.TrayIcons.Values.Select(t => new
            {
                id = t.Id,
                tooltip = t.Tooltip,
                processId = t.ProcessId,
                iconData = t.IconData != null ? Convert.ToBase64String(t.IconData) : null,
                isVisible = t.IsVisible,
                lastUpdated = t.LastUpdated.ToString("O")
            }).ToArray();

            return JsonSerializer.Serialize(trayIcons, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetTrayIconsJson: {ex.Message}");
            return "[]";
        }
    }

    /// <summary>
    /// Launch an application
    /// </summary>
    public async Task<bool> LaunchApp(string appIdOrPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(appIdOrPath))
                return false;

            var processId = await _shellCore.LaunchAppAsync(appIdOrPath);
            return processId > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error launching app '{appIdOrPath}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Focus a window by handle
    /// </summary>
    public bool FocusWindow(string hwndString)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hwndString))
                return false;

            if (IntPtr.TryParse(hwndString, out var hwnd))
            {
                _shellCore.FocusWindow(hwnd);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error focusing window '{hwndString}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Minimize a window by handle
    /// </summary>
    public bool MinimizeWindow(string hwndString)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hwndString))
                return false;

            if (IntPtr.TryParse(hwndString, out var hwnd))
            {
                var state = _shellCore.GetState();
                if (state.Windows.TryGetValue(hwnd, out var window))
                {
                    // This would require adding a MinimizeWindow method to ShellCore
                    // For now, we'll just focus it (which might restore if minimized)
                    _shellCore.FocusWindow(hwnd);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error minimizing window '{hwndString}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Switch to a workspace
    /// </summary>
    public bool SwitchWorkspace(string workspaceId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                return false;

            _shellCore.SwitchWorkspace(workspaceId);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error switching to workspace '{workspaceId}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Create a new workspace
    /// </summary>
    public bool CreateWorkspace(string workspaceId, string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(name))
                return false;

            _shellCore.CreateWorkspace(workspaceId, name);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating workspace '{workspaceId}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Move a window to a different workspace
    /// </summary>
    public bool MoveWindowToWorkspace(string hwndString, string workspaceId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hwndString) || string.IsNullOrWhiteSpace(workspaceId))
                return false;

            if (IntPtr.TryParse(hwndString, out var hwnd))
            {
                _shellCore.MoveWindowToWorkspace(hwnd, workspaceId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error moving window '{hwndString}' to workspace '{workspaceId}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get current shell state as JSON
    /// </summary>
    public string GetShellStateJson()
    {
        try
        {
            var state = _shellCore.GetState();
            var stateData = new
            {
                activeWorkspaceId = state.ActiveWorkspaceId,
                focusedWindowHandle = state.FocusedWindowHandle.ToString(),
                lastUpdated = state.LastUpdated.ToString("O"),
                windowCount = state.Windows.Count,
                workspaceCount = state.Workspaces.Count,
                trayIconCount = state.TrayIcons.Count
            };

            return JsonSerializer.Serialize(stateData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetShellStateJson: {ex.Message}");
            return "{}";
        }
    }

    /// <summary>
    /// Handle tray icon click
    /// </summary>
    public bool TrayIconClick(string trayIconId, string clickType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(trayIconId) || string.IsNullOrWhiteSpace(clickType))
                return false;

            // This would require exposing tray icon click handling in ShellCore
            // For now, just log the action
            Console.WriteLine($"Tray icon '{trayIconId}' clicked with type '{clickType}'");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling tray icon click '{trayIconId}': {ex.Message}");
            return false;
        }
    }

    private void SubscribeToShellEvents()
    {
        // Subscribe to shell events and forward them to the UI
        _eventPublisher.Subscribe<WindowCreatedEvent>(OnWindowCreated);
        _eventPublisher.Subscribe<WindowDestroyedEvent>(OnWindowDestroyed);
        _eventPublisher.Subscribe<WindowUpdatedEvent>(OnWindowUpdated);
        _eventPublisher.Subscribe<WindowFocusChangedEvent>(OnWindowFocusChanged);
        _eventPublisher.Subscribe<WorkspaceSwitchedEvent>(OnWorkspaceSwitched);
        _eventPublisher.Subscribe<WorkspaceCreatedEvent>(OnWorkspaceCreated);
        _eventPublisher.Subscribe<WindowMovedToWorkspaceEvent>(OnWindowMovedToWorkspace);
        _eventPublisher.Subscribe<TrayIconAddedEvent>(OnTrayIconAdded);
        _eventPublisher.Subscribe<TrayIconUpdatedEvent>(OnTrayIconUpdated);
        _eventPublisher.Subscribe<TrayIconRemovedEvent>(OnTrayIconRemoved);
    }

    private void OnWindowCreated(WindowCreatedEvent eventData)
    {
        SendEventToUI("windowCreated", new
        {
            hwnd = eventData.Window.Handle.ToString(),
            title = eventData.Window.Title,
            processId = eventData.Window.ProcessId,
            workspaceId = eventData.Window.WorkspaceId,
            state = eventData.Window.State.ToString(),
            isVisible = eventData.Window.IsVisible,
            appId = eventData.Window.AppId,
            className = eventData.Window.ClassName
        });
    }

    private void OnWindowDestroyed(WindowDestroyedEvent eventData)
    {
        SendEventToUI("windowDestroyed", new
        {
            hwnd = eventData.WindowHandle.ToString()
        });
    }

    private void OnWindowUpdated(WindowUpdatedEvent eventData)
    {
        SendEventToUI("windowUpdated", new
        {
            hwnd = eventData.Window.Handle.ToString(),
            title = eventData.Window.Title,
            state = eventData.Window.State.ToString(),
            isVisible = eventData.Window.IsVisible
        });
    }

    private void OnWindowFocusChanged(WindowFocusChangedEvent eventData)
    {
        SendEventToUI("windowFocusChanged", new
        {
            previousHwnd = eventData.PreviousWindowHandle?.ToString(),
            currentHwnd = eventData.CurrentWindowHandle?.ToString()
        });
    }

    private void OnWorkspaceSwitched(WorkspaceSwitchedEvent eventData)
    {
        SendEventToUI("workspaceSwitched", new
        {
            previousWorkspaceId = eventData.PreviousWorkspaceId,
            currentWorkspaceId = eventData.CurrentWorkspaceId
        });
    }

    private void OnWorkspaceCreated(WorkspaceCreatedEvent eventData)
    {
        SendEventToUI("workspaceCreated", new
        {
            id = eventData.Workspace.Id,
            name = eventData.Workspace.Name,
            isActive = eventData.Workspace.IsActive
        });
    }

    private void OnWindowMovedToWorkspace(WindowMovedToWorkspaceEvent eventData)
    {
        SendEventToUI("windowMovedToWorkspace", new
        {
            hwnd = eventData.WindowHandle.ToString(),
            previousWorkspaceId = eventData.PreviousWorkspaceId,
            newWorkspaceId = eventData.NewWorkspaceId
        });
    }

    private void OnTrayIconAdded(TrayIconAddedEvent eventData)
    {
        SendEventToUI("trayIconAdded", new
        {
            id = eventData.TrayIcon.Id,
            tooltip = eventData.TrayIcon.Tooltip,
            processId = eventData.TrayIcon.ProcessId,
            iconData = eventData.TrayIcon.IconData != null ? Convert.ToBase64String(eventData.TrayIcon.IconData) : null,
            isVisible = eventData.TrayIcon.IsVisible
        });
    }

    private void OnTrayIconUpdated(TrayIconUpdatedEvent eventData)
    {
        SendEventToUI("trayIconUpdated", new
        {
            id = eventData.TrayIcon.Id,
            tooltip = eventData.TrayIcon.Tooltip,
            iconData = eventData.TrayIcon.IconData != null ? Convert.ToBase64String(eventData.TrayIcon.IconData) : null,
            isVisible = eventData.TrayIcon.IsVisible
        });
    }

    private void OnTrayIconRemoved(TrayIconRemovedEvent eventData)
    {
        SendEventToUI("trayIconRemoved", new
        {
            id = eventData.TrayIconId
        });
    }

    private void SendEventToUI(string eventType, object eventData)
    {
        try
        {
            var message = new
            {
                type = eventType,
                data = eventData,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _webView.PostWebMessageAsString(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending event '{eventType}' to UI: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Unsubscribe from events
            _eventPublisher.Unsubscribe<WindowCreatedEvent>(OnWindowCreated);
            _eventPublisher.Unsubscribe<WindowDestroyedEvent>(OnWindowDestroyed);
            _eventPublisher.Unsubscribe<WindowUpdatedEvent>(OnWindowUpdated);
            _eventPublisher.Unsubscribe<WindowFocusChangedEvent>(OnWindowFocusChanged);
            _eventPublisher.Unsubscribe<WorkspaceSwitchedEvent>(OnWorkspaceSwitched);
            _eventPublisher.Unsubscribe<WorkspaceCreatedEvent>(OnWorkspaceCreated);
            _eventPublisher.Unsubscribe<WindowMovedToWorkspaceEvent>(OnWindowMovedToWorkspace);
            _eventPublisher.Unsubscribe<TrayIconAddedEvent>(OnTrayIconAdded);
            _eventPublisher.Unsubscribe<TrayIconUpdatedEvent>(OnTrayIconUpdated);
            _eventPublisher.Unsubscribe<TrayIconRemovedEvent>(OnTrayIconRemoved);

            _disposed = true;
        }
    }
}