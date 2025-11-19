using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Shell.Core;
using Shell.Core.Events;
using Shell.Core.Interfaces;
using Shell.Core.Models;
using Shell.Bootstrap;

namespace Shell.Bridge.WebView;

/// <summary>
/// Bridge API exposed to JavaScript for shell operations
/// </summary>
[ComVisible(true)]
public class ShellApi : IDisposable
{
    private const uint SPI_GETDESKWALLPAPER = 0x0073;
    private const string WallpaperHostName = "wallpaper.shelled";
    private const uint WM_CLOSE = 0x0010;

    private readonly ShellCore _shellCore;
    private readonly CoreWebView2 _webView;
    private readonly IEventPublisher _eventPublisher;
    private bool _disposed = false;
    private bool _wallpaperHostInitialized = false;

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
                lastUpdated = w.LastUpdated.ToString("O"),
                iconData = TryGetWindowIconBase64(w)
            }).ToArray();

            return JsonSerializer.Serialize(windows, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error in ListWindowsJson.", ex);
            return "[]";
        }
    }

    // JS host compatibility wrappers (lowercase-first naming)
    public string listWindowsJson() => ListWindowsJson();

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
            ShellLogger.Error(nameof(ShellApi), "Error in ListWorkspacesJson.", ex);
            return "[]";
        }
    }

    public string listWorkspacesJson() => ListWorkspacesJson();

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
            ShellLogger.Error(nameof(ShellApi), "Error in GetTrayIconsJson.", ex);
            return "[]";
        }
    }

    public string getTrayIconsJson() => GetTrayIconsJson();

    /// <summary>
    /// Get desktop items (user + public desktop) as JSON.
    /// </summary>
    public string GetDesktopItemsJson()
    {
        try
        {
            var items = new List<object>();

            void AddItems(string? root, bool isPublic)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    return;
                }

                foreach (var path in Directory.EnumerateFiles(root))
                {
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        if (attributes.HasFlag(FileAttributes.Hidden) ||
                            attributes.HasFlag(FileAttributes.System))
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        // If we can't read attributes, skip the entry.
                        continue;
                    }

                    var extension = Path.GetExtension(path);
                    var name = Path.GetFileNameWithoutExtension(path);

                    var iconData = TryGetDesktopItemIconBase64(path);

                    items.Add(new
                    {
                        name,
                        path,
                        isShortcut = string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase),
                        isPublic,
                        iconData
                    });
                }

                foreach (var directory in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        var attributes = File.GetAttributes(directory);
                        if (attributes.HasFlag(FileAttributes.Hidden) ||
                            attributes.HasFlag(FileAttributes.System))
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    var name = Path.GetFileName(directory);
                    var iconData = TryGetDesktopItemIconBase64(directory);

                    items.Add(new
                    {
                        name,
                        path = directory,
                        isShortcut = false,
                        isPublic,
                        iconData
                    });
                }
            }

            var userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

            AddItems(userDesktop, false);
            AddItems(commonDesktop, true);

            // Add a logical Recycle Bin shortcut if one is not already present.
            // This uses the shell URI so the existing process launcher can open it.
            const string recycleBinPath = "shell:RecycleBinFolder";
            var hasRecycleBin = items.OfType<object>()
                .Any(i =>
                {
                    try
                    {
                        var pathProp = i.GetType().GetProperty("path");
                        var value = pathProp?.GetValue(i) as string;
                        return string.Equals(value, recycleBinPath, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });

            if (!hasRecycleBin)
            {
                var recycleBinIcon = TryGetRecycleBinIconBase64();

                items.Add(new
                {
                    name = "Recycle Bin",
                    path = recycleBinPath,
                    isShortcut = false,
                    isPublic = true,
                    iconData = recycleBinIcon
                });
            }

            return JsonSerializer.Serialize(items, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error in GetDesktopItemsJson.", ex);
            return "[]";
        }
    }

    public string getDesktopItemsJson() => GetDesktopItemsJson();

    /// <summary>
    /// Get information about the current desktop background / wallpaper.
    /// This allows the Web UI to mirror the user's Windows wallpaper.
    /// </summary>
    public string GetDesktopBackgroundInfoJson()
    {
        try
        {
            string? wallpaperPath = EnsureWallpaperHostMapping();
            string? wallpaperUrl = null;

            if (!string.IsNullOrWhiteSpace(wallpaperPath) && File.Exists(wallpaperPath))
            {
                var fileName = Path.GetFileName(wallpaperPath);
                wallpaperUrl = $"https://{WallpaperHostName}/{Uri.EscapeDataString(fileName)}";
            }

            var (style, isTiled) = TryGetWallpaperStyle();
            var backgroundColor = TryGetDesktopBackgroundColor();

            var payload = new
            {
                wallpaperUrl,
                wallpaperStyle = style,
                isTiled,
                backgroundColor,
                hasWallpaper = !string.IsNullOrEmpty(wallpaperUrl)
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error in GetDesktopBackgroundInfoJson.", ex);
            return "{}";
        }
    }

      // JS compatibility wrapper
      public string getDesktopBackgroundInfoJson() => GetDesktopBackgroundInfoJson();
  
      /// <summary>
      /// Open the containing folder for a given file path using Explorer and
      /// select the item when possible.
      /// </summary>
      public bool OpenItemLocation(string path)
      {
          try
          {
              if (string.IsNullOrWhiteSpace(path))
                  return false;

              // shell: URIs and other virtual items do not map cleanly to a
              // physical location, so we skip them here.
              if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
              {
                  return false;
              }

              var fullPath = Path.GetFullPath(path);
              if (!File.Exists(fullPath))
              {
                  return false;
              }

              if (ShellConfiguration.DisableDangerousOperations)
              {
                  Console.WriteLine($"OpenItemLocation blocked in safe mode for '{fullPath}'");
                  return false;
              }

              var startInfo = new ProcessStartInfo
              {
                  FileName = "explorer.exe",
                  Arguments = $"/select,\"{fullPath}\"",
                  UseShellExecute = true
              };

              Process.Start(startInfo);
              return true;
          }
          catch (Exception ex)
          {
              ShellLogger.Error(nameof(ShellApi), $"Error opening item location for '{path}'.", ex);
              return false;
          }
      }

      // JS compatibility wrapper
      public bool openItemLocation(string path) => OpenItemLocation(path);

      /// <summary>
      /// Pre-initialize the virtual host mapping for the current wallpaper so that
      /// the first load from the Web UI does not race with host setup.
    /// Safe to call multiple times.
    /// </summary>
    public void InitializeDesktopBackgroundHost()
    {
        try
        {
            EnsureWallpaperHostMapping();
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error initializing desktop background host.", ex);
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
            ShellLogger.Error(nameof(ShellApi), $"Error launching app '{appIdOrPath}'.", ex);
            return false;
        }
    }

    // JS wrapper for camelCase API
    public Task<bool> launchApp(string appIdOrPath) => LaunchApp(appIdOrPath);

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

      public bool focusWindow(string hwnd) => FocusWindow(hwnd);
  
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
                  return _shellCore.MinimizeWindow(hwnd);
              }
  
              return false;
          }
          catch (Exception ex)
        {
            Console.WriteLine($"Error minimizing window '{hwndString}': {ex.Message}");
            return false;
        }
    }

      public bool minimizeWindow(string hwnd) => MinimizeWindow(hwnd);
  
      /// <summary>
      /// Restore a minimized window by handle
      /// </summary>
      public bool RestoreWindow(string hwndString)
      {
          try
          {
              if (string.IsNullOrWhiteSpace(hwndString))
                  return false;
  
              if (IntPtr.TryParse(hwndString, out var hwnd))
              {
                  return _shellCore.RestoreWindow(hwnd);
              }
  
              return false;
          }
        catch (Exception ex)
        {
            Console.WriteLine($"Error restoring window '{hwndString}': {ex.Message}");
            return false;
        }
    }

    public bool restoreWindow(string hwnd) => RestoreWindow(hwnd);

      /// <summary>
      /// Close a window by handle
      /// </summary>
      public bool CloseWindow(string hwndString)
      {
          try
          {
              if (string.IsNullOrWhiteSpace(hwndString))
                  return false;

              if (!IntPtr.TryParse(hwndString, out var hwnd))
                  return false;

              if (ShellConfiguration.DisableDangerousOperations)
              {
                  Console.WriteLine($"CloseWindow blocked in safe mode for window '{hwndString}'");
                  return false;
              }

              // Request the window to close using the standard WM_CLOSE message.
              // ShellCore will observe the resulting destroy event and update
              // internal state accordingly via its window system hooks.
              const uint WM_CLOSE = 0x0010;
              SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

              return true;
          }
          catch (Exception ex)
          {
              Console.WriteLine($"Error closing window '{hwndString}': {ex.Message}");
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

      // JS wrapper for camelCase API
      public bool closeWindow(string hwndString) => CloseWindow(hwndString);

    // JS wrapper for camelCase API
    public bool switchWorkspace(string workspaceId) => SwitchWorkspace(workspaceId);

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

    // JS wrapper for camelCase API
    public bool createWorkspace(string workspaceId, string name) => CreateWorkspace(workspaceId, name);

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

    // JS wrapper for camelCase API
    public bool moveWindowToWorkspace(string hwndString, string workspaceId) =>
        MoveWindowToWorkspace(hwndString, workspaceId);

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
    /// Get current system status (time, network, volume) as JSON.
    /// This is a best-effort snapshot intended for the Web UI.
    /// </summary>
    public string GetSystemStatusJson()
        {
            try
            {
                return SystemStatusProvider.GetSystemStatusJson();
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error in GetSystemStatusJson.", ex);
            return "{}";
        }
    }

    /// <summary>
    /// Set the master system volume as a percentage.
    /// </summary>
    public bool SetSystemVolume(int levelPercent)
    {
        try
        {
            return SystemStatusProvider.SetSystemVolumePercent(levelPercent);
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error in SetSystemVolume.", ex);
            return false;
        }
    }

    public bool setSystemVolume(int levelPercent) => SetSystemVolume(levelPercent);

    /// <summary>
    /// Toggle the master system mute state.
    /// </summary>
    public bool ToggleSystemMute()
    {
        try
        {
            return SystemStatusProvider.ToggleSystemMute();
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error in ToggleSystemMute.", ex);
            return false;
        }
    }

    public bool toggleSystemMute() => ToggleSystemMute();

    /// <summary>
    /// Open the operating system network settings UI.
    /// </summary>
    public bool OpenNetworkSettings()
    {
        try
        {
            return SystemStatusProvider.OpenNetworkSettings();
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error in OpenNetworkSettings.", ex);
            return false;
        }
    }

    public bool openNetworkSettings() => OpenNetworkSettings();

    /// <summary>
    /// Prefer a specific network kind when both Wi-Fi and Ethernet are available.
    /// </summary>
    public bool PreferNetwork(string preferredKind)
    {
        try
        {
            return SystemStatusProvider.PreferNetworkKind(preferredKind);
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error in PreferNetwork.", ex);
            return false;
        }
    }

    public bool preferNetwork(string preferredKind) => PreferNetwork(preferredKind);

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

    // JS wrapper for camelCase API
    public bool trayIconClick(string trayIconId, string clickType) => TrayIconClick(trayIconId, clickType);

    /// <summary>
    /// Restore Explorer as the system shell and launch it, then terminate any
    /// running Shelled processes (including the bootstrap) so the session is
    /// handed back cleanly.
    /// </summary>
    public bool RestoreExplorerShell()
    {
        try
        {
            void KillShelledProcesses()
            {
                try
                {
                    var currentPid = Process.GetCurrentProcess().Id;
                    var names = new[]
                    {
                        "myshell-bootstrap",
                        "ShellUiHost",
                        "Shell.Service"
                    };

                    foreach (var name in names)
                    {
                        foreach (var proc in Process.GetProcessesByName(name))
                        {
                            try
                            {
                                if (proc.Id == currentPid)
                                    continue;

                                proc.Kill();
                            }
                            catch
                            {
                                // Best-effort; ignore failures per process.
                            }
                            finally
                            {
                                proc.Dispose();
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore best-effort cleanup errors.
                }
            }

            // In dev/test mode, avoid touching Winlogon and just start Explorer
            // and tear down all Shelled processes so the user returns to a
            // normal desktop quickly.
            if (ShellConfiguration.DisableDangerousOperations)
            {
                ShellLogger.Warn(nameof(ShellApi),
                    "RestoreExplorerShell requested in dev/test mode; starting Explorer and exiting without registry changes.");

                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe")
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ShellLogger.Error(nameof(ShellApi), "Failed to start Explorer in dev/test mode.", ex);
                    return false;
                }

                KillShelledProcesses();
                Environment.Exit(0);
                return true;
            }

            // In real shell mode, restore Explorer as the Winlogon shell and
            // then start it and exit Shelled.
            var result = ShellRegistration.TryRestoreExplorerShell();
            ShellLogger.Info(nameof(ShellApi), $"RestoreExplorerShell registry result: {result}");

            if (result is ShellRegistrationResult.Success or ShellRegistrationResult.NoOp)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe")
                    {
                        UseShellExecute = true
                    });

                    KillShelledProcesses();
                    Environment.Exit(0);
                    return true; // Not reached, but keeps signature happy
                }
                catch (Exception ex)
                {
                    ShellLogger.Error(nameof(ShellApi), "Failed to start Explorer after shell restore.", ex);
                    return false;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error in RestoreExplorerShell.", ex);
            return false;
        }
    }

    // JS wrapper for camelCase API
    public bool restoreExplorerShell() => RestoreExplorerShell();

      /// <summary>
      /// Get launcher apps as JSON string
      /// </summary>
      public string GetLauncherAppsJson()
      {
          try
          {
              var state = _shellCore.GetState();
              var launcherApps = state.LauncherApps.Values
                  .Where(app => app.IsVisible)
                  .OrderBy(app => app.SortOrder)
                  .ThenBy(app => app.Name)
                  .Select(app => new
                  {
                      id = app.Id,
                      name = app.Name,
                      description = app.Description,
                      iconPath = app.IconPath,
                      executablePath = app.ExecutablePath,
                      category = app.Category,
                      isVisible = app.IsVisible,
                      sortOrder = app.SortOrder,
                      iconData = GetLauncherIconBase64(app)
                  })
                  .ToArray();

              return JsonSerializer.Serialize(launcherApps, new JsonSerializerOptions
              {
                  PropertyNamingPolicy = JsonNamingPolicy.CamelCase
              });
          }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error in GetLauncherAppsJson.", ex);
            return "[]";
        }
      }

      public string getLauncherAppsJson() => GetLauncherAppsJson();

    // JS compatibility wrapper
    public string getShellStateJson() => GetShellStateJson();

    // JS compatibility wrapper
    public string getSystemStatusJson() => GetSystemStatusJson();

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
        _eventPublisher.Subscribe<HotkeyPressedEvent>(OnHotkeyPressed);
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
            className = eventData.Window.ClassName,
            iconData = TryGetWindowIconBase64(eventData.Window)
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
            previousHwnd = eventData.PreviousWindowHandle.ToString(),
            currentHwnd = eventData.NewWindowHandle.ToString()
        });
    }

    private void OnWorkspaceSwitched(WorkspaceSwitchedEvent eventData)
    {
        SendEventToUI("workspaceSwitched", new
        {
            previousWorkspaceId = eventData.PreviousWorkspaceId,
            currentWorkspaceId = eventData.NewWorkspaceId
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

    private void OnHotkeyPressed(HotkeyPressedEvent eventData)
    {
        SendEventToUI("hotkeyPressed", new
        {
            hotkeyId = eventData.HotkeyId,
            modifiers = eventData.Modifiers,
            virtualKey = eventData.VirtualKey
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
            _eventPublisher.Unsubscribe<HotkeyPressedEvent>(OnHotkeyPressed);

            _disposed = true;
        }
    }

    private static string? TryGetCurrentWallpaperPath()
    {
        try
        {
            const int maxPath = 260;
            var sb = new StringBuilder(maxPath);
            if (SystemParametersInfo(SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0))
            {
                var path = sb.ToString().TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }
        }
        catch
        {
            // Ignore errors and fall back to registry
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var path = key?.GetValue("WallPaper") as string;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    /// <summary>
    /// Ensure that the virtual host mapping for the current wallpaper folder
    /// is set up on the WebView. Returns the current wallpaper path if one
    /// exists and is accessible.
    /// </summary>
    private string? EnsureWallpaperHostMapping()
    {
        try
        {
            var wallpaperPath = TryGetCurrentWallpaperPath();
            if (string.IsNullOrWhiteSpace(wallpaperPath) || !File.Exists(wallpaperPath))
            {
                return null;
            }

            if (!_wallpaperHostInitialized)
            {
                var folder = Path.GetDirectoryName(wallpaperPath)!;

                _webView.SetVirtualHostNameToFolderMapping(
                    WallpaperHostName,
                    folder,
                    CoreWebView2HostResourceAccessKind.Allow);

                _wallpaperHostInitialized = true;
            }

            return wallpaperPath;
        }
        catch (Exception ex)
        {
            ShellLogger.Error(nameof(ShellApi), "Error ensuring wallpaper host mapping.", ex);
            return null;
        }
    }

    private static (string style, bool isTiled) TryGetWallpaperStyle()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var style = key?.GetValue("WallpaperStyle") as string ?? "10";
            var tile = key?.GetValue("TileWallpaper") as string ?? "0";

            var isTiled = tile == "1";
            string styleName = style switch
            {
                "0" when isTiled => "tile",
                "0" => "center",
                "2" => "stretch",
                "6" => "fit",
                "10" => "fill",
                "22" => "span",
                _ when isTiled => "tile",
                _ => "fill"
            };

            return (styleName, isTiled);
        }
        catch
        {
            return ("fill", false);
        }
    }

    private static string TryGetDesktopBackgroundColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors");
            var raw = key?.GetValue("Background") as string;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "#000000";
            }

            var parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 &&
                byte.TryParse(parts[0], out var r) &&
                byte.TryParse(parts[1], out var g) &&
                byte.TryParse(parts[2], out var b))
            {
                return $"#{r:X2}{g:X2}{b:X2}";
            }
        }
        catch
        {
            // Ignore errors
        }

        return "#000000";
    }

    private static string? TryGetDesktopItemIconBase64(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var expandedPath = Environment.ExpandEnvironmentVariables(path);

            // Detect folders and use a dedicated folder icon path so directories
            // get the expected shell folder glyph instead of a generic document.
            try
            {
                var attributes = File.GetAttributes(expandedPath);
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    return TryGetFolderIconBase64(expandedPath);
                }
            }
            catch
            {
                if (Directory.Exists(expandedPath))
                {
                    return TryGetFolderIconBase64(expandedPath);
                }
            }

            using var icon = Icon.ExtractAssociatedIcon(expandedPath);
            if (icon == null)
            {
                return null;
            }

            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            // If we can't resolve an icon, fall back to text-only representation on the UI side.
            return null;
        }
    }

    private static string? TryGetFolderIconBase64(string folderPath)
    {
        try
        {
            var info = new SHFILEINFO();

            var result = SHGetFileInfo(
                folderPath,
                FileAttributes.Directory,
                ref info,
                (uint)Marshal.SizeOf(info),
                SHGFI_ICON | SHGFI_LARGEICON);

            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            using var icon = Icon.FromHandle(info.hIcon);
            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            DestroyIcon(info.hIcon);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetRecycleBinIconBase64()
    {
        try
        {
            // CLSID for the Windows Recycle Bin shell folder.
            const string recycleBinClsid = "{645FF040-5081-101B-9F08-00AA002F954E}";

            using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{recycleBinClsid}\DefaultIcon");
            var raw = key?.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            // DefaultIcon entries are typically in the form:
            //   "%SystemRoot%\\System32\\imageres.dll,-54"
            // or similar DLL / EXE resource references.
            raw = raw.Trim();

            // Split on the last comma so paths containing commas are still handled.
            var commaIndex = raw.LastIndexOf(',');
            var pathPart = commaIndex >= 0 ? raw.Substring(0, commaIndex) : raw;
            var indexPart = commaIndex >= 0 ? raw.Substring(commaIndex + 1) : "0";

            // Strip any surrounding quotes from the path and expand environment variables.
            pathPart = pathPart.Trim().Trim('"');
            var iconPath = Environment.ExpandEnvironmentVariables(pathPart);

            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
            {
                return null;
            }

            if (!int.TryParse(indexPart.Trim(), out var iconIndex))
            {
                iconIndex = 0;
            }

            // Use ExtractIconEx so we respect the resource index from the DLL/EXE,
            // which ensures the icon matches the user's Windows version (e.g. Win11).
            var largeIcons = new IntPtr[1];
            var extracted = ExtractIconEx(iconPath, iconIndex, largeIcons, null, 1);
            if (extracted == 0 || largeIcons[0] == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                using var icon = Icon.FromHandle(largeIcons[0]);
                using var bitmap = icon.ToBitmap();
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
            finally
            {
                DestroyIcon(largeIcons[0]);
            }
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetWindowIconBase64(ShellWindow window)
    {
        try
        {
            if (window.ProcessId <= 0)
            {
                return null;
            }

            using var process = Process.GetProcessById(window.ProcessId);
            var executablePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            return TryGetDesktopItemIconBase64(executablePath);
        }
        catch
        {
            // Best-effort only; if we can't resolve a window icon,
            // the Web UI will fall back to its text-only badge.
            return null;
        }
    }

    private static string? GetLauncherIconBase64(LauncherApp app)
    {
        try
        {
            var candidatePath = !string.IsNullOrWhiteSpace(app.IconPath)
                ? app.IconPath
                : app.ExecutablePath;

            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                return null;
            }

            return TryGetDesktopItemIconBase64(candidatePath);
        }
        catch
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        FileAttributes dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint ExtractIconEx(
        string lpszFile,
        int nIconIndex,
        IntPtr[]? phiconLarge,
        IntPtr[]? phiconSmall,
        uint nIcons);
}
