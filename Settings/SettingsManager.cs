using System;
using System.IO;
using System.Text.Json;
using TozoWindowsApp.Ble;

namespace TozoWindowsApp.Settings
{
    public class AppSettings
    {
        public TozoProtocol.AncMode CurrentAncMode { get; set; } = TozoProtocol.AncMode.AncOff;
        public bool RememberMySettings { get; set; } = true;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TozoWindowsApp",
            "settings.json");

        public static AppSettings Current { get; private set; } = new AppSettings();

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        Current = settings;
                    }
                }
            }
            catch
            {
                // Fallback to defaults
                Current = new AppSettings();
            }
        }

        public static void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory) && directory != null)
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Ignore save errors for now
            }
        }
    }
}
