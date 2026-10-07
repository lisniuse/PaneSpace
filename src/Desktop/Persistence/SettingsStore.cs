using PaneSpace.Core.Settings;

namespace PaneSpace.Persistence;

public static class SettingsStore
{
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PaneSpace", "settings.json");
    public static AppSettings Load() => JsonStore.Load<AppSettings>(FilePath) ?? new AppSettings();
    public static bool Save(AppSettings settings) => JsonStore.Save(FilePath, settings);
}
