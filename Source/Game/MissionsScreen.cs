using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Graphics.GUI;
using VRage.Utils;
using VRageMath;

namespace SirMissionsClient
{
    // The window of the missions, opened from the "Missions SIRIUS" block.
    // It draws what the model says, with the game's own controls: the
    // missions offered and those in progress, the choices of the selected
    // mission, then the wait for the mission server once a mission is
    // launched or joined. The player's clicks go back to the model, and what
    // it asks goes to Sir Missions through the plugin.
    internal sealed class MissionsScreen : MyGuiScreenBase
    {
        private const float Width = 0.94f;
        private const float Height = 0.86f;
        private const float Left = -0.45f;
        private const float Right = 0.01f;
        private const float ColumnWidth = 0.44f;
        private const float TextScale = 0.75f;
        private const int VisibleChoices = 4;

        private static readonly Vector4 ErrorColor = new Vector4(1f, 0.4f, 0.4f, 1f);
        private static readonly Vector4 InfoColor = new Vector4(0.85f, 0.9f, 1f, 1f);

        private readonly SirMissionsClientPlugin m_plugin;
        private readonly MissionsWindowModel m_model;

        private int m_drawnVersion = -1;
        private WindowView m_drawnView;
        private DateTime m_nextRefresh;
        private bool m_building;
        private bool m_rebuild;

        private MyGuiControlMultilineText m_waitText;
        private MyGuiControlButton m_launch;
        private MyGuiControlButton m_join;
        private MyGuiControlButton m_leave;

        public MissionsScreen(SirMissionsClientPlugin plugin, MissionsWindowModel model)
            : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(Width, Height), false, null, 0f, 0f)
        {
            m_plugin = plugin;
            m_model = model;
            CanHideOthers = false;
            EnabledBackgroundFade = true;
            CloseButtonEnabled = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName()
        {
            return "SirMissionsClientScreen";
        }

        public override bool Update(bool hasFocus)
        {
            try
            {
                var now = DateTime.UtcNow;
                m_model.Tick(now, m_drawnView);
                var view = m_model.View(now);
                if (m_rebuild || view != m_drawnView || m_model.Version != m_drawnVersion)
                {
                    m_rebuild = false;
                    RecreateControls(false);
                }
                else if (now >= m_nextRefresh)
                {
                    m_nextRefresh = now.AddSeconds(0.5);
                    // The time waited goes on.
                    if (m_waitText != null && view == WindowView.Waiting)
                        SetText(m_waitText, WaitText(now));
                }
            }
            catch (Exception e)
            {
                SirMissionsClientPlugin.Log("window update failed: " + e);
            }
            return base.Update(hasFocus);
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            m_building = true;
            try
            {
                var now = DateTime.UtcNow;
                m_waitText = null;
                m_launch = null;
                m_join = null;
                m_leave = null;
                m_drawnVersion = m_model.Version;
                m_drawnView = m_model.View(now);
                AddCaption(Texts.Caption, null, null, 0.8f);

                switch (m_drawnView)
                {
                    case WindowView.Loading:
                        Message(Texts.Loading);
                        AddBottomButtons(false);
                        break;
                    case WindowView.NoAnswer:
                        Message(Texts.NoAnswer);
                        AddBottomButtons(true);
                        break;
                    case WindowView.List:
                        BuildList();
                        break;
                    case WindowView.Waiting:
                        BuildWaiting(now);
                        break;
                }
            }
            finally
            {
                m_building = false;
            }
        }

        // ------------------------------------------------------------------
        // The missions offered and those in progress.

        private void BuildList()
        {
            var catalog = m_model.Catalog;

            AddLabel(new Vector2(Left, -0.355f), Texts.Offered, 0.8f, null);
            AddLabel(new Vector2(Right, -0.355f), Texts.InProgress, 0.8f, null);

            // Missions offered: name, players at most, duration, reward.
            var offered = NewTable(Left, new[] { 0.40f, 0.16f, 0.22f, 0.22f },
                new[] { Texts.ColumnName, Texts.ColumnMaxPlayers, Texts.ColumnDuration, Texts.ColumnReward });
            var selectedOffered = -1;
            for (var i = 0; i < catalog.Missions.Count; i++)
            {
                var mission = catalog.Missions[i];
                var row = new MyGuiControlTable.Row(mission.Id, mission.Description);
                row.AddCell(new MyGuiControlTable.Cell(mission.Name, mission.Id, mission.Description));
                row.AddCell(new MyGuiControlTable.Cell(Texts.MaxPlayers(mission.MaxPlayers)));
                row.AddCell(new MyGuiControlTable.Cell(Texts.Duration(mission.DurationSeconds)));
                row.AddCell(new MyGuiControlTable.Cell(Texts.Reward(mission.Reward)));
                offered.Add(row);
                if (mission.Id == m_model.SelectedMission)
                    selectedOffered = i;
            }
            if (selectedOffered >= 0)
                offered.SetSelectedRow(selectedOffered);
            offered.ItemSelected += (table, args) =>
            {
                if (m_building || table.SelectedRow == null)
                    return;
                m_model.SelectMission(table.SelectedRow.UserData as string);
                // Drawn again at the next update, not within the table's own click.
                m_rebuild = true;
            };
            Controls.Add(offered);

            // Missions in progress: name, launcher, players, places left, state.
            var running = NewTable(Right, new[] { 0.30f, 0.22f, 0.14f, 0.14f, 0.20f },
                new[] { Texts.ColumnName, Texts.ColumnLauncher, Texts.ColumnMaxPlayers, Texts.ColumnPlaces, Texts.ColumnState });
            var selectedRunning = -1;
            for (var i = 0; i < catalog.Running.Count; i++)
            {
                var mission = catalog.Running[i];
                var full = !mission.CanJoin;
                var color = full ? (Color?)new Color(ErrorColor) : null;
                var row = new MyGuiControlTable.Row(mission.Id, full ? Texts.FullHelp : null);
                row.AddCell(new MyGuiControlTable.Cell(mission.Name, mission.Id, null, color));
                row.AddCell(new MyGuiControlTable.Cell(mission.Launcher, null, null, color));
                row.AddCell(new MyGuiControlTable.Cell(Texts.Players(mission.Players, mission.MaxPlayers), null, null, color));
                row.AddCell(new MyGuiControlTable.Cell(Texts.Places(mission.Places), null, null, color));
                row.AddCell(new MyGuiControlTable.Cell(Texts.State(mission.State), null, null, color));
                running.Add(row);
                if (mission.Id == m_model.SelectedRunning)
                    selectedRunning = i;
            }
            if (selectedRunning >= 0)
                running.SetSelectedRow(selectedRunning);
            running.ItemSelected += (table, args) =>
            {
                if (m_building || table.SelectedRow == null)
                    return;
                m_model.SelectRunning(table.SelectedRow.UserData as string);
                m_rebuild = true;
            };
            Controls.Add(running);

            // What the selected missions are.
            var mission0 = m_model.Mission;
            AddText(new Vector2(Left, -0.075f), new Vector2(ColumnWidth, 0.15f),
                catalog.Missions.Count == 0 ? Texts.NoMission : Texts.Describe(mission0));
            var running0 = m_model.Running;
            AddText(new Vector2(Right, -0.075f), new Vector2(ColumnWidth, 0.32f),
                catalog.Running.Count == 0 ? Texts.NoRunning : Texts.Describe(running0, running0 == null ? null : catalog.Mission(running0.MissionId)));

            // The choices of the mission offered, made before launching it.
            BuildChoices();

            Status(new Vector2(0f, 0.272f));

            m_launch = AddButton(new Vector2(Left + ColumnWidth / 2, 0.335f), Texts.LaunchButton, b => m_plugin.Send(m_model.Launch(DateTime.UtcNow)));
            m_launch.Enabled = m_model.CanLaunch;
            if (mission0 != null)
                m_launch.SetToolTip(mission0.HasChoices ? Texts.ChoicesToMake : Texts.NoChoiceToMake);
            m_join = AddButton(new Vector2(Right + ColumnWidth / 2, 0.335f), Texts.JoinButton, b => m_plugin.Send(m_model.Join(DateTime.UtcNow)));
            m_join.Enabled = m_model.CanJoin;
            m_join.SetToolTip(running0 != null && !running0.CanJoin ? Texts.FullHelp : Texts.JoinNoChoice);

            AddBottomButtons(true);
        }

        private void BuildChoices()
        {
            var form = m_model.Form;
            if (form == null)
                return;
            var choices = form.Choices.Take(VisibleChoices).ToList();
            var y = 0.095f;
            foreach (var choice in choices)
            {
                AddLabel(new Vector2(Left, y), choice.Name, TextScale, null);
                var box = new MyGuiControlCombobox(
                    position: new Vector2(Left + 0.17f, y),
                    size: new Vector2(ColumnWidth - 0.17f, 0.04f),
                    originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                    toolTip: Texts.PickValue);
                for (var i = 0; i < choice.Values.Count; i++)
                    box.AddItem(i, choice.Values[i], i, null, false);
                var picked = form.Picked(choice.Name);
                var index = picked == null ? -1 : choice.Values.IndexOf(picked);
                if (index >= 0)
                    box.SelectItemByKey(index, false);
                var name = choice.Name;
                var values = choice.Values;
                box.ItemSelected += () =>
                {
                    var key = (int)box.GetSelectedKey();
                    if (key >= 0 && key < values.Count)
                        form.Pick(name, values[key]);
                    if (m_launch != null)
                        m_launch.Enabled = m_model.CanLaunch;
                };
                Controls.Add(box);
                y += 0.043f;
            }
        }

        // ------------------------------------------------------------------
        // The wait for the mission server.

        private void BuildWaiting(DateTime now)
        {
            m_waitText = AddText(new Vector2(-0.42f, -0.34f), new Vector2(0.84f, 0.42f), WaitText(now));

            var box = new MyGuiControlCheckbox(
                position: new Vector2(-0.42f, 0.13f),
                toolTip: Texts.AutoLeaveHelp,
                isChecked: m_model.Trip.AutoLeave,
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
            box.IsCheckedChanged = checkbox =>
            {
                if (!m_building)
                    m_plugin.SetAutoLeave(checkbox.IsChecked);
            };
            Controls.Add(box);
            AddLabel(new Vector2(-0.38f, 0.13f), Texts.AutoLeave, 0.8f, null);

            Status(new Vector2(0f, 0.21f));

            m_leave = AddButton(new Vector2(-0.22f, 0.29f), Texts.LeaveButton, b => m_plugin.Leave(DateTime.UtcNow));
            m_leave.Enabled = m_model.Trip.CanLeave;
            if (m_model.CanGoBackToList)
                AddButton(new Vector2(0f, 0.29f), Texts.BackButton, b => m_plugin.Send(m_model.BackToList(DateTime.UtcNow)));
            else
            {
                var giveUp = AddButton(new Vector2(0f, 0.29f), Texts.GiveUpButton, b => m_plugin.Send(m_model.GiveUp(DateTime.UtcNow)));
                giveUp.Enabled = m_model.CanGiveUp && !m_model.Pending;
            }
            AddButton(new Vector2(0.22f, 0.29f), Texts.CloseButton, b => CloseScreen());
        }

        private string WaitText(DateTime now)
        {
            var trip = m_model.Trip;
            var left = trip.Current != null && string.Equals(trip.LeftFor, trip.Current.Id, StringComparison.Ordinal);
            return Texts.Describe(trip.Current, trip.Since, now, trip.AutoLeave, left);
        }

        // ------------------------------------------------------------------
        // Controls.

        private MyGuiControlTable NewTable(float left, float[] widths, string[] names)
        {
            var table = new MyGuiControlTable();
            table.Position = new Vector2(left, -0.33f);
            table.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            table.Size = new Vector2(ColumnWidth, table.Size.Y);
            table.VisibleRowsCount = 6;
            table.ColumnsCount = widths.Length;
            table.SetCustomColumnWidths(widths);
            for (var i = 0; i < names.Length; i++)
                table.SetColumnName(i, new StringBuilder(names[i]));
            return table;
        }

        private void Message(string text)
        {
            AddText(new Vector2(-0.42f, -0.3f), new Vector2(0.84f, 0.3f), text);
        }

        private void Status(Vector2 position)
        {
            if (string.IsNullOrEmpty(m_model.Status))
                return;
            var label = new MyGuiControlLabel(
                position: position,
                text: m_model.Status,
                colorMask: m_model.StatusIsError ? ErrorColor : InfoColor,
                textScale: 0.8f,
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
            Controls.Add(label);
        }

        private void AddBottomButtons(bool refresh)
        {
            if (refresh)
                AddButton(new Vector2(-0.11f, 0.395f), Texts.RefreshButton, b => m_plugin.Send(m_model.Refresh(DateTime.UtcNow)));
            AddButton(new Vector2(refresh ? 0.11f : 0f, 0.395f), Texts.CloseButton, b => CloseScreen());
        }

        private MyGuiControlLabel AddLabel(Vector2 position, string text, float scale, Vector4? color)
        {
            var label = new MyGuiControlLabel(
                position: position,
                text: text,
                colorMask: color,
                textScale: scale,
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
            Controls.Add(label);
            return label;
        }

        private MyGuiControlMultilineText AddText(Vector2 position, Vector2 size, string text)
        {
            var box = new MyGuiControlMultilineText(
                position: position,
                size: size,
                font: "White",
                textScale: TextScale,
                textAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                contents: new StringBuilder(text ?? string.Empty),
                drawScrollbarV: true,
                drawScrollbarH: false,
                textBoxAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            box.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            Controls.Add(box);
            return box;
        }

        private static void SetText(MyGuiControlMultilineText box, string text)
        {
            var current = box.Text == null ? string.Empty : box.Text.ToString();
            if (current != text)
                box.Text = new StringBuilder(text);
        }

        private MyGuiControlButton AddButton(Vector2 position, string text, Action<MyGuiControlButton> click)
        {
            var button = new MyGuiControlButton(
                position: position,
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
                text: new StringBuilder(text),
                onButtonClick: b =>
                {
                    try
                    {
                        click(b);
                    }
                    catch (Exception e)
                    {
                        SirMissionsClientPlugin.Log("click failed: " + e);
                    }
                });
            Controls.Add(button);
            return button;
        }
    }
}
