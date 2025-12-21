# Radial Actions

[![Release](https://img.shields.io/github/release/danielchalmers/RadialActions?include_prereleases)](https://github.com/danielchalmers/RadialActions/releases)
[![License](https://img.shields.io/github/license/danielchalmers/RadialActions)](LICENSE)

🪄 **Magically summon a pie menu with customizable shortcuts for apps, media controls, and more!**

Radial Actions is a lightweight Windows utility that provides a customizable radial/pie menu accessible via a global hotkey. Perfect for quick access to frequently used actions without cluttering your desktop or taskbar.

## 📥 Installation

### Option 1: Download Release (Recommended)
1. Download the latest release from the [Releases page](https://github.com/danielchalmers/RadialActions/releases)
2. Extract the ZIP file to your preferred location
3. Run `RadialActions.exe`

### Option 2: Build from Source
1. Clone the repository
2. Open `RadialActions.sln` in Visual Studio 2022+
3. Build and run

## 🚀 Quick Start

1. **Launch** - Run RadialActions.exe (it will minimize to the system tray)
2. **Open Menu** - Press `Ctrl+Alt+Space` (default hotkey) to open the radial menu
3. **Select Action** - Click on a slice to execute the action
4. **Close** - Click outside the menu or press `Escape` to dismiss

## ⚙️ Configuration

Right-click the tray icon to access settings:

### General Settings
- **Hotkey** - Customize the activation hotkey (e.g., `Ctrl+Alt+Space`, `Win+Z`)
- **Menu Size** - Adjust the diameter of the pie menu (300-500 recommended)
- **Show Center Hole** - Toggle the center hole visual
- **Show Icons/Labels** - Toggle icon and text display

### Actions
Add, remove, and configure actions in the Actions tab:

| Action Type | Parameter | Description |
|-------------|-----------|-------------|
| `MediaKey` | `PlayPause`, `NextTrack`, `PreviousTrack`, `Stop` | Control media playback |
| `VolumeKey` | `VolumeUp`, `VolumeDown`, `Mute` | Control system volume |
| `LaunchApp` | Path to executable | Launch an application |
| `OpenUrl` | URL | Open a website |
| `RunCommand` | Command string | Run a command |
| `Keyboard` | Shortcut (e.g., `Ctrl+C`) | Simulate a keyboard shortcut |

### Advanced Settings
For advanced customization, click "Advanced settings" to edit the JSON settings file directly.

## 🎹 Supported Hotkey Modifiers

- `Ctrl` / `Control`
- `Alt`
- `Shift`
- `Win` / `Windows`

Combine with any letter, number, function key (F1-F24), or special key.

## 🛠️ Requirements

- Windows 10/11
- .NET Framework 4.8.1

## 🙏 Acknowledgments

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM library
- [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) - System tray icon support
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON serialization
- [PropertyChanged.Fody](https://github.com/Fody/PropertyChanged) - Property change notifications
- [Serilog](https://serilog.net/) - Logging

## 🔗 Other Projects

- [DesktopClock](https://github.com/danielchalmers/DesktopClock) - A digital clock for your desktop
- [Network Monitor](https://github.com/danielchalmers/Network-Monitor) - See latency and bandwidth usage
- [JournalApp](https://play.google.com/store/apps/details?id=com.danielchalmers.journalapp) - Stay on top of your well-being (Android)
