# SHELL_SETUP.md

Shelled completely replaces `explorer.exe`, so always start in the safest setup first and only register it as the Winlogon shell for a **dedicated test account** when you are confident that everything works. This guide walks through the three supported modes.

## 1. Run Shelled on top of Explorer (Development Mode)

This mode launches the shell UI host in a regular desktop session while Explorer keeps running. It is the recommended workflow while developing.

1. **Build the solution**
   ```powershell
   dotnet build Shelled.sln
   ```
2. **Start the adapters + core host** (temporary placeholder)
   - Launch `Shell.Bridge.WebView` from Visual Studio **or** run:
     ```powershell
     cd src\Shell.Bridge.WebView\bin\Debug\net8.0-windows
     .\ShellUiHost.exe
     ```
   - Leave Explorer running. The host opens a borderless window on top of the desktop; use `Alt+Tab` to get back to Explorer when needed.
3. **Enable verbose logging** (optional)
   - Run the host from a console window so you can watch log output.
4. **Exit Shelled**
   - Use `Alt+F4` only after toggling the hidden close handler or close from Visual Studio.

### Tips
- When experimenting with registry-related code, set `SHELL_TEST_MODE=1` in your environment so that safety checks stay enabled.
- If the Web UI fails to load, the host shows a fallback HTML UI with a clock so you still have a visible desktop.

## 2. Register Shelled as the Winlogon Shell (Test User Only)

Use this only after verifying the build in development mode.

1. **Create a dedicated local user**, e.g. `ShelledTest`. This avoids locking out your main account.
2. **Log in as the test user** and confirm Explorer runs normally.
3. **Open an elevated PowerShell** and run the following script. It stores the existing shell value and sets the Shelled host as the new shell.
   ```powershell
   $shellKey = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\Winlogon'
   $backup = Get-ItemProperty -Path $shellKey -Name Shell -ErrorAction SilentlyContinue
   if ($backup) {
       Set-ItemProperty -Path $shellKey -Name 'ShellBackup' -Value $backup.Shell
   }

   $hostPath = "$(Resolve-Path ..\..\..\src\Shell.Bridge.WebView\bin\Release\net8.0-windows\ShellUiHost.exe)"
   Set-ItemProperty -Path $shellKey -Name Shell -Value $hostPath
   ```
4. **Sign out** and sign back in. Winlogon should now launch Shelled instead of Explorer.
5. **Verify recovery access** before experimenting: ensure `Ctrl+Alt+Del` → Task Manager → `File > Run new task` works; you can use it to start `explorer.exe` if needed.

### Safety Flags
- **Never** run this script with `SHELL_TEST_MODE=1`; the host intentionally disables shell registration while the flag is present.
- Keep a secondary admin account with Explorer configured so you can revert quickly.

## 3. Reverting to Explorer

If anything goes wrong, use one of the following approaches.

### Method A – From Shelled Session
1. Press `Ctrl+Alt+Del` → open **Task Manager**.
2. Run `explorer.exe` from `File > Run new task`. This restores the familiar desktop temporarily.
3. In the same elevated Task Manager, run PowerShell and restore the registry value:
   ```powershell
   $shellKey = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\Winlogon'
   $backup = Get-ItemProperty -Path $shellKey -Name 'ShellBackup' -ErrorAction SilentlyContinue
   if ($backup) {
       Set-ItemProperty -Path $shellKey -Name Shell -Value $backup.Shell
   } else {
       Remove-ItemProperty -Path $shellKey -Name Shell -ErrorAction SilentlyContinue
   }
   ```
4. Sign out/in or reboot.

### Method B – Safe Mode
1. Power-cycle the machine and hold **Shift** while clicking **Restart** to open the recovery environment.
2. Choose **Troubleshoot → Advanced options → Startup Settings → Restart**.
3. Press **4** to boot into Safe Mode (Explorer starts automatically).
4. Open `regedit` and navigate to `HKCU\Software\Microsoft\Windows NT\CurrentVersion\Winlogon` for the affected user profile.
5. Restore `Shell` to `explorer.exe` (or delete it) and reboot.

### Method C – Command Prompt from Recovery
1. Boot into the recovery environment.
2. Open **Command Prompt** and load the affected user's hive (if necessary) with `reg load`.
3. Use `reg add "HKU\TempHive\Software\Microsoft\Windows NT\CurrentVersion\Winlogon" /v Shell /t REG_SZ /d explorer.exe /f` to reset the shell.
4. Unload the hive and reboot.

## Additional Recommendations
- Keep a printed copy of the recovery instructions nearby before registering the shell.
- Version control any scripts you use to switch shells so you can review them before running elevated.
- Consider adding a global "panic" shortcut in future work (`SAFE-01`) that automatically launches Explorer.
- When building release binaries for shell registration, prefer `dotnet publish -c Release` to capture all dependencies next to `ShellUiHost.exe`.

Stay safe, and never test new shell builds on production machines.
