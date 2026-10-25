using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeScorePopupSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static float previewAt;
        private static PrototypeRuntime previewGame;
        private static int previewCaptureFrame;
        private static string previewCapturePath;
        private static bool previewCaptureRequested;

        [MenuItem("IRONFRONT/Verify Score Popup", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Verify Score Popup")]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            var fixtures = new List<GameObject>();
            var feed = new List<PrototypeBotDownNotice>();
            PrototypeScoreSystem original = null;
            PrototypeScorePopup popup = null;
            float blue = 0f, red = 0f; bool restore = false;
            try
            {
                Check(game != null && game.Player != null && !game.IsNetworkReplica, "authority and local player exist");
                if (!game.MatchStarted) game.BeginBattle();
                Check(!game.Match.Winner.HasValue, "verify before the match ends");
                original = game.Scores; blue = game.Match.BlueTickets; red = game.Match.RedTickets;
                feed.AddRange(game.BotDownFeed.Entries); restore = true;
                float now = 100f;
                var scores = new PrototypeScoreSystem(game, () => now);
                typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, scores);
                game.Match.SetNetworkTickets(100f, 100f);
                popup = new PrototypeScorePopup(scores, game.Player, () => now);
                PrototypeTeam team = game.Player.Team, enemyTeam = team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                PrototypeBot target = Bot(game, enemyTeam, 0, fixtures), helper = Bot(game, team, 1, fixtures);
                Hit(scores, target, game.Player, team, 40f);
                Check(popup.Visible && popup.Total == 4 && popup.Rows.Count == 1 &&
                    popup.Rows[0].Kind == PrototypeScoreNoticeKind.Damage, "actual nonlethal health damage immediately shows four credits");
                Hit(scores, target, game.Player, team, 60f);
                Check(popup.Total == 100 && popup.Icons.Count == 1 && popup.Icons[0] == PrototypeScoreNoticeKind.Kill &&
                    popup.Rows[0].Kind == PrototypeScoreNoticeKind.Kill && popup.Rows[0].SettlementPoints == 100,
                    "the remaining kill increments merge into a total of exactly one hundred rather than double-counting settlement");
                Respawn(target); Hit(scores, target, helper, team, 1000f);
                Check(popup.Total == 100 && popup.Icons.Count == 1, "another actor's real kill is excluded from the local popup");
                Respawn(target); now += 1.39f; Hit(scores, target, game.Player, team, 40f);
                Check(popup.Total == 104, "a new credit inside the burst gap merges into the visible total");
                now += 1.41f; Hit(scores, target, game.Player, team, 20f);
                Check(popup.Total == 2 && popup.Rows.Count == 1 && popup.Icons.Count == 0,
                    "a credit beyond the burst gap starts a new number and clears old icons");
                Check(scores.TrySpend(game.Player, 20) && scores.Refund(game.Player, 20) && popup.Total == 2,
                    "actual budget debit and refund do not masquerade as earned combat points");
                now += 3.79f; Check(popup.Visible, "popup remains during its visibility interval");
                now += .02f; Check(!popup.Visible, "popup expires after the visibility interval");

                scores.Reset(); popup.Reset(); Respawn(target);
                var hull = Hull(game, enemyTeam, 2, fixtures);
                EmitFour(scores, game.Player, helper, target, hull);
                Check(popup.Total == 458 && popup.Icons.Count == 4 && popup.Rows.Count == 4 &&
                    popup.Icons[0] == PrototypeScoreNoticeKind.Kill && popup.Icons[1] == PrototypeScoreNoticeKind.Assist &&
                    popup.Icons[2] == PrototypeScoreNoticeKind.VehicleDestroyed && popup.Icons[3] == PrototypeScoreNoticeKind.VehicleAssist,
                    "four real terminal outcomes accumulate exact credited deltas and preserve four distinct icons with bounded rows");
                var textures = new HashSet<Texture2D>();
                foreach (PrototypeScoreNoticeKind kind in popup.Icons)
                {
                    Texture2D icon = PrototypeScoreIcons.For(kind);
                    Check(icon != null && icon.width >= 30 && icon.height >= 30 && textures.Add(icon) &&
                        ReferenceEquals(icon, PrototypeScoreIcons.For(kind)), "terminal icon textures are distinct and cached");
                }
                scores.Reset(); popup.Reset(); Respawn(target);
                Check(!popup.Visible && popup.Total == 0 && popup.Rows.Count == 0 && popup.Icons.Count == 0,
                    "a new match clears all presentation state");
                popup.Dispose(); Hit(scores, target, game.Player, team, 40f);
                Check(popup.Total == 0 && popup.Rows.Count == 0, "disposed presentation is unsubscribed from actual later rewards");
                Debug.Log("IRONFRONT score popup PASS: real damage and kill top-up total 100, recipient filtering, burst merge/restart, " +
                    "expiry, budget exclusion, four real outcome icons, bounded rows, exact total 458, cached textures, reset and unsubscribe");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT score popup FAIL: " + error); }
            finally
            {
                popup?.Dispose();
                if (restore && game != null)
                {
                    typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, original);
                    game.Match.SetNetworkTickets(blue, red);
                    var entries = (List<PrototypeBotDownNotice>)typeof(PrototypeBotDownFeed).GetField("entries", Private).GetValue(game.BotDownFeed);
                    entries.Clear(); entries.AddRange(feed);
                }
                foreach (GameObject fixture in fixtures) if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        [MenuItem("IRONFRONT/Preview Score Popup", true)]
        private static bool CanPreview() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Preview Score Popup")]
        private static void Preview()
        {
            try
            {
                previewGame = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(previewGame != null && previewGame.Player != null && !previewGame.IsNetworkReplica,
                    "preview uses an authoritative Play scene");
                if (!previewGame.MatchStarted) previewGame.BeginBattle();
                Check(previewGame.Player.Alive && !previewGame.Match.Winner.HasValue, "preview starts with a living player");
                EditorApplication.isPaused = false;
                if (previewGame.PauseOpen) typeof(PrototypeRuntime).GetMethod("SetPause", Private).Invoke(previewGame, new object[] { false });
                previewGame.SetSupportMenu(false); previewGame.Frontend.Close();
                previewGame.Scores.Reset(); previewGame.ScorePopup.Reset();
                var fixtures = new List<GameObject>();
                PrototypeTeam team = previewGame.Player.Team, enemy = team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                EmitFour(previewGame.Scores, previewGame.Player, Bot(previewGame, team, 1, fixtures),
                    Bot(previewGame, enemy, 0, fixtures), Hull(previewGame, enemy, 2, fixtures));
                Check(previewGame.ScorePopup.Total == 458 && previewGame.ScorePopup.Icons.Count == 4,
                    "preview receives the actual runtime reward stream and all four terminal outcomes");
                // Show the real OnGUI overlay, let its animated number settle,
                // then pause Play to preserve the composed frame for inspection.
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                previewAt = Time.time;
                previewCaptureRequested = false;
                previewCapturePath = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Builds/Verification050/score-popup.png"));
                EditorApplication.update -= PausePreview; EditorApplication.update += PausePreview;
                Debug.Log("IRONFRONT score popup preview: actual 458-point burst from kill, assist, vehicle destruction and vehicle assist; captures the real Game/OnGUI frame after animation, then pauses. Stop Play to discard this preview ledger and temporary actors.");
            }
            catch (Exception error)
            { EditorApplication.update -= PausePreview; Debug.LogError("IRONFRONT score popup preview FAIL: " + error); }
        }

        private static void PausePreview()
        {
            if (!EditorApplication.isPlaying || previewGame == null)
            { EditorApplication.update -= PausePreview; return; }
            try
            {
                if (!previewCaptureRequested)
                {
                    if (Time.time - previewAt < .35f) return;
                    Directory.CreateDirectory(Path.GetDirectoryName(previewCapturePath));
                    ScreenCapture.CaptureScreenshot(previewCapturePath, 2);
                    previewCaptureFrame = Time.frameCount;
                    previewCaptureRequested = true;
                    return;
                }
                // CaptureScreenshot runs asynchronously at the end of the game
                // frame. Keep at least two more real frames alive for its OnGUI
                // composition and image write before pausing the editor.
                if (Time.frameCount < previewCaptureFrame + 2) return;
                EditorApplication.update -= PausePreview; EditorApplication.isPaused = true;
                Debug.Log("IRONFRONT score popup screenshot saved: " + previewCapturePath);
            }
            catch (Exception error)
            {
                EditorApplication.update -= PausePreview;
                Debug.LogError("IRONFRONT score popup screenshot FAIL: " + error);
            }
        }

        private static void EmitFour(PrototypeScoreSystem score, PrototypePlayer player, PrototypeBot helper,
            PrototypeBot target, PrototypeCombinedArmsVehicle hull)
        {
            Hit(score, target, player, player.Team, 1000f);
            Respawn(target); Hit(score, target, player, player.Team, 40f); Hit(score, target, helper, player.Team, 60f);
            Hit(score, hull, player, player.Team, 1000f);
            typeof(PrototypeCombinedArmsVehicle).GetMethod("ResetVehicle", Private).Invoke(hull, null);
            Hit(score, hull, player, player.Team, 40f); Hit(score, hull, helper, player.Team, 1000f);
        }
        private static PrototypeBot Bot(PrototypeRuntime game, PrototypeTeam team, int index, List<GameObject> fixtures)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule); root.name = "Score Popup Preview Infantry";
            fixtures.Add(root); root.transform.SetParent(game.transform);
            var bot = root.AddComponent<PrototypeBot>(); bot.Initialize(game, game.Navigation, team,
                new Vector2(1250f + index * 30f, 900f), PrototypeInfantryClass.Assault); bot.enabled = false;
            root.transform.position += Vector3.up * 240f; return bot;
        }
        private static PrototypeCombinedArmsVehicle Hull(PrototypeRuntime game, PrototypeTeam team, int index, List<GameObject> fixtures)
        {
            var root = new GameObject("Score Popup Preview Hull"); fixtures.Add(root); root.transform.SetParent(game.transform);
            var hull = root.AddComponent<PrototypeCombinedArmsVehicle>(); hull.Initialize(game, team,
                PrototypeVehicleKind.Motorcycle, new Vector2(1250f + index * 30f, 900f), false);
            hull.enabled = false; hull.RespawnEnabled = false; root.transform.position += Vector3.up * 240f; return hull;
        }
        private static void Hit(PrototypeScoreSystem score, object target, object source, PrototypeTeam team, float damage)
        {
            if (target is PrototypeBot bot) score.ApplyDamage(bot, source, team, damage, () => bot.TakeDamage(damage));
            else if (target is IPrototypeVehicle vehicle) score.ApplyDamage(vehicle, source, team, damage, () => vehicle.TakeDamage(damage));
        }
        private static void Respawn(PrototypeBot bot) => typeof(PrototypeBot).GetMethod("Respawn", Private).Invoke(bot, null);
        private static void Check(bool value, string description) { if (!value) throw new InvalidOperationException(description); }
    }
}
