using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace RadialActions.Properties;

public sealed partial class Settings : ObservableObject
{
    private static readonly Lazy<Settings> _default = new(LoadAndInitializePersistenceState);

    /// <summary>
    /// Carries loaded settings plus whether future saves are allowed to write back to the same path.
    /// </summary>
    /// <param name="Settings">The settings object to use for the current app session.</param>
    /// <param name="CanSaveSettings">
    /// <c>false</c> when the primary settings file existed but could not be preserved before recovery.
    /// </param>
    private sealed record SettingsLoadResult(Settings Settings, bool CanSaveSettings);

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
    /// <returns><c>true</c> when the default settings file was written successfully; otherwise, <c>false</c>.</returns>
    public bool Save()
    {
        return SaveToFile(FilePath);
    }

    /// <summary>
    /// Saves this settings object to a specific path using an atomic file replacement.
    /// </summary>
    /// <param name="filePath">The primary settings file path to write.</param>
    /// <returns><c>true</c> when the primary file was written successfully; otherwise, <c>false</c>.</returns>
    internal bool SaveToFile(string filePath)
    {
        Log.Information("Saving to {FilePath}", filePath);

        try
        {
            var json = SerializeToJson();

            // Attempt to save multiple times.
            for (var i = 0; i < 4; i++)
            {
                try
                {
                    WriteAllTextAtomically(filePath, json);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Couldn't save file; waiting a bit");
                    Thread.Sleep(250);
                }
            }
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Failed to save settings");
        }

        return false;
    }

    /// <summary>
    /// Serializes this settings object to the app's JSON settings format.
    /// </summary>
    /// <returns>The formatted JSON settings document.</returns>
    public string SerializeToJson()
    {
        return JsonConvert.SerializeObject(this, _jsonSerializerSettings);
    }

    /// <summary>
    /// Deserializes a settings document and normalizes missing or invalid values.
    /// </summary>
    /// <param name="json">The JSON settings content to deserialize.</param>
    /// <returns>A normalized settings object.</returns>
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
    /// Reads settings from the specified path.
    /// </summary>
    /// <param name="filePath">The settings file path to read.</param>
    /// <returns>The complete JSON file content.</returns>
    private static string ReadJsonFromFile(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var streamReader = new StreamReader(fileStream);
        return streamReader.ReadToEnd();
    }

    /// <summary>
    /// Loads settings from the specified path with recovery behavior for unreadable primary files.
    /// </summary>
    /// <param name="filePath">The primary settings file path.</param>
    /// <returns>The settings object to use for the current app session.</returns>
    internal static Settings LoadFromFileWithRecovery(string filePath)
        => LoadFromFileWithRecoveryResult(filePath).Settings;

    /// <summary>
    /// Loads settings and records whether it is safe to save back to the primary path later.
    /// </summary>
    /// <param name="filePath">The primary settings file path.</param>
    /// <returns>
    /// A load result containing settings plus a save gate that protects unreadable files that could not be preserved.
    /// </returns>
    private static SettingsLoadResult LoadFromFileWithRecoveryResult(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Log.Information("Creating new settings because {FilePath} does not exist", filePath);
            return new SettingsLoadResult(CreateDefaultSettings(), CanSaveSettings: true);
        }

        try
        {
            var settings = LoadSettingsFileOrThrow(filePath);
            return new SettingsLoadResult(settings, CanSaveSettings: true);
        }
        catch (Exception ex)
        {
            return RecoverFromPrimaryLoadFailure(filePath, ex);
        }
    }

    /// <summary>
    /// Loads from the default path in JSON format and probes whether the directory is writable.
    /// </summary>
    /// <returns>The singleton settings object for the default settings file path.</returns>
    private static Settings LoadAndInitializePersistenceState()
    {
        var settings = LoadAndInitializePersistenceState(FilePath, out var canBeSaved);
        CanBeSaved = canBeSaved;
        Log.Debug("Settings can be saved: {CanBeSaved}", CanBeSaved);

        return settings;
    }

    /// <summary>
    /// Loads settings from a specific path and determines whether later saves should be enabled.
    /// </summary>
    /// <param name="filePath">The primary settings file path.</param>
    /// <param name="canBeSaved">
    /// Set to <c>true</c> only when recovery did not need to suppress saves and the directory is writable.
    /// </param>
    /// <returns>The settings object to use for the current app session.</returns>
    internal static Settings LoadAndInitializePersistenceState(string filePath, out bool canBeSaved)
    {
        var loadResult = LoadFromFileWithRecoveryResult(filePath);
        canBeSaved = loadResult.CanSaveSettings && ProbeSettingsDirectoryWritable(filePath);
        return loadResult.Settings;
    }

    /// <summary>
    /// Checks whether the settings directory can be written without modifying the real settings file.
    /// </summary>
    /// <param name="filePath">The primary settings file path whose directory should be probed.</param>
    /// <returns><c>true</c> when a throwaway probe file can be created and deleted; otherwise, <c>false</c>.</returns>
    internal static bool ProbeSettingsDirectoryWritable(string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            directoryPath = ".";
        }

        var probePath = Path.Combine(directoryPath, $".settings-write-probe-{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Settings directory is not writable");
            return false;
        }
        finally
        {
            TryDeleteFile(probePath);
        }
    }

    /// <summary>
    /// Creates a default settings object and runs the normal post-load normalization pass.
    /// </summary>
    /// <returns>A normalized default settings object.</returns>
    private static Settings CreateDefaultSettings()
    {
        var settings = new Settings();
        settings.NormalizeAfterLoad();
        return settings;
    }

    /// <summary>
    /// Reads and deserializes one settings file, treating an empty existing file as corruption.
    /// </summary>
    /// <param name="filePath">The settings file path to load.</param>
    /// <returns>A normalized settings object.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file exists but contains no JSON content.</exception>
    private static Settings LoadSettingsFileOrThrow(string filePath)
    {
        var json = ReadJsonFromFile(filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"Settings file is empty: {filePath}");
        }

        return DeserializeFromJson(json);
    }

    /// <summary>
    /// Recovers from a failed primary settings load without silently overwriting recoverable user data.
    /// </summary>
    /// <param name="filePath">The primary settings file path that failed to load.</param>
    /// <param name="loadException">The exception raised while loading the primary file.</param>
    /// <returns>
    /// Recovered settings and a save gate. Saves are disabled when the unreadable primary file could not be copied aside.
    /// </returns>
    private static SettingsLoadResult RecoverFromPrimaryLoadFailure(string filePath, Exception loadException)
    {
        Log.Error(loadException, "Failed to load {FilePath}", filePath);

        var canSaveDefaults = PreserveCorruptSettingsFile(filePath);
        if (!canSaveDefaults)
        {
            Log.Warning("Default settings will not be saved automatically because the unreadable primary file could not be preserved");
        }

        Log.Information("Creating new settings in memory after unreadable settings file");
        return new SettingsLoadResult(CreateDefaultSettings(), canSaveDefaults);
    }

    /// <summary>
    /// Copies an unreadable primary settings file to a timestamped corrupt snapshot path.
    /// </summary>
    /// <param name="filePath">The primary settings file path to preserve.</param>
    /// <returns>
    /// <c>true</c> when no primary file exists or the copy succeeds; <c>false</c> when recovery should avoid saving.
    /// </returns>
    private static bool PreserveCorruptSettingsFile(string filePath)
    {
        if (!File.Exists(filePath))
            return true;

        var corruptPath = CreateCorruptFilePath(filePath);

        try
        {
            using var source = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var destination = new FileStream(corruptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
            Log.Warning("Preserved unreadable settings file at {CorruptPath}", corruptPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to preserve unreadable settings file {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Creates a unique timestamped corrupt snapshot path for a settings file.
    /// </summary>
    /// <param name="filePath">The primary settings file path.</param>
    /// <returns>A non-existing path using the `.corrupt-*` naming pattern.</returns>
    private static string CreateCorruptFilePath(string filePath)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        for (var attempt = 0; ; attempt++)
        {
            var suffix = attempt == 0 ? string.Empty : $"-{attempt}";
            var corruptPath = $"{filePath}.corrupt-{timestamp}{suffix}";
            if (!File.Exists(corruptPath))
            {
                return corruptPath;
            }
        }
    }

    /// <summary>
    /// Writes text through a same-directory temporary file and atomically replaces the destination when possible.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="contents">The complete content to write.</param>
    private static void WriteAllTextAtomically(string filePath, string contents)
    {
        var directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            directoryPath = ".";
        }

        Directory.CreateDirectory(directoryPath);
        var tempFilePath = Path.Combine(directoryPath, $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempFilePath, contents);
            if (File.Exists(filePath))
            {
                File.Replace(tempFilePath, filePath, null);
            }
            else
            {
                File.Move(tempFilePath, filePath);
            }
        }
        finally
        {
            TryDeleteFile(tempFilePath);
        }
    }

    /// <summary>
    /// Deletes a file when it exists, logging and ignoring cleanup failures.
    /// </summary>
    /// <param name="filePath">The file path to delete.</param>
    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to delete temporary file {FilePath}", filePath);
        }
    }
}
