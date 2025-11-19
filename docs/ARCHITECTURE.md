# Shelled Architecture

Shelled is split into four cooperating layers. Each layer owns a narrow set of
responsibilities and communicates with the next layer using data contracts that
are easy to mock in tests.

At the solution level, these layers map to the following projects:

- src/Shell.Core – core models, events, configuration, and ShellCore.
- src/Shell.Adapters.Win32 – Win32 implementations of shell interfaces.
- src/Shell.Service – ShellCoreService hosting Core + adapters + UI host.
- src/Shell.Bridge.WebView – WebView2 host (ShellUiHost) and bridge.
- src/Shell.Bootstrap – myshell-bootstrap.exe shell entrypoint.
- src/Shell.Bootstrap.Infrastructure – shell-registration and bootstrap utilities.
- src/Shell.UI.Web – web desktop (HTML/CSS/JS) and its tests.
- 	ests/Shell.Tests – Core + adapter + UI-host unit/integration tests.
- 	ests/Shell.Bootstrap.Tests – bootstrap / shell-registration tests.
- 	ests/Shell.Service.Tests – service lifecycle tests.

` 
[ OS / Win32 ] -> [ Adapters (Win32) ] -> [ Shell Core ] -> [ Web UI ]
                                \
                                 -> [ Bridge / WebView Host ]
` 

## 1. Shell Core (Shell.Core)

*Pure domain logic.*

- Maintains the authoritative ShellState (windows, workspaces, tray icons,
  focus, launcher state, etc.).
- Depends only on simple interfaces such as IWindowSystem,
  IProcessLauncher, ITrayHost, and IHotkeyRegistry.
- Consumes events raised by the adapters and translates them into domain events
  (WindowCreatedEvent, WorkspaceSwitchedEvent, ...).
- Emits events to subscribers (WebView bridge, tests) via an in-process event
  dispatcher.
- Provides command handlers that mutate state and call adapters through the
  interfaces (e.g. FocusWindowCommand, SwitchWorkspaceCommand).

Because the Core is pure logic it can be tested with simple mocks and run in
any environment, including CI containers that do not have Win32 APIs.

## 2. OS Adapters (Shell.Adapters.Win32)

*All Win32 interactions live here.*

- IWindowSystem implementation wraps Win32 window enumeration, hooks, and
  window state manipulation (EnumWindows, SetWinEventHook, ShowWindow,
  etc.).
- IProcessLauncher wraps CreateProcess, ShellExecute, and handles dev-mode
  safety checks.
- ITrayHost owns the notify icon area via Shell_NotifyIcon and proxies click
  callbacks back to the Core.
- IHotkeyRegistry registers and unregisters hotkeys, routing them to Core
  commands.
- Adapters never contain business rules. They only translate OS concepts into
  the Core’s DTOs and events.

Adapters respect SHELL_TEST_MODE and dev-mode flags to avoid registry or
sh
ell-registration side effects during testing.

## 3. Bridge & UI Host (Shell.Bridge.WebView)

*A native host for the HTML desktop.*

- Creates a fullscreen, always-on-top window that embeds WebView2.
- Loads the bundled Web UI from Shell.UI.Web and exposes a window.shell`r
  bridge object in JS.
- Serializes Core events into JSON and delivers them via PostWebMessage.
- Deserializes JS commands (ocusWindow, launchApp, switchWorkspace,
  	rayIconInvoke, etc.) and forwards them to Core command handlers.
- Owns startup sequencing: boot Core, connect adapters, perform initial state
  sync (windows/workspaces/tray), and hand it to the Web UI.

## 4. Web Shell UI (Shell.UI.Web)

*A SPA that renders the entire desktop experience.*

- Maintains a client-side store (shellState) that mirrors Core state.
- Renders panels such as taskbar, system tray, workspace switcher, launcher, and
  notifications using HTML/CSS/JS.
- Listens for window.shell events (windowCreated, windowDestroyed,
  workspaceSwitched, etc.) and updates the store + DOM accordingly.
- Issues commands back to Core through the bridge when the user interacts with
  the UI.
- Contains no Win32 knowledge; the only inputs are the JSON payloads received
  from Core.

## Cross-Cutting Concerns

### Safety Modes

- **Dev Mode** – Allows running Shelled alongside Explorer for safe iteration.
- **Test Mode** (SHELL_TEST_MODE=1) – Ensures no registry or shell-registration
  mutations happen when running automated tests or CI.
- **Panic / Recovery** – A global command or hotkey triggers Explorer so the user
  can recover even if Shelled misbehaves.

### Testing Layers

- Core unit tests validate state transitions and event emission without touching
  Win32.
- Adapter integration tests combine Core with real Win32 adapters and helper
  apps.
- Web UI integration tests run the UI against a fake Core through the bridge.
- End-to-end tests drive the actual shell via UI automation.

### Build & Packaging

- Native components (Shell.Core, adapters, WebView host) are built as a single
  solution.
- Web assets are bundled separately and loaded by WebView2.
- Deployment artifacts include the native binaries plus the compiled web assets
  with configuration describing dev/test/prod modes.

### Data Contracts

- Domain models (ShellWindow, Workspace, TrayIcon) are plain, serializable
  objects defined once in Core and shared (via DTOs) with the bridge and UI.
- Events are small discriminated payloads (type + JSON body) so the Web UI can
  easily handle them.

This document is a living reference for future contributors—update it whenever a
layer’s responsibilities or data contracts change.

