using System;
using System.Linq;

namespace SirMissionsClient
{
    public enum WindowView
    {
        // The missions were asked; no answer yet.
        Loading,

        // No answer: Sir Missions does not run here (or is switched off), as
        // on a mission server.
        NoAnswer,

        // The missions offered and those in progress.
        List,

        // The player waits for his departure.
        Waiting,
    }

    // What the window of the missions shows and does, without the game: the
    // screen draws it and passes the player's clicks on. Each method that has
    // something to ask Sir Missions gives back the text to send, or null.
    public sealed class MissionsWindowModel
    {
        public static readonly TimeSpan AnswerDelay = TimeSpan.FromSeconds(8);

        private readonly Trip m_trip;
        private long m_nextRequest = 1;
        private long m_listRequest;
        private DateTime m_askedAt;
        private bool m_awaitedAsked;
        private DateTime m_pendingSince;

        public MissionsWindowModel(Trip trip)
        {
            m_trip = trip ?? new Trip();
        }

        public Trip Trip
        {
            get { return m_trip; }
        }

        public MissionCatalog Catalog { get; private set; }

        public string SelectedMission { get; private set; }

        public string SelectedRunning { get; private set; }

        public LaunchForm Form { get; private set; }

        // A line for the player: what was refused, mostly.
        public string Status { get; private set; }

        public bool StatusIsError { get; private set; }

        // A launch or a join was sent, its answer not yet back.
        public bool Pending { get; private set; }

        // Grows at each change the screen must redraw.
        public int Version { get; private set; }

        public WindowView View(DateTime now)
        {
            if (m_trip.Current != null)
                return WindowView.Waiting;
            if (Catalog != null)
                return WindowView.List;
            return now - m_askedAt >= AnswerDelay ? WindowView.NoAnswer : WindowView.Loading;
        }

        // The window opens: the missions are asked again, so that a mission
        // added on the server shows at once.
        public string Open(DateTime now)
        {
            Status = null;
            StatusIsError = false;
            Pending = false;
            return AskList(now);
        }

        public string Refresh(DateTime now)
        {
            return AskList(now);
        }

        private string AskList(DateTime now)
        {
            Catalog = null;
            m_awaitedAsked = false;
            m_askedAt = now;
            m_listRequest = m_nextRequest++;
            Changed();
            return Requests.Missions(m_listRequest);
        }

        // An answer of Sir Missions, or a departure told: true when the window
        // must be drawn again. outgoing: what to ask next, or null.
        public bool Receive(WireMessage message, DateTime now, out string outgoing)
        {
            outgoing = null;
            if (message == null)
                return false;

            switch (message.Verb)
            {
                case Protocol.Missions:
                {
                    var catalog = MissionCatalog.Read(message);
                    if (catalog == null)
                        return false;
                    // An older answer than the last question: the next one comes.
                    if (catalog.Request != 0 && m_listRequest != 0 && catalog.Request != m_listRequest)
                        return false;
                    Catalog = catalog;
                    Reselect();
                    // He already waits for a departure (the window was closed, or the
                    // plugin restarted): joining it again gives back its state.
                    // Never one he gave up: Sir Missions may not yet know.
                    if (catalog.Awaited.Length > 0 && m_trip.Current == null && !m_awaitedAsked && !m_trip.Ignores(catalog.Awaited))
                    {
                        m_awaitedAsked = true;
                        outgoing = Requests.Join(catalog.Awaited, m_nextRequest++);
                    }
                    Changed();
                    return true;
                }

                case Protocol.Refusal:
                    Pending = false;
                    Status = Texts.Refusal(message.Value(Protocol.Reason));
                    StatusIsError = true;
                    Changed();
                    return true;

                case Protocol.Departure:
                    // A departure he gave up: no longer his.
                    if (m_trip.Ignores(message.Value(Protocol.Id)))
                        return false;
                    Pending = false;
                    Status = null;
                    StatusIsError = false;
                    Changed();
                    return true;

                case Protocol.GaveUp:
                    Pending = false;
                    m_trip.GaveUp(message.Value(Protocol.Departure));
                    outgoing = AskList(now);
                    return true;

                default:
                    return false;
            }
        }

        // Called often: true when the window must be drawn again by itself (no
        // answer to the list, or to a launch, a join or a give up).
        public bool Tick(DateTime now, WindowView before)
        {
            if (Pending && now - m_pendingSince >= AnswerDelay)
            {
                Pending = false;
                Status = Texts.NoAnswerToRequest;
                StatusIsError = true;
                Changed();
                return true;
            }
            return View(now) != before;
        }

        private void Wait(DateTime now)
        {
            Pending = true;
            m_pendingSince = now;
            Status = null;
            StatusIsError = false;
            Changed();
        }

        public void SelectMission(string id)
        {
            var mission = Catalog == null ? null : Catalog.Mission(id);
            if (mission == null)
            {
                SelectedMission = null;
                Form = null;
            }
            else if (SelectedMission != mission.Id || Form == null)
            {
                SelectedMission = mission.Id;
                Form = new LaunchForm(mission);
            }
        }

        public void SelectRunning(string id)
        {
            var running = Catalog == null ? null : Catalog.RunningById(id);
            SelectedRunning = running == null ? null : running.Id;
        }

        public OfferedMission Mission
        {
            get { return Form == null ? null : Form.Mission; }
        }

        public RunningMission Running
        {
            get { return Catalog == null || SelectedRunning == null ? null : Catalog.RunningById(SelectedRunning); }
        }

        public bool CanLaunch
        {
            get { return !Pending && Form != null && Form.CanLaunch; }
        }

        public bool CanJoin
        {
            get { return !Pending && Running != null && Running.CanJoin; }
        }

        public string Launch(DateTime now)
        {
            if (!CanLaunch)
                return null;
            Wait(now);
            m_trip.Asked();
            return Form.Request(m_nextRequest++);
        }

        // A mission in progress is joined without choosing anything.
        public string Join(DateTime now)
        {
            if (!CanJoin)
                return null;
            Wait(now);
            m_trip.Asked(Running.Id);
            return Requests.Join(Running.Id, m_nextRequest++);
        }

        public bool CanGiveUp
        {
            get { return m_trip.Current != null && !m_trip.Current.IsEnded && !string.Equals(m_trip.LeftFor, m_trip.Current.Id, StringComparison.Ordinal); }
        }

        public string GiveUp(DateTime now)
        {
            if (!CanGiveUp)
                return null;
            Wait(now);
            return Requests.GiveUp(m_trip.Current.Id, m_nextRequest++);
        }

        // An ended departure (over, or failed) leaves the wait for the list.
        public bool CanGoBackToList
        {
            get { return m_trip.Current != null && m_trip.Current.IsEnded; }
        }

        public string BackToList(DateTime now)
        {
            if (!CanGoBackToList)
                return null;
            m_trip.Forget();
            return AskList(now);
        }

        public void Changed()
        {
            Version++;
        }

        // After a new list, the same mission stays selected when it is still
        // offered, with the values already picked.
        private void Reselect()
        {
            var keep = Form;
            if (SelectedMission != null && Catalog.Mission(SelectedMission) == null)
            {
                SelectedMission = null;
                Form = null;
            }
            if (SelectedMission == null && Catalog.Missions.Count > 0)
                SelectedMission = Catalog.Missions[0].Id;
            if (SelectedMission != null)
            {
                var mission = Catalog.Mission(SelectedMission);
                var form = new LaunchForm(mission);
                if (keep != null && keep.Mission != null && keep.Mission.Id == mission.Id)
                {
                    foreach (var picked in keep.Values())
                        form.Pick(picked.Key, picked.Value);
                }
                Form = form;
            }
            if (SelectedRunning != null && Catalog.RunningById(SelectedRunning) == null)
                SelectedRunning = null;
            if (SelectedRunning == null)
            {
                var first = Catalog.Running.FirstOrDefault(r => r.CanJoin) ?? Catalog.Running.FirstOrDefault();
                SelectedRunning = first == null ? null : first.Id;
            }
        }
    }
}
