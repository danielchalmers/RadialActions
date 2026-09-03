# 🪄 Radial Actions

[![Release](https://img.shields.io/github/release/danielchalmers/RadialActions?include_prereleases)](https://github.com/danielchalmers/RadialActions/releases)
[![License](https://img.shields.io/github/license/danielchalmers/RadialActions)](LICENSE)

Radial Actions is a free, open source pie menu launcher for Windows. Press a global hotkey and a radial menu opens at the pointer with the actions you set up: apps, files, folders, websites, media and volume controls, keyboard shortcuts, and PowerShell scripts.

<img width="600" height="600" alt="Radial Actions pie menu on Windows" src="https://github.com/user-attachments/assets/60363788-2fb3-4638-8c64-d4e8e2c2a5f1" />

## Features

- Opens with a global hotkey, `Ctrl+Alt+Space` by default. You can change it in Settings.
- Appears at the mouse pointer, or at the center of the screen if you turn on "Open at screen center".
- Hold the hotkey, move onto a slice, and release to run it, or press the hotkey and click a slice. "Release hotkey to trigger" can be turned off in Settings.
- Works from the keyboard too. The arrow keys select a slice, `Enter` runs it, and the number keys `1` to `9` run slices clockwise from the top.
- An Open action launches an app, file, folder, or URL, with optional arguments and a working directory.
- A Key action sends a media key (play/pause, previous, next, stop), a volume key (mute, down, up), Print Screen, or a shortcut you record, such as `Ctrl+Shift+M`.
- A Script action runs PowerShell stored in the action itself, with no separate file. It can run hidden with no console window, and can use `pwsh.exe` for PowerShell 7.
- Drag files, folders, apps, or links from Explorer or a browser onto the Actions list to add them.
- Each slice has a name and an emoji or symbol icon, and can be hidden without being deleted.
- Drag slices around the open menu to reorder them. The menu size is set in Settings.
- Runs in the system tray. It can start with Windows (off by default) and check GitHub for a new release at startup (on by default). The update check shows a notification and does not install anything.

## Getting started

1. [Install Radial Actions](#installation) and press `Ctrl+Alt+Space`. A fresh install has five slices: Play/Pause, Previous Track, Next Track, Mute, and File Explorer.
2. Right-click the tray icon and choose Actions to add, edit, and reorder slices. You can also right-click a slice in the open menu to edit it.
3. Press the hotkey and click a slice. The menu closes after the action runs, or when you press `Escape` or click outside it. To keep it open, turn on "Keep menu open after running an action".

Settings are stored as JSON in `RadialActions.settings` next to the exe. The About tab in Settings has a button that opens the file.

## Installation

Download the `.msi` installer or the portable `.zip` from the [Releases page](https://github.com/danielchalmers/RadialActions/releases). Both come as `x64` (most Intel and AMD PCs) and `arm64` (Windows on ARM), and both are self-contained, so no .NET runtime install is needed.

### Installer

Run the `.msi`. It installs per user into `%LocalAppData%\RadialActions\RadialActions` without admin rights, adds a Start Menu entry, and starts the app after a fresh install. To update, run the newer `.msi`.

### Portable

Extract the `.zip` to a folder you can write to and run `RadialActions.exe`. Settings stay in that folder.

### Build from source

The app is written in C# with WPF and needs the .NET 10 SDK. Clone the repository, then open `RadialActions.sln` in Visual Studio 2022 or later, or run `dotnet build RadialActions.sln`.
