using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeHitFeedbackSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 FixturePosition = new Vector3(1250f, 240f, 900f);
        private static float previewAt;
        private static int captureFrame;
        private static bool captured;
        private static string capturePath;

        [MenuItem("IRONFRONT/Verify Immediate Hit Feedback", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Verify Immediate Hit Feedback")]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            var fixtures = new List<GameObject>();
            var oldPulses = new HashSet<PrototypePulseVisual>(UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>());
            var feed = new List<PrototypeBotDownNotice>();
            PrototypeScoreSystem original = null; PrototypeHitFeedback feedback = null;
            float blue = 0f, red = 0f; bool restore = false;
            try
            {
                VerifyScaledGeometry();
                Check(game != null && game.Player != null && game.PlayerAudio != null && !game.IsNetworkReplica,
                    "authoritative local player and initialized audio exist");
                if (!game.MatchStarted) game.BeginBattle();
                game.Frontend.Close(); game.SetSupportMenu(false);
                Check(game.Player.Alive && !game.PauseOpen && !game.Match.Winner.HasValue && game.Player.CurrentVehicle == null,
                    "verify while alive and on foot in an unpaused match");
                original = game.Scores; blue = game.Match.BlueTickets; red = game.Match.RedTickets;
                feed.AddRange(game.BotDownFeed.Entries); restore = true;
                float now = 100f;
                var scores = new PrototypeScoreSystem(game, () => now);
                typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, scores);
                game.Match.SetNetworkTickets(100f, 100f);
                var anchor = new GameObject("Immediate Hit Feedback Verification"); fixtures.Add(anchor);
                feedback = anchor.AddComponent<PrototypeHitFeedback>(); feedback.enabled = false;
                feedback.Initialize(scores, game.Player, () => now);
                PrototypeTeam team = game.Player.Team, enemyTeam = team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                var enemy = Bot(game, enemyTeam, FixturePosition, fixtures);
                var friendly = Bot(game, team, FixturePosition + Vector3.right * 30f, fixtures);
                var hull = Hull(game, enemyTeam, FixturePosition + Vector3.right * 60f, fixtures);
                var gunnerVehicle = Hull(game, team, FixturePosition + Vector3.right * 100f, fixtures, PrototypeVehicleKind.Ifv);
                var crateRoot = new GameObject("Hit Feedback Neutral Supply"); fixtures.Add(crateRoot);
                var crate = crateRoot.AddComponent<PrototypeAmmoSupplyPoint>();
                crate.Initialize(game, new Vector2(1450f, 900f), "Hit Feedback Supply", game.Player);
                crateRoot.transform.position += Vector3.up * 240f;

                int audioBefore = game.PlayerAudio.HudCueCount;
                Bullet(game, game.Player.transform, team, enemy.GetComponent<Collider>(), 1f / 31f, fixtures);
                Check(enemy.Health < 100f && feedback.HitsObserved == 1 && feedback.Visible && !feedback.IsLethal &&
                    scores.Get(game.Player).Total == 0 && feedback.HasPendingCue,
                    "a real one-health bullet lights the marker immediately without waiting for a damage point");
                now += .044f; feedback.Tick(); Check(feedback.CueRequests == 0, "audio aggregation waits its short interval");
                now += .002f; feedback.Tick();
                Check(feedback.CueRequests == 1 && feedback.LastCueRequested == PrototypePlayerCue.Hit &&
                    game.PlayerAudio.HudCueCount == audioBefore + 1 && game.PlayerAudio.LastCue == PrototypePlayerCue.Hit,
                    "the real two-dimensional hit cue plays once after the aggregation interval");
                now += .18f; Check(!feedback.Visible, "normal hit strokes expire promptly");

                feedback.Reset();
                Bullet(game, game.Player.transform, team, hull.GetComponent<Collider>(), 4f, fixtures);
                Bullet(game, game.Player.transform, team, friendly.GetComponent<Collider>(), 4f, fixtures);
                Bullet(game, game.Player.transform, team, game.Player.GetComponent<Collider>(), 4f, fixtures);
                Bullet(game, game.Player.transform, team, crate.GetComponent<Collider>(), 1f, fixtures);
                var airRound = Bullet(game, game.Player.transform, team, null, 1f, fixtures);
                Physics.SyncTransforms();
                object[] query = { new Vector3(1500f, 350f, 1200f), Vector3.up, 20f, Vector3.zero, null };
                Check(!(bool)InvokeArgs(airRound, "TryHit", query) && hull.Health == hull.MaxHealth && friendly.Health == 100f &&
                    crate.Health < PrototypeAmmoSupplyPoint.MaxHealth && feedback.HitsObserved == 0 && !feedback.Visible && !feedback.HasPendingCue,
                    "armor-blocked hull, friend, self, neutral crate and air have no hit confirmation");
                Bullet(game, friendly.transform, team, enemy.GetComponent<Collider>(), .1f, fixtures);
                Check(feedback.HitsObserved == 0, "another actor's real hit is excluded from local feedback");

                Respawn(enemy); feedback.Reset();
                Check(gunnerVehicle.TrySetPlayerSeat(game.Player, 1), "actual gunner owns the weapon at launch");
                var mounted = Track(PrototypeVehicleProjectile.Launch(game, team, gunnerVehicle.transform,
                    gunnerVehicle.WeaponMuzzle.position, Vector3.forward, 180f, 175f, 20f, 0f, 0f, 1f), fixtures);
                gunnerVehicle.RemoveDriver(game.Player);
                Physics.SyncTransforms(); Invoke(mounted, "Explode", enemy.transform.position, enemy.GetComponent<Collider>());
                Check(feedback.HitsObserved == 1 && feedback.Visible && !feedback.IsLethal,
                    "real mounted impact keeps the departing launch operator's local feedback attribution");

                feedback.Reset(); audioBefore = game.PlayerAudio.HudCueCount;
                var rocket = Track(PrototypeBallisticProjectile.LaunchRocket(game, team, game.Player.transform,
                    hull.transform.position, Vector3.forward), fixtures);
                Physics.SyncTransforms(); Invoke(rocket, "Explode", hull.GetComponent<BoxCollider>().bounds.center);
                Check(!hull.Alive && feedback.HitsObserved == 1 && feedback.KillsObserved == 1 && feedback.IsLethal,
                    "real anti-armor vehicle destruction immediately strengthens the marker to red");
                now += .046f; feedback.Tick();
                Check(feedback.CueRequests == 1 && feedback.LastCueRequested == PrototypePlayerCue.Kill &&
                    game.PlayerAudio.HudCueCount == audioBefore + 1 && game.PlayerAudio.LastCue == PrototypePlayerCue.Kill,
                    "vehicle damage plus destruction selects one stronger kill cue");

                Respawn(enemy); enemy.transform.position = FixturePosition;
                var second = Bot(game, enemyTeam, FixturePosition + Vector3.right * 1.2f, fixtures);
                enemy.gameObject.AddComponent<BoxCollider>().size = new Vector3(.7f, 1.8f, .7f);
                feedback.Reset(); audioBefore = game.PlayerAudio.HudCueCount;
                var shell = Track(PrototypeBallisticProjectile.LaunchShell(game, team, game.Player.transform,
                    FixturePosition, Vector3.forward, 170f, 230f), fixtures);
                Physics.SyncTransforms(); Invoke(shell, "Explode", FixturePosition);
                Check(!enemy.Alive && !second.Alive && feedback.HitsObserved == 2 && feedback.KillsObserved == 2 && feedback.IsLethal,
                    "a real multi-victim blast reacts to both health losses and rejects duplicate body colliders");
                now += .046f; feedback.Tick();
                Check(feedback.CueRequests == 1 && game.PlayerAudio.HudCueCount == audioBefore + 1 &&
                    game.PlayerAudio.LastCue == PrototypePlayerCue.Kill, "one blast's hit and kill layers coalesce into one kill sound");
                Invoke(shell, "Explode", FixturePosition);
                Check(feedback.HitsObserved == 2 && feedback.KillsObserved == 2, "repeat dead impacts cannot duplicate feedback");
                now += .26f; Check(!feedback.Visible, "kill strokes also expire promptly");
                Respawn(enemy); feedback.Reset();
                Bullet(game, game.Player.transform, team, enemy.GetComponent<Collider>(), .1f, fixtures);
                feedback.Reset(); now += .10f; feedback.Tick();
                Check(!feedback.Visible && !feedback.HasPendingCue && feedback.CueRequests == 0,
                    "lifecycle reset cancels both marker and unplayed sound");
                Bullet(game, game.Player.transform, team, enemy.GetComponent<Collider>(), .1f, fixtures);
                Check(feedback.HasPendingCue && feedback.Visible, "a real hit queues confirmation immediately before pause");
                audioBefore = game.PlayerAudio.HudCueCount;
                typeof(PrototypeRuntime).GetMethod("SetPause", Private).Invoke(game, new object[] { true });
                feedback.Tick();
                Check(!feedback.Visible && !feedback.HasPendingCue, "inactive paused context clears the queued marker and cue even while game time freezes");
                typeof(PrototypeRuntime).GetMethod("SetPause", Private).Invoke(game, new object[] { false });
                now += .10f; feedback.Tick();
                Check(feedback.CueRequests == 0 && game.PlayerAudio.HudCueCount == audioBefore,
                    "resuming gameplay cannot dispatch an old pre-pause hit cue");
                feedback.Dispose(); Bullet(game, game.Player.transform, team, enemy.GetComponent<Collider>(), .1f, fixtures);
                Check(feedback.HitsObserved == 0 && !feedback.Visible && !feedback.HasPendingCue,
                    "disposed feedback unsubscribes from actual later damage");
                Debug.Log("IRONFRONT immediate hit feedback PASS: real sub-point health loss, white marker, actual Hit/Kill audio, " +
                    "armor/friend/self/air/crate exclusion, foreign recipient, launch operator retention, red vehicle kill, " +
                    "multi-target blast dedup/one stronger sound, short expiry, pause queue cancellation, reset and unsubscribe");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT immediate hit feedback FAIL: " + error); }
            finally
            {
                feedback?.Dispose();
                if (restore && game != null)
                {
                    typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, original); game.Match.SetNetworkTickets(blue, red);
                    var entries = (List<PrototypeBotDownNotice>)typeof(PrototypeBotDownFeed).GetField("entries", Private).GetValue(game.BotDownFeed);
                    entries.Clear(); entries.AddRange(feed);
                }
                foreach (GameObject fixture in fixtures) if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                foreach (PrototypePulseVisual pulse in UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>())
                    if (!oldPulses.Contains(pulse)) UnityEngine.Object.DestroyImmediate(pulse.gameObject);
            }
        }

        [MenuItem("IRONFRONT/Preview White Hit Marker", true)]
        private static bool CanPreviewWhite() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Preview White Hit Marker")]
        private static void PreviewWhite() => Preview(false);
        [MenuItem("IRONFRONT/Preview Red Kill Marker", true)]
        private static bool CanPreviewRed() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Preview Red Kill Marker")]
        private static void PreviewRed() => Preview(true);
        private static void Preview(bool lethal)
        {
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && game.HitFeedback != null && !game.IsNetworkReplica,
                    "preview needs the initialized local feedback");
                if (!game.MatchStarted) game.BeginBattle();
                Check(game.Player.Alive && !game.Match.Winner.HasValue, "preview starts before match end");
                EditorApplication.isPaused = false;
                typeof(PrototypeRuntime).GetMethod("SetPause", Private).Invoke(game, new object[] { false });
                game.SetSupportMenu(false); game.Frontend.Close();
                game.Player.EquipFirearm(); game.HitFeedback.Reset(); game.ScorePopup?.Reset();
                var fixtures = new List<GameObject>();
                PrototypeTeam enemy = game.Player.Team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                var target = Bot(game, enemy, FixturePosition, fixtures);
                Bullet(game, game.Player.transform, game.Player.Team, target.GetComponent<Collider>(), lethal ? 4f : 1f / 31f, fixtures);
                Check(game.HitFeedback.Visible && game.HitFeedback.IsLethal == lethal, "real projectile produced the requested marker state");
                EditorApplication.ExecuteMenuItem("Window/General/Game"); previewAt = Time.time; captured = false;
                capturePath = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Builds/Verification051/" + (lethal ? "kill-marker.png" : "hit-marker.png")));
                EditorApplication.update -= CapturePreview; EditorApplication.update += CapturePreview;
                Debug.Log("IRONFRONT hit marker preview: actual " + (lethal ? "lethal red" : "one-health white") +
                    " projectile confirmation; captures real OnGUI and pauses. Stop Play removes preview fixtures.");
            }
            catch (Exception error) { EditorApplication.update -= CapturePreview; Debug.LogError("IRONFRONT hit marker preview FAIL: " + error); }
        }
        private static void CapturePreview()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= CapturePreview; return; }
            if (!captured)
            {
                if (Time.time - previewAt < .065f) return;
                Directory.CreateDirectory(Path.GetDirectoryName(capturePath)); ScreenCapture.CaptureScreenshot(capturePath, 2);
                captureFrame = Time.frameCount; captured = true; return;
            }
            if (Time.frameCount < captureFrame + 2) return;
            EditorApplication.update -= CapturePreview; EditorApplication.isPaused = true;
            Debug.Log("IRONFRONT hit marker screenshot saved: " + capturePath);
        }
        private static PrototypeBot Bot(PrototypeRuntime game, PrototypeTeam team, Vector3 position, List<GameObject> fixtures)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule); root.name = "Hit Feedback Verification Infantry";
            fixtures.Add(root); root.transform.SetParent(game.transform);
            var bot = root.AddComponent<PrototypeBot>(); bot.Initialize(game, game.Navigation, team,
                new Vector2(position.x, position.z), PrototypeInfantryClass.Assault); bot.enabled = false; root.transform.position = position; return bot;
        }
        private static PrototypeCombinedArmsVehicle Hull(PrototypeRuntime game, PrototypeTeam team, Vector3 position,
            List<GameObject> fixtures, PrototypeVehicleKind kind = PrototypeVehicleKind.Motorcycle)
        {
            var root = new GameObject("Hit Feedback Verification Hull"); fixtures.Add(root); root.transform.SetParent(game.transform);
            var hull = root.AddComponent<PrototypeCombinedArmsVehicle>(); hull.Initialize(game, team, kind,
                new Vector2(position.x, position.z), false); hull.enabled = false; hull.RespawnEnabled = false;
            root.transform.position = position; return hull;
        }
        private static PrototypeBallisticProjectile Bullet(PrototypeRuntime game, Transform source, PrototypeTeam team,
            Collider collider, float scale, List<GameObject> fixtures)
        {
            var bullet = Track(PrototypeBallisticProjectile.LaunchBullet(game, team, source, FixturePosition + Vector3.up * 10f,
                Vector3.forward, PrototypeInfantryRoles.GetWeapon(PrototypeWeaponId.Carbine), scale), fixtures);
            if (collider != null) Invoke(bullet, "ApplyBulletDamage", collider, 0f); return bullet;
        }
        private static PrototypeBallisticProjectile Track(PrototypeBallisticProjectile projectile, List<GameObject> fixtures)
        { Check(projectile != null, "actual ballistic projectile launches"); projectile.enabled = false; fixtures.Add(projectile.gameObject); return projectile; }
        private static PrototypeVehicleProjectile Track(PrototypeVehicleProjectile projectile, List<GameObject> fixtures)
        { Check(projectile != null, "actual mounted projectile launches"); projectile.enabled = false; fixtures.Add(projectile.gameObject); return projectile; }
        private static void Respawn(PrototypeBot bot) => typeof(PrototypeBot).GetMethod("Respawn", Private).Invoke(bot, null);
        [MenuItem("IRONFRONT/Verify Hit Marker Screen Geometry")]
        private static void VerifyScreenGeometry()
        {
            try { VerifyScaledGeometry(); Debug.Log("IRONFRONT hit marker geometry PASS: physical center, radial stroke distances and opposite symmetry under 0.612 / 1 / 1.75 canvas scale plus viewport translation"); }
            catch (Exception error) { Debug.LogError("IRONFRONT hit marker geometry FAIL: " + error); }
        }
        private static void VerifyScaledGeometry()
        {
            MethodInfo transform = typeof(PrototypeHitFeedback).GetMethod("StrokeMatrix", BindingFlags.Static | BindingFlags.NonPublic);
            Vector2 center = new Vector2(480f, 320f);
            const float gap = 7f, length = 8.5f;
            foreach (float scale in new[] { .612f, 1f, 1.75f })
            {
                Matrix4x4 viewport = Matrix4x4.TRS(new Vector3(13f, -7f, 0f), Quaternion.identity,
                    new Vector3(scale, scale, 1f));
                Vector3 physicalCenter = new Vector3(13f + center.x * scale, -7f + center.y * scale, 0f);
                Vector3[] midpoints = new Vector3[4]; Vector3 sum = Vector3.zero;
                for (int i = 0; i < 4; i++)
                {
                    Matrix4x4 stroke = (Matrix4x4)transform.Invoke(null, new object[] { viewport, center, 45f + i * 90f });
                    Check(Vector3.Distance(stroke.MultiplyPoint3x4(center), physicalCenter) < .001f,
                        "rotating a stroke preserves the actual physical viewport center at non-unit scale");
                    Vector3 inner = stroke.MultiplyPoint3x4(new Vector3(center.x + gap, center.y, 0f));
                    Vector3 outer = stroke.MultiplyPoint3x4(new Vector3(center.x + gap + length, center.y, 0f));
                    Check(Mathf.Abs(Vector3.Distance(inner, physicalCenter) - gap * scale) < .001f &&
                        Mathf.Abs(Vector3.Distance(outer, physicalCenter) - (gap + length) * scale) < .001f,
                        "actual stroke endpoint radii scale with the canvas and cannot migrate toward the screen edge");
                    midpoints[i] = (inner + outer) * .5f; sum += midpoints[i];
                }
                Check(Vector3.Distance(sum * .25f, physicalCenter) < .001f &&
                    Vector3.Distance((midpoints[0] + midpoints[2]) * .5f, physicalCenter) < .001f &&
                    Vector3.Distance((midpoints[1] + midpoints[3]) * .5f, physicalCenter) < .001f,
                    "four drawn strokes balance around the reticle rather than a displaced pivot");
            }
        }
        private static object Invoke(object target, string name, params object[] args) => InvokeArgs(target, name, args);
        private static object InvokeArgs(object target, string name, object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static void Check(bool value, string description) { if (!value) throw new InvalidOperationException(description); }
    }
}
