using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeScoreboardSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("IRONFRONT/Verify Team Scoreboard", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Team Scoreboard")]
        public static void Verify()
        {
            PrototypeRuntime game = null;
            var fixtures = new List<GameObject>();
            var oldBots = new List<PrototypeBot>();
            var oldVehicles = new List<IPrototypeVehicle>();
            var oldRemote = new List<PrototypeRemotePlayer>();
            var oldFeed = new List<PrototypeBotDownNotice>();
            var previousEffects = new HashSet<PrototypePulseVisual>(
                UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>());
            var previousAudio = new HashSet<AudioSource>(UnityEngine.Object.FindObjectsByType<AudioSource>());
            PrototypeScoreSystem previousScores = null;
            PrototypeGameMode previousMode = PrototypeGameMode.Conquest;
            float previousBlue = 0f, previousRed = 0f;
            bool restore = false;
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && game.MatchStarted &&
                    !game.IsNetworkReplica && !game.Match.Winner.HasValue, "live authoritative match exists");
                previousScores = game.Scores;
                previousMode = game.Match.Mode;
                previousBlue = game.Match.BlueTickets; previousRed = game.Match.RedTickets;
                oldBots.AddRange(game.Bots); oldVehicles.AddRange(game.Vehicles);
                oldRemote.AddRange(game.RemotePlayers); oldFeed.AddRange(game.BotDownFeed.Entries);
                game.Bots.Clear(); game.Vehicles.Clear(); game.RemotePlayers.Clear();
                Set(game.Match, "<Mode>k__BackingField", PrototypeGameMode.Conquest);
                game.Match.SetNetworkTickets(100f, 100f);
                float now = 100f;
                var scores = new PrototypeScoreSystem(game, () => now);
                Set(game, "<Scores>k__BackingField", scores);
                restore = true;
                PrototypeTeam team = game.Player.Team;
                PrototypeTeam enemyTeam = team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                PrototypeBot assistant = Bot(game, team, fixtures);
                PrototypeBot killer = Bot(game, team, fixtures);
                PrototypeBot unscored = Bot(game, team, fixtures);
                PrototypeBot victim = Bot(game, enemyTeam, fixtures);
                var remoteRoot = new GameObject("Scoreboard actual remote player");
                fixtures.Add(remoteRoot); remoteRoot.transform.SetParent(game.transform);
                var remote = remoteRoot.AddComponent<PrototypeRemotePlayer>();
                remote.Initialize(game, 27, team); remote.enabled = false; game.RemotePlayers.Add(remote);
                var roster = new[] {
                    new PrototypeLanRosterMember { id = 7, name = "本机测试玩家", connected = true },
                    new PrototypeLanRosterMember { id = 27, name = "联网队友名字", connected = true }
                };
                var board = new PrototypeScoreboard(game);
                PrototypeScoreboardSnapshot initial = board.BuildSnapshot(roster, 7);
                Check(initial.rows.Length == game.Bots.Count + game.RemotePlayers.Count + 1,
                    "every actual player and bot is listed, including zero-score actors");
                Check(Row(initial, "player:7").displayName == "本机测试玩家" &&
                    Row(initial, "player:27").displayName == "联网队友名字" &&
                    Row(initial, "bot:0").displayName == assistant.DisplayName,
                    "names come from the local roster, remote roster and actual bot roster");
                Check(!scores.TryGet(unscored, out _) && Row(initial, "bot:2").totalEarned == 0,
                    "a read-only zero-score row does not create a phantom score account");

                Hit(scores, victim, assistant, team, 25f);
                Hit(scores, victim, killer, team, 75f);
                Check(!victim.Alive && scores.Get(killer).Kills == 1 && scores.Get(killer).TotalEarned == 100 &&
                    scores.Get(assistant).Assists == 1 && scores.Get(assistant).TotalEarned == 52,
                    "real damage/kill settlement and assistance belong to their actual bot actors");
                PrototypeScoreboardSnapshot personnel = board.BuildSnapshot(roster, 7);
                Check(Row(personnel, "bot:1").kills == 1 && Row(personnel, "bot:1").totalEarned == 100 &&
                    Row(personnel, "bot:0").assists == 1 && Row(personnel, "bot:0").totalEarned == 52,
                    "board rows project the existing settled accounts instead of manufacturing results");
                Check(scores.TrySpend(killer, 60) && scores.Get(killer).Credits == 40 &&
                    Row(board.BuildSnapshot(roster, 7), "bot:1").totalEarned == 100,
                    "support spending reduces available credits but cannot reduce the board's earned score");
                Check(scores.Refund(killer, 60) && !scores.Refund(killer, 60) &&
                    scores.Get(killer).Credits == 100 && scores.Get(killer).TotalEarned == 100,
                    "a refund restores its real debit once and never inflates earned score");
                Respawn(killer);
                Check(Row(board.BuildSnapshot(roster, 7), "bot:1").kills == 1 &&
                    scores.Get(killer).TotalEarned == 100, "a new life retains current-match results");

                var hull = Vehicle(game, enemyTeam, PrototypeVehicleKind.Motorcycle, false, fixtures);
                scores.ApplyDamage(hull, assistant, team, 25f, () => hull.TakeDamage(25f));
                scores.ApplyDamage(hull, killer, team, 60f, () => hull.TakeDamage(60f));
                Check(!hull.Alive && scores.Get(killer).VehiclesDestroyed == 1 &&
                    scores.Get(killer).TotalEarned == 300 && scores.Get(assistant).VehicleAssists == 1,
                    "real hull destruction and vehicle assistance settle independently of infantry statistics");
                PrototypeScoreboardSnapshot vehicleScores = board.BuildSnapshot(roster, 7);
                PrototypeScoreboardRow assistRow = Row(vehicleScores, "bot:0");
                Check(assistRow.assists == 1 && assistRow.vehicleAssists == 1 &&
                    Row(vehicleScores, "bot:1").vehiclesDestroyed == 1,
                    "both assistance categories are available for the displayed combined assist count");

                var aiVehicle = Vehicle(game, team, PrototypeVehicleKind.Ifv, true, fixtures);
                Respawn(victim);
                object capturedSource = PrototypeScoreSystem.ActorForShooter(aiVehicle.transform);
                Check(ReferenceEquals(capturedSource, aiVehicle), "uncrewed AI gun fire captures the actual vehicle account");
                Hit(scores, victim, capturedSource, team, 1000f);
                Respawn(victim);
                Hit(scores, victim, remote, team, 1000f);
                PrototypeScoreboardSnapshot settled = board.BuildSnapshot(roster, 7);
                Check(Row(settled, "vehicle:1").totalEarned == 100 &&
                    Row(settled, "vehicle:1").displayName.StartsWith("[AI载具]", StringComparison.Ordinal) &&
                    Row(settled, "bot:2").totalEarned == 0,
                    "autonomous vehicle points get a transparent vehicle row and are not invented for an infantry bot");
                Check(Row(settled, "player:27").kills == 1 &&
                    Row(settled, "player:27").totalEarned == 100,
                    "the actual remote player's earned statistics are represented");
                IReadOnlyList<PrototypeScoreboardRow> first = board.GetRows(team, roster, 7);
                IReadOnlyList<PrototypeScoreboardRow> second = board.GetRows(team, roster, 7);
                for (int i = 1; i < first.Count; i++)
                    Check(first[i - 1].totalEarned >= first[i].totalEarned,
                        "the team is ordered by cumulative earned points");
                for (int i = 0; i < first.Count; i++)
                    Check(first[i].actorId == second[i].actorId, "ties have repeatable stable actor order");
                Check(first[0].actorId == "bot:1" &&
                    Index(first, "player:27") < Index(first, "vehicle:1"),
                    "known equal-score rows use stable roster order rather than a random sorting tie");

                VerifyNetwork(game, board, settled, team, scores, killer);
                scores.Reset(); board.Reset();
                PrototypeScoreboardSnapshot reset = board.BuildSnapshot(roster, 7);
                foreach (PrototypeScoreboardRow row in reset.rows)
                    Check(row.totalEarned == 0 && row.kills == 0 && row.assists == 0 &&
                        row.vehicleAssists == 0 && row.vehiclesDestroyed == 0,
                        "a new match clears all personal counters");
                Check(reset.rows.Length == game.Bots.Count + game.RemotePlayers.Count + 1 &&
                    !board.HasNetworkSnapshot, "reset removes AI vehicle history and stale replica rows");
                Debug.Log("IRONFRONT team scoreboard PASS: actual player/AI/remote names and rows, damage/kill/assist/" +
                    "vehicle ownership, earned score versus spend/refund, new-life persistence, stable team sorting, " +
                    "read-only host snapshot isolation, legacy/malformed/authority guards and match reset");
            }
            catch (Exception exception) { Debug.LogError("IRONFRONT team scoreboard FAIL: " + exception.GetBaseException().Message); }
            finally
            {
                if (restore && game != null)
                {
                    Set(game, "<Scores>k__BackingField", previousScores);
                    Set(game.Match, "<Mode>k__BackingField", previousMode);
                    game.Match.SetNetworkTickets(previousBlue, previousRed);
                    game.Bots.Clear(); game.Bots.AddRange(oldBots);
                    game.Vehicles.Clear(); game.Vehicles.AddRange(oldVehicles);
                    game.RemotePlayers.Clear(); game.RemotePlayers.AddRange(oldRemote);
                    var entries = (List<PrototypeBotDownNotice>)Field(game.BotDownFeed, "entries");
                    entries.Clear(); entries.AddRange(oldFeed);
                }
                foreach (GameObject fixture in fixtures)
                    if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                foreach (PrototypePulseVisual effect in UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>())
                    if (!previousEffects.Contains(effect)) UnityEngine.Object.DestroyImmediate(effect.gameObject);
                foreach (AudioSource source in UnityEngine.Object.FindObjectsByType<AudioSource>())
                    if (!previousAudio.Contains(source)) UnityEngine.Object.DestroyImmediate(source.gameObject);
            }
        }

        private static void VerifyNetwork(PrototypeRuntime game, PrototypeScoreboard board,
            PrototypeScoreboardSnapshot snapshot, PrototypeTeam team, PrototypeScoreSystem scores, PrototypeBot killer)
        {
            Check(!board.ApplyReadOnlySnapshot(snapshot), "authoritative worlds reject replica-only board data");
            bool previousNetwork = (bool)Field(game, "networkModeActive");
            bool previousHost = (bool)Field(game, "networkHost");
            object previousLan = Field(game, "lan");
            try
            {
                Set(game, "networkModeActive", true); Set(game, "networkHost", false);
                var wire = new PrototypeLanSnapshot { tick = 9, scoreboard = snapshot };
                wire = JsonUtility.FromJson<PrototypeLanSnapshot>(JsonUtility.ToJson(wire));
                int before = scores.Get(killer).TotalEarned;
                Check(board.ApplyReadOnlySnapshot(wire.scoreboard) && board.HasNetworkSnapshot,
                    "the optional board survives actual snapshot JSON serialization");
                Check(board.GetRows(team, null, 27)[0].totalEarned == before &&
                    !scores.TrySpend(killer, 10) && scores.Get(killer).TotalEarned == before,
                    "a replica displays host rows without granting local scoring or support spending");
                wire.scoreboard.rows[0].totalEarned = 999999;
                Check(board.GetRows(team, null, 27)[0].totalEarned == before,
                    "the cached view copies host input instead of retaining a mutable network object");
                var bad = new PrototypeScoreboardSnapshot { rows = new[] {
                    new PrototypeScoreboardRow { actorId = "bot:0", displayName = "bad", team = 0, totalEarned = -1 }
                } };
                Check(!board.ApplyReadOnlySnapshot(bad) &&
                    board.GetRows(team, null, 27)[0].totalEarned == before,
                    "negative or malformed host fields cannot replace a validated board");
                bad = new PrototypeScoreboardSnapshot { rows = new[] { snapshot.rows[0], snapshot.rows[0] } };
                Check(!board.ApplyReadOnlySnapshot(bad), "duplicate actor identities are rejected");
                Check(!board.ApplyReadOnlySnapshot(new PrototypeScoreboardSnapshot {
                    rows = new PrototypeScoreboardRow[PrototypeScoreboard.MaximumRows + 1] }),
                    "oversized board snapshots are rejected");
                Check(!board.ApplyReadOnlySnapshot(null) && !board.HasNetworkSnapshot &&
                    board.GetRows(team).Count == 0 && board.BuildSnapshot() == null,
                    "an older host with no board yields unavailable data rather than fictional local zero scores");
                Set(game, "networkHost", true);
                Set(game, "lan", null);
                Check(game.IsNetworkReplica && game.IsNetworkHost &&
                    board.GetRows(team, null, 7)[0].totalEarned == before &&
                    board.BuildSnapshot(null, 7) != null && !board.ApplyReadOnlySnapshot(snapshot) &&
                    !scores.TrySpend(killer, 10),
                    "a paused or disconnected host can read real history while simulation scoring remains blocked");
            }
            finally
            { Set(game, "lan", previousLan); Set(game, "networkModeActive", previousNetwork); Set(game, "networkHost", previousHost); }
        }

        private static PrototypeBot Bot(PrototypeRuntime game, PrototypeTeam team, List<GameObject> fixtures)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fixtures.Add(root); root.transform.SetParent(game.transform);
            PrototypeBot bot = root.AddComponent<PrototypeBot>();
            bot.Initialize(game, game.Navigation, team, new Vector2(330f + fixtures.Count * 3f, 330f), PrototypeInfantryClass.Assault);
            bot.enabled = false; game.Bots.Add(bot); return bot;
        }

        private static PrototypeCombinedArmsVehicle Vehicle(PrototypeRuntime game, PrototypeTeam team,
            PrototypeVehicleKind kind, bool ai, List<GameObject> fixtures)
        {
            var root = new GameObject("Scoreboard real " + kind); fixtures.Add(root);
            root.transform.SetParent(game.transform);
            var vehicle = root.AddComponent<PrototypeCombinedArmsVehicle>();
            vehicle.Initialize(game, team, kind, new Vector2(325f, 345f), ai);
            vehicle.enabled = false; game.Vehicles.Add(vehicle); return vehicle;
        }

        private static PrototypeScoreboardRow Row(PrototypeScoreboardSnapshot snapshot, string actorId)
        {
            foreach (PrototypeScoreboardRow row in snapshot.rows) if (row.actorId == actorId) return row;
            throw new InvalidOperationException("missing real actor row " + actorId);
        }
        private static int Index(IReadOnlyList<PrototypeScoreboardRow> rows, string id)
        { for (int i = 0; i < rows.Count; i++) if (rows[i].actorId == id) return i; return int.MaxValue; }
        private static void Hit(PrototypeScoreSystem scores, PrototypeBot target, object source, PrototypeTeam team, float amount) =>
            scores.ApplyDamage(target, source, team, amount, () => target.TakeDamage(amount));
        private static void Respawn(PrototypeBot bot) => typeof(PrototypeBot).GetMethod("Respawn", Private).Invoke(bot, null);
        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void Check(bool value, string detail)
        { if (!value) throw new InvalidOperationException(detail); }
    }
}
