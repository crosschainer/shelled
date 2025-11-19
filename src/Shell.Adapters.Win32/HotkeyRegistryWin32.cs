using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Shell.Core;
using Shell.Core.Interfaces;
using Shell.Core.Models;

namespace Shell.Adapters.Win32;

/// <summary>
/// Win32 implementation of global hotkey registration using RegisterHotKey/UnregisterHotKey
/// </summary>
public class HotkeyRegistryWin32 : IHotkeyRegistry, IDisposable
{
    private readonly Dictionary<string, RegisteredHotkey> _registeredHotkeys = new();
    private readonly Dictionary<int, string> _hotkeyIdMap = new(); // atomId -> hotkeyId
    private IntPtr _messageWindow = IntPtr.Zero;
    private Win32Api.WndProc? _windowProc;
    private int _nextAtomId = 1000; // Start from 1000 to avoid conflicts
    private bool _disposed = false;

    private struct RegisteredHotkey
    {
        public string Id;
        public int AtomId;
        public int Modifiers;
        public int VirtualKey;
        public DateTime RegisteredAt;
    }

    /// <summary>
    /// Event fired when a registered hotkey is pressed
    /// </summary>
    public event Action<string>? HotkeyPressed;

    public HotkeyRegistryWin32()
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
            // Create a message-only window to receive hotkey notifications
            var hInstance = Win32Api.GetModuleHandle(null);
            _windowProc = WindowProc;

            var wndClass = new Win32Api.WNDCLASS
            {
                lpfnWndProc = _windowProc,
                hInstance = hInstance,
                lpszClassName = "ShelledHotkeyRegistry"
            };

            var classAtom = Win32Api.RegisterClass(ref wndClass);
            if (classAtom == 0)
            {
                throw new InvalidOperationException("Failed to register window class for hotkey registry");
            }

            _messageWindow = Win32Api.CreateWindowEx(
                0,
                "ShelledHotkeyRegistry",
                "Shelled Hotkey Registry",
                0,
                0, 0, 0, 0,
                Win32Api.HWND_MESSAGE, // Message-only window
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero);

            if (_messageWindow == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create message window for hotkey registry");
            }
        }
        catch (Exception ex) when (ShellConfiguration.IsTestMode)
        {
            // In test mode, we might be on a non-Windows system
            // Log the error but don't throw
            System.Diagnostics.Debug.WriteLine($"Failed to initialize hotkey message window (test mode): {ex.Message}");
        }
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        const uint WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY)
        {
            var atomId = wParam.ToInt32();
            if (_hotkeyIdMap.TryGetValue(atomId, out var hotkeyId))
            {
                HotkeyPressed?.Invoke(hotkeyId);
            }
            return IntPtr.Zero;
        }

        return Win32Api.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Register a global hotkey
    /// </summary>
    /// <param name="id">Unique identifier for the hotkey</param>
    /// <param name="modifiers">Modifier keys (MOD_ALT, MOD_CONTROL, etc.)</param>
    /// <param name="virtualKey">Virtual key code</param>
    /// <returns>True if registration succeeded</returns>
    public bool RegisterHotkey(string id, int modifiers, int virtualKey)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Hotkey ID cannot be null or empty", nameof(id));

        if (_registeredHotkeys.ContainsKey(id))
        {
            // Already registered
            return false;
        }

        var safeMode = ShellConfiguration.DisableDangerousOperations ||
                       !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                       _messageWindow == IntPtr.Zero;

        if (safeMode)
        {
            // In test mode or on non-Windows platforms, just track the registration without calling Win32 APIs
            var testHotkey = new RegisteredHotkey
            {
                Id = id,
                AtomId = _nextAtomId++,
                Modifiers = modifiers,
                VirtualKey = virtualKey,
                RegisteredAt = DateTime.UtcNow
            };
            _registeredHotkeys[id] = testHotkey;
            _hotkeyIdMap[testHotkey.AtomId] = id;
            return true;
        }

        var atomId = _nextAtomId++;
        
        try
        {
            var success = Win32Api.RegisterHotKey(_messageWindow, atomId, (uint)modifiers, (uint)virtualKey);
            if (success)
            {
                var hotkey = new RegisteredHotkey
                {
                    Id = id,
                    AtomId = atomId,
                    Modifiers = modifiers,
                    VirtualKey = virtualKey,
                    RegisteredAt = DateTime.UtcNow
                };
                _registeredHotkeys[id] = hotkey;
                _hotkeyIdMap[atomId] = id;
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to register hotkey {id}: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Unregister a global hotkey
    /// </summary>
    /// <param name="id">Unique identifier for the hotkey</param>
    /// <returns>True if unregistration succeeded</returns>
    public bool UnregisterHotkey(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (!_registeredHotkeys.TryGetValue(id, out var hotkey))
        {
            // Not registered
            return false;
        }

        var safeMode = ShellConfiguration.DisableDangerousOperations ||
                       !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                       _messageWindow == IntPtr.Zero;

        if (safeMode)
        {
            // In test mode or on non-Windows platforms, just remove from tracking
            _registeredHotkeys.Remove(id);
            _hotkeyIdMap.Remove(hotkey.AtomId);
            return true;
        }

        try
        {
            var success = Win32Api.UnregisterHotKey(_messageWindow, hotkey.AtomId);
            // Keep local tracking consistent even if Win32 fails,
            // so callers don't get stuck with phantom registrations.
            _registeredHotkeys.Remove(id);
            _hotkeyIdMap.Remove(hotkey.AtomId);
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to unregister hotkey {id}: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Get all currently registered hotkeys (for testing/debugging)
    /// </summary>
    public IEnumerable<string> GetRegisteredHotkeyIds()
    {
        return _registeredHotkeys.Keys;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Unregister all hotkeys
        var hotkeyIds = _registeredHotkeys.Keys.ToList();
        foreach (var id in hotkeyIds)
        {
            UnregisterHotkey(id);
        }

        // Destroy message window
        if (_messageWindow != IntPtr.Zero && !ShellConfiguration.DisableDangerousOperations)
        {
            try
            {
                Win32Api.DestroyWindow(_messageWindow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to destroy hotkey message window: {ex.Message}");
            }
            _messageWindow = IntPtr.Zero;
        }

        _disposed = true;
    }
}
