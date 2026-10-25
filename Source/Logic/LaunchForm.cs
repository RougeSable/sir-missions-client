using System;
using System.Collections.Generic;
using System.Linq;

namespace SirMissionsClient
{
    // The choices the launcher makes before launching a mission: one value
    // for each choice the mission declares. A mission without choices is
    // launched at once; nothing is picked in advance, so that the launcher
    // never sends a value he did not see.
    public sealed class LaunchForm
    {
        private readonly Dictionary<string, string> m_picked = new Dictionary<string, string>(StringComparer.Ordinal);

        public LaunchForm(OfferedMission mission)
        {
            Mission = mission;
        }

        public OfferedMission Mission { get; private set; }

        // The choices that ask for a value: a choice without any value is not
        // one (Sir Missions does not offer such a mission anyway).
        public IEnumerable<MissionChoice> Choices
        {
            get { return Mission == null ? Enumerable.Empty<MissionChoice>() : Mission.Choices.Where(c => c.Values.Count > 0); }
        }

        // False when the value is not one of the choice's.
        public bool Pick(string choice, string value)
        {
            var declared = Choices.FirstOrDefault(c => string.Equals(c.Name, choice, StringComparison.Ordinal));
            if (declared == null || !declared.Values.Contains(value))
                return false;
            m_picked[choice] = value;
            return true;
        }

        public string Picked(string choice)
        {
            string value;
            return choice != null && m_picked.TryGetValue(choice, out value) ? value : null;
        }

        public List<string> Missing()
        {
            return Choices.Where(c => Picked(c.Name) == null).Select(c => c.Name).ToList();
        }

        public bool CanLaunch
        {
            get { return Mission != null && Missing().Count == 0; }
        }

        // The values picked, in the order the mission declares its choices.
        public List<KeyValuePair<string, string>> Values()
        {
            return Choices
                .Where(c => Picked(c.Name) != null)
                .Select(c => new KeyValuePair<string, string>(c.Name, Picked(c.Name)))
                .ToList();
        }

        public string Request(long request)
        {
            return Mission == null ? null : Requests.Launch(Mission.Id, Values(), request);
        }
    }
}
