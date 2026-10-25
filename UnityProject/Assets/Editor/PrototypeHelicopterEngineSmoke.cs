using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeHelicopterEngineSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("IRONFRONT/Verify Helicopters and Engines", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Verify Helicopters and Engines")]
        public static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            var objects = new List<GameObject>();
            var existingRounds = new HashSet<PrototypeBallisticProjectile>(
                UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>());
            PrototypeBot enemyBot = null;
            Vector3 savedBot = Vector3.zero, savedPlayer = Vector3.zero;
            Quaternion savedRotation = Quaternion.identity;
            float originalScale = Time.timeScale;
            try
            {
                Check(game != null && game.Player != null, "runtime/player exist");
                if (!game.MatchStarted) game.BeginBattle();
                game.Frontend?.Close();
                PrototypePlayer player = game.Player;
                Check(player.Alive, "player is alive");
                player.ForceExitVehicle(); savedPlayer = player.transform.position;
                savedRotation = player.transform.rotation;
                Check((int)PrototypeVehicleKind.Helicopter == 2 && (int)PrototypeVehicleKind.Motorcycle == 4 &&
                    (int)PrototypeVehicleKind.ReconHelicopter == 5, "legacy vehicle integer IDs are unchanged");
                var profiles = new HashSet<PrototypeEngineSound>();
                int liveVehicles = 0, totalSeats = 0;
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                {
                    var audio = vehicle.VehicleTransform.GetComponent<PrototypeVehicleEngineAudio>();
                    Check(audio != null, vehicle.VehicleName + " has engine audio");
                    Check(audio.IdleSource != null && audio.LoadSource != null &&
                        audio.IdleSource.clip != null && audio.LoadSource.clip != null,
                        vehicle.VehicleName + " has actual idle and load assets");
                    Check(audio.IdleSource.clip.channels == 1 && audio.LoadSource.clip.channels == 1 &&
                        audio.IdleSource.loop && audio.LoadSource.loop && audio.IdleSource.spatialBlend == 1f,
                        "engine beds are mono looped positional audio");
                    profiles.Add(audio.Profile); liveVehicles++;
                    totalSeats += ((IPrototypeSeatedVehicle)vehicle).SeatCount;
                }
                Check(liveVehicles == 18 && totalSeats == 56 && profiles.Count == 5,
                    "18 vehicles, 56 seats and five distinctive engine profiles");
                Vector2 bay = new Vector2(-325f, -320f);
                PrototypeCombinedArmsVehicle recon = NewVehicle(game, player.Team,
                    PrototypeVehicleKind.ReconHelicopter, bay, objects);
                PrototypeCombinedArmsVehicle attack = NewVehicle(game, player.Team,
                    PrototypeVehicleKind.Helicopter, bay + Vector2.right * 25f, objects);
                Check(recon.SeatCount == 3 && attack.SeatCount == 4 && !recon.Kind.Equals(attack.Kind),
                    "recon has three total seats; attack has four");
                Check(recon.transform.Find("Light reconnaissance airframe") != null &&
                    attack.transform.Find("Armored attack airframe") != null,
                    "the helicopter airframes are visibly different models");
                Check(!PrototypeVehicleDefinition.Get(recon.Kind).Guided &&
                    !PrototypeVehicleDefinition.Get(attack.Kind).Guided,
                    "helicopters use the requested MG/cannon loadouts");
                Enter(player, recon);
                Check(!recon.SelectWeapon(1) && recon.WeaponSlot == 0, "recon refuses a cannon/missile slot");
                int cannonCount = UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>().Length;
                int mgCount = UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().Length;
                Check(recon.TryFireWeapon() && UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().Length == mgCount+1 &&
                    UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>().Length == cannonCount,
                    "recon primary launches a machine-gun round, not HE or a missile");
                Check(!recon.TryFireWeapon(), "recon MG obeys its firing interval");
                Check(player.TrySwitchVehicleSeat(2) && !recon.TryFireWeapon() &&
                    !recon.ReconRadar.TryScan(), "recon passenger cannot shoot or trigger radar");
                Check(player.TrySwitchVehicleSeat(0), "recon pilot returns");
                recon.transform.position += Vector3.up * 50f;
                PrototypeTeam enemyTeam = player.Team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                var enemyVehicle = NewVehicle(game, enemyTeam, PrototypeVehicleKind.Motorcycle,
                    bay + Vector2.right * 70f, objects);
                game.Vehicles.Add(enemyVehicle);
                foreach (PrototypeBot bot in game.Bots)
                    if (bot.Alive && bot.Team == enemyTeam && !bot.IsPassenger)
                    { enemyBot = bot; savedBot = bot.transform.position; break; }
                Check(enemyBot != null, "enemy infantry exists for the actual radar scan");
                enemyBot.transform.position = new Vector3(bay.x + 8f,
                    PrototypeLayout.HeightAt(bay.x + 8f, bay.y + 8f), bay.y + 8f);
                Physics.SyncTransforms();
                Check(recon.ReconRadar.TryScan() && !recon.ReconRadar.TryScan(),
                    "pilot radar scans with a twelve-second cooldown");
                bool foundVehicle = false, foundBot = false;
                foreach (PrototypeReconRadar.Contact contact in recon.ReconRadar.Contacts)
                {
                    if (contact.Target == enemyVehicle.transform) foundVehicle = true;
                    if (contact.Target == enemyBot.transform) foundBot = true;
                    var scanned = contact.Target.GetComponent<PrototypeCombinedArmsVehicle>();
                    if (scanned != null) Check(scanned.Team != player.Team, "radar excludes friendly vehicles");
                    var scannedBot = contact.Target.GetComponent<PrototypeBot>();
                    if (scannedBot != null) Check(scannedBot.Team != player.Team, "radar excludes friendly infantry");
                }
                Check(foundVehicle && foundBot, "actual scan finds a nearby enemy vehicle and visible infantry below");
                Check(!PrototypeReconRadar.VehicleInRange(Vector3.zero, Vector3.forward * 181f) &&
                    PrototypeReconRadar.VehicleInRange(Vector3.zero, Vector3.forward * 180f) &&
                    !PrototypeReconRadar.InfantryBelow(Vector3.up * 50f, Vector3.forward * 120f) &&
                    !PrototypeReconRadar.InfantryBelow(Vector3.up * 50f, Vector3.up * 55f),
                    "scan has finite range and a downward optical cone");
                typeof(PrototypeReconRadar).GetField("expires", Private).SetValue(recon.ReconRadar, Time.time - 1f);
                typeof(PrototypeReconRadar).GetMethod("Update", Private).Invoke(recon.ReconRadar, null);
                Check(recon.ReconRadar.Contacts.Count == 0, "six-second marks expire instead of tracking indefinitely");
                player.ForceExitVehicle(); Enter(player, attack);
                Check(attack.SelectWeapon(1) && attack.IsDownwardMachineGun, "attack pilot selects belly MG");
                CheckDownwardAim(attack);
                Check(player.TrySwitchVehicleSeat(1) && attack.IsDownwardMachineGun, "attack crew seat also uses belly MG");
                CheckDownwardAim(attack);
                Check(player.TrySwitchVehicleSeat(0) && attack.SelectWeapon(0) &&
                    !attack.IsDownwardMachineGun, "attack pilot returns to forward cannon");
                var engine = attack.GetComponent<PrototypeVehicleEngineAudio>();
                for (int step = 0; step < 30; step++) engine.Step(true, 0f, .1f);
                float idlePitch = engine.IdleSource.pitch;
                for (int step = 0; step < 30; step++) engine.Step(true, 1f, .1f);
                Check(engine.IdleSource.isPlaying && engine.LoadSource.isPlaying &&
                    engine.EngineLoad > .95f && engine.LoadSource.volume > 0f && engine.IdleSource.pitch > idlePitch,
                    "actual loop sources play and respond to engine load");
                Check(engine.IdleSource.gameObject != attack.gameObject,
                    "engine sources are isolated from the hull weapon one-shot source");
                Time.timeScale = 0f;
                typeof(PrototypeVehicleEngineAudio).GetMethod("LateUpdate", Private).Invoke(engine, null);
                Check(!engine.IdleSource.isPlaying && !engine.LoadSource.isPlaying, "paused engines become silent");
                Time.timeScale = 1f; engine.Step(true, 1f, .1f);
                Check(engine.IdleSource.isPlaying, "engine resumes after pause");
                for (int step = 0; step < 30; step++) engine.Step(false, 0f, .1f);
                Check(!engine.IdleSource.isPlaying && !engine.LoadSource.isPlaying, "engine shutdown stops both loops");
                AudioSource first = engine.IdleSource, second = engine.LoadSource;
                engine.Initialize(game, attack, PrototypeEngineSound.Rotor, 38f);
                Check(engine.IdleSource == first && engine.LoadSource == second &&
                    first.gameObject.GetComponents<AudioSource>().Length == 2, "reinitialization reuses exactly two engine voices");
                engine.Step(true, 1f, .1f); engine.enabled = false;
                Check(!first.isPlaying && !second.isPlaying, "disabling audio releases loop playback");
                Debug.Log("IRONFRONT helicopters/engines PASS: stable vehicle IDs, 18 vehicles/56 seats, original mono idle/load beds, " +
                    "recon three-seat MG, attack four-seat cannon/downward MG camera/bore limits, enemy-only finite radar/cooldown/expiry, " +
                    "throttle audio, independent weapon source, pause/resume/shutdown and two-source lifecycle");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT helicopters/engines FAIL: " + error); }
            finally
            {
                Time.timeScale = originalScale > 0f ? originalScale : 1f;
                if (game != null && game.Player != null)
                {
                    game.Player.ForceExitVehicle();
                    CharacterController cc = game.Player.GetComponent<CharacterController>();
                    cc.enabled = false; game.Player.transform.SetPositionAndRotation(savedPlayer,savedRotation); cc.enabled = true;
                }
                if (enemyBot != null) enemyBot.transform.position = savedBot;
                if (game != null) foreach (GameObject item in objects)
                    if (item != null && item.GetComponent<PrototypeCombinedArmsVehicle>() is IPrototypeVehicle vehicle)
                        game.Vehicles.Remove(vehicle);
                foreach (GameObject item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
                foreach (PrototypeBallisticProjectile round in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    if (!existingRounds.Contains(round)) UnityEngine.Object.DestroyImmediate(round.gameObject);
            }
        }
        private static void CheckDownwardAim(PrototypeCombinedArmsVehicle vehicle)
        {
            float yaw = 180f, pitch = -45f;
            vehicle.ClampWeaponView(ref yaw,ref pitch);
            Check(Mathf.Approximately(yaw,75f) && Mathf.Approximately(pitch,15f), "view clamps to downward MG cone");
            foreach (Vector3 target in new[] { vehicle.transform.position + Vector3.up*100f,
                vehicle.transform.position + Vector3.down*100f, vehicle.transform.position - vehicle.transform.forward*100f })
            {
                vehicle.AimAt(target,.05f);
                Vector3 local = vehicle.transform.InverseTransformDirection(vehicle.WeaponMuzzle.forward);
                float elevation = Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
                float relativeYaw = Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
                Check(elevation <= -14.99f && elevation >= -85.01f && Mathf.Abs(relativeYaw)<=75.01f,
                    "visible MG bore obeys the same downward elevation and yaw limits");
            }
            MethodInfo look = typeof(PrototypePlayer).GetMethod("ApplyVehicleLook",Private);
            MethodInfo camera = typeof(PrototypePlayer).GetMethod("UpdateVehicleCamera",Private);
            look.Invoke(vehicle.Occupant,new object[] {180f,-100f}); camera.Invoke(vehicle.Occupant,null);
            Vector3 cameraLocal = vehicle.transform.InverseTransformDirection(vehicle.Occupant.ViewCamera.transform.forward);
            Check(cameraLocal.y < -.25f && Mathf.Abs(Mathf.Atan2(cameraLocal.x,cameraLocal.z)*Mathf.Rad2Deg)<=75.01f,
                "actual player camera shares the belly MG down-looking cone");
        }
        private static PrototypeCombinedArmsVehicle NewVehicle(PrototypeRuntime game, PrototypeTeam team,
            PrototypeVehicleKind kind, Vector2 at, List<GameObject> objects)
        {
            var root = new GameObject("Helicopter/engine smoke " + kind); objects.Add(root);
            var vehicle = root.AddComponent<PrototypeCombinedArmsVehicle>();
            vehicle.Initialize(game,team,kind,at,false); return vehicle;
        }
        private static void Enter(PrototypePlayer player, IPrototypeVehicle vehicle)
        {
            Check((bool)typeof(PrototypePlayer).GetMethod("EnterVehicle",Private).Invoke(player,new object[] {vehicle}),
                "player can enter " + vehicle.VehicleName);
        }
        private static void Check(bool condition,string text)
        { if (!condition) throw new InvalidOperationException(text); }
    }
}
