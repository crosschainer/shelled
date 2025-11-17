using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Shell.Core;
using Shell.Core.Interfaces;
using Shell.Core.Models;

namespace Shell.Adapters.Win32;

/// <summary>
/// Win32 implementation of the window system interface
/// </summary>
public class WindowSystemWin32 : IWindowSystem, IDisposable
{
    private readonly List<IntPtr> _eventHooks = new();
    private readonly Win32Api.WinEventDelegate _winEventDelegate;
    private bool _disposed = false;

    public event Action<IntPtr>? WindowCreated;
    public event Action<IntPtr>? WindowDestroyed;
    public event Action<IntPtr>? WindowActivated;
    public event Action<IntPtr>? WindowUpdated;

    public WindowSystemWin32()
    {
        _winEventDelegate = WinEventProc;
        
        if (!ShellConfiguration.DisableDangerousOperations)
        {
            SetupWindowHooks();
        }
    }

    public IEnumerable<ShellWindow> EnumWindows()
    {
        var windows = new List<ShellWindow>();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Win32Api.EnumWindows((hWnd, lParam) =>
            {
                if (IsTopLevelWindow(hWnd))
                {
                    var windowInfo = GetWindowInfo(hWnd);
                    if (windowInfo != null)
                    {
                        windows.Add(windowInfo);
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        
        return windows;
    }

    public bool IsTopLevelWindow(IntPtr hwnd)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        // Check if window is valid
        if (!Win32Api.IsWindow(hwnd))
            return false;

        // Check if window has no parent (top-level)
        var parent = Win32Api.GetParent(hwnd);
        if (parent != IntPtr.Zero)
            return false;

        // Check if window has no owner
        var owner = Win32Api.GetWindow(hwnd, Win32Api.GW_OWNER);
        if (owner != IntPtr.Zero)
            return false;

        // Check if window is visible
        if (!Win32Api.IsWindowVisible(hwnd))
            return false;

        // Get window title - skip windows without titles
        var titleLength = Win32Api.GetWindowTextLength(hwnd);
        if (titleLength == 0)
            return false;

        return true;
    }

    public ShellWindow? GetWindowInfo(IntPtr hwnd)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        if (!Win32Api.IsWindow(hwnd))
            return null;

        try
        {
            // Get window title
            var titleLength = Win32Api.GetWindowTextLength(hwnd);
            var title = string.Empty;
            if (titleLength > 0)
            {
                var titleBuilder = new StringBuilder(titleLength + 1);
                Win32Api.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
                title = titleBuilder.ToString();
            }

            // Get window class name
            var classNameBuilder = new StringBuilder(256);
            Win32Api.GetClassName(hwnd, classNameBuilder, classNameBuilder.Capacity);
            var className = classNameBuilder.ToString();

            // Get process ID
            Win32Api.GetWindowThreadProcessId(hwnd, out var processId);

            // Determine window state
            var windowState = WindowState.Normal;
            if (!Win32Api.IsWindowVisible(hwnd))
            {
                windowState = WindowState.Hidden;
            }
            // Note: Additional state detection (minimized/maximized) would require more Win32 calls

            return new ShellWindow
            {
                Handle = hwnd,
                Title = title,
                ClassName = className,
                ProcessId = (int)processId,
                State = windowState,
                IsVisible = Win32Api.IsWindowVisible(hwnd),
                WorkspaceId = "default", // Will be assigned by ShellCore
                AppId = className // Simple app ID based on class name
            };
        }
        catch (Exception ex)
        {
            // Log error in real implementation
            if (ShellConfiguration.VerboseLogging)
            {
                Console.WriteLine($"Error getting window info for {hwnd}: {ex.Message}");
            }
            return null;
        }
    }

    public void ShowWindow(IntPtr hwnd, WindowState state)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        if (ShellConfiguration.DisableDangerousOperations)
        {
            if (ShellConfiguration.VerboseLogging)
            {
                Console.WriteLine($"ShowWindow blocked in safe mode: {hwnd} -> {state}");
            }
            return;
        }

        var nCmdShow = state switch
        {
            WindowState.Normal => Win32Api.SW_SHOWNORMAL,
            WindowState.Minimized => Win32Api.SW_SHOWMINIMIZED,
            WindowState.Maximized => Win32Api.SW_SHOWMAXIMIZED,
            WindowState.Hidden => Win32Api.SW_HIDE,
            _ => Win32Api.SW_SHOWNORMAL
        };

        Win32Api.ShowWindow(hwnd, nCmdShow);
    }

    public void SetForegroundWindow(IntPtr hwnd)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        if (ShellConfiguration.DisableDangerousOperations)
        {
            if (ShellConfiguration.VerboseLogging)
            {
                Console.WriteLine($"SetForegroundWindow blocked in safe mode: {hwnd}");
            }
            return;
        }

        Win32Api.SetForegroundWindow(hwnd);
    }

    public bool IsVisible(IntPtr hwnd)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        return Win32Api.IsWindow(hwnd) && Win32Api.IsWindowVisible(hwnd);
    }

    private void SetupWindowHooks()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            // Hook window creation events
            var createHook = Win32Api.SetWinEventHook(
                Win32Api.EVENT_OBJECT_CREATE,
                Win32Api.EVENT_OBJECT_CREATE,
                IntPtr.Zero,
                _winEventDelegate,
                0, 0,
                Win32Api.WINEVENT_OUTOFCONTEXT | Win32Api.WINEVENT_SKIPOWNPROCESS);

            if (createHook != IntPtr.Zero)
                _eventHooks.Add(createHook);

            // Hook window destruction events
            var destroyHook = Win32Api.SetWinEventHook(
                Win32Api.EVENT_OBJECT_DESTROY,
                Win32Api.EVENT_OBJECT_DESTROY,
                IntPtr.Zero,
                _winEventDelegate,
                0, 0,
                Win32Api.WINEVENT_OUTOFCONTEXT | Win32Api.WINEVENT_SKIPOWNPROCESS);

            if (destroyHook != IntPtr.Zero)
                _eventHooks.Add(destroyHook);

            // Hook foreground window changes
            var foregroundHook = Win32Api.SetWinEventHook(
                Win32Api.EVENT_SYSTEM_FOREGROUND,
                Win32Api.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventDelegate,
                0, 0,
                Win32Api.WINEVENT_OUTOFCONTEXT | Win32Api.WINEVENT_SKIPOWNPROCESS);

            if (foregroundHook != IntPtr.Zero)
                _eventHooks.Add(foregroundHook);

            // Hook window name/state changes
            var nameChangeHook = Win32Api.SetWinEventHook(
                Win32Api.EVENT_OBJECT_NAMECHANGE,
                Win32Api.EVENT_OBJECT_NAMECHANGE,
                IntPtr.Zero,
                _winEventDelegate,
                0, 0,
                Win32Api.WINEVENT_OUTOFCONTEXT | Win32Api.WINEVENT_SKIPOWNPROCESS);

            if (nameChangeHook != IntPtr.Zero)
                _eventHooks.Add(nameChangeHook);

            var stateChangeHook = Win32Api.SetWinEventHook(
                Win32Api.EVENT_OBJECT_STATECHANGE,
                Win32Api.EVENT_OBJECT_STATECHANGE,
                IntPtr.Zero,
                _winEventDelegate,
                0, 0,
                Win32Api.WINEVENT_OUTOFCONTEXT | Win32Api.WINEVENT_SKIPOWNPROCESS);

            if (stateChangeHook != IntPtr.Zero)
                _eventHooks.Add(stateChangeHook);
        }
        catch (Exception ex)
        {
            if (ShellConfiguration.VerboseLogging)
            {
                Console.WriteLine($"Error setting up window hooks: {ex.Message}");
            }
        }
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Only process window events (idObject == 0)
        if (idObject != 0 || hwnd == IntPtr.Zero)
            return;

        try
        {
            switch (eventType)
            {
                case Win32Api.EVENT_OBJECT_CREATE:
                    // Only fire for top-level windows
                    if (IsTopLevelWindow(hwnd))
                    {
                        WindowCreated?.Invoke(hwnd);
                    }
                    break;

                case Win32Api.EVENT_OBJECT_DESTROY:
                    WindowDestroyed?.Invoke(hwnd);
                    break;

                case Win32Api.EVENT_SYSTEM_FOREGROUND:
                    WindowActivated?.Invoke(hwnd);
                    break;

                case Win32Api.EVENT_OBJECT_NAMECHANGE:
                case Win32Api.EVENT_OBJECT_STATECHANGE:
                    // Only fire for windows we're tracking
                    if (Win32Api.IsWindow(hwnd))
                    {
                        WindowUpdated?.Invoke(hwnd);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            if (ShellConfiguration.VerboseLogging)
            {
                Console.WriteLine($"Error in WinEventProc: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Unhook all event hooks
        foreach (var hook in _eventHooks)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Win32Api.UnhookWinEvent(hook);
            }
        }
        _eventHooks.Clear();

        _disposed = true;
    }
}