using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Calls the actual vehicle Update on independent occupied hulls. The test
    // never replaces Drive with a duplicate movement calculation.
    public static class PrototypeScoreboardVehicleCoastSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool waiting;
        private static double waitingSince;

        [MenuItem("IRONFRONT/Verify Scoreboard Vehicle Coasting", true)]
        private static bool CanRun() => EditorApplication.isPlaying && !waiting;

        [MenuItem("IRONFRONT/Verify Scoreboard Vehicle Coasting")]
        public static void Verify()
        {
            if (Time.deltaTime <= 0f)
            {
                if (!waiting) waitingSince = EditorApplication.timeSinceStartup;
                waiting = true; EditorApplication.update -= Wait; EditorApplication.update += Wait; return;
            }
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            PrototypePlayer oldPlayer = game?.Player;
            PrototypeMatch oldMatch = game?.Match;
            PrototypeScoreSystem oldScores = game?.Scores;
            PrototypeFrontend oldFrontend = game?.Frontend;
            PrototypeScoreboard oldBoard = game?.Scoreboard;
            bool oldStarted = game != null && game.MatchStarted, oldPause = game != null && game.PauseOpen;
            bool oldSupport = game != null && game.SupportMenuOpen, oldOpen = game != null && game.ScoreboardOpen;
            float oldVolume = AudioListener.volume, oldScale = Time.timeScale;
            CursorLockMode oldLock = Cursor.lockState; bool oldCursor = Cursor.visible;
            GameObject fixture = null; PrototypePlayer player = null; PrototypeFrontend frontend = null;
            bool swapped = false;
            var added = new List<IPrototypeVehicle>();
            var oldRounds = new HashSet<PrototypeBallisticProjectile>(UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>());
            var oldShells = new HashSet<PrototypeVehicleProjectile>(UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>());
            Exception failure = null;
            try
            {
                Check(game != null && oldPlayer != null && oldMatch != null && !game.IsNetworkReplica,
                    "authoritative real Play runtime exists");
                var match = new PrototypeMatch(); match.ConfigureSolo(PrototypeGameMode.Conquest, 100);
                var scores = new PrototypeScoreSystem(game); match.ScoreSystem = scores;
                frontend = new PrototypeFrontend(game);
                Set(game, "<Match>k__BackingField", match); Set(game, "<Scores>k__BackingField", scores);
                Set(game, "<Frontend>k__BackingField", frontend); Set(game, "<Scoreboard>k__BackingField", new PrototypeScoreboard(game));
                Set(game, "<MatchStarted>k__BackingField", true); Set(game, "<PauseOpen>k__BackingField", false);
                Set(game, "<SupportMenuOpen>k__BackingField", false); Set(game, "<ScoreboardOpen>k__BackingField", false); swapped = true;
                fixture = new GameObject("EDITOR ONLY scoreboard vehicle coasting fixture");
                var actor = new GameObject("Independent coast driver"); actor.transform.SetParent(fixture.transform);
                actor.AddComponent<CharacterController>(); player = actor.AddComponent<PrototypePlayer>();
                Set(game, "<Player>k__BackingField", player); player.Initialize(game); player.enabled = false;
                foreach (Camera camera in actor.GetComponentsInChildren<Camera>()) camera.enabled = false;
                foreach (AudioListener listener in actor.GetComponentsInChildren<AudioListener>()) listener.enabled = false;
                frontend.Close();
                foreach (string kind in new[] { "tank", "scout", "transport", "ifv", "motorcycle" })
                {
                    IPrototypeSeatedVehicle vehicle = Make(game, fixture, kind, added);
                    Check((bool)Invoke(player, "EnterVehicle", vehicle), kind + " actual driver seat accepts fixture player");
                    Check(game.SetScoreboard(true) && vehicle.SeatCanDrive, kind + " standings opens with local driver authority");
                    Set(vehicle, "<Speed>k__BackingField", 6f);
                    Vector3 start = vehicle.VehicleTransform.position; Quaternion heading = vehicle.VehicleTransform.rotation;
                    Quaternion bore = ((IPrototypeWeaponAim)vehicle).WeaponMuzzle != null ?
                        ((IPrototypeWeaponAim)vehicle).WeaponMuzzle.rotation : Quaternion.identity;
                    if (vehicle is PrototypeCombinedArmsVehicle combined)
                    {
                        combined.ApplyPlayerInput(1f, 1f, 0f, false, false, .05f);
                        Check(combined.Speed == 6f && Vector3.Distance(start, combined.transform.position) < .001f &&
                            Quaternion.Angle(heading, combined.transform.rotation) < .001f,
                            kind + " public manual-input API still rejects the open scoreboard");
                    }
                    Invoke(vehicle, "Update");
                    float coastSpeed = vehicle.Speed;
                    Check(coastSpeed > 0f && coastSpeed < 6f && Vector3.Distance(start, vehicle.VehicleTransform.position) > 0f &&
                        Quaternion.Angle(heading, vehicle.VehicleTransform.rotation) < .001f,
                        kind + " real scoreboard-open Update translates while braking with centered steering; dt=" + Time.deltaTime);
                    for (int i = 0; i < 5; i++) Invoke(vehicle, "Update");
                    Check(vehicle.Speed <= coastSpeed && Vector3.Distance(start, vehicle.VehicleTransform.position) > 0f,
                        kind + " actual repeated Updates continue monotonic coast-down");
                    if (((IPrototypeWeaponAim)vehicle).WeaponMuzzle != null)
                        Check(Quaternion.Angle(bore, ((IPrototypeWeaponAim)vehicle).WeaponMuzzle.rotation) < .001f,
                            kind + " open standings does not aim the mounted weapon");
                    CheckNoRounds(oldRounds, oldShells, kind);

                    IPrototypeSeatedVehicle blocker = Make(game, fixture, "tank", added);
                    Vector3 stoppedAt = vehicle.VehicleTransform.position;
                    blocker.VehicleTransform.position = stoppedAt + vehicle.VehicleTransform.forward *
                        (PrototypeVehicleSpace.Radius(vehicle) + PrototypeVehicleSpace.Radius(blocker) - .02f);
                    Physics.SyncTransforms(); Set(vehicle, "<Speed>k__BackingField", 6f); Invoke(vehicle, "Update");
                    Check(vehicle.Speed == 0f && Vector3.Distance(stoppedAt, vehicle.VehicleTransform.position) < .001f,
                        kind + " original hull collision path stops inertial motion at another actual vehicle");
                    Remove(game, blocker, added);
                    Check(game.SetScoreboard(false) && vehicle.Speed == 0f, kind + " closing standings does not restore a stale speed");
                    Invoke(vehicle, "Update");
                    Check(vehicle.Speed == 0f && Vector3.Distance(stoppedAt, vehicle.VehicleTransform.position) < .001f,
                        kind + " actual next closed-overlay Update has no old-velocity jump without manual input");
                    Check(game.SetScoreboard(true) && player.TrySwitchVehicleSeat(1) && !vehicle.SeatCanDrive,
                        kind + " unchanged gunner/passenger transfer relinquishes driver authority");
                    Set(vehicle, "<Speed>k__BackingField", 4f);
                    Vector3 passengerPosition = vehicle.VehicleTransform.position; Quaternion passengerHeading = vehicle.VehicleTransform.rotation;
                    Invoke(vehicle, "Update");
                    Check(Vector3.Distance(passengerPosition, vehicle.VehicleTransform.position) < .001f &&
                        Quaternion.Angle(passengerHeading, vehicle.VehicleTransform.rotation) < .001f,
                        kind + " non-driver scoreboard branch cannot claim control of hull motion");
                    CheckNoRounds(oldRounds, oldShells, kind);
                    player.ForceExitVehicle(); game.SetScoreboard(false); Remove(game, vehicle, added);
                }
            }
            catch (Exception error) { failure = error.GetBaseException(); }
            finally
            {
                try
                {
                    if (player != null && player.CurrentVehicle != null) player.ForceExitVehicle();
                    if (game != null) foreach (IPrototypeVehicle vehicle in added) game.Vehicles.Remove(vehicle);
                    if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture); frontend?.Dispose();
                    foreach (var round in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                        if (!oldRounds.Contains(round)) UnityEngine.Object.DestroyImmediate(round.gameObject);
                    foreach (var round in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                        if (!oldShells.Contains(round)) UnityEngine.Object.DestroyImmediate(round.gameObject);
                }
                catch (Exception error) { failure = failure ?? error.GetBaseException(); }
                finally
                {
                    if (swapped && game != null)
                    {
                        Set(game, "<Player>k__BackingField", oldPlayer); Set(game, "<Match>k__BackingField", oldMatch);
                        Set(game, "<Scores>k__BackingField", oldScores); Set(game, "<Frontend>k__BackingField", oldFrontend);
                        Set(game, "<Scoreboard>k__BackingField", oldBoard); Set(game, "<MatchStarted>k__BackingField", oldStarted);
                        Set(game, "<PauseOpen>k__BackingField", oldPause); Set(game, "<SupportMenuOpen>k__BackingField", oldSupport);
                        Set(game, "<ScoreboardOpen>k__BackingField", oldOpen);
                    }
                    AudioListener.volume = oldVolume; Time.timeScale = oldScale; Cursor.lockState = oldLock; Cursor.visible = oldCursor;
                }
            }
            if (failure != null) Debug.LogError("IRONFRONT scoreboard vehicle coast FAIL: " + failure);
            else Debug.Log("IRONFRONT scoreboard vehicle coast PASS: actual T90/R4/U8/IFV/motorcycle Update retains braking displacement, real occupied-hull collision stops, centered heading/no mounted aiming or new projectiles, public input rejection/non-driver authority, closed-overlay zero-speed stability; independent player/match/accounts and original state restored");
        }

        private static IPrototypeSeatedVehicle Make(PrototypeRuntime game, GameObject parent, string kind, List<IPrototypeVehicle> added)
        {
            var item = new GameObject("Actual coast fixture " + kind); item.transform.SetParent(parent.transform);
            Vector2 spawn = new Vector2(-325f, -320f);
            IPrototypeSeatedVehicle vehicle;
            if (kind == "tank") { var tank = item.AddComponent<PrototypeTankVehicle>(); tank.Initialize(game, game.Player.Team, spawn, 0f); vehicle = tank; }
            else if (kind == "scout") { var scout = item.AddComponent<PrototypeScoutVehicle>(); scout.Initialize(game, game.Player.Team, spawn, false); vehicle = scout; }
            else if (kind == "transport") { var rover = item.AddComponent<PrototypeTransportVehicle>(); rover.Initialize(game, game.Player.Team, spawn, false); vehicle = rover; }
            else
            {
                var combined = item.AddComponent<PrototypeCombinedArmsVehicle>();
                combined.Initialize(game, game.Player.Team, kind == "ifv" ? PrototypeVehicleKind.Ifv : PrototypeVehicleKind.Motorcycle, spawn, false);
                combined.RespawnEnabled = false; vehicle = combined;
            }
            ((Behaviour)vehicle).enabled = false; added.Add(vehicle); game.Vehicles.Add(vehicle); return vehicle;
        }
        private static void Remove(PrototypeRuntime game, IPrototypeVehicle vehicle, List<IPrototypeVehicle> added)
        { game.Vehicles.Remove(vehicle); added.Remove(vehicle); UnityEngine.Object.DestroyImmediate(vehicle.VehicleTransform.gameObject); }
        private static void CheckNoRounds(HashSet<PrototypeBallisticProjectile> oldRounds, HashSet<PrototypeVehicleProjectile> oldShells, string kind)
        {
            foreach (var round in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                Check(oldRounds.Contains(round), kind + " scoreboard-open Update creates no mounted round");
            foreach (var round in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                Check(oldShells.Contains(round), kind + " scoreboard-open Update creates no cannon/missile projectile");
        }
        private static void Wait()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - waitingSince > 10d)
            { EditorApplication.update -= Wait; waiting = false; Debug.LogError("IRONFRONT scoreboard vehicle coast FAIL: real Play dt did not advance"); return; }
            if (Time.deltaTime <= 0f) return;
            EditorApplication.update -= Wait; waiting = false; Verify();
        }
        private static object Invoke(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static void Check(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
    }
}
