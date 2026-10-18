using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.GameServices;

namespace SirMissionsClient
{
    // Leaving for another server, through the game's own join. The parts of
    // the game it uses change between versions: they are reached by name at
    // run time, and a name that is no longer there only switches off the
    // matching step (with a line in the log), never the plugin.
    internal static class GameJoin
    {
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // A mission server has no public port: the game reaches it by its Steam
        // identity alone, through Steam's own relay. The game's Steam join only
        // checks that the server item carries a Steam address of the right
        // form, never uses it: this one says "no address".
        public const string NoAddress = "steam://0.0.0.0:0";

        // The server the game is on: its address, its name and its Steam
        // identity. Null in a local game.
        public static ServerAddress Current()
        {
            try
            {
                var game = MyMultiplayer.Static;
                if (game == null)
                    return null;
                var here = new ServerAddress { SteamId = game.ServerId };
                var item = ServerItemOf(game);
                if (item != null)
                {
                    here.ConnectionString = item.ConnectionString ?? string.Empty;
                    here.Name = item.Name ?? string.Empty;
                    if (here.SteamId == 0)
                        here.SteamId = item.SteamID;
                }
                if (string.IsNullOrEmpty(here.Name))
                {
                    try
                    {
                        here.Name = game.HostName ?? string.Empty;
                    }
                    catch (Exception)
                    {
                    }
                }
                // A server joined without an address is reached by its identity.
                if (here.ConnectionString == NoAddress)
                    here.ConnectionString = string.Empty;
                return here;
            }
            catch (Exception e)
            {
                SirMissionsClientPlugin.Log("current server not read: " + e.Message);
                return null;
            }
        }

        public static ulong CurrentSteamId()
        {
            var here = Current();
            return here == null ? 0UL : here.SteamId;
        }

        // The client of a dedicated server keeps the server item it joined in
        // an internal class: found by its type, never by a name that could
        // change (as Sir Holomap does).
        private static MyGameServerItem ServerItemOf(object game)
        {
            if (game == null)
                return null;
            foreach (var property in game.GetType().GetProperties(Instance))
            {
                if (property.PropertyType == typeof(MyGameServerItem) && property.GetIndexParameters().Length == 0)
                    return property.GetValue(game, null) as MyGameServerItem;
            }
            foreach (var field in game.GetType().GetFields(Instance))
            {
                if (field.FieldType == typeof(MyGameServerItem))
                    return field.GetValue(game) as MyGameServerItem;
            }
            return null;
        }

        // Leaves the current server for the main menu, then joins the one with
        // this Steam identity, without any address, the way Sir Holomap does.
        // False when the game's join is not found; failed: called when the
        // game's join could not be started.
        public static bool JoinBySteamId(ulong steamId, string name, Action failed)
        {
            if (steamId == 0)
                return false;

            MethodInfo join = null;
            foreach (var method in typeof(MyJoinGameHelper).GetMethods(Static).Where(m => m.Name == "JoinGame"))
            {
                var parameters = method.GetParameters();
                if (parameters.Length >= 2
                    && parameters[0].ParameterType == typeof(MyGameServerItem)
                    && parameters[1].ParameterType == typeof(Dictionary<string, string>))
                {
                    join = method;
                    break;
                }
            }
            if (join == null)
            {
                SirMissionsClientPlugin.Log("the game's join by server item was not found");
                return false;
            }

            var server = new MyGameServerItem
            {
                SteamID = steamId,
                Name = string.IsNullOrEmpty(name) ? steamId.ToString() : name,
                ConnectionString = NoAddress,
                ServerVersion = (int)MyFinalBuildConstants.APP_VERSION,
            };

            // The rules a server gives through its public port: none here. The
            // world, its settings and its mods come with the join itself.
            var arguments = Arguments(join.GetParameters(), server, failed);
            arguments[1] = new Dictionary<string, string>();

            SirMissionsClientPlugin.Log("joining the server " + steamId + " by its Steam identity");
            return LeaveThen(() => join.Invoke(null, arguments), failed);
        }

        // Goes back to the universe: by its address, as the game's own
        // "+connect" does, or by its Steam identity when it has none.
        public static bool ReturnTo(ServerAddress back, Action failed)
        {
            if (back == null || !back.IsKnown)
                return false;
            if (string.IsNullOrEmpty(back.ConnectionString))
                return JoinBySteamId(back.SteamId, back.Name, failed);

            var joinServer = typeof(MyJoinGameHelper).GetMethod("JoinServer", Static, null, new[] { typeof(string) }, null);
            if (joinServer == null)
            {
                SirMissionsClientPlugin.Log("the game's join by address was not found: going back by Steam identity");
                return back.SteamId != 0 && JoinBySteamId(back.SteamId, back.Name, failed);
            }
            SirMissionsClientPlugin.Log("going back to " + back.ConnectionString);
            var address = back.ConnectionString;
            return LeaveThen(() => joinServer.Invoke(null, new object[] { address }), failed);
        }

        // The world being played is first unloaded, back to the main menu, as
        // the game's own server browser does; the join starts from there, at
        // the game's next frame (never inside a network message). Already at
        // the menu, the join starts at once. False when the world cannot be
        // left (the game's unload is not found): nothing is done then.
        private static bool LeaveThen(Action join, Action failed)
        {
            MethodInfo unload = null;
            if (MySession.Static != null)
            {
                unload = typeof(MySessionLoader).GetMethod("UnloadAndExitToMenu", Static, null, Type.EmptyTypes, null);
                if (unload == null)
                {
                    SirMissionsClientPlugin.Log("the game's way to the main menu was not found");
                    return false;
                }
            }

            MySandboxGame.Static.Invoke(() =>
            {
                try
                {
                    if (unload != null && MySession.Static != null)
                        unload.Invoke(null, null);
                    join();
                }
                catch (Exception e)
                {
                    SirMissionsClientPlugin.Log("join failed: " + e);
                    if (failed != null)
                        failed();
                }
            }, "SirMissionsClientJoin");
            return true;
        }

        private static object[] Arguments(ParameterInfo[] parameters, MyGameServerItem server, Action failed)
        {
            var arguments = new object[parameters.Length];
            arguments[0] = server;
            for (var i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(Action))
                    arguments[i] = failed;
                else if (parameters[i].HasDefaultValue)
                    arguments[i] = parameters[i].DefaultValue;
                else if (parameters[i].ParameterType.IsValueType)
                    arguments[i] = Activator.CreateInstance(parameters[i].ParameterType);
            }
            return arguments;
        }

        // Is the game busy joining or loading a world? Read from the screens it
        // shows (its progress and loading screens). Null when the game's
        // screen list cannot be read.
        public static bool? JoinScreensOpen()
        {
            try
            {
                var screens = ScreenList();
                if (screens == null)
                    return null;
                foreach (var screen in screens)
                {
                    if (screen == null)
                        continue;
                    var name = screen.GetType().Name;
                    if (name.IndexOf("Progress", StringComparison.Ordinal) >= 0
                        || name.IndexOf("Loading", StringComparison.Ordinal) >= 0
                        || name.IndexOf("Join", StringComparison.Ordinal) >= 0
                        || name.IndexOf("Download", StringComparison.Ordinal) >= 0
                        || name.IndexOf("MessageBox", StringComparison.Ordinal) >= 0)
                        return true;
                }
                return false;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static FieldInfo s_screens;
        private static bool s_screensLooked;

        private static System.Collections.IEnumerable ScreenList()
        {
            if (!s_screensLooked)
            {
                s_screensLooked = true;
                const string managerName = "Sandbox.Graphics.GUI.MyScreenManager";
                var manager = typeof(MyGuiSandbox).Assembly.GetType(managerName);
                if (manager == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        manager = assembly.GetType(managerName);
                        if (manager != null)
                            break;
                    }
                }
                if (manager != null)
                {
                    foreach (var field in manager.GetFields(Static))
                    {
                        if (field.Name == "m_screens" && typeof(System.Collections.IEnumerable).IsAssignableFrom(field.FieldType))
                        {
                            s_screens = field;
                            break;
                        }
                    }
                }
                if (s_screens == null)
                    SirMissionsClientPlugin.Log("the game's screen list was not found: no way back after a failed join");
            }
            return s_screens != null ? s_screens.GetValue(null) as System.Collections.IEnumerable : null;
        }
    }
}
