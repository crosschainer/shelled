# shelled – Explorer Replacement Backbone

This project is a **Windows shell replacement** that aims to fully replace `explorer.exe` as the system shell, while keeping the **UI layer purely HTML/CSS/JS** for easy customization.

Think of it as:

> A minimal, testable **backbone** that does all the dangerous native work (shell, windows, tray, hotkeys), and a **web-based desktop environment** that talks to it.

The goal is **not** to re-theme Windows, but to build something closer to Cairo / KDE / GNOME:
a different *shell* that handles taskbar, launcher, workspaces, and tray, with the desktop environment implemented as a web app.

---

## High-Level Design

The system is split into four main parts:

1. **Shell Core (`Shell.Core`)**  
   - Pure domain logic: keeps track of windows, workspaces, tray icons, focused window, etc.  
   - Consumes events from OS adapters (window created/destroyed, tray updates, hotkeys).  
   - Exposes a clean API and emits high-level events.

2. **OS Adapters (`Shell.Adapters.Win32`)**  
   - Thin wrappers around Win32 / COM:  
     - `IWindowSystem` (EnumWindows, hooks, ShowWindow, SetForegroundWindow…)  
     - `IProcessLauncher` (CreateProcess / ShellExecute)  
     - Tray host (`Shell_NotifyIcon`)  
     - Hotkey registry (`RegisterHotKey`)  
   - These are the **only** components allowed to call Win32 APIs directly.

3. **UI Host & Bridge (`Shell.Bridge.WebView`)**  
   - Native fullscreen window that hosts **WebView2**.  
   - Loads the web-based shell UI (built from `Shell.UI.Web`).  
   - Exposes a bridge object (e.g. `ShellApi`) to JS:
     - `listWindowsJson()`, `launchApp()`, `focusWindow()`, `switchWorkspace()`, etc.  
   - Forwards Core events to the web UI via `PostWebMessageAsString`.

4. **Web Shell UI (`Shell.UI.Web`)**  
   - A SPA written in HTML/CSS/JS (framework-agnostic: React, Vue, or vanilla).  
   - Renders panels, taskbar, launcher, workspace switcher, tray, notifications.  
   - Talks *only* to `window.shell.*` and listens for message events.  
   - No direct knowledge of Win32 — it just knows about “windows,” “workspaces,” and “tray icons.”

---

## Runtime Behavior

### As a Shell

When registered as the system shell:

1. `winlogon.exe` starts `myshell-bootstrap.exe` instead of `explorer.exe`.
2. `myshell-bootstrap.exe`:
   - Starts the **Shell Core service** (with all adapters).
   - Starts the **UI Host** (WebView2 window) and loads the web shell.
3. Shell Core:
   - Hooks window events, tracks running apps, manages virtual workspaces.
   - Hosts the system tray area.
   - Listens for global hotkeys and system events.
4. Web UI:
   - Renders taskbar, launcher, etc. based on the current shell state.
   - Sends commands back to Core via the bridge (e.g. focus window, launch app).

### In Dev Mode

For development, the shell can **run on top of Explorer**:

- Shell Core + UI Host run as normal apps.
- Explorer is still running in the background (normal desktop + taskbar).
- This makes iteration and debugging safer.
- Once stable, you can configure the shell to fully replace Explorer.

---

## Key Concepts

- **Shell State**  
  A single in-memory model of everything the shell cares about:
  - `windows[]`, `workspaces[]`, `activeWorkspaceId`, `trayIcons[]`, `focusedWindowHandle`, etc.

- **Events**  
  The Core emits high-level events like:
  - `WindowCreatedEvent`, `WindowDestroyedEvent`, `WindowStateChangedEvent`
  - `WorkspaceSwitchedEvent`
  - `TrayIconAddedEvent`, `TrayIconRemovedEvent`
  
  These are translated into JSON messages for the web UI.

- **Virtual Workspaces**  
  Initially, workspaces are implemented as an **internal abstraction** (not bound to Windows native virtual desktops):
  - Each workspace has a set of window handles.
  - When switching, non-active workspace windows are hidden (`SW_HIDE`) and active workspace windows are shown (`SW_SHOW`).

---

## Testing Strategy

Testing is a first-class concern. The project is designed to be testable in layers:

1. **Unit Tests (Pure Core)**  
   - Test the `Shell.Core` state and event handling logic with mocked adapters.  
   - No Win32, no UI — just domain logic:
     - window creation/destruction
     - workspace changes
     - tray updates

2. **Integration Tests (Core + Real Adapters)**  
   - Use real Win32 adapters with simple helper apps (e.g. `FakeApp.exe`) that create/destroy windows and tray icons.  
   - Verify that:
     - real window creation triggers `WindowCreatedEvent`
     - closing apps triggers `WindowDestroyedEvent`
     - workspace switching hides/shows windows in reality

3. **UI Integration Tests (Bridge + Web UI, Fake Core)**  
   - Use a **FakeShellCore** plugged into the WebView2 host.  
   - Push synthetic state into the UI and inspect the DOM via scripts:
     - number of taskbar items
     - titles rendered correctly
   - Simulate user actions in HTML and verify calls back into the fake core:
     - clicking a taskbar item → `focusWindow(hwnd)`
     - clicking a launcher icon → `launchApp(appId)`

4. **End-to-End (E2E) Tests**  
   - Full context: real Core + real adapters + real UI.  
   - Driven by UI automation (e.g. FlaUI / UIAutomation).  
   - Scenarios:
     - open shell, launch Notepad, ensure its taskbar entry appears
     - create windows in multiple workspaces, switch via UI, verify visibility
     - test tray icons: helper app adds tray icon, verify appearance and click behavior

For a detailed breakdown of tasks and related tests, see [`Tasks.md`](./Tasks.md).

---

## AI Agent Guidelines

This repo is **AI-agent-friendly**.  
The AI agent should use `Tasks.md` to track progress and status.

### Tasks File

- All tasks live in [`Tasks.md`](./Tasks.md).
- Each task has:
  - A checkbox: `[ ]` for TODO, `[x]` for done.
  - A unique ID (e.g. `CORE-01`, `ADAPT-WS-01`).
  - A `status:` tag (e.g. `status:todo`, `status:in-progress`, `status:done`, `status:blocked`).
  - Optional `blocker:` tag for a short description.

**The AI agent may:**

- Update checkboxes:
  - `[ ]` → `[x]` when a task is completed.
- Update status tags:
  - `status:todo` → `status:in-progress`
  - `status:in-progress` → `status:done`
  - `status:in-progress` → `status:blocked` (with `blocker:...`)
- Add or modify `blocker:` notes.

**The AI agent must not:**

- Change task IDs (e.g. `CORE-01`).
- Remove tasks.
- Rewrite the overall structure of `Tasks.md`.

### Typical AI Agent Actions

Examples of what an AI agent might do:

- When it writes `Shell.Core` models:
  - Mark `CORE-01` as `status:done` and check the box.
- When it stubs out `IWindowSystem` but hasn’t implemented Win32 hooks:
  - Mark `ADAPT-WS-01` as `status:in-progress`.
- If it cannot complete a task because a dependency is missing:
  - Mark that task as `status:blocked`
  - Add `blocker:Waiting for X to be implemented`.

This makes it possible to use an agent in a loop to gradually implement the project in a traceable way.

---

## Safety / Recovery

Because this project replaces `explorer.exe`, it includes safety measures:

- **Dev Mode**: run the shell on top of Explorer without touching Winlogon.
- **Test Mode** (`SHELL_TEST_MODE=1`):  
  - No registry edits.  
  - No modifications to the user shell.
- **Panic / Recovery**:
  - Global shortcut or command to spawn `explorer.exe` as a fallback.
  - Documentation on how to restore Explorer as shell via Safe Mode and Registry (`SHELL_SETUP.md` / `Recovery.md`).

---

## Getting Started (Conceptual)

> Actual commands and setup steps will be defined once the implementation stack (C#/C++/Rust & frontend framework) is fixed.

Conceptually, the flow will be:

1. Clone the repo.
2. Build:
   - `Shell.Core`
   - `Shell.Adapters.Win32`
   - `Shell.Bridge.WebView`
   - `Shell.UI.Web`
3. Run in **Dev Mode** (on top of Explorer) to experiment and iterate.
4. Run tests:
   - Unit tests (`Shell.Core`)
   - Integration tests (`Shell.Tests`)
5. After stabilization, follow `SHELL_SETUP.md` to register the shell for a test user.

For concrete build/run instructions, see `SHELL_SETUP.md` and `TESTING.md` once they are implemented (see corresponding tasks in `Tasks.md`).

---
