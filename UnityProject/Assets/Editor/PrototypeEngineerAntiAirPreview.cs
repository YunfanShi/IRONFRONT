using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Temporary Play-mode fixture for inspecting the real model and gameplay
    // HUD. It edits no scene assets or preferences; stopping Play removes it.
    public static class PrototypeEngineerAntiAirPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject fixture;
        private static PrototypeCombinedArmsVehicle aircraft;
        private static PrototypeRuntime previewGame;
        private static string captureName = "anti-air-locked-3x.png";
        private static readonly Dictionary<Behaviour, bool> frozen = new Dictionary<Behaviour, bool>();
        private static string capturePath;
        private static bool capturePending, captureRequested;
        private static int preparedFrame, capturedFrame;
        private static float preparedAt;
        private static double captureStarted;

        [MenuItem("IRONFRONT/Preview Engineer Anti-Air", true)]
        [MenuItem("IRONFRONT/Preview Anti-Air Search 1.5x", true)]
        [MenuItem("IRONFRONT/Preview Anti-Air Acquiring 1.5x", true)]
        [MenuItem("IRONFRONT/Preview Anti-Air Locked 3x", true)]
        private static bool CanPreview() => EditorApplication.isPlaying && !capturePending;
        [MenuItem("IRONFRONT/Preview Engineer Anti-Air")]
        private static void Preview() => Prepare(51, 3f, "anti-air-locked-3x.png");
        [MenuItem("IRONFRONT/Preview Anti-Air Search 1.5x")]
        private static void PreviewSearch() => Prepare(0, 1.5f, "anti-air-search-1.5x.png");
        [MenuItem("IRONFRONT/Preview Anti-Air Acquiring 1.5x")]
        private static void PreviewAcquiring() => Prepare(25, 1.5f, "anti-air-acquiring-1.5x.png");
        [MenuItem("IRONFRONT/Preview Anti-Air Locked 3x")]
        private static void PreviewLocked() => Prepare(51, 3f, "anti-air-locked-3x.png");

        private static void Prepare(int ticks, float zoom, string filename)
        {
            try
            {
                PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                if (game == null || game.Player == null || game.IsNetworkReplica)
                    throw new InvalidOperationException("A Solo/host Play-mode runtime is required.");
                if (!game.MatchStarted) game.BeginBattle();
                if (!game.Player.Alive || game.Match.Winner.HasValue)
                    throw new InvalidOperationException("Start a fresh active battle with a living player.");
                EditorApplication.update -= CaptureFrame;
                RestoreActors();
                EditorApplication.isPaused = false;
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                game.Frontend.Close(); game.SetSupportMenu(false);
                if (game.PauseOpen) typeof(PrototypeRuntime).GetMethod("SetPause", Private)
                    .Invoke(game, new object[] { false });
                if (fixture != null)
                {
                    previewGame?.Vehicles.Remove(aircraft);
                    UnityEngine.Object.DestroyImmediate(fixture);
                }
                previewGame = game;
                PrototypePlayer player = game.Player;
                player.ForceExitVehicle();
                // Disable simulation before setting the public seeker state;
                // OnDisable resets the old lock and hides old held equipment.
                // Rendering and the Runtime's actual OnGUI remain enabled.
                Freeze(player);
                foreach (PrototypeBot bot in game.Bots) Freeze(bot);
                foreach (PrototypeRemotePlayer actor in game.RemotePlayers) Freeze(actor);
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                    if (vehicle is Behaviour behaviour) Freeze(behaviour);
                game.HitFeedback?.Reset(); game.ScorePopup?.Reset();
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Engineer,
                    primary = PrototypeWeaponId.Carbine, secondary = PrototypeWeaponId.Pistol,
                    throwable = PrototypeThrowableId.Frag });
                float ground = PrototypeLayout.HeightAt(-325f, -320f);
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.transform.SetPositionAndRotation(new Vector3(-325f, ground + .3f, -320f), Quaternion.identity);
                controller.enabled = true;
                SetField(player, "crouched", false); SetField(player, "prone", false);
                SetField(player, "slideEndsAt", 0f); SetField(player, "sprinting", false);
                if (!player.IsFirstPerson) player.TogglePerspective();
                fixture = new GameObject("EDITOR ONLY - Engineer SAM visual preview fixture");
                GameObject target = new GameObject("EDITOR ONLY - Actual enemy H8 lock target");
                target.transform.SetParent(fixture.transform, false);
                aircraft = target.AddComponent<PrototypeCombinedArmsVehicle>();
                aircraft.Initialize(game, player.Team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue,
                    PrototypeVehicleKind.Helicopter, new Vector2(-325f, -225f), false);
                aircraft.RespawnEnabled = false;
                Freeze(aircraft);
                aircraft.transform.SetPositionAndRotation(new Vector3(-317f, ground + 27f, -225f),
                    Quaternion.Euler(0f, 75f, 0f));
                game.Vehicles.Add(aircraft);
                if (!player.TryEquipAntiAirLauncher()) throw new InvalidOperationException("Engineer could not equip SAM.");
                Vector3 eye = player.transform.position + Vector3.up * 1.65f;
                Vector3 travel = aircraft.transform.position + Vector3.up * 1.7f - eye;
                SetField(player, "pitch", -Mathf.Atan2(travel.y, travel.z) * Mathf.Rad2Deg);
                SetField(player, "aiming", true);
                if (Mathf.Abs(player.AntiAirZoom - zoom) > .01f && !player.TryToggleAntiAirZoom())
                    throw new InvalidOperationException("Public aiming zoom toggle was unavailable.");
                typeof(PrototypePlayer).GetMethod("PositionInfantryCamera", Private)
                    .Invoke(player, new object[] { 1f });
                // Use the same aiming presentation and public seeker as play.
                for (int i = 0; i < 200; i++)
                {
                    typeof(PrototypePlayer).GetMethod("UpdateInfantryPresentation", Private).Invoke(player, null);
                    player.RefreshHeldPresentation();
                }
                player.ViewCamera.fieldOfView = player.AntiAirAimFieldOfView;
                Physics.SyncTransforms();
                for (int i = 0; i < ticks; i++) player.UpdateAntiAirLock(true, .1f);
                if (ticks > 0 && (!ReferenceEquals(player.AntiAirLockTarget, aircraft) || !aircraft.BeingLocked) ||
                    ticks >= 50 && !player.AntiAirLocked || ticks < 50 && player.AntiAirLocked)
                    throw new InvalidOperationException("Preview aircraft did not acquire through the real optical seeker.");
                captureName = filename;
                BeginCapture();
                Debug.Log("IRONFRONT SAM visual preview prepared: " + zoom.ToString("0.#") + "x, " +
                    (ticks == 0 ? "SEARCH" : ticks < 50 ? "ACQUIRING yellow target" : "LOCKED red target") +
                    ", actual first-person engineer model and off-center enemy H8; capturing real OnGUI before pausing. Stop Play removes temporary fixture.");
            }
            catch (Exception error)
            {
                EditorApplication.update -= CaptureFrame; capturePending = false; RestoreActors();
                Debug.LogError("IRONFRONT SAM visual preview FAIL: " + error);
            }
        }

        [MenuItem("IRONFRONT/Capture Anti-Air Launcher Preview", true)]
        private static bool CanCapture() => EditorApplication.isPlaying && previewGame != null && !capturePending;
        [MenuItem("IRONFRONT/Capture Anti-Air Launcher Preview")]
        private static void Capture()
        {
            PrototypePlayer player = previewGame?.Player;
            if (player == null) throw new InvalidOperationException("Prepare the SAM visual preview first.");
            int ticks = player.AntiAirLocked ? 51 : Mathf.RoundToInt(player.AntiAirLockProgress * 50f);
            Prepare(ticks, player.AntiAirZoom, captureName);
        }

        private static void BeginCapture()
        {
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Builds", "Verification051");
            Directory.CreateDirectory(folder); capturePath = Path.Combine(folder, captureName);
            if (File.Exists(capturePath)) File.Delete(capturePath);
            preparedAt = Time.time; preparedFrame = Time.frameCount;
            captureStarted = EditorApplication.timeSinceStartup;
            capturePending = true; captureRequested = false;
            EditorApplication.update -= CaptureFrame; EditorApplication.update += CaptureFrame;
        }

        private static void CaptureFrame()
        {
            try
            {
                if (!EditorApplication.isPlaying || previewGame == null)
                { EndCapture(false); return; }
                if (EditorApplication.timeSinceStartup - captureStarted > 8d)
                    throw new InvalidOperationException("Game frames or screenshot output did not complete.");
                if (!captureRequested)
                {
                    if (Time.time - preparedAt < .1f || Time.frameCount < preparedFrame + 2) return;
                    ScreenCapture.CaptureScreenshot(capturePath, 2);
                    capturedFrame = Time.frameCount; captureRequested = true; return;
                }
                if (Time.frameCount < capturedFrame + 2 || !File.Exists(capturePath) || new FileInfo(capturePath).Length == 0) return;
                EndCapture(true);
                Debug.Log("IRONFRONT SAM HUD screenshot saved: " + capturePath +
                    " (actual Game view including target frame, magnification and real OnGUI; Editor paused)");
            }
            catch (Exception error)
            {
                EndCapture(true); Debug.LogError("IRONFRONT SAM HUD screenshot FAIL: " + error);
            }
        }
        private static void EndCapture(bool pause)
        {
            EditorApplication.update -= CaptureFrame; capturePending = false;
            if (pause && EditorApplication.isPlaying) EditorApplication.isPaused = true;
            RestoreActors();
        }
        private static void Freeze(Behaviour actor)
        {
            if (actor == null || frozen.ContainsKey(actor)) return;
            frozen.Add(actor, actor.enabled); actor.enabled = false;
        }
        private static void RestoreActors()
        {
            foreach (KeyValuePair<Behaviour, bool> state in frozen)
                if (state.Key != null) state.Key.enabled = state.Value;
            frozen.Clear();
        }
        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);
    }
}
