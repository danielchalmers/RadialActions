using System;
using System.ComponentModel;
using System.IO;
using Newtonsoft.Json;

namespace RadialActions.Properties;

public sealed class Settings : INotifyPropertyChanged, IDisposable
{
    private readonly FileSystemWatcher _watcher;

    private static readonly Lazy<Settings> _default = new(LoadAndAttemptSave);

    private static readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        // Make it easier to read by a human.
        Formatting = Formatting.Indented,

        // Prevent a single error from taking down the whole file.
        Error = (_, e) => e.ErrorContext.Handled = true,
    };

    static Settings()
    {
        // Settings file path from the same directory as the executable.
        var settingsFileName = Path.GetFileNameWithoutExtension(App.MainFileInfo.FullName) + ".settings";
        FilePath = Path.Combine(App.MainFileInfo.DirectoryName, settingsFileName);
    }

    // Private constructor to enforce singleton pattern.
    private Settings()
    {
        // Watch for changes in the settings file.
        _watcher = new(App.MainFileInfo.DirectoryName, Path.GetFileName(FilePath))
        {
            EnableRaisingEvents = true,
        };
        _watcher.Changed += FileChanged;
    }

#pragma warning disable CS0067 // The event 'Settings.PropertyChanged' is never used. Handled by Fody.
    public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

    /// <summary>
    /// The singleton instance of the local settings file.
    /// </summary>
    public static Settings Default => _default.Value;

    /// <summary>
    /// The full path to the settings file.
    /// </summary>
    public static string FilePath { get; private set; }

    /// <summary>
    /// Indicates if the settings file can be saved to.
    /// </summary>
    /// <remarks>
    /// <c>false</c> could indicate the file is in a folder that requires administrator permissions among other constraints.
    /// </remarks>
    public static bool CanBeSaved { get; private set; }

    /// <summary>
    /// Checks if the settings file exists on the disk.
    /// </summary>
    public static bool Exists => File.Exists(FilePath);

    #region "Properties"

    /// <summary>
    /// The index of the selected tab in the settings window.
    /// </summary>
    public int SettingsTabIndex { get; set; }

    /// <summary>
    /// The hotkey used to activate the radial menu.
    /// </summary>
    public string ActivationHotkey { get; set; } = "ctrl+alt+4";

    #endregion "Properties"

    /// <summary>
    /// Saves to the default path in JSON format.
    /// </summary>
    public bool Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(this, _jsonSerializerSettings);

            // Attempt to save multiple times.
            for (var i = 0; i < 4; i++)
            {
                try
                {
                    File.WriteAllText(FilePath, json);
                    return true;
                }
                catch
                {
                    // Wait before next attempt to read.
                    System.Threading.Thread.Sleep(250);
                }
            }
        }
        catch (JsonSerializationException)
        {
        }

        return false;
    }

    /// <summary>
    /// Populates the given settings with values from the default path.
    /// </summary>
    private static void Populate(Settings settings)
    {
        using var fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var streamReader = new StreamReader(fileStream);
        using var jsonReader = new JsonTextReader(streamReader);

        JsonSerializer.Create(_jsonSerializerSettings).Populate(jsonReader, settings);
    }

    /// <summary>
    /// Loads from the default path in JSON format.
    /// </summary>
    private static Settings LoadFromFile()
    {
        try
        {
            var settings = new Settings();
            Populate(settings);
            return settings;
        }
        catch
        {
            return new();
        }
    }

    /// <summary>
    /// Loads from the default path in JSON format then attempts to save in order to check if it can be done.
    /// </summary>
    private static Settings LoadAndAttemptSave()
    {
        var settings = LoadFromFile();

        CanBeSaved = settings.Save();

        return settings;
    }

    /// <summary>
    /// Occurs after the watcher detects a change in the settings file.
    /// </summary>
    private void FileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            Populate(this);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        // We don't dispose of the watcher anymore because it would actually hang indefinitely if you had multiple instances open.
        //_watcher?.Dispose();
    }
}
