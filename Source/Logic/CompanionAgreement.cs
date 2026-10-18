using System;
using System.Globalization;

namespace SirMissionsClient
{
    // The agreement with Sir Missions Companion, the mod that places the
    // "Missions SIRIUS" block. Both live in the player's game and talk through
    // the game's mod messages, a text each:
    // - the mod asks for the window on RequestChannel: "ouvrir", then
    //   "bloc=<entity id of the block used>";
    // - this plugin answers "ouverte" on AnswerChannel, at once and within the
    //   same call. No answer, no plugin: the mod then tells the player he needs
    //   this plugin.
    public static class CompanionAgreement
    {
        public const long RequestChannel = 5465480001L;
        public const long AnswerChannel = 5465480002L;

        public const string Open = "ouvrir";
        public const string Opened = "ouverte";
        public const string BlockKey = "bloc";

        // True when the message asks for the window; the block used, or 0.
        public static bool IsOpenRequest(object message, out long block)
        {
            block = 0;
            var text = message as string;
            if (text == null)
                return false;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            if (!string.Equals(lines[0].Trim(), Open, StringComparison.Ordinal))
                return false;
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                var prefix = BlockKey + "=";
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    long.TryParse(line.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out block);
            }
            return true;
        }
    }
}
