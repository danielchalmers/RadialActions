﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace RadialActions;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static FileInfo MainFileInfo = new(Process.GetCurrentProcess().MainModule.FileName);

    /// <summary>
    /// Sets or deletes a value in the registry which enables the current executable to run on system startup.
    /// </summary>
    public static void SetRunOnStartup(bool runOnStartup)
    {
        var keyName = "RadialActions";
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

        if (runOnStartup)
            key?.SetValue(keyName, MainFileInfo.FullName);
        else
            key?.DeleteValue(keyName, false);
    }

    /// <summary>
    /// Shows a singleton window of the specified type.
    /// If the window is already open, it activates the existing window.
    /// Otherwise, it creates and shows a new instance of the window.
    /// </summary>
    /// <typeparam name="T">The type of the window to show.</typeparam>
    /// <param name="owner">The owner window for the singleton window.</param>
    public static void ShowSingletonWindow<T>(Window owner) where T : Window, new()
    {
        var window = Current.Windows.OfType<T>().FirstOrDefault() ?? new T();
        window.Owner = owner;

        // Restore an existing window.
        if (window.IsVisible)
        {
            SystemCommands.RestoreWindow(window);
            window.Activate();
            return;
        }

        // Show the new window.
        window.Show();
    }
}
