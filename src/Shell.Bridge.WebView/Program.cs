using System;
using System.Windows.Forms;

namespace Shell.Bridge.WebView;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        
        var mainForm = new ShellUiHostForm();
        Application.Run(mainForm);
    }
}
