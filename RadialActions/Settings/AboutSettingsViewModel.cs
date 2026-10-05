using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadialActions.Properties;

namespace RadialActions;

public partial class AboutSettingsViewModel : ObservableObject
{
    private const string MessageBoxCaption = "Radial Actions";

    public AboutSettingsViewModel(Settings settings)
    {
        Settings = settings;
    }

    public Settings Settings { get; }
    public string AppVersion { get; } = AppInfoFormatter.FormatVersion(FileVersionInfo.GetVersionInfo(App.MainFileInfo.FullName)?.FileVersion) ?? "Unknown";
    public string Architecture { get; } = AppInfoFormatter.FormatArchitecture(RuntimeInformation.ProcessArchitecture);
    public string RuntimeDescription { get; } = RuntimeInformation.FrameworkDescription;
    public string OsDescription { get; } = RuntimeInformation.OSDescription;
    public string ExecutablePath { get; } = App.MainFileInfo.FullName;
    public string SettingsFilePath { get; } = RadialActions.Properties.Settings.FilePath;

    [RelayCommand]
    private void OpenExeFolder()
    {
        var directory = App.MainFileInfo.DirectoryName;
        if (string.IsNullOrWhiteSpace(directory))
            return;

        Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenSettingsFile()
    {
        if (RadialActions.Properties.Settings.CanBeSaved)
        {
            RadialActions.Properties.Settings.Default.Save();
        }

        var path = RadialActions.Properties.Settings.FilePath;
        if (!RadialActions.Properties.Settings.Exists)
        {
            MessageBox.Show(
                GetSettingsFileMissingMessage(path),
                MessageBoxCaption,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        OpenInNotepad(path, "the settings file");
    }

    [RelayCommand]
    private void OpenLicenses()
    {
        OpenInNotepad(Path.Combine(App.MainFileInfo.DirectoryName, "Licenses.txt"), "the third-party licenses");
    }

    internal static string GetSettingsFileMissingMessage(string path)
    {
        return $"Couldn't create the settings file at {path}.";
    }

    internal static string GetOpenInNotepadFailedMessage(string fileDescription, string error, string path)
    {
        return $"Couldn't open {fileDescription} in Notepad.\n\n{error}\n\nThe file is at {path}.";
    }

    private static void OpenInNotepad(string path, string fileDescription)
    {
        try
        {
            Process.Start("notepad", path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Couldn't open {Path} in Notepad", path);
            MessageBox.Show(
                GetOpenInNotepadFailedMessage(fileDescription, ex.Message, path),
                MessageBoxCaption,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
