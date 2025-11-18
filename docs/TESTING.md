# Shelled Testing Guide

Shelled’s layered architecture makes it possible to test most functionality
without touching the real Windows shell.  This document explains each test layer
and how to run it as the repositories come online.

> **Note:** Commands below use placeholder project names that match the solution
> layout planned in `Tasks.md`.  Update the commands as the actual projects and
> scripts are added.

## Environment Flags

| Variable          | Purpose                                                                |
| ----------------- | ---------------------------------------------------------------------- |
| `SHELL_TEST_MODE` | When set to `1`, adapters and installers must avoid shell/registry ops. |
| `DEV_MODE`        | Optional flag to keep Explorer running while Shelled is open.           |

Ensure `SHELL_TEST_MODE=1` is exported for every automated test invocation.

```powershell
$env:SHELL_TEST_MODE = "1"
```

## 1. Core Unit Tests (`Shell.Core.Tests`)

Scope: Pure domain logic (state transitions, event emission, command handling).

```powershell
# Runs inside Windows or Linux CI containers because no Win32 APIs are needed
 dotnet test src/Shell.Core.Tests/Shell.Core.Tests.csproj
```

These tests inject fakes for `IWindowSystem`, `IProcessLauncher`, etc.  They
simulate window creation/destruction, workspace changes, and tray updates, then
assert the resulting `ShellState` and emitted events.

## 2. Adapter Integration Tests (`Shell.Adapters.Tests`)

Scope: Shell Core combined with the real Win32 adapters.  Requires Windows test
machines because the tests create real top-level windows and tray icons.

```powershell
# Requires Windows runner with desktop interaction
 dotnet test src/Shell.Adapters.Tests/Shell.Adapters.Tests.csproj
```

Each test launches helper apps (e.g. `FakeApp.exe`) that create deterministic
windows or tray icons.  Assertions verify that Core receives the corresponding
adapter events (`WindowCreatedEvent`, `TrayIconRemovedEvent`, etc.).  Keep these
tests guarded behind `SHELL_TEST_MODE=1` so no shell registration occurs.

## 3. Bridge + Web UI Integration Tests (`Shell.UI.Tests`)

Scope: WebView2 host + Web UI with a fake Core implementation.

```powershell
# Example script; finalize once the UI framework is chosen
 pnpm --dir src/Shell.UI.Web test
```

- The native WebView host loads a fake bridge that exposes deterministic shell
  state and records every command invocation.
- Tests push synthetic state (windows, workspaces, tray icons) and use Playwright
  or similar to query the DOM for `.taskbar-item`, `.workspace-chip`, etc.
- Interaction tests click UI elements and assert that the fake Core recorded
  the expected commands (`focusWindow`, `launchApp`).

## 4. End-to-End Tests (`Shell.E2E`)

Scope: Real adapters + real Core + real Web UI, orchestrated via UI automation
(FlaUI, WinAppDriver, or Playwright for desktop).

```powershell
# Executed on dedicated Windows machines with Explorer disabled for the session
 ./scripts/run-e2e.ps1
```

Typical scenarios:

1. **Launcher / Process Flow** – Start Shelled, open the launcher, click Notepad,
   assert Notepad is running and its taskbar item appears.
2. **Workspace Visibility** – Place helper apps on different workspaces, switch
   via UI buttons, verify visibility toggles through Win32 inspection.
3. **Tray Interaction** – Start a helper tray app, wait for icon to appear in the
   UI, click it, assert the helper logs the callback.

The launcher/process flow scenario now has a cross-platform automated check that
runs inside `src/Shell.UI.Web` using Node’s built-in test runner and `jsdom`.
The test file `tests/e2e/launcher.e2e.test.js` wires up the real HTML/CSS/JS,
stubs the bridge API, simulates opening the launcher, launches the `notepad`
app entry, and verifies that the taskbar reflects the new window.  Run it via
`npm test` inside `src/Shell.UI.Web` to validate the full UI→bridge→Core loop
without requiring a Windows desktop session.

## 5. Linting & Static Analysis

Add linters as the codebase grows:

- `.editorconfig` + Roslyn analyzers for .NET projects: `dotnet format` and
  `dotnet build -warnaserror`.
- ESLint/TypeScript checks for `Shell.UI.Web`: `pnpm lint`.

Include these commands in CI once the corresponding tooling is in place.

## 6. Continuous Integration

A Windows GitHub Actions workflow should run at least:

1. `dotnet test` (Core + adapters)
2. `pnpm test` (Web UI)
3. Optional nightly `./scripts/run-e2e.ps1` on a self-hosted runner.

CI must export `SHELL_TEST_MODE=1` to prevent accidental registry writes.  If a
workflow needs to exercise shell registration, gate it behind manual approval
and ensure it runs on disposable VMs.

## 7. Troubleshooting

- **Tests hang** – Ensure helper apps or UI hosts are closed between runs.  The
  scripts should include cleanup hooks that kill leftover processes.
- **Tray or hotkey tests fail in CI** – Confirm the runner has an interactive
  desktop (RDP session) because headless Windows services cannot receive those
  events.
- **Web UI tests flaky** – Capture WebView2 logs and DOM snapshots.  Use retries
  sparingly; prefer deterministic fake-core inputs.

Keep this guide up-to-date as new projects and scripts land in the repository.
