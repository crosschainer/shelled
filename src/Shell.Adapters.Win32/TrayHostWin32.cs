using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Shell.Core;
using Shell.Core.Models;
using Shell.Core.Interfaces;

namespace Shell.Adapters.Win32;

/// <summary>
/// Win32 implementation of ITrayHost using Shell_NotifyIcon APIs
/// </summary>
public class TrayHostWin32 : ITrayHost, IDisposable
{
    private readonly ConcurrentDictionary<string, TrayIconData> _trayIcons = new();
    private IntPtr _messageWindow;
    private Win32Api.WndProc? _windowProc;
    private bool _disposed;
    private uint _nextIconId = 1;

    // Events
    public event Action<TrayIcon>? TrayIconAdded;
    public event Action<TrayIcon>? TrayIconUpdated;
    public event Action<string>? TrayIconRemoved;
    public event Action<string, TrayClickType>? TrayIconClicked;
    public event Action<string, TrayBalloonInfo>? TrayBalloonShown;
    public event Action<string>? TrayBalloonClicked;
    public event Action<string, string>? TrayMenuItemClicked;

    private class TrayIconData
    {
        public uint IconId { get; set; }
        public TrayIcon TrayIcon { get; set; } = null!;
        public Win32Api.NOTIFYICONDATA NotifyIconData { get; set; }
    }

    public TrayHostWin32()
    {
        if (!ShellConfiguration.DisableDangerousOperations)
        {
            InitializeMessageWindow();
        }
    }

    private void InitializeMessageWindow()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On non-Windows platforms, skip Win32 initialization
            return;
        }

        try
        {
            // Create a message-only window to receive tray icon notifications
            var hInstance = Win32Api.GetModuleHandle(null);
            _windowProc = WindowProc;

            var wndClass = new Win32Api.WNDCLASS
            {
                lpfnWndProc = _windowProc,
                hInstance = hInstance,
                lpszClassName = "ShelledTrayHost"
            };

            var classAtom = Win32Api.RegisterClass(ref wndClass);
            if (classAtom == 0)
            {
                throw new InvalidOperationException("Failed to register window class for tray host");
            }

            _messageWindow = Win32Api.CreateWindowEx(
                0, // dwExStyle
                "ShelledTrayHost", // lpClassName
                "Shelled Tray Host", // lpWindowName
                0, // dwStyle (message-only window)
                0, 0, 0, 0, // position and size (ignored for message-only)
                new IntPtr(-3), // HWND_MESSAGE (message-only window)
                IntPtr.Zero, // hMenu
                hInstance, // hInstance
                IntPtr.Zero); // lpParam

            if (_messageWindow == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create message window for tray host");
            }
        }
        catch (Exception ex)
        {
            // In test mode or if initialization fails, we'll work in safe mode
            Console.WriteLine($"TrayHostWin32 initialization failed (safe mode): {ex.Message}");
        }
    }

    private IntPtr WindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
    {
        if (uMsg == Win32Api.WM_TRAYICON)
        {
            HandleTrayIconMessage(wParam, lParam);
        }

        return Win32Api.DefWindowProc(hWnd, uMsg, wParam, lParam);
    }

    private void HandleTrayIconMessage(IntPtr wParam, IntPtr lParam)
    {
        var iconId = (uint)wParam.ToInt32();
        var message = (uint)lParam.ToInt32();

        // Find the tray icon by ID
        var trayIconData = FindTrayIconById(iconId);
        if (trayIconData == null) return;

        var trayIcon = trayIconData.TrayIcon;

        switch (message)
        {
            case Win32Api.WM_LBUTTONUP:
                TrayIconClicked?.Invoke(trayIcon.Id, TrayClickType.LeftClick);
                break;

            case Win32Api.WM_RBUTTONUP:
                TrayIconClicked?.Invoke(trayIcon.Id, TrayClickType.RightClick);
                break;

            case Win32Api.WM_LBUTTONDBLCLK:
                TrayIconClicked?.Invoke(trayIcon.Id, TrayClickType.DoubleClick);
                break;
        }
    }

    private TrayIconData? FindTrayIconById(uint iconId)
    {
        foreach (var kvp in _trayIcons)
        {
            if (kvp.Value.IconId == iconId)
                return kvp.Value;
        }
        return null;
    }

    public void AddTrayIcon(TrayIcon trayIcon)
    {
        if (trayIcon == null) throw new ArgumentNullException(nameof(trayIcon));

        if (ShellConfiguration.DisableDangerousOperations || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // In test mode or on non-Windows platforms, just track the icon and fire events
            var mockData = new TrayIconData
            {
                IconId = _nextIconId++,
                TrayIcon = trayIcon
            };
            _trayIcons[trayIcon.Id] = mockData;
            TrayIconAdded?.Invoke(trayIcon);
            return;
        }

        if (_messageWindow == IntPtr.Zero)
        {
            throw new InvalidOperationException("Tray host not properly initialized");
        }

        var iconId = _nextIconId++;
        var notifyIconData = new Win32Api.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<Win32Api.NOTIFYICONDATA>(),
            hWnd = _messageWindow,
            uID = iconId,
            uFlags = Win32Api.NIF_MESSAGE | Win32Api.NIF_TIP,
            uCallbackMessage = Win32Api.WM_TRAYICON,
            szTip = trayIcon.Tooltip ?? string.Empty
        };

        // Set icon if available
        if (trayIcon.IconHandle != IntPtr.Zero)
        {
            notifyIconData.hIcon = trayIcon.IconHandle;
            notifyIconData.uFlags |= Win32Api.NIF_ICON;
        }

        var success = Win32Api.Shell_NotifyIcon(Win32Api.NIM_ADD, ref notifyIconData);
        if (!success)
        {
            throw new InvalidOperationException($"Failed to add tray icon: {trayIcon.Id}");
        }

        var trayIconData = new TrayIconData
        {
            IconId = iconId,
            TrayIcon = trayIcon,
            NotifyIconData = notifyIconData
        };

        _trayIcons[trayIcon.Id] = trayIconData;
        TrayIconAdded?.Invoke(trayIcon);
    }

    public void RemoveTrayIcon(string trayIconId)
    {
        if (string.IsNullOrEmpty(trayIconId)) throw new ArgumentException("Tray icon ID cannot be null or empty", nameof(trayIconId));

        if (!_trayIcons.TryRemove(trayIconId, out var trayIconData))
        {
            return; // Icon not found
        }

        if (!ShellConfiguration.DisableDangerousOperations && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _messageWindow != IntPtr.Zero)
        {
            var notifyIconData = trayIconData.NotifyIconData;
            Win32Api.Shell_NotifyIcon(Win32Api.NIM_DELETE, ref notifyIconData);
        }

        TrayIconRemoved?.Invoke(trayIconId);
    }

    public void UpdateTrayIcon(TrayIcon trayIcon)
    {
        if (trayIcon == null) throw new ArgumentNullException(nameof(trayIcon));

        if (!_trayIcons.TryGetValue(trayIcon.Id, out var trayIconData))
        {
            throw new ArgumentException($"Tray icon not found: {trayIcon.Id}", nameof(trayIcon));
        }

        // Update our tracking
        trayIconData.TrayIcon = trayIcon;

        if (!ShellConfiguration.DisableDangerousOperations && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _messageWindow != IntPtr.Zero)
        {
            // Update the Win32 tray icon
            var notifyIconData = trayIconData.NotifyIconData;
            notifyIconData.szTip = trayIcon.Tooltip ?? string.Empty;
            notifyIconData.uFlags = Win32Api.NIF_MESSAGE | Win32Api.NIF_TIP;

            if (trayIcon.IconHandle != IntPtr.Zero)
            {
                notifyIconData.hIcon = trayIcon.IconHandle;
                notifyIconData.uFlags |= Win32Api.NIF_ICON;
            }

            Win32Api.Shell_NotifyIcon(Win32Api.NIM_MODIFY, ref notifyIconData);
            trayIconData.NotifyIconData = notifyIconData;
        }

        TrayIconUpdated?.Invoke(trayIcon);
    }

    public void ShowBalloonNotification(string trayIconId, TrayBalloonInfo balloonInfo)
    {
        if (string.IsNullOrEmpty(trayIconId)) throw new ArgumentException("Tray icon ID cannot be null or empty", nameof(trayIconId));
        if (balloonInfo == null) throw new ArgumentNullException(nameof(balloonInfo));

        if (!_trayIcons.TryGetValue(trayIconId, out var trayIconData))
        {
            throw new ArgumentException($"Tray icon not found: {trayIconId}", nameof(trayIconId));
        }

        // Update tray icon with balloon info
        trayIconData.TrayIcon.BalloonInfo = balloonInfo;

        if (!ShellConfiguration.DisableDangerousOperations && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _messageWindow != IntPtr.Zero)
        {
            var notifyIconData = trayIconData.NotifyIconData;
            notifyIconData.uFlags = Win32Api.NIF_INFO;
            notifyIconData.szInfo = balloonInfo.Text ?? string.Empty;
            notifyIconData.szInfoTitle = balloonInfo.Title ?? string.Empty;
            notifyIconData.uTimeoutOrVersion = (uint)Math.Max(0, balloonInfo.TimeoutMs);

            // Map balloon icon type
            notifyIconData.dwInfoFlags = balloonInfo.Icon switch
            {
                TrayBalloonIcon.Info => Win32Api.NIIF_INFO,
                TrayBalloonIcon.Warning => Win32Api.NIIF_WARNING,
                TrayBalloonIcon.Error => Win32Api.NIIF_ERROR,
                _ => Win32Api.NIIF_NONE
            };

            Win32Api.Shell_NotifyIcon(Win32Api.NIM_MODIFY, ref notifyIconData);
        }

        TrayBalloonShown?.Invoke(trayIconId, balloonInfo);
    }

    public void UpdateTrayIconMenu(string trayIconId, TrayMenu? menu)
    {
        if (string.IsNullOrEmpty(trayIconId)) throw new ArgumentException("Tray icon ID cannot be null or empty", nameof(trayIconId));

        if (!_trayIcons.TryGetValue(trayIconId, out var trayIconData))
        {
            throw new ArgumentException($"Tray icon not found: {trayIconId}", nameof(trayIconId));
        }

        // Update our tracking
        trayIconData.TrayIcon.Menu = menu;

        // Note: Win32 Shell_NotifyIcon doesn't directly handle menus - they're typically
        // shown in response to right-click events. The menu handling would be done
        // by the UI layer when it receives TrayIconClicked events.
    }

    /// <summary>
    /// Test helper to simulate a tray icon click without requiring Win32 messages.
    /// This is used by integration tests when running in non-Windows environments.
    /// </summary>
    /// <param name="trayIconId">The tray icon identifier to simulate a click for.</param>
    /// <param name="clickType">The type of click that occurred.</param>
    /// <exception cref="ArgumentException">Thrown when the tray icon ID is not known.</exception>
    public void SimulateTrayIconClicked(string trayIconId, TrayClickType clickType)
    {
        if (string.IsNullOrEmpty(trayIconId))
        {
            throw new ArgumentException("Tray icon ID cannot be null or empty", nameof(trayIconId));
        }

        if (!_trayIcons.ContainsKey(trayIconId))
        {
            throw new ArgumentException($"Tray icon not found: {trayIconId}", nameof(trayIconId));
        }

        TrayIconClicked?.Invoke(trayIconId, clickType);
    }

    public IEnumerable<TrayIcon> GetTrayIcons()
    {
        return _trayIcons.Values.Select(data => data.TrayIcon);
    }

    public void ShowBalloonNotification(string trayIconId, string title, string text, TrayBalloonIcon icon = TrayBalloonIcon.None, int timeoutMs = 5000)
    {
        var balloonInfo = new TrayBalloonInfo
        {
            Title = title,
            Text = text,
            Icon = icon,
            TimeoutMs = timeoutMs
        };
        ShowBalloonNotification(trayIconId, balloonInfo);
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Remove all tray icons
        foreach (var kvp in _trayIcons.ToList())
        {
            RemoveTrayIcon(kvp.Key);
        }

        // Clean up message window
        if (_messageWindow != IntPtr.Zero)
        {
            Win32Api.DestroyWindow(_messageWindow);
            _messageWindow = IntPtr.Zero;
        }

        // Unregister window class
        if (!ShellConfiguration.DisableDangerousOperations && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var hInstance = Win32Api.GetModuleHandle(null);
            Win32Api.UnregisterClass("ShelledTrayHost", hInstance);
        }

        _disposed = true;
    }
}