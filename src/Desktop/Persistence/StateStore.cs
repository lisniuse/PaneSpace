using System.Text.Json;
using PaneSpace.Core.Sessions;

namespace PaneSpace.Persistence;

/// <summary>Persists the camera (pan) and every window's logical canvas position
/// to %LOCALAPPDATA%\PaneSpace\state.json. Handles (hwnd) die with the session,
/// so identity is process-name + title, disambiguated by nearest saved position.</summary>
public static class StateStore
{
    private static string FilePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PaneSpace", "state.json");

    private static string LegacyFilePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CamCanvas", "state.json");

    public static void Save(SessionState s)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            // write-rename for atomicity
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch { /* never break the app for persistence */ }
    }

    public static SessionState? Load()
    {
        try
        {
            // Read the old product's layout on first run; subsequent saves use PaneSpace.
            string path = File.Exists(FilePath) ? FilePath : LegacyFilePath;
            return File.Exists(path)
                ? JsonSerializer.Deserialize<SessionState>(File.ReadAllText(path))
                : null;
        }
        catch { return null; }
    }
}
