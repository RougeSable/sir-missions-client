using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SirMissionsClient
{
    // The words of the agreement with Sir Missions, the server plugin of the
    // universe, as it fixed them (its README, "L'accord avec Sir Missions
    // Client"). They travel as they are: they are the agreement's own words,
    // never shown to the player.
    public static class Protocol
    {
        // Where this plugin asks, and where Sir Missions answers and tells a
        // player what becomes of his departure. A mission server says "go back"
        // on the same channel. These are the channels of Sir Missions since its
        // ticket #822, which left 47231 and 47232 (47231 is also Sir Joueurs').
        public const ushort RequestChannel = 47241;
        public const ushort AnswerChannel = 47242;

        // Asked by this plugin.
        public const string Missions = "missions";
        public const string Launch = "lancer";
        public const string Join = "rejoindre";
        public const string GiveUp = "quitter";

        // Answered or told by Sir Missions.
        public const string Departure = "depart";
        public const string GaveUp = "quitte";
        public const string Refusal = "refus";
        public const string Return = "retour";

        // Blocks of the "missions" answer.
        public const string MissionBlock = "mission";
        public const string RunningBlock = "enCours";

        public const string Request = "requete";
        public const string ChoicePrefix = "choix.";
        public const string Awaited = "attendu";

        // Keys of the blocks and of a departure.
        public const string Id = "id";
        public const string Name = "nom";
        public const string Description = "description";
        public const string World = "monde";
        public const string MaxPlayers = "joueursMax";
        public const string Duration = "duree";
        public const string Reward = "recompense";
        public const string Choice = "choix";
        public const string ChoiceValue = "valeur";
        public const string State = "etat";
        public const string Players = "joueurs";
        public const string Places = "places";
        public const string Launcher = "lanceur";
        public const string StartTime = "demarrage";
        public const string Server = "serveur";
        public const string Reason = "raison";

        // Reasons of a refusal: this plugin words them for the player.
        public const string UnknownMission = "mission-inconnue";
        public const string IncompleteMission = "mission-incomplete";
        public const string MissingChoice = "choix-manquant";
        public const string InvalidChoice = "choix-invalide";
        public const string Full = "complete";
        public const string AlreadyWaiting = "deja-en-mission";
        public const string UnknownDeparture = "depart-inconnu";
        public const string ClosedDeparture = "depart-ferme";
        public const string UnknownPlayer = "joueur-inconnu";
    }

    // A text exchanged with Sir Missions. Its first line names what is asked
    // or answered; each other line is "key=value". In keys and values, "\" is
    // written "\\", a line break "\n" and "=" "\e". A line alone ("mission",
    // "enCours") opens a block whose lines follow.
    public sealed class WireMessage
    {
        private const char Escape = '\\';

        private readonly List<KeyValuePair<string, string>> m_lines;

        private WireMessage(string verb, List<KeyValuePair<string, string>> lines)
        {
            Verb = verb;
            m_lines = lines;
        }

        public string Verb { get; private set; }

        // Every line after the first, in order; a line alone has a null value.
        public IReadOnlyList<KeyValuePair<string, string>> Lines
        {
            get { return m_lines; }
        }

        // The first value of a key, or an empty text.
        public string Value(string key)
        {
            foreach (var line in m_lines)
            {
                if (line.Value != null && string.Equals(line.Key, key, StringComparison.Ordinal))
                    return line.Value;
            }
            return string.Empty;
        }

        // Every "prefix.name=value" line, by name, in order.
        public List<KeyValuePair<string, string>> Prefixed(string prefix)
        {
            return m_lines
                .Where(l => l.Value != null && l.Key.StartsWith(prefix, StringComparison.Ordinal) && l.Key.Length > prefix.Length)
                .Select(l => new KeyValuePair<string, string>(l.Key.Substring(prefix.Length), l.Value))
                .ToList();
        }

        // A text read back, or null when it is not a message at all.
        public static WireMessage Read(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            var lines = text.Replace("\r\n", "\n").Split('\n');
            var verb = lines[0].Trim();
            if (verb.Length == 0)
                return null;

            var read = new List<KeyValuePair<string, string>>();
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0)
                    continue;
                var equal = line.IndexOf('=');
                if (equal < 0)
                    read.Add(new KeyValuePair<string, string>(Unprotect(line.Trim()), null));
                else
                    read.Add(new KeyValuePair<string, string>(Unprotect(line.Substring(0, equal)), Unprotect(line.Substring(equal + 1))));
            }
            return new WireMessage(verb, read);
        }

        public static string Protect(string text)
        {
            var result = new StringBuilder();
            foreach (var character in text ?? string.Empty)
            {
                if (character == Escape)
                    result.Append(Escape).Append(Escape);
                else if (character == '\n')
                    result.Append(Escape).Append('n');
                else if (character == '\r')
                    continue;
                else if (character == '=')
                    result.Append(Escape).Append('e');
                else
                    result.Append(character);
            }
            return result.ToString();
        }

        public static string Unprotect(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Escape) < 0)
                return text ?? string.Empty;

            var result = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];
                if (character != Escape || i + 1 >= text.Length)
                {
                    result.Append(character);
                    continue;
                }
                var next = text[++i];
                if (next == 'n')
                    result.Append('\n');
                else if (next == 'e')
                    result.Append('=');
                else
                    result.Append(next);
            }
            return result.ToString();
        }

        public static long Number(string text)
        {
            long value;
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0L;
        }

        public static ulong SteamId(string text)
        {
            ulong value;
            return ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) ? value : 0UL;
        }
    }

    // Writes a message, line by line.
    public sealed class WireWriter
    {
        private readonly StringBuilder m_text = new StringBuilder();

        public WireWriter(string verb)
        {
            m_text.Append(verb);
        }

        public WireWriter Line(string key, string value)
        {
            m_text.Append('\n').Append(WireMessage.Protect(key)).Append('=').Append(WireMessage.Protect(value));
            return this;
        }

        public WireWriter Line(string key, long value)
        {
            return Line(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public string Text()
        {
            return m_text.ToString();
        }
    }

    // What this plugin asks Sir Missions.
    public static class Requests
    {
        public static string Missions(long request)
        {
            return new WireWriter(Protocol.Missions).Line(Protocol.Request, request).Text();
        }

        // A launch, with the value picked for each choice of the mission.
        public static string Launch(string missionId, IEnumerable<KeyValuePair<string, string>> choices, long request)
        {
            var writer = new WireWriter(Protocol.Launch).Line(Protocol.Request, request).Line(Protocol.MissionBlock, missionId ?? string.Empty);
            if (choices != null)
            {
                foreach (var choice in choices)
                    writer.Line(Protocol.ChoicePrefix + choice.Key, choice.Value ?? string.Empty);
            }
            return writer.Text();
        }

        // A join: the players who join choose nothing.
        public static string Join(string departureId, long request)
        {
            return new WireWriter(Protocol.Join).Line(Protocol.Request, request).Line(Protocol.Departure, departureId ?? string.Empty).Text();
        }

        public static string GiveUp(string departureId, long request)
        {
            return new WireWriter(Protocol.GiveUp).Line(Protocol.Request, request).Line(Protocol.Departure, departureId ?? string.Empty).Text();
        }
    }
}
