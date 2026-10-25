# Sir Missions Client

A client-side plugin for Space Engineers, loaded by Pulsar. Part of Sirius Immersion.

It draws the **window of the SIRIUS missions**, opened from the "Missions SIRIUS" block, and makes the player's game leave for a mission server and come back. Three products share the missions:

- **Sir Missions** (the server plugin of the universe) keeps the missions, asks the studio for a mission server for each departure, and pays the rewards;
- **Sir Missions Companion** (a mod) places the "Missions SIRIUS" block;
- **Sir Missions Client** (this plugin) draws the window and makes the player leave and come back.

A mission server has no public port: the player's game joins it by its Steam identity alone.

## What it does

- **The window of the missions.** Using a "Missions SIRIUS" block (use key, F, in front of it) opens the window. It asks Sir Missions for its missions at each opening, and shows:
  - the **missions offered**, each with its name, its maximum of players, its duration and its reward; the selected one is described below the list (its description too);
  - the **missions in progress**, launched by other players, with who launched them, their players, their **places left** and their state. A full mission shows "Full" and cannot be joined.
  - **Refresh** asks again; a server that does not answer (no Sir Missions, or switched off, or a mission server) shows "No missions are offered on this server."
- **Launching a mission.** The player picks a value for each choice the mission declares (a list per choice, under its description); **Launch** stays greyed until each one has a value. A mission without choices launches with a click. The values go to Sir Missions with the launch, which hands them to the mission server.
- **Joining a mission in progress.** **Join** joins the selected mission without choosing anything: the choices stay those of the launcher.
- **The wait.** Once a mission is launched or joined, the window turns into the wait: the state of the mission server (asked, **waiting for its turn** when the studio's limit of mission servers is reached, starting, ready), the **start time announced** by Sir Missions, the time already waited, the players and the choices. The box **"Leave automatically when it is ready"** is checked at installation and the player's choice is kept for the next games. **Give up** leaves the wait before the server is ready: the window goes back to the list, and that mission no longer comes back to the wait unless the player joins it again himself. The window may be closed: the wait goes on, and using the block again shows it.
- **Leaving.** As soon as the mission server is ready, the game joins it by its Steam identity, without any address: by itself when the box is checked (the players who joined during the start leave with the launcher), otherwise with **Leave now**, the window opening by itself with a notification. Checking the box while the server is ready leaves at once. Like the game's own server browser, the game first leaves the universe for the main menu, then joins. If the game cannot start the join, the player is told and may leave again or give up; if the join fails once at the main menu (the mission server cannot be reached), the menu says so and offers to go back to the universe. Back there, the window of the missions opens by itself, says that the game could not join the mission server, and shows the list: the player may join the same mission again, or launch or join another one.
- **Coming back.** At the end of the mission, the mission server tells each player to go back: the game goes back to the universe it left (through the main menu, by its address, or by its Steam identity when the game's join by address is not found), where the character waits where the player left it. If the mission session ends without that order (a lost connection), or if a join fails, the main menu offers to go back to the universe.
- **Without this plugin** a player plays as usual: nothing goes through the server for him, and the block tells him he needs the plugin (that message is the mod's).

The plugin patches nothing in the game. A fault is written to the game log (lines starting with `[sir-missions-client]`) and never stops the game; after twenty faults in a row the plugin stops for the game and says so.

## How to use it

1. In Pulsar, add **Sir Missions Client** to your plugins and restart the game.
2. On a Sirius Immersion universe, stand in front of a "Missions SIRIUS" block and press **F**.
3. Select a mission, pick its values, **Launch** it, or select a mission in progress and **Join** it.
4. Wait: your game leaves by itself when the mission server is ready (or click **Leave now** if you unchecked the box), and comes back at the end.

The player's choice of the box is kept in `%AppData%\SpaceEngineers\Storage\sir-missions-client\settings.txt`.

## The agreements

- **With Sir Missions Companion**, in the player's game, through the game's mod messages: the mod asks on channel **5465480001** (`ouvrir`, then `bloc=<block>`), and the plugin answers `ouverte` on channel **5465480002** within the same call, then opens the window.
- **With Sir Missions**, through the game's network messages: the plugin asks on channel **47241** (`missions`, `lancer` with `mission` and `choix.<name>=<value>`, `rejoindre` with `depart`, `quitter` with `depart`), and reads on channel **47242** the answers `missions`, `depart`, `refus`, `quitte`, and the order `retour` of a mission server. The whole format is described in the README of Sir Missions; the words of the agreement are kept as Sir Missions writes them. These two channels are the ones Sir Missions uses since it left 47231 and 47232 (47231 is also used by Sir Joueurs): the plugin needs a Sir Missions that speaks on them.

## Limits

- At most four choices of a mission are shown in the window.
- Sir Missions sends only the missions in progress that can still be joined: a full mission disappears from the list rather than showing "Full".
- Joining without an address leaves a placeholder entry (`0.0.0.0`) in the game's history of servers.

## Development

- `Source/Logic`: the plugin's logic, without the game: the agreement with Sir Missions and with the mod, the missions, the choices, the wait, the departure and the return, the texts. Tested in `tests/` (xUnit): `dotnet test tests/tests.csproj`.
- `Source/Game`: what needs the game: the plugin (`SirMissionsClientPlugin`), the window (`MissionsScreen`, the game's own controls) and the joins (`GameJoin`, reached by name at run time, so that a change of the game switches off the join only).
- `sir-missions-client.csproj` builds the plugin against the game (`dotnet build -p:Bin64=...`, or `Directory.Build.props`, see `Directory.Build.props.example`); Pulsar compiles the `Source` folder itself. `PluginHub/sir-missions-client.xml` is its Pulsar sheet.

It evolves through tickets in the sir-studio3 studio: `main` carries the work accepted in qualification, `qualification` the work still waiting for it. Every ticket that changes what the plugin does keeps this README up to date.
