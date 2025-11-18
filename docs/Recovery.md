# Recovery Guide for Shelled

Shelled replaces `explorer.exe`, so you should always know how to recover control of the desktop if something goes wrong. This
playbook walks through the supported recovery paths and highlights the safeguards that keep your system usable.

## 1. Quick Recovery (Start Explorer Manually)

1. Press <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Esc</kbd> to open **Task Manager**.
2. Choose **File → Run new task**.
3. Type `explorer.exe`, check **Create this task with administrative privileges**, and click **OK**.
4. Explorer will relaunch and you can inspect or uninstall Shelled safely.

> Tip: when developing Shelled, keep a PowerShell window open with `Start-Process explorer.exe` ready so you can relaunch the
> default shell instantly.

## 2. Boot into Safe Mode

If the desktop never appears or input is unresponsive, reboot the PC in Safe Mode and restore Explorer from there.

1. Hold **Shift** while clicking **Restart** from the Windows login screen.
2. Navigate to **Troubleshoot → Advanced options → Startup Settings → Restart**.
3. Press **4** (or **F4**) to boot into **Safe Mode**.
4. After signing in, Safe Mode loads Explorer automatically. Continue with the registry recovery steps below.

## 3. Reset the Winlogon Shell Registry Value

Shelled registers itself by writing to the per-user Winlogon shell value at `HKCU\Software\Microsoft\Windows NT\CurrentVersion\Winlogon`.
To revert to Explorer:

1. Launch **regedit.exe** (from Safe Mode or an Explorer session started manually).
2. Navigate to `HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Winlogon`.
3. Find the `Shell` value. If it references Shelled (e.g., `myshell-bootstrap.exe`), double-click it and change the value to
   `explorer.exe`.
4. Close the Registry Editor and sign out/in (or reboot) to apply the change.

### Using PowerShell

```powershell
$shellKey = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\Winlogon'
Set-ItemProperty -Path $shellKey -Name Shell -Value 'explorer.exe'
```

## 4. Recovering Another User Profile

If the affected account cannot sign in, sign in as an administrator and load the broken user hive manually:

1. Open **Registry Editor** and select `HKEY_USERS`.
2. Choose **File → Load Hive...** and open `%SystemDrive%\Users\BrokenUser\NTUSER.DAT`.
3. Enter a temporary name (e.g., `TempHive`).
4. Navigate to `HKEY_USERS\TempHive\Software\Microsoft\Windows NT\CurrentVersion\Winlogon` and change the `Shell` value to
   `explorer.exe`.
5. Select `TempHive`, choose **File → Unload Hive...**, and confirm.
6. Reboot and sign in as the recovered user.

## 5. Preventive Safeguards

- **Test Mode**: Export `SHELL_TEST_MODE=1` when running automated tests or experimenting locally. Shelled code paths that touch
  the registry or replace Explorer check this flag and skip dangerous operations.
- **Development Mode**: Run Shelled alongside Explorer until you fully trust your build.
- **Panic Plan**: Keep instructions above nearby (printed or on another device) so you can recover even if WebView2 UI is frozen.

Document last updated whenever you make shell-related changes. Treat recovery as part of your release checklist.
