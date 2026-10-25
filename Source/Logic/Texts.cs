using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SirMissionsClient
{
    // Every text the player reads, in English.
    public static class Texts
    {
        public const string Caption = "SIRIUS Missions";
        public const string YourUniverse = "your universe";

        // The window.
        public const string Loading = "Asking the server for its missions...";
        public const string NoAnswer = "No missions are offered on this server.";
        public const string NoAnswerToRequest = "The server did not answer: try again.";
        public const string Offered = "Missions offered";
        public const string InProgress = "Missions in progress";
        public const string NoMission = "No mission is offered at the moment.";
        public const string NoRunning = "No mission in progress can be joined.";
        public const string PickMission = "Select a mission to read it.";
        public const string PickRunning = "Select a mission in progress to join it.";
        public const string ColumnName = "Mission";
        public const string ColumnMaxPlayers = "Players";
        public const string ColumnDuration = "Duration";
        public const string ColumnReward = "Reward";
        public const string ColumnLauncher = "Launched by";
        public const string ColumnPlaces = "Places left";
        public const string ColumnState = "State";
        public const string LaunchButton = "Launch";
        public const string JoinButton = "Join";
        public const string CloseButton = "Close";
        public const string RefreshButton = "Refresh";
        public const string PickValue = "Pick a value";
        public const string Full = "Full";
        public const string FullHelp = "This mission is full: it can no longer be joined.";
        public const string JoinNoChoice = "You join with the choices of the player who launched it.";
        public const string ChoicesToMake = "Pick a value for each choice, then launch.";
        public const string NoChoiceToMake = "This mission has no choice: it launches at once.";

        // The wait.
        public const string AutoLeave = "Leave automatically when it is ready";
        public const string AutoLeaveHelp = "Checked, your game joins the mission server by itself as soon as it is ready. Unchecked, a button lets you leave when you want.";
        public const string LeaveButton = "Leave now";
        public const string GiveUpButton = "Give up";
        public const string BackButton = "Back to the missions";
        public const string StateAsked = "Sir Missions asked the studio for a mission server.";
        public const string StateWaitingForTurn = "All mission servers are busy: your mission waits for its turn. It starts as soon as a mission server is free.";
        public const string StateStarting = "The mission server is starting.";
        public const string StateReadyAuto = "The mission server is ready: your game joins it.";
        public const string StateReadyClick = "The mission server is ready: click \"Leave now\" to join it.";
        public const string StateReadyLeft = "Your game is joining the mission server.";
        public const string StateOver = "This mission is over.";
        public const string StateFailed = "The mission server could not start. You can launch the mission again.";
        public const string StateUnknown = "Waiting for news of the mission server.";

        // Notifications.
        public const string NotifyReady = "Your mission server is ready.";
        public const string NotifyLeaving = "Leaving for the mission...";
        public const string NotifyGoingBack = "The mission is over: going back to {0}.";
        public const string NotifyGoBackUnknown = "The mission is over: join your universe again from the server list.";
        public const string NotifyJoinFailed = "Your game could not join the mission server. You can try again, or give up.";
        public const string NotifyNoJoin = "This version of the game cannot join a mission server: Sir Missions Client needs an update.";
        public const string NotifyFault = "Sir Missions Client stopped after a fault: see the game log.";

        // The way back, offered at the main menu.
        public const string WayBackCaption = "Back from the mission";
        public const string WayBackQuestion = "The game is no longer on the mission server. Go back to {0}?";
        public const string WayBackAfterMissedMission = "Your game could not join the mission server. Go back to {0}?";
        public const string MissedMission = "Your game could not join the mission server. Join it again, or pick another mission.";

        public static string Refusal(string reason)
        {
            switch (reason)
            {
                case Protocol.UnknownMission: return "This mission is no longer offered.";
                case Protocol.IncompleteMission: return "This mission is not complete on the server: it cannot be launched.";
                case Protocol.MissingChoice: return "Pick a value for each choice before launching.";
                case Protocol.InvalidChoice: return "One of the values picked is not offered by this mission.";
                case Protocol.Full: return "This mission is full.";
                case Protocol.AlreadyWaiting: return "You already wait for another mission.";
                case Protocol.UnknownDeparture: return "This mission is no longer in progress.";
                case Protocol.ClosedDeparture: return "This mission can no longer be joined.";
                case Protocol.UnknownPlayer: return "The server does not know you yet: try again in a moment.";
                default: return "The server refused.";
            }
        }

        public static string State(DepartureState state)
        {
            switch (state)
            {
                case DepartureState.Asked: return "Asked";
                case DepartureState.WaitingForTurn: return "Waiting for its turn";
                case DepartureState.Starting: return "Starting";
                case DepartureState.Ready: return "In progress";
                case DepartureState.Over: return "Over";
                case DepartureState.Failed: return "Failed";
                default: return "Waiting";
            }
        }

        // What the wait says of the departure.
        public static string Waiting(DepartureNews news, bool autoLeave, bool left)
        {
            if (news == null)
                return StateUnknown;
            switch (news.State)
            {
                case DepartureState.Asked: return StateAsked;
                case DepartureState.WaitingForTurn: return StateWaitingForTurn;
                case DepartureState.Starting: return StateStarting;
                case DepartureState.Ready:
                    if (left)
                        return StateReadyLeft;
                    return autoLeave ? StateReadyAuto : StateReadyClick;
                case DepartureState.Over: return StateOver;
                case DepartureState.Failed: return StateFailed;
                default: return StateUnknown;
            }
        }

        public static string Duration(long seconds)
        {
            if (seconds <= 0)
                return "No time limit";
            if (seconds < 60)
                return Count(seconds, "second");
            var minutes = (seconds + 30) / 60;
            var days = minutes / 1440;
            var hours = minutes % 1440 / 60;
            var rest = minutes % 60;
            var parts = new List<string>();
            if (days > 0)
                parts.Add(Count(days, "day"));
            if (hours > 0)
                parts.Add(Count(hours, "hour"));
            if (rest > 0)
                parts.Add(Count(rest, "minute"));
            return string.Join(" ", parts.ToArray());
        }

        public static string Reward(long credits)
        {
            if (credits <= 0)
                return "No reward";
            return credits.ToString("N0", CultureInfo.InvariantCulture) + " credits";
        }

        public static string MaxPlayers(long players)
        {
            return players <= 0 ? "-" : players.ToString(CultureInfo.InvariantCulture);
        }

        public static string Players(long players, long maxPlayers)
        {
            return players.ToString(CultureInfo.InvariantCulture) + " / " + MaxPlayers(maxPlayers);
        }

        public static string Places(long places)
        {
            return places <= 0 ? Full : places.ToString(CultureInfo.InvariantCulture);
        }

        // The start time announced: "about 2 minutes".
        public static string StartTime(long seconds)
        {
            if (seconds <= 0)
                return "unknown";
            if (seconds < 90)
                return "about " + Count(seconds, "second");
            return "about " + Count((seconds + 30) / 60, "minute");
        }

        public static string Elapsed(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;
            var minutes = (long)elapsed.TotalMinutes;
            return minutes.ToString(CultureInfo.InvariantCulture) + ":" + elapsed.Seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        public static string Choices(IEnumerable<KeyValuePair<string, string>> choices)
        {
            var list = (choices ?? Enumerable.Empty<KeyValuePair<string, string>>()).Select(c => c.Key + ": " + c.Value).ToArray();
            return list.Length == 0 ? "None" : string.Join(", ", list);
        }

        // The block of text that describes a mission offered.
        public static string Describe(OfferedMission mission)
        {
            if (mission == null)
                return PickMission;
            var lines = new List<string>();
            lines.Add(mission.Name);
            if (!string.IsNullOrEmpty(mission.Description))
                lines.Add(mission.Description);
            lines.Add("");
            lines.Add("Players at most: " + MaxPlayers(mission.MaxPlayers) + "    Duration: " + Duration(mission.DurationSeconds) + "    Reward: " + Reward(mission.Reward) + " each");
            if (!string.IsNullOrEmpty(mission.World))
                lines.Add("World: " + mission.World);
            return string.Join("\n", lines.ToArray());
        }

        public static string Describe(RunningMission running, OfferedMission mission)
        {
            if (running == null)
                return PickRunning;
            var lines = new List<string>();
            lines.Add(running.Name + (string.IsNullOrEmpty(running.Launcher) ? "" : ", launched by " + running.Launcher));
            if (mission != null && !string.IsNullOrEmpty(mission.Description))
                lines.Add(mission.Description);
            lines.Add("");
            lines.Add("Players: " + Players(running.Players, running.MaxPlayers) + "    Places left: " + Places(running.Places) + "    " + State(running.State));
            lines.Add("Duration: " + Duration(running.DurationSeconds) + "    Reward: " + Reward(running.Reward) + " each");
            lines.Add("Choices of the launcher: " + Choices(running.Choices));
            lines.Add(running.CanJoin ? JoinNoChoice : FullHelp);
            return string.Join("\n", lines.ToArray());
        }

        // The block of text of the wait.
        public static string Describe(DepartureNews news, DateTime? since, DateTime now, bool autoLeave, bool left)
        {
            if (news == null)
                return StateUnknown;
            var lines = new List<string>();
            lines.Add(news.Name);
            lines.Add("");
            lines.Add(Waiting(news, autoLeave, left));
            lines.Add("");
            if (news.IsWaiting)
            {
                lines.Add("Announced start time: " + StartTime(news.StartSeconds)
                    + (since.HasValue ? "    Waiting for " + Elapsed(now - since.Value) : ""));
            }
            lines.Add("Players: " + Players(news.Players, news.MaxPlayers) + "    Duration: " + Duration(news.DurationSeconds) + "    Reward: " + Reward(news.Reward) + " each");
            lines.Add("Choices: " + Choices(news.Choices));
            return string.Join("\n", lines.ToArray());
        }

        private static string Count(long count, string unit)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + unit + (count == 1 ? "" : "s");
        }
    }
}
