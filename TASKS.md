# shelled - Shell Replacement – Tasks

> **Conventions for AI agent**
> - Each task line uses tags:
>   - `status:todo | in-progress | done | blocked`
>   - `blocker:...` (optional, short description)
> - AI agent can **only modify**:
>   - `[ ]` → `[x]`
>   - `status:...`
>   - `blocker:...`
> - Do **not** change task IDs (e.g. `CORE-01`).

---

## 0. High-Level Milestones

- [ ] `M-01` Define architecture & APIs  
  - `status:todo`

- [ ] `M-02` Implement minimal shell (core + basic UI)  
  - `status:todo`

- [ ] `M-03` Replace Explorer as shell for a test user  
  - `status:todo`

- [ ] `M-04` Implement full window management (workspaces, tray, etc.)  
  - `status:todo`

- [ ] `M-05` Testing suite (unit + integration + E2E) green  
  - `status:todo`

---

## 1. Repository & Project Setup

- [x] `SETUP-01` Create solution / workspace for:
  - `Shell.Core` (domain logic)
  - `Shell.Adapters.Win32` (OS adapters)
  - `Shell.Bridge.WebView` (native UI host + bridge)
  - `Shell.UI.Web` (HTML/CSS/JS frontend)
  - `Shell.Tests` (unit + integration)
  - `status:done`

- [x] `SETUP-02` Add test framework (e.g. xUnit/NUnit) and basic CI workflow (Windows runner)
  - `status:done`

- [x] `SETUP-03` Add “test mode” flag env var (`SHELL_TEST_MODE`) to disable shell registration and dangerous behavior in tests  
  - `status:done`

---

## 2. Shell Core – Domain Model (Pure Logic)

### 2.1 Core State & Events

- [x] `CORE-01` Define domain models:
  - `ShellWindow` (hwnd, title, processId, workspaceId, state, isVisible, appId, etc.)
  - `Workspace` (id, name, list of window handles)
  - `ShellState` (windows[], workspaces[], activeWorkspaceId, focusedWindowHandle, trayIcons[], etc.)
  - `status:done`

- [x] `CORE-02` Define domain events:
  - `WindowCreatedEvent`, `WindowDestroyedEvent`, `WindowStateChangedEvent`
  - `WorkspaceSwitchedEvent`, `WorkspaceUpdatedEvent`
  - `TrayIconAddedEvent`, `TrayIconUpdatedEvent`, `TrayIconRemovedEvent`
  - `status:done`

- [x] `CORE-03` Implement `ShellCore` state machine (no Win32; uses interfaces only):
  - consumes adapter events
  - updates internal `ShellState`
  - emits domain events to subscribers
  - `status:done`

#### Tests (Domain Logic)

- [x] `TEST-CORE-01` Unit tests: window creation/destruction updates state correctly  
  - e.g. feed “window created” → state has new window → emit `WindowCreatedEvent`  
  - `status:done`

- [x] `TEST-CORE-02` Unit tests: workspace switching hides/shows windows in state (flags only, no Win32)  
  - `status:done`

- [x] `TEST-CORE-03` Unit tests: tray icon add/update/remove events update `ShellState.trayIcons` and emit events  
  - `status:done`

---

## 3. OS Adapters – Win32 Integration

### 3.1 Window System Adapter

- [x] `ADAPT-WS-01` Define `IWindowSystem` interface:
  - `EnumWindows()`
  - `IsTopLevelWindow(hwnd)`
  - `GetWindowInfo(hwnd) → ShellWindow snapshot`
  - `ShowWindow(hwnd, state)`
  - `SetForegroundWindow(hwnd)`
  - `IsVisible(hwnd)`
  - Hook registration methods for shell events (`OnWindowCreated`, `OnWindowDestroyed`, `OnWindowActivated`, etc.)
  - `status:done`

- [x] `ADAPT-WS-02` Implement `WindowSystemWin32` using:
  - `EnumWindows`, `GetWindowText`, `GetClassName`, `GetWindowThreadProcessId`
  - `SetWinEventHook` or `SetWindowsHookEx(WH_SHELL)` for window creation/destruction/activation
  - `status:done`

#### Tests (Core + Real Win32)

- [x] `TEST-INT-WS-01` Integration: Core detects new window when a helper app creates one
  - Use real `WindowSystemWin32` and `ProcessLauncherWin32`
  - Launch `FakeApp.exe` which creates a top-level window with known title
  - Assert `ShellCore` emits `WindowCreatedEvent`  
  - `status:done`

- [x] `TEST-INT-WS-02` Integration: Core detects window close when helper app exits
  - `status:done`

- [ ] `TEST-INT-WS-03` Integration: Core tracks focus changes when user activates another window (trigger via test automation or simulated calls)  
  - `status:todo`

### 3.2 Process Launcher

- [x] `ADAPT-PL-01` Define `IProcessLauncher` interface:
  - `LaunchApp(appIdOrPath)`
  - `GetRunningProcesses()`
  - `status:done`

- [x] `ADAPT-PL-02` Implement `ProcessLauncherWin32` using `ShellExecuteEx` / `CreateProcess`  
  - `status:done`

#### Tests

- [ ] `TEST-INT-PL-01` Integration: `ShellCore.LaunchApp("notepad")` starts process and eventually results in a `WindowCreatedEvent`  
  - `status:todo`

### 3.3 Workspaces (Internal Abstraction)

- [x] `ADAPT-WS-VM-01` Implement internal virtual workspaces (not native Windows virtual desktops initially):
  - mapping `workspaceId → set<hwnd>`
  - `SwitchWorkspace(id)` hides non-active workspace windows via `IWindowSystem.ShowWindow(SW_HIDE/SW_SHOW)`
  - `status:done`

#### Tests

- [ ] `TEST-INT-VM-01` Integration: assigning windows to workspaces + switching workspace hides/shows windows correctly using real `WindowSystemWin32`  
  - Use helper apps #1 and #2  
  - `status:todo`

### 3.4 Tray Host

- [x] `ADAPT-TRAY-01` Define `ITrayHost` and tray icon model:
  - Used to store icon handle / image, tooltip, menu info, balloon notifications
  - `status:done`

- [x] `ADAPT-TRAY-02` Implement tray host window handling `Shell_NotifyIcon` (NIM_ADD / NIM_MODIFY / NIM_DELETE)
  - Translate into domain events and update `ShellState.trayIcons`
  - `status:done`

#### Tests

- [x] `TEST-INT-TRAY-01` Integration: when an app adds a tray icon, Core emits `TrayIconAddedEvent` and state reflects it  
  - `status:done`

- [ ] `TEST-INT-TRAY-02` Integration: clicking a tray icon in UI calls back into Core and triggers appropriate callbacks to the app  
  - (depends on UI work; cross-section test)  
  - `status:todo`

### 3.5 Hotkeys & System Events

- [x] `ADAPT-HK-01` Implement `IHotkeyRegistry` using `RegisterHotKey`/`UnregisterHotKey`
  - Map global shortcuts (e.g. `Win` or `Win+Space` for launcher)
  - `status:done`

- [x] `ADAPT-SYS-01` Implement handling of system events (`WM_QUERYENDSESSION`, `WM_ENDSESSION`) and forward them to Core / UI  
  - `status:done`

#### Tests

- [x] `TEST-INT-HK-01` Integration: registering a test hotkey triggers callback in Core; verify Core receives event  
  - `status:done`

---

## 4. Shell Core Process & Bootstrap

- [ ] `BOOT-01` Implement `ShellCoreService`:
  - lifecycle: start, stop, restart UI host
  - maintains instance of `ShellCore`, `WindowSystemWin32`, `ProcessLauncherWin32`, `TrayHostWin32`, `HotkeyRegistryWin32`
  - `status:todo`

- [ ] `BOOT-02` Implement `myshell-bootstrap.exe`:
  - created as `Shell` in `Winlogon` registry key (per-user for safety)
  - starts `ShellCoreService`
  - starts `ShellUiHost` process
  - handles crash-restart logic for `ShellUiHost`
  - `status:todo`

- [ ] `BOOT-03` Implement “safe mode” startup:
  - if `Alt` (or some key) is held during login, start Explorer instead of custom shell
  - `status:todo`

#### Tests

- [ ] `TEST-INT-BOOT-01` Integration (manual/semi-automated): verify that when bootstrap runs as shell, it launches Core + UI Host and system is usable  
  - `status:todo`

---

## 5. UI Host – WebView2 Bridge

- [x] `UIHOST-01` Implement `ShellUiHost.exe`:
  - Borderless, fullscreen window
  - Hosts WebView2 control
  - Loads `index.html` from `Shell.UI.Web` build output
  - `status:done`

- [x] `UIHOST-02` Implement bridge object `ShellApi` exposed to JS:
  - `listWindowsJson()`
  - `launchApp(appIdOrPath)`
  - `focusWindow(hwnd)`
  - `minimizeWindow(hwnd)`
  - `switchWorkspace(id)`
  - `listWorkspacesJson()`
  - `getTrayIconsJson()`
  - `status:done`

- [x] `UIHOST-03` Implement event push:
  - Core → UI via `webView.CoreWebView2.PostWebMessageAsString(payloadJson)`
  - JS listens via `window.chrome.webview.addEventListener("message", ...)`
  - `status:done`

#### Tests (Bridge + UI, Fake Core)

- [ ] `TEST-INT-BRIDGE-01` Integration: fake core pushes window state; UI taskbar DOM reflects two windows  
  - Use `FakeShellCore` + test mode UI host  
  - Inspect DOM via `ExecuteScriptAsync`  
  - `status:todo`

- [ ] `TEST-INT-BRIDGE-02` Integration: clicking a taskbar item in HTML triggers `focusWindow(hwnd)` in `FakeShellCore`  
  - `status:todo`

---

## 6. HTML/CSS/JS Shell UI

### 6.1 Basic Layout

- [x] `UI-01` Implement base layout:
  - top panel (clock, status)
  - bottom panel (taskbar: windows + tray)
  - launcher overlay (Start menu equivalent)
  - workspace indicators
  - `status:done`

- [x] `UI-02` Define client-side state store (`shellState`) containing:
  - windows, workspaces, active workspace, tray icons, focused window, etc.
  - `status:done`

- [x] `UI-03` Implement initial sync from Core:
  - on load, call `window.shell.listWindowsJson()`, `listWorkspacesJson()`, `getTrayIconsJson()`
  - render taskbar, workspaces, tray
  - `status:done`

- [x] `UI-04` Implement event handling:
  - on `"windowCreated"`, `"windowDestroyed"`, `"windowUpdated"`, `"workspaceSwitched"`, `"trayIconUpdated"` messages, update client state + re-render
  - `status:done`

### 6.2 Interaction

- [x] `UI-05` Taskbar interactions:
  - click taskbar item → focus window (or restore from minimized)
  - right-click → show app context menu (close, move to workspace, etc.)
  - `status:done`

- [x] `UI-06` Launcher:
  - show/hide via hotkey event from Core
  - app grid backed by config JSON or API from Core
  - clicking icon → `launchApp(appId)`
  - `status:done`

- [x] `UI-07` Workspaces:
  - click workspace indicator → `switchWorkspace(id)`
  - UI highlight active workspace
  - optional: drag taskbar entry to another workspace
  - `status:done`

- [x] `UI-08` Tray:
  - render icons from `trayIcons` state
  - click icon → send `trayIconInvoke(id, action)` call to Core
  - `status:done`

#### Tests (UI Logic, with Fake Core or isolated in JS)

- [ ] `TEST-UI-01` JS unit tests: reducer/store updates state on events correctly (window created/destroyed/updated, workspace switched, tray icon changes)  
  - `status:todo`

- [ ] `TEST-UI-02` Integration (FakeShellCore): pushing two windows results in two `.taskbar-item` elements in DOM  
  - `status:todo`

- [ ] `TEST-UI-03` Integration (FakeShellCore): clicking `.taskbar-item` calls `focusWindow` in the fake core  
  - `status:todo`

---

## 7. End-to-End (E2E) Tests

These use **real Core + real UI + real adapters**.

- [ ] `TEST-E2E-01` Start shell in test session:
  - Launch Core + UI Host manually (not as Winlogon shell)
  - Use UI automation tool (e.g. FlaUI) to:
    - find the launcher button
    - click to open launcher
    - click “Notepad”
    - assert Notepad process is running and taskbar entry appears
  - `status:todo`

- [ ] `TEST-E2E-02` Workspace switch:
  - Open two helper apps
  - Move them to two workspaces
  - Use UI automation to click workspace controls
  - Assert visibility states via Win32 or UI automation
  - `status:todo`

- [ ] `TEST-E2E-03` Tray icon interaction:
  - Start helper app that creates a tray icon
  - Assert tray icon appears in HTML UI
  - Click tray icon via UI automation
  - Assert helper app receives callback (e.g. logs or state change)
  - `status:todo`

---

## 8. Safety & Recovery

- [ ] `SAFE-01` Implement “panic” command:
  - global hotkey or CLI arg to:
    - start `explorer.exe` as temporary shell
    - or restore original shell registry value
  - `status:todo`

- [ ] `SAFE-02` Document recovery procedure in `docs/Recovery.md`:
  - Safe Mode boot
  - Restoring `explorer.exe` as shell
  - `status:todo`

- [ ] `SAFE-03` Ensure `SHELL_TEST_MODE` prevents any registry modifications or shell registration when running tests/CI  
  - `status:todo`

---

## 9. Documentation

- [x] `DOC-01` `ARCHITECTURE.md`: explain layered design (Core, Adapters, UI Host, Web UI, Tests)
  - `status:done`

- [x] `DOC-02` `TESTING.md`: describe how to run:
  - unit tests (domain)
  - integration tests (Core + adapters + helper apps)
  - UI integration tests (WebView2 + FakeCore)
  - E2E tests (automation)
  - `status:done`

- [x] `DOC-03` `SHELL_SETUP.md`: how to:
  - run shell on top of Explorer (dev mode)
  - register as Winlogon shell (test user only)
  - revert to Explorer
  - `status:done`

---
