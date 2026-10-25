using System;
using System.Collections.Generic;
using System.Linq;

namespace SirMissionsClient
{
    // A choice a mission declares, made by the player who launches it (a
    // mode, for instance): its name and the values it may take, in order.
    public sealed class MissionChoice
    {
        public string Name = string.Empty;
        public readonly List<string> Values = new List<string>();
    }

    // A mission offered by Sir Missions, as the window shows it.
    public sealed class OfferedMission
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string World = string.Empty;
        public long MaxPlayers;

        // In seconds; 0 when the mission has no time limit.
        public long DurationSeconds;

        // Credits, for each player.
        public long Reward;

        public readonly List<MissionChoice> Choices = new List<MissionChoice>();

        public bool HasChoices
        {
            get { return Choices.Any(c => c.Values.Count > 0); }
        }
    }

    // A mission launched by a player, that others may still join.
    public sealed class RunningMission
    {
        public string Id = string.Empty;
        public string MissionId = string.Empty;
        public string Name = string.Empty;
        public DepartureState State;
        public long Players;
        public long MaxPlayers;
        public long Places;
        public string Launcher = string.Empty;
        public long DurationSeconds;
        public long Reward;

        // The launcher's choices: those who join keep them.
        public readonly List<KeyValuePair<string, string>> Choices = new List<KeyValuePair<string, string>>();

        // A full mission is never offered to join.
        public bool CanJoin
        {
            get { return Places > 0 && State != DepartureState.Over && State != DepartureState.Failed; }
        }
    }

    // The answer to "missions": the missions offered, the missions in
    // progress, and the departure the asking player already waits for.
    public sealed class MissionCatalog
    {
        public string Awaited = string.Empty;
        public long Request;
        public readonly List<OfferedMission> Missions = new List<OfferedMission>();
        public readonly List<RunningMission> Running = new List<RunningMission>();

        public OfferedMission Mission(string id)
        {
            return Missions.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.Ordinal));
        }

        public RunningMission RunningById(string id)
        {
            return Running.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.Ordinal));
        }

        public static MissionCatalog Read(WireMessage message)
        {
            if (message == null || message.Verb != Protocol.Missions)
                return null;

            var catalog = new MissionCatalog();
            OfferedMission mission = null;
            RunningMission running = null;
            foreach (var line in message.Lines)
            {
                if (line.Value == null)
                {
                    mission = null;
                    running = null;
                    if (line.Key == Protocol.MissionBlock)
                    {
                        mission = new OfferedMission();
                        catalog.Missions.Add(mission);
                    }
                    else if (line.Key == Protocol.RunningBlock)
                    {
                        running = new RunningMission();
                        catalog.Running.Add(running);
                    }
                    continue;
                }

                if (mission != null)
                    ReadMissionLine(mission, line.Key, line.Value);
                else if (running != null)
                    ReadRunningLine(running, line.Key, line.Value);
                else if (line.Key == Protocol.Awaited)
                    catalog.Awaited = line.Value;
                else if (line.Key == Protocol.Request)
                    catalog.Request = WireMessage.Number(line.Value);
            }

            catalog.Missions.RemoveAll(m => m.Id.Length == 0);
            catalog.Running.RemoveAll(m => m.Id.Length == 0);
            return catalog;
        }

        private static void ReadMissionLine(OfferedMission mission, string key, string value)
        {
            switch (key)
            {
                case Protocol.Id: mission.Id = value; break;
                case Protocol.Name: mission.Name = value; break;
                case Protocol.Description: mission.Description = value; break;
                case Protocol.World: mission.World = value; break;
                case Protocol.MaxPlayers: mission.MaxPlayers = WireMessage.Number(value); break;
                case Protocol.Duration: mission.DurationSeconds = WireMessage.Number(value); break;
                case Protocol.Reward: mission.Reward = WireMessage.Number(value); break;
                case Protocol.Choice:
                    mission.Choices.Add(new MissionChoice { Name = value });
                    break;
                case Protocol.ChoiceValue:
                    if (mission.Choices.Count > 0)
                        mission.Choices[mission.Choices.Count - 1].Values.Add(value);
                    break;
            }
        }

        private static void ReadRunningLine(RunningMission running, string key, string value)
        {
            switch (key)
            {
                case Protocol.Id: running.Id = value; break;
                case Protocol.MissionBlock: running.MissionId = value; break;
                case Protocol.Name: running.Name = value; break;
                case Protocol.State: running.State = Departures.StateOf(value); break;
                case Protocol.Players: running.Players = WireMessage.Number(value); break;
                case Protocol.MaxPlayers: running.MaxPlayers = WireMessage.Number(value); break;
                case Protocol.Places: running.Places = WireMessage.Number(value); break;
                case Protocol.Launcher: running.Launcher = value; break;
                case Protocol.Duration: running.DurationSeconds = WireMessage.Number(value); break;
                case Protocol.Reward: running.Reward = WireMessage.Number(value); break;
                default:
                    if (key.StartsWith(Protocol.ChoicePrefix, StringComparison.Ordinal) && key.Length > Protocol.ChoicePrefix.Length)
                        running.Choices.Add(new KeyValuePair<string, string>(key.Substring(Protocol.ChoicePrefix.Length), value));
                    break;
            }
        }
    }

    public enum DepartureState
    {
        Unknown,

        // Sir Missions asked the studio for a mission server.
        Asked,

        // The studio's limit of mission servers is reached: the mission waits for its turn.
        WaitingForTurn,

        // The mission server is starting.
        Starting,

        // The mission server is ready: its Steam identity comes with it.
        Ready,

        Over,

        // The studio could not start the mission server.
        Failed,
    }

    // What Sir Missions tells a player of a departure, at the launch, at the
    // join and at each change.
    public sealed class DepartureNews
    {
        public string Id = string.Empty;
        public string MissionId = string.Empty;
        public string Name = string.Empty;
        public DepartureState State;
        public long StartSeconds;
        public long Players;
        public long MaxPlayers;
        public long DurationSeconds;
        public long Reward;
        public long Request;

        // The mission server's Steam identity, once ready; 0 before.
        public ulong Server;

        public readonly List<KeyValuePair<string, string>> Choices = new List<KeyValuePair<string, string>>();

        public bool IsReady
        {
            get { return State == DepartureState.Ready && Server != 0; }
        }

        public bool IsWaiting
        {
            get { return State == DepartureState.Asked || State == DepartureState.WaitingForTurn || State == DepartureState.Starting || State == DepartureState.Unknown; }
        }

        public bool IsEnded
        {
            get { return State == DepartureState.Over || State == DepartureState.Failed; }
        }

        public static DepartureNews Read(WireMessage message)
        {
            if (message == null || message.Verb != Protocol.Departure)
                return null;
            var news = new DepartureNews
            {
                Id = message.Value(Protocol.Id),
                MissionId = message.Value(Protocol.MissionBlock),
                Name = message.Value(Protocol.Name),
                State = Departures.StateOf(message.Value(Protocol.State)),
                StartSeconds = WireMessage.Number(message.Value(Protocol.StartTime)),
                Players = WireMessage.Number(message.Value(Protocol.Players)),
                MaxPlayers = WireMessage.Number(message.Value(Protocol.MaxPlayers)),
                DurationSeconds = WireMessage.Number(message.Value(Protocol.Duration)),
                Reward = WireMessage.Number(message.Value(Protocol.Reward)),
                Server = WireMessage.SteamId(message.Value(Protocol.Server)),
                Request = WireMessage.Number(message.Value(Protocol.Request)),
            };
            news.Choices.AddRange(message.Prefixed(Protocol.ChoicePrefix));
            return news.Id.Length == 0 ? null : news;
        }
    }

    public static class Departures
    {
        public static DepartureState StateOf(string code)
        {
            switch (code)
            {
                case "demande": return DepartureState.Asked;
                case "attente-de-son-tour": return DepartureState.WaitingForTurn;
                case "demarrage": return DepartureState.Starting;
                case "pret": return DepartureState.Ready;
                case "termine": return DepartureState.Over;
                case "echec": return DepartureState.Failed;
                default: return DepartureState.Unknown;
            }
        }
    }
}
