using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadialActions.Properties;

namespace RadialActions;

public partial class AdvancedSettingsViewModel : ObservableObject
{
    public AdvancedSettingsViewModel(Settings settings)
    {
        Settings = settings;
    }

    public Settings Settings { get; }
    public string FileVersion { get; } = FileVersionInfo.GetVersionInfo(App.MainFileInfo.FullName)?.FileVersion ?? "Unknown";
    public string Architecture { get; } = RuntimeInformation.ProcessArchitecture.ToString();
    public string RuntimeDescription { get; } = RuntimeInformation.FrameworkDescription;
    public string OsDescription { get; } = RuntimeInformation.OSDescription;
    public string ExecutablePath { get; } = App.MainFileInfo.FullName;

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

        if (!RadialActions.Properties.Settings.Exists)
        {
            MessageBox.Show(
                "Settings file doesn't exist and couldn't be created.",
                "Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

       try
       {
           Process.Start("notepad", RadialActions.Properties.Settings.FilePath);
       }
       catch (Exception ex)
       {
           Log.Error(ex, "Couldn't open notepad");
           MessageBox.Show(
               "Couldn't open settings file.\n\n" +
               "This app may have been reuploaded without permission. If you paid for it, ask for a refund and download it for free from the original source: https://github.com/danielchalmers/RadialActions.\n\n" +
               $"If it still doesn't work, create a new Issue at that link with details on what happened and include this error: \"{ex.Message}\"",
               "Settings",
               MessageBoxButton.OK,
               MessageBoxImage.Error);
        }
    }
}
