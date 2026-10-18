using System;
using System.IO;
using System.Text;

namespace SirMissionsClient
{
    // What the player chose, kept on his machine for the next games.
    public sealed class ClientSettings
    {
        // "Leave automatically when it is ready", checked at installation.
        public bool AutoLeave = true;
    }

    // The settings file, settings.txt in the player's storage folder: one
    // "key=value" line per setting. A missing or unreadable file gives the
    // defaults; an unknown line is ignored.
    public static class SettingsFile
    {
        public const string FileName = "settings.txt";
        private const string AutoLeaveKey = "autoLeave";

        public static ClientSettings Parse(string text)
        {
            var settings = new ClientSettings();
            if (string.IsNullOrEmpty(text))
                return settings;
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                var equal = line.IndexOf('=');
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || equal <= 0)
                    continue;
                var key = line.Substring(0, equal).Trim();
                var value = line.Substring(equal + 1).Trim();
                bool flag;
                if (key == AutoLeaveKey && bool.TryParse(value, out flag))
                    settings.AutoLeave = flag;
            }
            return settings;
        }

        public static string Write(ClientSettings settings)
        {
            var text = new StringBuilder();
            text.Append("# Sir Missions Client settings.\n");
            text.Append(AutoLeaveKey).Append('=').Append((settings ?? new ClientSettings()).AutoLeave ? "true" : "false").Append('\n');
            return text.ToString();
        }

        // problem: why the defaults are used, or null.
        public static ClientSettings Load(string directory, out string problem)
        {
            problem = null;
            try
            {
                var path = Path.Combine(directory, FileName);
                if (!File.Exists(path))
                    return new ClientSettings();
                return Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                problem = "settings not read: " + e.Message;
                return new ClientSettings();
            }
        }

        public static void Save(string directory, ClientSettings settings)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, FileName), Write(settings));
        }
    }
}
