using System.IO;
using System.Text.Json;
using ItemInterpreter.Data;

namespace ItemInterpreter.Logic
{
    public class NotificationSettingsService
    {
        private readonly string _settingsPath;

        public NotificationSettingsService(string? settingsPath = null)
        {
            _settingsPath = settingsPath ?? "notification_settings.json";
        }

        public NotificationSettings Load()
        {
            if (!File.Exists(_settingsPath))
            {
                return new NotificationSettings();
            }

            try
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<NotificationSettings>(json) ?? new NotificationSettings();
            }
            catch
            {
                return new NotificationSettings();
            }
        }

        public void Save(NotificationSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
    }
}
