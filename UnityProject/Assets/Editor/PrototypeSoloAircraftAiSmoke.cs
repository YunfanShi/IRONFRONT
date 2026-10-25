using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeSoloAircraftAiSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("IRONFRONT/Verify Solo Aircraft AI", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Verify Solo Aircraft AI")]
        public static void Verify()
        {
            PrototypeRuntime game = null;
            PrototypeCombinedArmsVehicle helicopter = null;
            PrototypeLoadout loadout = null;
            Vector3 playerPosition = Vector3.zero, aircraftPosition = Vector3.zero;
            Quaternion playerRotation = Quaternion.identity, aircraftRotation = Quaternion.identity;
            bool firstPerson = false, beganBattle = false;
            PrototypeGameMode originalMode = PrototypeGameMode.Conquest;
            int originalTicketLimit = 100;
            var aircraftFields = new Dictionary<string, object>();
            var originalFlares = new HashSet<PrototypeVehicleCountermeasure>(
                UnityEngine.Object.FindObjectsByType<PrototypeVehicleCountermeasure>());
            var originalProjectiles = new HashSet<PrototypeVehicleProjectile>(
                UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>());
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && !game.IsNetworkSession,
                    "solo runtime and player exist");
                Check(!game.MatchStarted, "run this menu first in a fresh Play session to verify the parked lobby");
                var ids = new List<IPrototypeVehicle>(game.Vehicles);
                int attackAircraft = 0;
                foreach (IPrototypeVehicle vehicle in ids)
                {
                    if (!(vehicle is PrototypeCombinedArmsVehicle aircraft)) continue;
                    Check(!(bool)Field(aircraft, "aiControlled") && !aircraft.EngineRunning && aircraft.Altitude < .2f,
                        "all aircraft and extra ground vehicles remain parked before deployment");
                    if (aircraft.Team == PrototypeTeam.Red && aircraft.Kind == PrototypeVehicleKind.Helicopter)
                    { helicopter = aircraft; attackAircraft++; }
                }
                Check(helicopter != null && attackAircraft == 1, "one existing red attack helicopter exists");
                originalMode = game.Match.Mode; originalTicketLimit = Mathf.RoundToInt(game.Match.TicketLimit);
                loadout = game.Player.Loadout.Copy(); firstPerson = game.Player.IsFirstPerson;
                playerPosition = game.Player.transform.position; playerRotation = game.Player.transform.rotation;
                aircraftPosition = helicopter.transform.position; aircraftRotation = helicopter.transform.rotation;
                foreach (string name in new[] { "verticalVelocity", "enginePower", "roll", "flightPitch", "nextShot",
                    "nextMG", "nextSecondary", "nextCountermeasure", "countermeasureUntil", "nextPlan", "goal", "blockedUntil" })
                    aircraftFields[name] = Field(helicopter, name);
                aircraftFields["Speed"] = helicopter.Speed;
                game.Match.ConfigureSolo(PrototypeGameMode.Breakthrough);
                Check(game.Match.ActiveObjective != null && game.Match.ActiveObjective.Owner == PrototypeTeam.Red,
                    "Breakthrough frontline starts friendly to the defending aircraft");
                game.BeginBattle(); beganBattle = true; game.Frontend.Close();
                int activeControllers = 0;
                foreach (IPrototypeVehicle vehicle in ids)
                    if (vehicle is PrototypeCombinedArmsVehicle aircraft && (bool)Field(aircraft, "aiControlled"))
                    { activeControllers++; Check(ReferenceEquals(aircraft, helicopter), "only the original red H8 is enabled"); }
                Check(activeControllers == 1 && helicopter.EngineRunning && helicopter.IsActiveThreat,
                    "solo BeginBattle activates the parked red H8 without an extra crew or vehicle");
                Vector2 objective = game.Match.ActiveObjective.Definition.Position;
                float initialDistance = Vector2.Distance(helicopter.MapPosition, objective);
                for (int i = 0; i < 180; i++) Invoke(helicopter, "UpdateAi", .05f);
                Check(helicopter.Alive && helicopter.Altitude > 35f &&
                    Vector2.Distance(helicopter.MapPosition, objective) < initialDistance - 120f &&
                    Vector2.Distance((Vector2)Field(helicopter, "goal"), objective) < .01f,
                    "real AI takes off, climbs and flies toward the active friendly-owned Breakthrough point");
                Check(game.Vehicles.Count == ids.Count, "flight adds no aircraft to the fleet");
                for (int i = 0; i < ids.Count; i++)
                    Check(ReferenceEquals(ids[i], game.Vehicles[i]), "all original IDs remain stable");

                PrototypePlayer player = game.Player;
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Engineer,
                    primary = PrototypeWeaponId.Carbine, secondary = PrototypeWeaponId.Pistol });
                if (!player.IsFirstPerson) player.TogglePerspective();
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.transform.position = helicopter.transform.position + Vector3.back * 95f - Vector3.up * 10f;
                controller.enabled = true;
                Aim(player, helicopter);
                Check(player.TryEquipAntiAirLauncher(), "engineer may equip SAM against the live AI aircraft");
                player.UpdateAntiAirLock(true, .1f);
                Check(ReferenceEquals(player.AntiAirLockTarget, helicopter) && helicopter.BeingLocked,
                    "a real engineer seeker warns the solo AI aircraft");
                Invoke(helicopter, "UpdateAi", .05f);
                Check(helicopter.CountermeasureActive && !helicopter.BeingLocked &&
                    player.AntiAirLockTarget == null &&
                    Mathf.Abs((float)Field(helicopter, "countermeasureUntil") - Time.time - 10f) < .02f &&
                    Mathf.Abs(helicopter.CountermeasureReadyIn - 24f) < .02f,
                    "AI responds with real flares, cancels acquisition and starts ten-second immunity / 24-second cooldown");
                Aim(player, helicopter);
                for (int i = 0; i < 50; i++) player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockTarget == null && !player.AntiAirLocked,
                    "continuous aim cannot reacquire during flare immunity");
                // Move the real deadlines by ten elapsed seconds rather than
                // blocking the Editor for ten seconds. The seeker and AI
                // response themselves still use their production methods.
                Set(helicopter, "countermeasureUntil", Time.time - .01f);
                Set(helicopter, "nextCountermeasure", Time.time + 14f);
                Aim(player, helicopter);
                for (int i = 0; i < 49; i++) player.UpdateAntiAirLock(true, .1f);
                Check(!player.AntiAirLocked && helicopter.BeingLocked,
                    "4.9 seconds of reacquisition does not grant a lock");
                player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLocked && ReferenceEquals(player.AntiAirLockTarget, helicopter),
                    "the flare cooldown leaves enough time for a full five-second SAM lock");
                Invoke(helicopter, "UpdateAi", .05f);
                Check(!helicopter.CountermeasureActive && player.AntiAirLocked &&
                    helicopter.CountermeasureReadyIn > 13f,
                    "AI cannot spam another flare during cooldown or erase the second completed lock");
                Set(helicopter, "nextCountermeasure", Time.time - .01f);
                Invoke(helicopter, "UpdateAi", .05f);
                Check(helicopter.CountermeasureActive && player.AntiAirLockTarget == null,
                    "AI may deploy again once the real countermeasure cooldown expires");
                Debug.Log("IRONFRONT solo aircraft AI PASS: parked lobby, one existing red H8 activated in solo, " +
                    "stable fleet IDs, real takeoff and active Breakthrough defense, automatic flares, " +
                    "ten-second lock immunity, 24-second cooldown and five-second reacquisition window");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT solo aircraft AI FAIL: " + error); }
            finally
            {
                if (game != null && loadout != null)
                {
                    game.Player.ResetAntiAirLock(); game.Player.EquipFirearm();
                    if (game.Player.IsFirstPerson != firstPerson) game.Player.TogglePerspective();
                    game.Player.ApplyLoadout(loadout);
                    var controller = game.Player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    game.Player.transform.SetPositionAndRotation(playerPosition, playerRotation);
                    controller.enabled = true;
                    game.Match.ConfigureSolo(originalMode, originalTicketLimit);
                }
                if (helicopter != null && aircraftFields.Count > 0)
                {
                    helicopter.ClearLockWarning();
                    helicopter.transform.SetPositionAndRotation(aircraftPosition, aircraftRotation);
                    foreach (KeyValuePair<string, object> field in aircraftFields)
                        if (field.Key == "Speed") typeof(PrototypeCombinedArmsVehicle).GetProperty("Speed")
                            .SetValue(helicopter, field.Value);
                        else Set(helicopter, field.Key, field.Value);
                    // The match is now running, so leave the real solo behavior
                    // enabled; its next normal frame starts from its own bay.
                    helicopter.SetAiControlled(beganBattle);
                }
                foreach (PrototypeVehicleCountermeasure flare in UnityEngine.Object.FindObjectsByType<PrototypeVehicleCountermeasure>())
                    if (!originalFlares.Contains(flare)) UnityEngine.Object.DestroyImmediate(flare.gameObject);
                foreach (PrototypeVehicleProjectile projectile in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                    if (!originalProjectiles.Contains(projectile)) UnityEngine.Object.DestroyImmediate(projectile.gameObject);
                Physics.SyncTransforms();
            }
        }
        private static void Aim(PrototypePlayer player, PrototypeCombinedArmsVehicle target)
        {
            player.ViewCamera.transform.position = player.transform.position + Vector3.up * 1.65f;
            player.ViewCamera.transform.rotation = Quaternion.LookRotation(
                target.transform.position + Vector3.up * 1.7f - player.ViewCamera.transform.position);
            Physics.SyncTransforms();
        }
        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] values) =>
            target.GetType().GetMethod(name, Private).Invoke(target, values);
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); }
    }
}
