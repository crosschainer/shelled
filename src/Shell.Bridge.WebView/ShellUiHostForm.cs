using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Shell.Bridge.WebView;

public partial class ShellUiHostForm : Form
{
    private WebView2 webView = null!;

    public ShellUiHostForm()
    {
        InitializeComponent();
        InitializeAsync();
    }

    private void InitializeComponent()
    {
        // Configure the form as borderless, fullscreen
        this.FormBorderStyle = FormBorderStyle.None;
        this.WindowState = FormWindowState.Maximized;
        this.TopMost = true;
        this.ShowInTaskbar = false;
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
            }
            else
            {
                var indexPath = Path.Combine(uiPath, "index.html");
                if (File.Exists(indexPath))
                {
                    webView.CoreWebView2.Navigate($"file:///{indexPath.Replace('\\', '/')}");
                }
                else
                {
                    webView.NavigateToString(CreateFallbackHTML());
                }
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
        // TODO: Initialize bridge API here
    }

    private void ShellUiHostForm_Load(object? sender, EventArgs e)
    {
        // Ensure the form covers the entire screen
        this.Bounds = Screen.PrimaryScreen.Bounds;
    }

    private void ShellUiHostForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // TODO: Cleanup resources, notify shell core of shutdown
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
}