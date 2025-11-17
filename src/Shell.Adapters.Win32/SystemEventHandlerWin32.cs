using Shell.Core.Interfaces;
using System.Runtime.InteropServices;

namespace Shell.Adapters.Win32;

/// <summary>
/// Win32 implementation of system event handling
/// </summary>
public class SystemEventHandlerWin32 : ISystemEventHandler, IDisposable
{
    private IntPtr _messageWindow = IntPtr.Zero;
    private Win32Api.WndProc? _wndProc;
    private bool _isListening = false;
    private bool _disposed = false;

    public event Action<SystemEventType, SystemEventArgs>? SystemEventOccurred;

    public bool IsListening => _isListening;

    public SystemEventHandlerWin32()
    {
        InitializeMessageWindow();
    }

    public void StartListening()
    {
        if (_isListening || _disposed)
            return;

        _isListening = true;
    }

    public void StopListening()
    {
        if (!_isListening)
            return;

        _isListening = false;
    }

    private void InitializeMessageWindow()
    {
        try
        {
            // Create a message-only window to receive system events
            _wndProc = MessageWindowProc;
            
            var wndClass = new Win32Api.WNDCLASS
            {
                lpfnWndProc = _wndProc,
                hInstance = Win32Api.GetModuleHandle(null),
                lpszClassName = "ShelledSystemEventHandler"
            };

            var classAtom = Win32Api.RegisterClass(ref wndClass);
            if (classAtom == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != Win32Api.ERROR_CLASS_ALREADY_EXISTS)
                {
                    throw new InvalidOperationException($"Failed to register window class. Error: {error}");
                }
            }

            _messageWindow = Win32Api.CreateWindowEx(
                0,
                "ShelledSystemEventHandler",
                "Shelled System Event Handler",
                0,
                0, 0, 0, 0,
                Win32Api.HWND_MESSAGE, // Message-only window
                IntPtr.Zero,
                Win32Api.GetModuleHandle(null),
                IntPtr.Zero);

            if (_messageWindow == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to create message window. Error: {Marshal.GetLastWin32Error()}");
            }
        }
        catch (DllNotFoundException)
        {
            // Running on non-Windows platform - create a stub implementation
            _messageWindow = new IntPtr(1); // Non-zero to indicate "success"
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize system event handler", ex);
        }
    }

    private IntPtr MessageWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (!_isListening)
            return Win32Api.DefWindowProc(hWnd, msg, wParam, lParam);

        try
        {
            switch (msg)
            {
                case Win32Api.WM_QUERYENDSESSION:
                    HandleQueryEndSession(wParam, lParam);
                    return new IntPtr(1); // Allow shutdown by default
                
                case Win32Api.WM_ENDSESSION:
                    HandleEndSession(wParam, lParam);
                    break;
                
                case Win32Api.WM_POWERBROADCAST:
                    HandlePowerBroadcast(wParam, lParam);
                    break;
                
                case Win32Api.WM_DISPLAYCHANGE:
                    HandleDisplayChange();
                    break;
                
                case Win32Api.WM_WTSSESSION_CHANGE:
                    HandleSessionChange(wParam, lParam);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the message loop
            System.Diagnostics.Debug.WriteLine($"Error in system event handler: {ex}");
        }

        return Win32Api.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void HandleQueryEndSession(IntPtr wParam, IntPtr lParam)
    {
        var eventArgs = new SystemEventArgs
        {
            CanCancel = true,
            Cancel = false,
            Reason = GetShutdownReason(lParam)
        };

        SystemEventOccurred?.Invoke(SystemEventType.QueryEndSession, eventArgs);
    }

    private void HandleEndSession(IntPtr wParam, IntPtr lParam)
    {
        var isEnding = wParam.ToInt32() != 0;
        if (isEnding)
        {
            var eventArgs = new SystemEventArgs
            {
                Reason = GetShutdownReason(lParam)
            };

            SystemEventOccurred?.Invoke(SystemEventType.EndSession, eventArgs);
        }
    }

    private void HandlePowerBroadcast(IntPtr wParam, IntPtr lParam)
    {
        var powerEvent = wParam.ToInt32();
        switch (powerEvent)
        {
            case Win32Api.PBT_APMSUSPEND:
                SystemEventOccurred?.Invoke(SystemEventType.PowerSuspend, new SystemEventArgs());
                break;
            
            case Win32Api.PBT_APMRESUMEAUTOMATIC:
            case Win32Api.PBT_APMRESUMESUSPEND:
                SystemEventOccurred?.Invoke(SystemEventType.PowerResume, new SystemEventArgs());
                break;
        }
    }

    private void HandleDisplayChange()
    {
        SystemEventOccurred?.Invoke(SystemEventType.DisplaySettingsChanged, new SystemEventArgs());
    }

    private void HandleSessionChange(IntPtr wParam, IntPtr lParam)
    {
        var sessionEvent = wParam.ToInt32();
        switch (sessionEvent)
        {
            case Win32Api.WTS_SESSION_LOCK:
                SystemEventOccurred?.Invoke(SystemEventType.SessionLock, new SystemEventArgs());
                break;
            
            case Win32Api.WTS_SESSION_UNLOCK:
                SystemEventOccurred?.Invoke(SystemEventType.SessionUnlock, new SystemEventArgs());
                break;
        }
    }

    private string GetShutdownReason(IntPtr lParam)
    {
        var reason = lParam.ToInt32();
        return reason switch
        {
            Win32Api.ENDSESSION_CLOSEAPP => "Application close requested",
            Win32Api.ENDSESSION_CRITICAL => "Critical shutdown",
            Win32Api.ENDSESSION_LOGOFF => "User logoff",
            _ => "Unknown reason"
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopListening();

        if (_messageWindow != IntPtr.Zero && _messageWindow != new IntPtr(1))
        {
            try
            {
                Win32Api.DestroyWindow(_messageWindow);
            }
            catch (DllNotFoundException)
            {
                // Ignore on non-Windows platforms
            }
        }

        _messageWindow = IntPtr.Zero;
        _wndProc = null;
        _disposed = true;
    }
}