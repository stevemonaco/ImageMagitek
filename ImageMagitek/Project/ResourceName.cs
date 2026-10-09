using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Linq;
using System.Text;

namespace ImageMagitek.Project;

/// <summary>
/// The rule a new resource or folder name must follow so it maps to a portable file or directory name on every supported OS.
/// </summary>
public static class ResourceName
{
    private const int MaxCharacters = 100;
    private const int MaxUtf8Bytes = 240;

    private static readonly SearchValues<char> _forbiddenCharacters = SearchValues.Create("<>:\"/\\|?*");

    private static readonly FrozenSet<string> _reservedNames = new[]
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks <paramref name="name"/> against the name rule and returns the first rule it breaks.
    /// </summary>
    public static MagitekResult Validate(string name, bool isFolder)
    {
        if (string.IsNullOrEmpty(name))
            return new MagitekResult.Failed("Name cannot be empty");

        if (char.IsWhiteSpace(name[0]))
            return new MagitekResult.Failed("Name cannot start with whitespace");

        if (char.IsWhiteSpace(name[^1]))
            return new MagitekResult.Failed("Name cannot end with whitespace");

        if (name[^1] == '.')
            return new MagitekResult.Failed("Name cannot end with '.'");

        var forbiddenIndex = name.AsSpan().IndexOfAny(_forbiddenCharacters);
        if (forbiddenIndex >= 0)
            return new MagitekResult.Failed($"Name cannot contain '{name[forbiddenIndex]}'");

        foreach (var c in name)
        {
            if (c < ' ')
                return new MagitekResult.Failed($"Name cannot contain the control character U+{(int)c:X4}");
        }

        var dotIndex = name.IndexOf('.');
        var stem = dotIndex >= 0 ? name[..dotIndex] : name;
        if (_reservedNames.Contains(stem))
            return new MagitekResult.Failed($"'{stem}' is a reserved device name");

        if (name.EnumerateRunes().Count() > MaxCharacters)
            return new MagitekResult.Failed($"Name cannot be longer than {MaxCharacters} characters");

        if (Encoding.UTF8.GetByteCount(name) > MaxUtf8Bytes)
            return new MagitekResult.Failed($"Name cannot be longer than {MaxUtf8Bytes} bytes as UTF-8");

        if (isFolder && name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return new MagitekResult.Failed("Folder name cannot end with '.xml'");

        return MagitekResult.SuccessResult;
    }

    /// <summary>
    /// Whether two names count as the same sibling name: equal after NFC normalization, ignoring case.
    /// </summary>
    public static bool AreSame(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string name)
    {
        try
        {
            return name.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return name;
        }
    }
}
