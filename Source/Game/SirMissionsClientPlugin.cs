using System;
using System.IO;
using System.Text;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.FileSystem;
using VRage.Plugins;
using VRage.Utils;

namespace SirMissionsClient
{
    // Sir Missions Client, a client-side plugin loaded by Pulsar. It draws the
    // window of the missions, opened from the "Missions SIRIUS" block of Sir
    // Missions Companion, and makes the player leave for a mission server and
    // come back. Sir Missions, on the universe, keeps the missions: this
    // plugin only asks it, and follows what it says. It changes nothing else
    // in the game, and a player without it plays as usual.
    public class SirMissionsClientPlugin : IPlugin
    {
        public const string Id = "sir-missions-client";

        // After this many faults in a row the plugin stops for the session,
        // rather than writing the same fault to the log every frame.
        private const int MaximumFaults = 20;

        private readonly Trip m_trip = new Trip();
        private readonly WayBack m_wayBack = new WayBack();
        private MissionsWindowModel m_model;
        private MissionsScreen m_screen;
        private ClientSettings m_settings = new ClientSettings();
        private string m_settingsDirectory;

        private Action<ushort, byte[], ulong, bool> m_receive;
        private Action<object> m_companion;
        private bool m_hooked;
        private bool m_openAsked;
        private int m_faults;
        private bool m_stopped;

        public static SirMissionsClientPlugin Instance { get; private set; }

        public static void Log(string text)
        {
            MyLog.Default.WriteLine("[" + Id + "] " + text);
        }

        public void Init(object gameInstance)
        {
            Instance = this;

            // The player's settings folder: %AppData%\SpaceEngineers\Storage\sir-missions-client
            m_settingsDirectory = Path.Combine(MyFileSystem.UserDataPath, "Storage", Id);
            string problem;
            m_settings = SettingsFile.Load(m_settingsDirectory, out problem);
            if (problem != null)
                Log(problem + "; using the defaults");
            m_trip.AutoLeave = m_settings.AutoLeave;
            m_model = new MissionsWindowModel(m_trip);
            Log("loaded, leave automatically: " + (m_settings.AutoLeave ? "yes" : "no"));
        }

        public void Update()
        {
            if (m_stopped)
                return;
            try
            {
                var now = DateTime.UtcNow;
                var inWorld = InWorld();
                if (inWorld && !m_hooked)
                    Hook(now);
                else if (!inWorld && m_hooked)
                    Unhook(now);

                if (m_openAsked)
                {
                    m_openAsked = false;
                    if (inWorld)
                        OpenWindow();
                }

                // Still here long after leaving: the game's join did not happen.
                if (inWorld && m_hooked && m_trip.LeaveStalled(now))
                    LeaveFailed(Texts.NotifyJoinFailed);

                var busy = inWorld ? false : GameJoin.JoinScreensOpen();
                if (m_wayBack.Update(now, inWorld, busy))
                    OfferWayBack();
                m_faults = 0;
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void Fault(Exception e)
        {
            Log("fault: " + e);
            if (++m_faults < MaximumFaults)
                return;
            m_stopped = true;
            Log("too many faults: stopped for this game");
            try
            {
                Unhook(DateTime.UtcNow);
                Notify(Texts.NotifyFault, 10000, "Red");
            }
            catch (Exception)
            {
            }
        }

        private static bool InWorld()
        {
            var session = MySession.Static;
            return session != null && session.Ready
                && MyAPIGateway.Session != null
                && MyAPIGateway.Multiplayer != null
                && MyAPIGateway.Utilities != null;
        }

        // A world is loaded: listen to Sir Missions and to the block.
        private void Hook(DateTime now)
        {
            m_hooked = true;
            m_receive = OnServerMessage;
            m_companion = OnCompanionMessage;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(Protocol.AnswerChannel, m_receive);
            MyAPIGateway.Utilities.RegisterMessageHandler(CompanionAgreement.RequestChannel, m_companion);

            var server = GameJoin.CurrentSteamId();
            m_trip.Arrived(server);
            if (m_trip.OnMission)
                Log("on the mission server " + server);
            m_model.Changed();
        }

        private void Unhook(DateTime now)
        {
            m_hooked = false;
            try
            {
                if (MyAPIGateway.Multiplayer != null && m_receive != null)
                    MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(Protocol.AnswerChannel, m_receive);
                if (MyAPIGateway.Utilities != null && m_companion != null)
                    MyAPIGateway.Utilities.UnregisterMessageHandler(CompanionAgreement.RequestChannel, m_companion);
            }
            catch (Exception)
            {
                // The game is going away with its handlers.
            }
            m_receive = null;
            m_companion = null;
            m_screen = null;

            // The mission session ended without the order to go back (a lost
            // connection, the server gone): the way back is offered at the menu.
            if (m_trip.OnMission && !m_trip.Returning && m_trip.Back != null && m_trip.Back.IsKnown)
                m_wayBack.Arm(now);
        }

        // ------------------------------------------------------------------
        // Sir Missions Companion: the block asks for the window.

        private void OnCompanionMessage(object message)
        {
            try
            {
                long block;
                if (!CompanionAgreement.IsOpenRequest(message, out block))
                    return;
                // The answer goes within the same call: the mod then knows the
                // plugin is there. The window opens at the next update.
                MyAPIGateway.Utilities.SendModMessage(CompanionAgreement.AnswerChannel, CompanionAgreement.Opened);
                m_openAsked = true;
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void OpenWindow()
        {
            if (m_screen != null && m_screen.State != MyGuiScreenState.CLOSED && m_screen.State != MyGuiScreenState.CLOSING)
                return;
            Send(m_model.Open(DateTime.UtcNow));
            var screen = new MissionsScreen(this, m_model);
            screen.Closed += (source, isUnloading) =>
            {
                if (m_screen == source)
                    m_screen = null;
            };
            m_screen = screen;
            MyGuiSandbox.AddScreen(screen);
        }

        // ------------------------------------------------------------------
        // Sir Missions, and the mission server.

        private void OnServerMessage(ushort channel, byte[] data, ulong sender, bool fromServer)
        {
            try
            {
                // Only the server speaks on this channel.
                if (!fromServer || data == null)
                    return;
                var message = WireMessage.Read(Encoding.UTF8.GetString(data));
                if (message == null)
                    return;

                var now = DateTime.UtcNow;
                if (message.Verb == Protocol.Return)
                {
                    GoBack(now);
                    return;
                }

                var action = TripAction.None;
                if (message.Verb == Protocol.Departure)
                    action = m_trip.Hear(DepartureNews.Read(message), now);

                string outgoing;
                m_model.Receive(message, now, out outgoing);
                Send(outgoing);
                Act(action, now);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void Act(TripAction action, DateTime now)
        {
            switch (action)
            {
                case TripAction.Leave:
                    Leave(now);
                    break;
                case TripAction.AskToLeave:
                    Notify(Texts.NotifyReady, 5000, "Green");
                    if (m_screen == null)
                        OpenWindow();
                    break;
            }
        }

        // The mission server is ready: the game joins it by its Steam
        // identity, remembering the universe it leaves.
        public void Leave(DateTime now)
        {
            if (!m_trip.CanLeave)
                return;
            var news = m_trip.Current;
            if (!m_trip.Leave(GameJoin.Current(), now))
                return;
            m_model.Changed();
            Notify(Texts.NotifyLeaving, 3000, "White");
            m_wayBack.Arm(now);
            if (!GameJoin.JoinBySteamId(m_trip.MissionServer, news.Name, OnJoinFailed))
                LeaveFailed(Texts.NotifyNoJoin);
        }

        // The game's join failed. Still on the universe: the player may leave
        // again, or give up. Already at the main menu: the way back offers the
        // universe he came from.
        private void OnJoinFailed()
        {
            try
            {
                if (InWorld())
                    LeaveFailed(Texts.NotifyJoinFailed);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void LeaveFailed(string notification)
        {
            Log("the game did not leave for the mission server");
            m_trip.LeaveFailed();
            m_wayBack.Disarm();
            m_model.Changed();
            Notify(notification, 10000, "Red");
        }

        // The mission is over: back to the universe, where the character
        // waits where it was left.
        private void GoBack(DateTime now)
        {
            var action = m_trip.ReturnOrder();
            if (action == TripAction.GoBack)
            {
                Notify(string.Format(Texts.NotifyGoingBack, m_trip.Back.DisplayName), 5000, "White");
                m_wayBack.Arm(now);
                if (!GameJoin.ReturnTo(m_trip.Back, OnReturnFailed))
                {
                    m_wayBack.Disarm();
                    Notify(Texts.NotifyNoJoin, 10000, "Red");
                }
            }
            else if (action == TripAction.GoBackUnknown)
            {
                Notify(Texts.NotifyGoBackUnknown, 15000, "White");
            }
        }

        // The way back failed. Still on the mission server: its next order to
        // go back tries again. At the main menu: the way back is offered there.
        private void OnReturnFailed()
        {
            try
            {
                if (InWorld())
                {
                    m_trip.ReturnFailed();
                    m_wayBack.Disarm();
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void OfferWayBack()
        {
            var back = m_trip.Back;
            if (back == null || !back.IsKnown)
                return;
            Log("no longer on the mission server: offering the way back to " + back.DisplayName);
            var box = MyGuiSandbox.CreateMessageBox(
                styleEnum: MyMessageBoxStyleEnum.Info,
                buttonType: MyMessageBoxButtonsType.YES_NO,
                messageText: new StringBuilder(string.Format(Texts.WayBackQuestion, back.DisplayName)),
                messageCaption: new StringBuilder(Texts.WayBackCaption),
                callback: result =>
                {
                    if (result != MyGuiScreenMessageBox.ResultEnum.YES)
                        return;
                    m_wayBack.Arm(DateTime.UtcNow);
                    if (!GameJoin.ReturnTo(back, null))
                        m_wayBack.Disarm();
                },
                focusedResult: MyGuiScreenMessageBox.ResultEnum.YES,
                canHideOthers: false);
            MyGuiSandbox.AddScreen(box);
        }

        // ------------------------------------------------------------------
        // Asked by the window.

        public void Send(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var multiplayer = MyAPIGateway.Multiplayer;
            if (multiplayer == null)
                return;
            multiplayer.SendMessageToServer(Protocol.RequestChannel, Encoding.UTF8.GetBytes(text), true);
        }

        public void SetAutoLeave(bool autoLeave)
        {
            if (m_settings.AutoLeave == autoLeave)
                return;
            m_settings.AutoLeave = autoLeave;
            m_trip.AutoLeave = autoLeave;
            try
            {
                SettingsFile.Save(m_settingsDirectory, m_settings);
            }
            catch (Exception e)
            {
                Log("settings not saved: " + e.Message);
            }
            // Checked while the server is ready: the game leaves at once.
            if (autoLeave && m_trip.CanLeave)
                Leave(DateTime.UtcNow);
            m_model.Changed();
        }

        private static void Notify(string text, int milliseconds, string font)
        {
            if (MyAPIGateway.Utilities != null && MyAPIGateway.Session != null)
                MyAPIGateway.Utilities.ShowNotification(text, milliseconds, font);
        }

        public void Dispose()
        {
            try
            {
                if (m_hooked)
                    Unhook(DateTime.UtcNow);
            }
            catch (Exception e)
            {
                Log("incomplete shutdown: " + e.Message);
            }
            Instance = null;
        }
    }
}
