using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Shell.Core;
using Shell.Core.Interfaces;

namespace Shell.Bridge.WebView;

public partial class ShellUiHostForm : Form
{
    private const uint SPI_SETWORKAREA = 0x002F;
    private const uint SPI_GETWORKAREA = 0x0030;
    private const uint SPI_GETDESKWALLPAPER = 0x0073;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const int HTTRANSPARENT = -1;
    // Approximate reserved areas matching the top panel and taskbar heights.
    private const int TopReservedPixels = 64;
    private const int BottomReservedPixels = 88;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private WebView2 webView = null!;
    private ShellCore? _shellCore;
    private IEventPublisher? _eventPublisher;
    private ShellApi? _shellApi;
    private RECT _originalWorkArea;
    private bool _hasOriginalWorkArea;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, uint fWinIni);

    public ShellUiHostForm()
    {
        InitializeComponent();
        InitializeAsync();
    }

    public ShellUiHostForm(ShellCore shellCore, IEventPublisher eventPublisher)
    {
        _shellCore = shellCore ?? throw new ArgumentNullException(nameof(shellCore));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        InitializeComponent();
        InitializeAsync();
    }

    private void InitializeComponent()
    {
        // Configure the form. In dev/test mode it's windowed.
        // In full shell-replacement mode we use a borderless fullscreen form,
        // but we do not keep it TopMost so normal application windows can
        // appear between the top panel and taskbar.
        if (Shell.Core.ShellConfiguration.IsTestMode || Shell.Core.ShellConfiguration.IsDevMode)
        {
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Normal;
            this.TopMost = false;
            this.ShowInTaskbar = true;
            this.Width = 1280;
            this.Height = 720;
        }
        else
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = false;
            this.ShowInTaskbar = false;
        }
        this.Text = "Shelled UI Host";
        
        // Create and configure WebView2
        webView = new WebView2()
        {
            Dock = DockStyle.Fill
        };
        
        this.Controls.Add(webView);
        
        // Handle form events
        this.Load += ShellUiHostForm_Load;
        this.FormClosing += ShellUiHostForm_FormClosing;
    }

    private async void InitializeAsync()
    {
        try
        {
            // Initialize WebView2
            await webView.EnsureCoreWebView2Async(null);
            
            // Configure WebView2 settings
            webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = true; // Enable for development
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
            
            // Set up navigation event handlers
            webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            webView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
            
            // Load the shell UI
            await LoadShellUI();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "Shelled UI Host Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    }

    private async Task LoadShellUI()
    {
        try
        {
            // Look for the Shell.UI.Web build output
            var uiPath = FindShellUIPath();
            
            if (string.IsNullOrEmpty(uiPath))
            {
                // Fallback: create a simple HTML page
                webView.NavigateToString(CreateFallbackHTML());
                return;
            }

            var indexPath = Path.Combine(uiPath, "index.html");
            if (File.Exists(indexPath))
            {
                // Preconfigure wallpaper virtual host mapping before navigating
                // so the first wallpaper request from the Web UI succeeds.
                TrySetupWallpaperHostMapping(webView.CoreWebView2);

                // Use a virtual host mapping so the UI is served from a
                // synthetic https origin instead of file:// to avoid CORS
                // issues with ES module scripts.
                const string hostName = "app.shelled";

                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    hostName,
                    uiPath,
                    CoreWebView2HostResourceAccessKind.Allow);

                webView.CoreWebView2.Navigate($"https://{hostName}/index.html");
            }
            else
            {
                webView.NavigateToString(CreateFallbackHTML());
            }
        }
        catch (Exception ex)
        {
            webView.NavigateToString(CreateErrorHTML(ex.Message));
        }
    }

    private string? FindShellUIPath()
    {
        // Look for Shell.UI.Web build output in common locations
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var possiblePaths = new[]
        {
            Path.Combine(baseDir, "..", "..", "..", "..", "Shell.UI.Web", "dist"),
            Path.Combine(baseDir, "..", "..", "..", "..", "Shell.UI.Web", "build"),
            Path.Combine(baseDir, "..", "..", "..", "..", "Shell.UI.Web", "src"),
            Path.Combine(baseDir, "ui"),
            Path.Combine(baseDir, "web")
        };

        foreach (var path in possiblePaths)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Ignore path resolution errors
            }
        }

        return null;
    }

    private string CreateFallbackHTML()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <title>Shelled - Shell Replacement</title>
    <style>
        body {
            margin: 0;
            padding: 0;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            height: 100vh;
            display: flex;
            flex-direction: column;
        }
        .top-panel {
            height: 40px;
            background: rgba(0,0,0,0.2);
            display: flex;
            align-items: center;
            justify-content: flex-end;
            padding: 0 20px;
        }
        .main-content {
            flex: 1;
            display: flex;
            align-items: center;
            justify-content: center;
            flex-direction: column;
        }
        .bottom-panel {
            height: 60px;
            background: rgba(0,0,0,0.3);
            display: flex;
            align-items: center;
            padding: 0 20px;
        }
        .logo {
            font-size: 48px;
            font-weight: bold;
            margin-bottom: 20px;
        }
        .status {
            font-size: 18px;
            opacity: 0.8;
        }
        .taskbar {
            display: flex;
            gap: 10px;
        }
        .taskbar-item {
            background: rgba(255,255,255,0.2);
            padding: 8px 16px;
            border-radius: 4px;
            cursor: pointer;
            transition: background 0.2s;
        }
        .taskbar-item:hover {
            background: rgba(255,255,255,0.3);
        }
        .clock {
            font-size: 14px;
            font-weight: bold;
        }
    </style>
</head>
<body>
    <div class='top-panel'>
        <div class='clock' id='clock'></div>
    </div>
    <div class='main-content'>
        <div class='logo'>Shelled</div>
        <div class='status'>Shell UI Host is running</div>
        <div class='status'>WebView2 initialized successfully</div>
    </div>
    <div class='bottom-panel'>
        <div class='taskbar'>
            <div class='taskbar-item'>UI Host</div>
        </div>
    </div>
    <script>
        function updateClock() {
            const now = new Date();
            document.getElementById('clock').textContent = now.toLocaleTimeString();
        }
        setInterval(updateClock, 1000);
        updateClock();
        
        console.log('Shelled UI Host loaded successfully');
    </script>
</body>
</html>";
    }

    private string CreateErrorHTML(string error)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <title>Shelled - Error</title>
    <style>
        body {{
            margin: 0;
            padding: 20px;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: #f44336;
            color: white;
            height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            flex-direction: column;
        }}
        .error-title {{
            font-size: 32px;
            font-weight: bold;
            margin-bottom: 20px;
        }}
        .error-message {{
            font-size: 16px;
            background: rgba(0,0,0,0.2);
            padding: 20px;
            border-radius: 8px;
            max-width: 600px;
            word-wrap: break-word;
        }}
    </style>
</head>
<body>
    <div class='error-title'>Shelled UI Host Error</div>
    <div class='error-message'>{error}</div>
</body>
</html>";
    }

    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            Console.WriteLine("Shell UI loaded successfully");
        }
        else
        {
            Console.WriteLine($"Navigation failed: {e.WebErrorStatus}");
        }
    }

    private void CoreWebView2_DOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
    {
        Console.WriteLine("Shell UI DOM content loaded");
        InitializeBridgeApi();
    }

    private void InitializeBridgeApi()
    {
        try
        {
            if (_shellCore != null && _eventPublisher != null)
            {
                // Create and expose the ShellApi bridge object
                _shellApi = new ShellApi(_shellCore, webView.CoreWebView2, _eventPublisher);
                webView.CoreWebView2.AddHostObjectToScript("shell", _shellApi);

                // Pre-initialize wallpaper host mapping so the first background
                // load in the Web UI has a valid virtual host.
                _shellApi.InitializeDesktopBackgroundHost();
                
                Console.WriteLine("ShellApi bridge initialized successfully");
                
                // Send initial connection event to UI
                var connectionMessage = new
                {
                    type = "connected",
                    data = new { status = "Bridge API initialized" },
                    timestamp = DateTime.UtcNow.ToString("O")
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(connectionMessage, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
                
                webView.CoreWebView2.PostWebMessageAsString(json);
            }
            else
            {
                Console.WriteLine("ShellCore or EventPublisher not available - running in fallback mode");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing bridge API: {ex.Message}");
        }
    }

    private void ShellUiHostForm_Load(object? sender, EventArgs e)
    {
        // Ensure the form covers the entire screen
        this.Bounds = Screen.PrimaryScreen.Bounds;

        // Reserve space for the top panel and taskbar so maximized windows
        // do not overlap them when running as the real shell.
        if (!Shell.Core.ShellConfiguration.DisableDangerousOperations)
        {
            TryReserveWorkAreaForShellChrome();
        }
    }

    private void ShellUiHostForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // Restore the original work area when the shell host is closing.
        RestoreOriginalWorkArea();

        // Cleanup resources
        _shellApi?.Dispose();
        webView?.Dispose();
    }

    protected override void SetVisibleCore(bool value)
    {
        // Ensure the form is always visible (can't be minimized)
        base.SetVisibleCore(true);
    }

    // Handle Alt+F4 and other close attempts
    protected override void WndProc(ref Message m)
    {
        const int WM_SYSCOMMAND = 0x0112;
        const int SC_CLOSE = 0xF060;

        if (m.Msg == WM_SYSCOMMAND && (int)m.WParam == SC_CLOSE)
        {
            // Prevent closing via Alt+F4 or system menu
            // TODO: In production, this should be configurable or have a special key combination
            return;
        }

        base.WndProc(ref m);
    }

    private void TryReserveWorkAreaForShellChrome()
    {
        try
        {
            var currentWorkArea = new RECT();
            if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref currentWorkArea, 0))
            {
                _originalWorkArea = currentWorkArea;
                _hasOriginalWorkArea = true;
            }

            var screenBounds = Screen.PrimaryScreen.Bounds;

            var newWorkArea = new RECT
            {
                Left = screenBounds.Left,
                Top = screenBounds.Top + TopReservedPixels,
                Right = screenBounds.Right,
                Bottom = screenBounds.Bottom - BottomReservedPixels
            };

            if (newWorkArea.Bottom <= newWorkArea.Top)
            {
                // Avoid applying an invalid work area.
                return;
            }

            SystemParametersInfo(
                SPI_SETWORKAREA,
                0,
                ref newWorkArea,
                SPIF_SENDCHANGE | SPIF_UPDATEINIFILE);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update work area for shell chrome: {ex.Message}");
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
            // Ignore and fall back to registry
        }

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
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

    private void TrySetupWallpaperHostMapping(CoreWebView2 coreWebView2)
    {
        try
        {
            var wallpaperPath = TryGetCurrentWallpaperPath();
            if (string.IsNullOrWhiteSpace(wallpaperPath) || !File.Exists(wallpaperPath))
            {
                return;
            }

            var folder = Path.GetDirectoryName(wallpaperPath)!;
            const string wallpaperHostName = "wallpaper.shelled";

            coreWebView2.SetVirtualHostNameToFolderMapping(
                wallpaperHostName,
                folder,
                CoreWebView2HostResourceAccessKind.Allow);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set up wallpaper host mapping: {ex.Message}");
        }
    }

    private void RestoreOriginalWorkArea()
    {
        if (!_hasOriginalWorkArea || Shell.Core.ShellConfiguration.DisableDangerousOperations)
        {
            return;
        }

        try
        {
            var workArea = _originalWorkArea;
            SystemParametersInfo(
                SPI_SETWORKAREA,
                0,
                ref workArea,
                SPIF_SENDCHANGE | SPIF_UPDATEINIFILE);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restore original work area: {ex.Message}");
        }
    }
}
