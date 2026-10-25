using System.IO;
using System.Text.Json;

namespace NullSave.CharPad;

internal sealed class Settings
{

    #region Fields

    internal static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CharPad", "settings.json");

    #endregion

    #region Properties

    /// <summary>Window's left edge in device-independent pixels; null until the window has been closed once.</summary>
    public double? Left { get; set; }

    /// <summary>Window's top edge in device-independent pixels; null until the window has been closed once.</summary>
    public double? Top { get; set; }

    /// <summary>Window width in device-independent pixels (its restored size if it was maximized).</summary>
    public double Width { get; set; } = 1000;

    /// <summary>Window height in device-independent pixels (its restored size if it was maximized).</summary>
    public double Height { get; set; } = 720;

    /// <summary>Whether the window was maximized when it was closed.</summary>
    public bool Maximized { get; set; }

    /// <summary>Name of the last selected font family.</summary>
    public string Font { get; set; } = CharGrid.DEFAULT_FONT_NAME;

    /// <summary>Glyph size in points.</summary>
    public double Size { get; set; } = 24;

    /// <summary>Width of the preview pane in device-independent pixels.</summary>
    public double PreviewWidth { get; set; } = 300;

    /// <summary>Whether the advanced view was open.</summary>
    public bool Advanced { get; set; }

    /// <summary>Code page of the selected character set; 0 means Unicode.</summary>
    public int Charset { get; set; }

    #endregion

    #region Public Methods

    /// <summary>Reads the saved settings, or returns defaults if the file is missing or unreadable.</summary>
    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        { }

        return new Settings();
    }

    /// <summary>Writes the settings to <see cref="FilePath"/>; failures are ignored so closing the app never shows an error.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { }
    }

    #endregion

}
