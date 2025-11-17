# Shelled – Windows Shell Replacement

**Shelled** is a modern Windows shell replacement that fully replaces `explorer.exe` as the system shell. The entire desktop environment UI is implemented in HTML/CSS/JS and runs inside WebView2, while a native backbone handles all Windows integration.

## 🎯 Project Vision

> A minimal, testable **native backbone** that handles all Windows shell responsibilities, with a **web-based desktop environment** for the user interface.

Unlike Windows themes or shell modifications, Shelled is a complete shell replacement similar to alternative desktop environments on Linux (KDE, GNOME, etc.). It provides:

- **Custom taskbar and launcher**
- **Virtual workspace management** 
- **System tray hosting**
- **Window management**
- **Global hotkeys**
- **Modern web-based UI**

## 🏗️ Architecture

Shelled uses a clean layered architecture with strict separation of concerns:

### 1. **Shell Core** (`Shell.Core`)
- **Pure domain logic** - no Win32 dependencies
- Manages shell state: windows, workspaces, tray icons, focus tracking
- Processes events from OS adapters
- Emits high-level domain events
- **Status**: ✅ Complete with comprehensive test coverage

### 2. **Win32 Adapters** (`Shell.Adapters.Win32`)
- **Only layer allowed to call Win32 APIs**
- Implements interfaces defined by Core:
  - `IWindowSystem` - window enumeration, hooks, show/hide, focus
  - `IProcessLauncher` - application launching
  - `ITrayHost` - system tray management  
  - `IHotkeyRegistry` - global hotkey registration
  - `ISystemEventHandler` - system shutdown/logoff events
- **Status**: ✅ Complete with Win32 implementations

### 3. **UI Host & Bridge** (`Shell.Bridge.WebView`)
- **Borderless fullscreen window** hosting WebView2
- Loads HTML/CSS/JS shell UI from `Shell.UI.Web`
- Exposes `ShellApi` bridge object to JavaScript
- Forwards Core events to web UI via messaging
- **Status**: ✅ UI Host complete, Bridge API in progress

### 4. **Web Shell UI** (`Shell.UI.Web`)
- **Framework-agnostic** HTML/CSS/JS frontend
- Renders taskbar, launcher, workspace switcher, system tray
- Communicates only via bridge API - no Win32 knowledge
- **Status**: 🚧 Basic structure, needs implementation

## 🚀 Getting Started

### Prerequisites

- **Windows 10/11** (required for WebView2)
- **.NET 8.0 SDK** or later
- **Visual Studio 2022** or **Visual Studio Code** (recommended)
- **Git** for version control

### Building on Windows

1. **Clone the repository**
   ```cmd
   git clone https://github.com/crosschainer/shelled.git
   cd shelled
   ```

2. **Restore dependencies and build**
   ```cmd
   dotnet restore
   dotnet build
   ```

3. **Run tests**
   ```cmd
   dotnet test
   ```

4. **Run the UI Host (development mode)**
   ```cmd
   cd src\Shell.Bridge.WebView\bin\Debug\net8.0-windows
   .\ShellUiHost.exe
   ```

### Project Structure

```
shelled/
├── src/
│   ├── Shell.Core/              # Domain logic and interfaces
│   ├── Shell.Adapters.Win32/    # Win32 API implementations  
│   ├── Shell.Bridge.WebView/    # UI Host executable (ShellUiHost.exe)
│   └── Shell.UI.Web/            # HTML/CSS/JS frontend
├── tests/
│   └── Shell.Tests/             # Unit and integration tests
├── TASKS.md                     # Development task tracking
└── README.md                    # This file
```

### Development Workflow

1. **Development Mode**: Run Shelled alongside Explorer for safe testing
2. **Unit Tests**: Test core logic with mocked dependencies
3. **Integration Tests**: Test with real Win32 APIs (Windows only)
4. **UI Development**: Modify HTML/CSS/JS in `Shell.UI.Web`

## 🧪 Testing

The project has comprehensive test coverage:

- **124 total tests** with **100% pass rate**
- **Unit tests** for pure domain logic
- **Integration tests** for Win32 adapter functionality  
- **UI Host tests** for WebView2 integration
- **Mock implementations** for cross-platform development

Run specific test categories:
```cmd
# All tests
dotnet test

# Core domain logic only
dotnet test --filter "Core"

# UI Host tests
dotnet test --filter "UiHost"

# System integration tests (Windows only)
dotnet test --filter "Integration"
```

## 🔧 Current Status

### ✅ Completed Components

- **Shell Core**: Complete domain model with window, workspace, and tray management
- **Win32 Adapters**: Full Win32 implementations for all shell interfaces
- **System Events**: Proper handling of shutdown/logoff events
- **Virtual Workspaces**: Internal workspace switching with window show/hide
- **UI Host**: Borderless fullscreen WebView2 host (`ShellUiHost.exe`)
- **Test Suite**: Comprehensive unit and integration test coverage

### 🚧 In Progress

- **Bridge API**: JavaScript bridge for UI ↔ Core communication
- **Web UI**: HTML/CSS/JS desktop environment implementation

### 📋 Next Steps

- Complete bridge API implementation (`UIHOST-02`)
- Build responsive web-based shell UI
- Add shell registration and bootstrap executable
- End-to-end testing and polish

## 🛡️ Safety Features

Since Shelled replaces the system shell, it includes safety measures:

- **Development Mode**: Run alongside Explorer without system changes
- **Test Mode**: Environment variable `SHELL_TEST_MODE=1` disables dangerous operations
- **Recovery Options**: Documentation for restoring Explorer via Safe Mode
- **Graceful Fallback**: Error handling with fallback UI when components fail

## 🤖 AI Agent Integration

This project is designed to work with AI development agents:

- **Task Tracking**: All work items tracked in `TASKS.md` with status tags
- **Clean Architecture**: Well-defined interfaces and separation of concerns  
- **Comprehensive Tests**: Automated verification of functionality
- **Documentation**: Clear guidelines for AI agents in `TASKS.md`

## 📄 License

This project is open source. See LICENSE file for details.

## 🤝 Contributing

Contributions are welcome! Please see `TASKS.md` for current development priorities and status.

---

**Note**: This is a system-level shell replacement. Always test in development mode first and ensure you have recovery options before replacing Explorer as your system shell.