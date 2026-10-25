using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // A read-only projection of existing battle accounts. A replica displays
    // host rows and never invents local scores from cosmetic hit/death events.
    public sealed class PrototypeScoreboard
    {
        public const int SnapshotVersion = 1;
        public const int MaximumRows = 64;
        private readonly PrototypeRuntime game;
        private PrototypeScoreboardSnapshot networkSnapshot;
        private Vector2 blueScroll;
        private Vector2 redScroll;
        private GUIStyle title;
        private GUIStyle nameStyle;
        private GUIStyle number;
        private GUIStyle caption;
        private GUIStyle status;
        private static readonly IReadOnlyList<PrototypeScoreboardRow> Empty =
            Array.AsReadOnly(Array.Empty<PrototypeScoreboardRow>());

        public bool HasNetworkSnapshot => networkSnapshot != null;
        // Simulation authority can pause during a host's connection/room
        // transition. Their existing local history still belongs to that host.
        private bool ReadOnlyReplica => game != null && game.IsNetworkSession && !game.IsNetworkHost;

        public PrototypeScoreboard(PrototypeRuntime runtime) => game = runtime;

        public void Reset()
        {
            networkSnapshot = null;
            blueScroll = redScroll = Vector2.zero;
        }

        public PrototypeScoreboardSnapshot BuildSnapshot(
            IReadOnlyList<PrototypeLanRosterMember> roster = null, int localId = -1)
        {
            if (game == null || ReadOnlyReplica) return null;
            var rows = new List<PrototypeScoreboardRow>();
            if (game.Player != null)
            {
                string localName = game.Frontend?.PlayerName ?? "Player";
                if (roster != null && localId >= 0)
                    foreach (PrototypeLanRosterMember member in roster)
                        if (member != null && member.id == localId) { localName = member.name; break; }
                Add(rows, game.Player, PlayerActorId(localId), localName,
                    game.Player.Team, game.Player.Alive, true, true, Mathf.Max(0, localId));
            }
            foreach (PrototypeRemotePlayer remote in game.RemotePlayers)
            {
                if (remote == null) continue;
                string name = "玩家 #" + remote.Id;
                if (roster != null)
                    foreach (PrototypeLanRosterMember member in roster)
                        if (member != null && member.id == remote.Id)
                        { name = member.name; break; }
                Add(rows, remote, PlayerActorId(remote.Id), name, remote.Team,
                    remote.Alive, true, remote.IsConnected, Mathf.Max(0, remote.Id));
            }
            for (int i = 0; i < game.Bots.Count; i++)
            {
                PrototypeBot bot = game.Bots[i];
                if (bot == null) continue;
                Add(rows, bot, "bot:" + i, bot.DisplayName, bot.Team,
                    bot.Alive, false, true, 1000 + i);
            }
            // Autonomous vehicle fire already belongs to the vehicle account.
            // Preserve that real history even after a player boards its seats;
            // never transfer the points to an unrelated infantry bot or pilot.
            for (int i = 0; i < game.Vehicles.Count; i++)
            {
                IPrototypeVehicle vehicle = game.Vehicles[i];
                if (vehicle == null || game.Scores == null ||
                    !game.Scores.TryGet(vehicle, out PrototypeScoreSystem.Entry entry) ||
                    entry.TotalEarned <= 0) continue;
                Add(rows, vehicle, "vehicle:" + i, "[AI载具] " + vehicle.VehicleName,
                    vehicle.Team, vehicle.Alive, false, true, 2000 + i);
            }
            rows.Sort(CompareRows);
            return new PrototypeScoreboardSnapshot { version = SnapshotVersion, rows = rows.ToArray() };
        }

        private void Add(List<PrototypeScoreboardRow> rows, object actor, string id, string displayName,
            PrototypeTeam team, bool alive, bool human, bool connected, int order)
        {
            PrototypeScoreSystem.Entry entry = null;
            game.Scores?.TryGet(actor, out entry);
            rows.Add(new PrototypeScoreboardRow { actorId = id, displayName = CleanName(displayName, id),
                team = (int)team, stableOrder = order, kills = entry?.Kills ?? 0,
                assists = entry?.Assists ?? 0, vehicleAssists = entry?.VehicleAssists ?? 0,
                vehiclesDestroyed = entry?.VehiclesDestroyed ?? 0, totalEarned = entry?.TotalEarned ?? 0,
                alive = alive, isHuman = human, connected = connected });
        }

        public bool ApplyReadOnlySnapshot(PrototypeScoreboardSnapshot snapshot)
        {
            if (game == null || !ReadOnlyReplica) return false;
            // Missing data from an older host is unavailable, not a fake zero.
            if (snapshot == null) { networkSnapshot = null; return false; }
            if (snapshot.version != SnapshotVersion || snapshot.rows == null ||
                snapshot.rows.Length > MaximumRows) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var rows = new PrototypeScoreboardRow[snapshot.rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                PrototypeScoreboardRow row = snapshot.rows[i];
                if (row == null || !ValidActorId(row.actorId) || !ids.Add(row.actorId) ||
                    row.team < 0 || row.team > 1 || row.stableOrder < 0 || row.stableOrder > 1000000 ||
                    row.kills < 0 || row.assists < 0 || row.vehicleAssists < 0 ||
                    row.vehiclesDestroyed < 0 || row.totalEarned < 0 ||
                    row.displayName == null || row.displayName.Length > 64) return false;
                rows[i] = Copy(row);
            }
            Array.Sort(rows, CompareRows);
            networkSnapshot = new PrototypeScoreboardSnapshot { version = SnapshotVersion, rows = rows };
            return true;
        }

        public IReadOnlyList<PrototypeScoreboardRow> GetRows(PrototypeTeam team,
            IReadOnlyList<PrototypeLanRosterMember> roster = null, int localId = -1)
        {
            PrototypeScoreboardSnapshot snapshot = ReadOnlyReplica ?
                networkSnapshot : BuildSnapshot(roster, localId);
            if (snapshot?.rows == null) return Empty;
            var rows = new List<PrototypeScoreboardRow>();
            foreach (PrototypeScoreboardRow row in snapshot.rows)
                if (row.team == (int)team) rows.Add(Copy(row));
            return rows.AsReadOnly();
        }

        public void Draw(float width, float height,
            IReadOnlyList<PrototypeLanRosterMember> roster = null, int localId = -1)
        {
            if (game?.Match == null) return;
            EnsureStyles();
            Rect panel = new Rect(24f, 52f, Mathf.Max(560f, width - 48f), Mathf.Max(330f, height - 90f));
            Fill(new Rect(0f, 0f, width, height), new Color(.005f, .009f, .014f, .64f));
            Fill(panel, new Color(.016f, .029f, .040f, .97f));
            GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 30f),
                "战局积分  /  SCOREBOARD", title);
            GUI.Label(new Rect(panel.x + 20f, panel.yMax - 30f, panel.width - 40f, 22f),
                "Y 关闭  ·  战局继续  ·  助攻 = 人员 + 载具  ·  总分不随支援花费减少", caption);
            float half = (panel.width - 52f) * .5f;
            Rect blue = new Rect(panel.x + 18f, panel.y + 52f, half, panel.height - 94f);
            Rect red = new Rect(blue.xMax + 16f, blue.y, half, blue.height);
            DrawTeam(blue, PrototypeTeam.Blue, game.Match.BlueTickets,
                GetRows(PrototypeTeam.Blue, roster, localId), PlayerActorId(localId), ref blueScroll);
            DrawTeam(red, PrototypeTeam.Red, game.Match.RedTickets,
                GetRows(PrototypeTeam.Red, roster, localId), PlayerActorId(localId), ref redScroll);
            if (ReadOnlyReplica && networkSnapshot == null)
                GUI.Label(new Rect(panel.x + 20f, panel.y + 113f, panel.width - 40f, 40f),
                    "主机积分待同步 · 当前房主尚未提供积分面板数据", status);
        }

        private void DrawTeam(Rect area, PrototypeTeam team, float tickets,
            IReadOnlyList<PrototypeScoreboardRow> rows, string localActorId, ref Vector2 scroll)
        {
            Color accent = team == PrototypeTeam.Blue ? new Color(.32f, .78f, 1f) : new Color(1f, .40f, .35f);
            Fill(new Rect(area.x, area.y, area.width, 35f), new Color(accent.r * .17f, accent.g * .17f, accent.b * .17f, 1f));
            Color previous = GUI.color; GUI.color = accent;
            GUI.Label(new Rect(area.x + 9f, area.y + 5f, area.width - 18f, 24f),
                (team == PrototypeTeam.Blue ? "蓝方" : "红方") + "  ·  剩余兵力 " +
                Mathf.CeilToInt(Mathf.Max(0f, tickets)), title);
            GUI.color = previous;
            float lineY = area.y + 39f;
            GUI.Label(new Rect(area.x + 8f, lineY, area.width * .40f, 21f), "玩家 / AI", caption);
            Header(area, .43f, lineY, "击杀", "本局已结算的人员击杀");
            Header(area, .57f, lineY, "助攻", "人员助攻与载具助攻的总和");
            Header(area, .71f, lineY, "毁车", "本局已结算的载具摧毁");
            Header(area, .85f, lineY, "总分", "本局累计赚取；呼叫支援仅扣可用积分，退款不增加总分");
            Rect view = new Rect(area.x, area.y + 63f, area.width, Mathf.Max(40f, area.height - 63f));
            float rowWidth = area.width - 18f;
            scroll = GUI.BeginScrollView(view, scroll,
                new Rect(0f, 0f, rowWidth, Mathf.Max(view.height, rows.Count * 25f)), false, false);
            for (int i = 0; i < rows.Count; i++)
            {
                PrototypeScoreboardRow row = rows[i];
                Rect line = new Rect(0f, i * 25f, rowWidth, 24f);
                bool local = row.isHuman && row.actorId == localActorId;
                Fill(line, local ? new Color(.25f, .30f, .15f, .75f) :
                    i % 2 == 0 ? new Color(.06f, .085f, .105f, .75f) : new Color(.035f, .050f, .065f, .75f));
                string suffix = !row.connected ? " · 离线" : !row.alive ? " · 倒地" : "";
                GUI.Label(new Rect(7f, line.y + 3f, rowWidth * .42f - 8f, 19f),
                    new GUIContent((local ? "▶ " : row.isHuman || row.actorId.StartsWith("vehicle:", StringComparison.Ordinal) ? "" : "AI · ") + row.displayName,
                        row.displayName + suffix), nameStyle);
                DrawNumber(rowWidth, .43f, line.y, row.kills.ToString());
                DrawNumber(rowWidth, .57f, line.y, ((long)row.assists + row.vehicleAssists).ToString());
                DrawNumber(rowWidth, .71f, line.y, row.vehiclesDestroyed.ToString());
                DrawNumber(rowWidth, .85f, line.y, row.totalEarned.ToString());
            }
            GUI.EndScrollView();
        }

        private void Header(Rect area, float column, float y, string text, string tooltip)
        {
            float rowWidth = area.width - 18f;
            GUI.Label(new Rect(area.x + rowWidth * column, y, rowWidth * .14f, 21f),
                new GUIContent(text, tooltip), number);
        }
        private void DrawNumber(float width, float column, float y, string value) =>
            GUI.Label(new Rect(width * column, y + 3f, width * .14f, 19f), new GUIContent(value, value), number);

        private void EnsureStyles()
        {
            if (title != null) return;
            title = Style(16, FontStyle.Bold, TextAnchor.MiddleLeft);
            nameStyle = Style(12, FontStyle.Normal, TextAnchor.MiddleLeft);
            number = Style(12, FontStyle.Normal, TextAnchor.MiddleRight);
            caption = Style(11, FontStyle.Normal, TextAnchor.MiddleLeft);
            status = Style(14, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        private static GUIStyle Style(int size, FontStyle weight, TextAnchor align)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight,
                alignment = align, richText = false, clipping = TextClipping.Clip };
            style.normal.textColor = new Color(.89f, .92f, .94f);
            return style;
        }

        private static void Fill(Rect rectangle, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rectangle, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static string PlayerActorId(int id) => id < 0 ? "player:local" : "player:" + id;

        private static bool ValidActorId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 48) return false;
            string[] parts = id.Split(':');
            return parts.Length == 2 &&
                ((parts[0] == "player" && parts[1] == "local") ||
                 ((parts[0] == "player" || parts[0] == "bot" || parts[0] == "vehicle") &&
                  int.TryParse(parts[1], out int index) && index >= 0));
        }

        private static string CleanName(string value, string fallback)
        {
            var name = new StringBuilder();
            foreach (char character in value ?? "")
                if (!char.IsControl(character) && name.Length < 40) name.Append(character);
            string cleaned = name.ToString().Trim();
            return string.IsNullOrEmpty(cleaned) ? fallback : cleaned;
        }

        private static PrototypeScoreboardRow Copy(PrototypeScoreboardRow row) =>
            new PrototypeScoreboardRow { actorId = row.actorId, displayName = CleanName(row.displayName, row.actorId),
                team = row.team, stableOrder = row.stableOrder, kills = row.kills, assists = row.assists,
                vehicleAssists = row.vehicleAssists, vehiclesDestroyed = row.vehiclesDestroyed,
                totalEarned = row.totalEarned, alive = row.alive, isHuman = row.isHuman, connected = row.connected };

        private static int CompareRows(PrototypeScoreboardRow a, PrototypeScoreboardRow b)
        {
            int score = b.totalEarned.CompareTo(a.totalEarned);
            if (score != 0) return score;
            int order = a.stableOrder.CompareTo(b.stableOrder);
            return order != 0 ? order : string.CompareOrdinal(a.actorId, b.actorId);
        }
    }
}
