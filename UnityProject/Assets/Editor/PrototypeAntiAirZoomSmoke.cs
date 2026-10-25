using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeAntiAirZoomSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("IRONFRONT/Verify Anti-Air Zoom and Tracking", true)]
        private static bool CanVerify() => EditorApplication.isPlaying && !EditorApplication.isPaused;
        [MenuItem("IRONFRONT/Verify Anti-Air Zoom and Tracking")]
        private static void Verify()
        {
            PrototypeRuntime game = null; PrototypePlayer player = null; PrototypeLoadout oldLoadout = null;
            Vector3 oldPosition = Vector3.zero; Quaternion oldRotation = Quaternion.identity;
            bool oldFirst = false; float oldZoom = 1.5f;
            var objects = new List<GameObject>();
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && !game.IsNetworkReplica, "authoritative runtime exists");
                if (!game.MatchStarted) game.BeginBattle();
                game.Frontend.Close(); game.SetSupportMenu(false);
                if (game.PauseOpen) Invoke(game, "SetPause", false);
                player = game.Player; Check(player.Alive && !game.Match.Winner.HasValue, "living local player in battle");
                player.ForceExitVehicle(); oldPosition = player.transform.position; oldRotation = player.transform.rotation;
                oldLoadout = player.Loadout.Copy(); oldFirst = player.IsFirstPerson; oldZoom = player.AntiAirZoom;
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Engineer,
                    primary = PrototypeWeaponId.Carbine, secondary = PrototypeWeaponId.Pistol });
                CharacterController controller = player.GetComponent<CharacterController>();
                controller.enabled = false; player.transform.SetPositionAndRotation(new Vector3(900f, 240f, 900f), Quaternion.identity); controller.enabled = true;
                if (!player.IsFirstPerson) player.TogglePerspective();
                SetField(player, "pitch", -14f); SetField(player, "aiming", true);
                Check(player.TryEquipAntiAirLauncher(), "engineer equips SAM"); SetField(player, "aiming", true);
                if (player.AntiAirZoom > 2f) player.TryToggleAntiAirZoom();
                Invoke(player, "PositionInfantryCamera", 1f);
                PrototypeTeam enemy = player.Team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                float sourceY = player.ViewCamera.transform.position.y;
                float targetY = sourceY + Mathf.Tan(14f * Mathf.Deg2Rad) * 120f - 1.7f;
                var target = Vehicle(game, enemy, new Vector3(907f, targetY, 1020f), objects);
                var crossing = Vehicle(game, enemy, new Vector3(950f, targetY, 1020f), objects);
                player.ViewCamera.fieldOfView = player.AntiAirAimFieldOfView;
                Check(Mathf.Abs(player.AntiAirAimFieldOfView - 55f) < .2f,
                    "1.5x uses the wide field of view computed from a 76-degree reference");
                float reference = Mathf.Tan(76f * Mathf.Deg2Rad * .5f);
                Check(Mathf.Abs(reference / Mathf.Tan(player.AntiAirAimFieldOfView * Mathf.Deg2Rad * .5f) - 1.5f) < .001f,
                    "default magnification measures focal scale rather than dividing degrees");
                for (int i = 0; i < 20; i++) player.UpdateAntiAirLock(true, .1f);
                Check(ReferenceEquals(player.AntiAirLockTarget, target) && player.AntiAirLockProgress > .39f && !player.AntiAirLocked,
                    "the initially off-center aircraft has a real partial continuous acquisition");
                crossing.transform.position = new Vector3(900f, targetY, 1020f); Physics.SyncTransforms();
                player.UpdateAntiAirLock(true, .1f);
                Check(ReferenceEquals(player.AntiAirLockTarget, target) && player.AntiAirLockProgress > .41f,
                    "a nearer-crosshair aircraft at the same depth does not steal a still-visible valid target");
                Check(PrototypeAntiAirHud.TryTargetRect(player, 960f, 600f, out Rect low, out float distance) &&
                    low.center.x > 480f && distance > 119f && distance < 126f,
                    "yellow target frame projects the actual off-center hull into the GUI canvas");
                float progress = player.AntiAirLockProgress;
                Check(player.TryToggleAntiAirZoom() && player.AntiAirZoom == 3f &&
                    player.AntiAirLockProgress == progress && ReferenceEquals(player.AntiAirLockTarget, target),
                    "T switches to 3x without discarding a legal same-target lock");
                player.ViewCamera.fieldOfView = player.AntiAirAimFieldOfView;
                Check(Mathf.Abs(reference / Mathf.Tan(player.AntiAirAimFieldOfView * Mathf.Deg2Rad * .5f) - 3f) < .001f &&
                    player.AntiAirAimFieldOfView > 29f && player.AntiAirAimFieldOfView < 29.4f,
                    "3x is the proper focal magnification at approximately 29 degrees");
                Check(PrototypeAntiAirHud.TryTargetRect(player, 960f, 600f, out Rect high, out _) &&
                    Mathf.Abs((high.center.x - 480f) / (low.center.x - 480f) - 2f) < .015f,
                    "doubling magnification doubles the real target's screen offset without double-applying GUI scale");
                target.transform.position += Vector3.right * 7f; Physics.SyncTransforms(); player.UpdateAntiAirLock(true, .1f);
                Check(ReferenceEquals(player.AntiAirLockTarget, target) && player.AntiAirLockProgress > progress,
                    "the original visible target remains retained inside eight degrees even outside the new-acquisition cone");
                target.transform.position += Vector3.right * 10f; Physics.SyncTransforms(); player.UpdateAntiAirLock(true, .1f);
                Check(ReferenceEquals(player.AntiAirLockTarget, crossing) && player.AntiAirLockProgress < .03f,
                    "leaving the bounded retention cone resets progress and selects a fresh centered aircraft");
                // Trails/effects never enlarge the projection, which is based
                // on the aircraft's physical hull rather than renderer bounds.
                Check(PrototypeAntiAirHud.TryTargetRect(player, 960f, 600f, out Rect beforeTrail, out _), "centered target projects");
                var trailObject = new GameObject("Huge seeker test trail"); trailObject.transform.SetParent(crossing.transform);
                var trail = trailObject.AddComponent<LineRenderer>(); trail.positionCount = 2;
                trail.SetPositions(new[] { crossing.transform.position, crossing.transform.position + Vector3.right * 1000f });
                Check(PrototypeAntiAirHud.TryTargetRect(player, 960f, 600f, out Rect afterTrail, out _) && beforeTrail == afterTrail,
                    "a thousand-metre effect trail cannot stretch the target frame");
                PrototypePlayerAudio audio = player.GetComponent<PrototypePlayerAudio>();
                Check(audio != null && audio.HudSource != null, "actual local feedback source exists");
                for (int i = 0; i < 48; i++) player.UpdateAntiAirLock(true, .1f);
                Check(!player.AntiAirLocked && !player.TryUseHeldEquipment(), "4.9 seconds still cannot launch");
                int cueBeforeReady = audio.HudCueCount;
                player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLocked && audio.HudCueCount == cueBeforeReady + 1 && audio.LastCue == PrototypePlayerCue.LockReady,
                    "the five-second completion emits one actual ready cue");
                float completed = player.AntiAirLockCompletedAt;
                for (int i = 0; i < 20; i++) player.UpdateAntiAirLock(true, .1f);
                Check(audio.HudCueCount == cueBeforeReady + 1 && player.AntiAirLockCompletedAt == completed,
                    "holding an already-complete lock does not replay ready audio or restart the completion flash");
                player.UpdateAntiAirLock(false, .1f);
                Check(audio.HudCueCount == cueBeforeReady + 2 && audio.LastCue == PrototypePlayerCue.LockLost && player.AntiAirLockTarget == null,
                    "leaving a real acquired target emits a single lost cue");
                for (int i = 0; i < 5; i++) player.UpdateAntiAirLock(false, .1f);
                Check(audio.HudCueCount == cueBeforeReady + 2, "no-target updates cannot spam lost cues");
                SetField(player, "aiming", false);
                Check(!player.TryToggleAntiAirZoom() && player.AntiAirZoom == 3f, "zoom input is gated by active SAM aiming");
                player.EquipFirearm(); Check(!player.TryToggleAntiAirZoom(), "T cannot change another held weapon's zoom");
                Debug.Log("IRONFRONT anti-air zoom/tracking PASS: focal 1.5x/3x fields, gated T, unchanged continuous five-second lock, " +
                    "same-target retention against crossing aircraft, bounded off-axis reset, actual hull canvas projection, effects excluded, " +
                    "single ready/lost feedback transitions and stable completion flash");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT anti-air zoom/tracking FAIL: " + error); }
            finally
            {
                if (game != null) foreach (GameObject item in objects)
                    if (item != null && item.GetComponent<PrototypeCombinedArmsVehicle>() is IPrototypeVehicle vehicle) game.Vehicles.Remove(vehicle);
                if (player != null && oldLoadout != null)
                {
                    player.ResetAntiAirLock(false); player.ApplyLoadout(oldLoadout);
                    SetField(player, "antiAirZoom", oldZoom); SetField(player, "aiming", false); SetField(player, "pitch", 0f);
                    CharacterController controller = player.GetComponent<CharacterController>();
                    controller.enabled = false; player.transform.SetPositionAndRotation(oldPosition, oldRotation); controller.enabled = true;
                    if (player.IsFirstPerson != oldFirst) player.TogglePerspective(); Invoke(player, "PositionInfantryCamera", 1f);
                }
                foreach (GameObject item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }
        private static PrototypeCombinedArmsVehicle Vehicle(PrototypeRuntime game, PrototypeTeam team, Vector3 at, List<GameObject> objects)
        {
            var root = new GameObject("SAM zoom crossing-aircraft fixture"); objects.Add(root);
            var vehicle = root.AddComponent<PrototypeCombinedArmsVehicle>();
            vehicle.Initialize(game, team, PrototypeVehicleKind.Helicopter, new Vector2(at.x, at.z), false);
            vehicle.RespawnEnabled = false; vehicle.transform.position = at; game.Vehicles.Add(vehicle); return vehicle;
        }
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static void Check(bool value, string text) { if (!value) throw new InvalidOperationException(text); }
    }
}
