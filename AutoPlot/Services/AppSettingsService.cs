using AutoPlot.Models;
using System.IO;
using System.Text.Json;

namespace AutoPlot.Services
{
    public static class AppSettingsService
    {
        private static readonly string SettingsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoPlot");

        private static readonly string SettingsPath =
            Path.Combine(SettingsDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new AppSettings();

                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(SettingsDirectory);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, options));
        }
    }
}
