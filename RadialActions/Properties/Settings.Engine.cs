using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace RadialActions.Properties;

public sealed partial class Settings : ObservableObject
{
    private static readonly Lazy<Settings> _default = new(LoadAndAttemptSave);

    private static readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        // Make it easier to read by a human.
        Formatting = Formatting.Indented,

        // Replace collections instead of appending to them.
        ObjectCreationHandling = ObjectCreationHandling.Replace,

        // Prevent a single error from taking down the whole file.
        Error = (_, e) =>
        {
            Log.Error(e.ErrorContext.Error, "Serializer error");
            e.ErrorContext.Handled = true;
        },
    };

    static Settings()
    {
        // Settings file path from the same directory as the executable (not working directory).
        var settingsFileName = Path.GetFileNameWithoutExtension(App.MainFileInfo.FullName) + ".settings";
        FilePath = Path.Combine(App.MainFileInfo.DirectoryName, settingsFileName);
    }

    // Private constructor to enforce singleton pattern.
    private Settings() { }

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

    /// <summary>
    /// Saves to the default path in JSON format.
    /// </summary>
    public bool Save()
    {
        Log.Information($"Saving to {FilePath}");

        try
        {
            var json = SerializeToJson();

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
                    Log.Debug("Couldn't save file; Waiting a bit");
                    Thread.Sleep(250);
                }
            }
        }
        catch (JsonSerializationException ex)
        {
            Log.Error(ex, "Failed to save settings");
        }

        return false;
    }

    public string SerializeToJson()
    {
        return JsonConvert.SerializeObject(this, _jsonSerializerSettings);
    }

    public static Settings DeserializeFromJson(string json)
    {
        var settings = new Settings();
        if (!string.IsNullOrWhiteSpace(json))
        {
            JsonConvert.PopulateObject(json, settings, _jsonSerializerSettings);
        }

        settings.NormalizeAfterLoad();
        return settings;
    }

    /// <summary>
    /// Reads settings from the default path.
    /// </summary>
    private static string ReadJsonFromFile()
    {
        using var fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var streamReader = new StreamReader(fileStream);
        return streamReader.ReadToEnd();
    }

    /// <summary>
    /// Loads from the default path in JSON format.
    /// </summary>
    private static Settings LoadFromFile()
    {
        try
        {
            var json = ReadJsonFromFile();
            return DeserializeFromJson(json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to load {FilePath}");
            Log.Information("Creating new settings");
            var settings = new Settings();
            settings.NormalizeAfterLoad();
            return settings;
        }
    }

    /// <summary>
    /// Loads from the default path in JSON format then attempts to save in order to check if it can be done.
    /// </summary>
    private static Settings LoadAndAttemptSave()
    {
        var settings = LoadFromFile();

        CanBeSaved = settings.Save();
        Log.Debug($"Settings can be saved: {CanBeSaved}");

        return settings;
    }

}
