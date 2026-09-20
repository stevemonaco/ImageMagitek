using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TileShop.Shared.Models;

namespace TileShop.Shared.Services;

/// <summary>
/// Holds the active <see cref="UserPreferences"/> and persists them as JSON in the user's profile
/// </summary>
public sealed class UserPreferencesStore
{
    private readonly string _fileName;
    private readonly ILogger<UserPreferencesStore> _logger;

    public UserPreferences Preferences { get; private set; } = new();

    public static string DefaultFileName { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileShop", "preferences.json");

    public UserPreferencesStore(string fileName, ILogger<UserPreferencesStore> logger)
    {
        _fileName = fileName;
        _logger = logger;
    }

    /// <summary>
    /// Loads preferences from disk, falling back to defaults when the file is missing or unreadable
    /// </summary>
    public void Load()
    {
        if (!File.Exists(_fileName))
        {
            Preferences = new();
            return;
        }

        try
        {
            using var stream = File.OpenRead(_fileName);
            Preferences = JsonSerializer.Deserialize(stream, UserPreferencesJsonContext.Default.UserPreferences) ?? new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not read preferences from '{FileName}', using defaults", _fileName);
            Preferences = new();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_fileName)!);

            // Write to a sibling file first so a crash mid-write cannot truncate the real one
            var tempFileName = _fileName + ".tmp";
            using (var stream = File.Create(tempFileName))
                JsonSerializer.Serialize(stream, Preferences, UserPreferencesJsonContext.Default.UserPreferences);

            File.Move(tempFileName, _fileName, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not save preferences to '{FileName}'", _fileName);
        }
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(UserPreferences))]
internal sealed partial class UserPreferencesJsonContext : JsonSerializerContext;
