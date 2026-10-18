using System;
using System.Collections.Generic;

namespace SirMissionsClient
{
    // A server the game can go back to: the universe the player left for a
    // mission. Its address when it has one (a universe has a public port), and
    // its Steam identity in any case.
    public sealed class ServerAddress
    {
        public string ConnectionString = string.Empty;
        public string Name = string.Empty;
        public ulong SteamId;

        public bool IsKnown
        {
            get { return !string.IsNullOrEmpty(ConnectionString) || SteamId != 0; }
        }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Name))
                    return Name;
                return string.IsNullOrEmpty(ConnectionString) ? Texts.YourUniverse : ConnectionString;
            }
        }
    }

    public enum TripAction
    {
        None,

        // The mission server is ready and the box is checked: the game leaves.
        Leave,

        // The mission server is ready and the box is not checked: the player
        // leaves with a click.
        AskToLeave,

        // The mission is over: the game goes back to the universe it came from.
        GoBack,

        // The mission is over, but the universe the game came from is not
        // known (the plugin was not there when the player left).
        GoBackUnknown,
    }

    // A player's trip: the departure he waits for on the universe, the leave
    // for the mission server, the mission, and the return. Sir Missions says
    // what becomes of the departure; the mission server says when to go back.
    public sealed class Trip
    {
        // "Leave automatically when it is ready".
        public bool AutoLeave = true;

        // The departure the player waits for, as last told; null when none.
        public DepartureNews Current { get; private set; }

        // When the current departure was first heard of.
        public DateTime? Since { get; private set; }

        // Still on the universe this long after leaving: the game's join did
        // not start (or stopped without a word). The player may try again.
        public static readonly TimeSpan LeaveTimeout = TimeSpan.FromSeconds(45);

        // The departure the game left for, never left for twice.
        public string LeftFor { get; private set; }

        // When the game left for it.
        public DateTime? LeftAt { get; private set; }

        public ulong MissionServer { get; private set; }

        // Where the game goes back at the end of the mission.
        public ServerAddress Back { get; private set; }

        public bool OnMission { get; private set; }

        public bool Returning { get; private set; }

        // The departures the player gave up. Sir Missions tells a player who
        // gives up once more about it, and maybe again at its next check:
        // these words are no longer for him, unless he joins it again himself.
        private readonly HashSet<string> m_givenUp = new HashSet<string>(StringComparer.Ordinal);

        // The player launches or joins a mission himself: he may leave again,
        // even for a departure he already left for and came back from.
        // joining: the departure he joins; null for a launch, which always
        // makes a new departure.
        public void Asked(string joining = null)
        {
            LeftFor = null;
            LeftAt = null;
            if (joining != null)
                m_givenUp.Remove(joining);
        }

        // A departure the player gave up, and did not join again.
        public bool Ignores(string id)
        {
            return id != null && m_givenUp.Contains(id);
        }

        public TripAction Hear(DepartureNews news, DateTime now)
        {
            if (news == null || Ignores(news.Id))
                return TripAction.None;
            if (Current == null || !string.Equals(Current.Id, news.Id, StringComparison.Ordinal))
                Since = now;
            Current = news;
            if (!CanLeave)
                return TripAction.None;
            return AutoLeave ? TripAction.Leave : TripAction.AskToLeave;
        }

        // Sir Missions confirmed the player no longer waits for this departure.
        public void GaveUp(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            m_givenUp.Add(id);
            if (Current != null && string.Equals(Current.Id, id, StringComparison.Ordinal))
                Forget();
        }

        public void Forget()
        {
            Current = null;
            Since = null;
        }

        public bool CanLeave
        {
            get { return Current != null && Current.IsReady && !string.Equals(LeftFor, Current.Id, StringComparison.Ordinal); }
        }

        // The game leaves for the mission server, remembering where it comes
        // from. False when there is nothing to leave for.
        public bool Leave(ServerAddress here, DateTime? now = null)
        {
            if (!CanLeave)
                return false;
            LeftFor = Current.Id;
            LeftAt = now;
            MissionServer = Current.Server;
            if (here != null && here.IsKnown && here.SteamId != MissionServer)
                Back = here;
            OnMission = false;
            Returning = false;
            return true;
        }

        // The game could not leave (the game's join is missing): the player
        // may try again.
        public void LeaveFailed()
        {
            LeftFor = null;
            LeftAt = null;
            MissionServer = 0;
        }

        // The game left for the mission server long ago, and the world of the
        // universe is still there: the leave failed.
        public bool LeaveStalled(DateTime now)
        {
            return LeftAt != null && Current != null
                && string.Equals(LeftFor, Current.Id, StringComparison.Ordinal)
                && now - LeftAt.Value >= LeaveTimeout;
        }

        // A world is loaded, on the server with this Steam identity.
        public void Arrived(ulong server)
        {
            if (MissionServer != 0 && server == MissionServer)
            {
                // Arrived: the wait is over, the mission begins.
                OnMission = true;
                Returning = false;
                Forget();
                return;
            }

            OnMission = false;
            Returning = false;
            // Back from the mission: the trip is over.
            if (LeftFor != null && Current != null && string.Equals(Current.Id, LeftFor, StringComparison.Ordinal))
                Forget();
            if (LeftFor != null)
                MissionServer = 0;
        }

        // The mission server says "go back to the universe": once only, it says
        // it again every twenty seconds to those who stay.
        public TripAction ReturnOrder()
        {
            if (Returning)
                return TripAction.None;
            Returning = true;
            return Back != null && Back.IsKnown ? TripAction.GoBack : TripAction.GoBackUnknown;
        }

        // The game could not start its way back while still on the mission
        // server: the next order to go back tries again.
        public void ReturnFailed()
        {
            Returning = false;
        }
    }
}
