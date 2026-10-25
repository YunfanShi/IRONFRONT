using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeEngineerAntiAirSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("IRONFRONT/Verify Engineer Anti-Air", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Verify Engineer Anti-Air")]
        public static void Verify()
        {
            PrototypeRuntime game = null;
            PrototypePlayer player = null;
            PrototypeLoadout loadout = null;
            Vector3 oldPosition = Vector3.zero;
            Quaternion oldRotation = Quaternion.identity;
            bool oldFirstPerson = false;
            var objects = new List<GameObject>();
            var originalMissiles = new HashSet<PrototypeVehicleProjectile>(
                UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>());
            var originalClouds = new HashSet<PrototypeSmokeCloud>(UnityEngine.Object.FindObjectsByType<PrototypeSmokeCloud>());
            var originalFlares = new HashSet<PrototypeVehicleCountermeasure>(UnityEngine.Object.FindObjectsByType<PrototypeVehicleCountermeasure>());
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && !game.IsNetworkReplica, "local runtime/player exist");
                if (!game.MatchStarted) game.BeginBattle();
                game.Frontend.Close();
                if (game.PauseOpen) Invoke(game, "SetPause", false);
                game.SetSupportMenu(false);
                player = game.Player;
                Check(player.Alive && !game.Match.Winner.HasValue, "living player in active battle");
                player.ForceExitVehicle();
                oldPosition = player.transform.position; oldRotation = player.transform.rotation;
                oldFirstPerson = player.IsFirstPerson; loadout = player.Loadout.Copy();
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Engineer,
                    primary = PrototypeWeaponId.Carbine, secondary = PrototypeWeaponId.Pistol });
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.transform.SetPositionAndRotation(new Vector3(900f, 240f, 900f), Quaternion.identity);
                controller.enabled = true;
                if (!player.IsFirstPerson) player.TogglePerspective();
                PrototypeTeam enemyTeam = player.Team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                var enemy = Vehicle(game, enemyTeam, PrototypeVehicleKind.Helicopter,
                    new Vector3(900f, 270f, 995f), objects);
                var friendly = Vehicle(game, player.Team, PrototypeVehicleKind.Helicopter,
                    new Vector3(1000f, 270f, 995f), objects);
                var ground = Vehicle(game, enemyTeam, PrototypeVehicleKind.AntiAir,
                    new Vector3(800f, 240f, 995f), objects);
                var landed = Vehicle(game, enemyTeam, PrototypeVehicleKind.ReconHelicopter,
                    new Vector3(1100f, 0f, 995f), objects);
                landed.transform.position = new Vector3(1100f,
                    PrototypeLayout.HeightAt(1100f, 995f) + .05f, 995f);
                Check(player.TryEquipAntiAirLauncher() && player.AntiAirMissileCount == 2,
                    "engineer equips a separate two-missile AA launcher");
                foreach (bool firstPerson in new[] { false, true })
                {
                    if (player.IsFirstPerson != firstPerson) player.TogglePerspective();
                    player.RefreshHeldPresentation();
                    Check(player.HeldEquipmentVisual != null && player.HeldEquipmentVisual.activeInHierarchy &&
                        player.HeldEquipmentVisual.transform.Find("Infrared seeker electronics") != null,
                        "distinct infrared launcher is held in both views");
                }
                Aim(player, enemy);
                for (int i = 0; i < 49; i++) player.UpdateAntiAirLock(true, .1f);
                Check(ReferenceEquals(player.AntiAirLockTarget, enemy) && player.AntiAirLockProgress > .97f &&
                    !player.AntiAirLocked && enemy.BeingLocked && !player.TryUseHeldEquipment() &&
                    player.AntiAirMissileCount == 2, "4.9 seconds warns aircraft but cannot fire or consume ammo");
                player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLocked && enemy.BeingLocked, "five continuous aimed seconds lock the aircraft");
                int before = player.AntiAirMissileCount;
                Check(player.TryUseHeldEquipment() && player.AntiAirMissileCount == before - 1,
                    "one left click launches one guided missile and consumes one AA round");
                Check(!player.TryUseHeldEquipment() && player.AntiAirMissileCount == before - 1,
                    "cooldown rejects duplicate launch without consuming ammunition");
                PrototypeVehicleProjectile missile = LatestPlayerMissile(player, originalMissiles);
                Check(missile != null && missile.IsGuided && enemy.IncomingMissile &&
                    missile.transform.Find("Guided infrared anti-air missile body") != null,
                    "actual modeled missile has a live seeker and produces incoming warning");
                float health = enemy.Health;
                Vector3 oldVelocity = (Vector3)Field(missile, "velocity");
                enemy.transform.position += Vector3.right * 5f;
                Invoke(missile, "Step", .02f);
                Check(Vector3.Angle(oldVelocity, (Vector3)Field(missile, "velocity")) > .1f,
                    "the physical missile turns toward a moving aircraft");
                for (int i = 0; i < 150 && missile.enabled; i++) Invoke(missile, "Step", .02f);
                Check(enemy.Health < health && enemy.Alive && missile.HitCollider != null,
                    "swept guided missile hits the real aircraft hull and applies damage");
                UnityEngine.Object.DestroyImmediate(missile.gameObject);
                SetField(player, "nextEquipmentUseAt", -1f);
                Lock(player, enemy);
                player.UpdateAntiAirLock(false, .1f);
                Check(player.AntiAirLockTarget == null && player.AntiAirLockProgress == 0f && !enemy.BeingLocked,
                    "releasing aim immediately clears progress and the aircraft lock warning");
                Lock(player, enemy);
                player.ViewCamera.transform.rotation = Quaternion.LookRotation(Vector3.right);
                player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockProgress == 0f, "aiming away clears continuous acquisition");
                Lock(player, enemy);
                var changedTarget = Vehicle(game, enemyTeam, PrototypeVehicleKind.Jet,
                    new Vector3(930f, 275f, 980f), objects);
                Aim(player, changedTarget); player.UpdateAntiAirLock(true, .1f);
                Check(ReferenceEquals(player.AntiAirLockTarget, changedTarget) && !player.AntiAirLocked &&
                    player.AntiAirLockProgress < .03f && !enemy.BeingLocked,
                    "switching airborne targets begins a fresh acquisition and clears old warning");
                changedTarget.transform.position += Vector3.right * 600f;
                Lock(player, enemy);
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "SAM optical cover"; objects.Add(wall);
                wall.transform.position = Vector3.Lerp(player.ViewCamera.transform.position,
                    enemy.transform.position + Vector3.up * 1.7f, .5f);
                wall.transform.localScale = new Vector3(30f, 30f, .1f);
                player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockProgress == 0f, "a real thin wall blocks acquisition");
                wall.GetComponent<Collider>().enabled = false;
                Lock(player, enemy);
                Vector3 smokeMiddle = Vector3.Lerp(player.ViewCamera.transform.position,
                    enemy.transform.position + Vector3.up * 1.7f, .5f);
                PrototypeSmokeCloud screen = PrototypeSmokeCloud.Spawn(game, smokeMiddle - Vector3.up * 2f, true);
                screen.PreviewAtAge(6f);
                player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockProgress == 0f, "opaque smoke blocks the optical seeker");
                UnityEngine.Object.DestroyImmediate(screen.gameObject);
                Lock(player, enemy);
                Invoke(game, "SetPause", true); player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockProgress == 0f && !enemy.BeingLocked, "pause resets the seeker");
                Invoke(game, "SetPause", false);
                Lock(player, enemy); player.EquipFirearm();
                Check(player.AntiAirLockProgress == 0f && !enemy.BeingLocked, "equipment switch resets the seeker");
                Check(player.TryEquipAntiAirLauncher(), "remaining missile can be re-equipped");
                Aim(player, friendly); player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockTarget == null, "friendly aircraft cannot be locked");
                Aim(player, ground); player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockTarget == null, "enemy ground vehicles cannot be locked");
                Aim(player, landed); player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockTarget == null, "landed aircraft cannot be locked");
                Lock(player, enemy);
                Check(game.SetSupportMenu(true) && player.AntiAirLockTarget == null &&
                    !player.TryEquipAntiAirLauncher(), "requisition overlay clears and gates equipment/lock");
                game.SetSupportMenu(false); player.TryEquipAntiAirLauncher();
                Lock(player, enemy);
                Check(player.TryUseHeldEquipment() && player.AntiAirMissileCount == 0 &&
                    player.HeldEquipment == PrototypeHeldEquipment.Firearm, "last missile consumes once and stows empty launcher");
                missile = LatestPlayerMissile(player, originalMissiles);
                Check(missile != null && missile.IsGuided, "second missile starts guided");
                player.ResupplyExplosives(); player.TryEquipAntiAirLauncher(); Lock(player, enemy);
                var friendlyAA = Vehicle(game, player.Team, PrototypeVehicleKind.AntiAir,
                    new Vector3(900f, 240f, 855f), objects);
                friendlyAA.SetAiControlled(true);
                for (int i = 0; i < 50; i++)
                {
                    friendlyAA.AimAt(enemy.transform.position + Vector3.up * 1.7f, .05f);
                    friendlyAA.UpdateLock(.1f);
                }
                Check(ReferenceEquals(friendlyAA.LockedTarget, enemy) && friendlyAA.LockProgress >= 1f,
                    "existing vehicle seeker also acquires the aircraft");
                player.UpdateAntiAirLock(false, .1f);
                Check(enemy.BeingLocked && player.AntiAirLockTarget == null,
                    "clearing infantry lock preserves another vehicle's active lock warning");
                Invoke(friendlyAA, "ClearSeekerLock");
                Check(!enemy.BeingLocked, "clearing the final seeker removes the aircraft lock warning");
                enemy.WarnLock(player);
                var warnings = (Dictionary<object, float>)Field(enemy, "lockWarnings");
                warnings[player] = Time.time - .01f;
                Check(!enemy.BeingLocked && warnings.Count == 0, "expired source warnings are removed");
                Lock(player, enemy);
                for (int i = 0; i < 50; i++) friendlyAA.UpdateLock(.1f);
                enemy.SetAiControlled(true);
                Check(enemy.DeployCountermeasure() && !missile.IsGuided && player.AntiAirLockTarget == null &&
                    player.AntiAirLockProgress == 0f && friendlyAA.LockedTarget == null && !enemy.BeingLocked &&
                    !enemy.IncomingMissile && enemy.LockImmunityRemaining > 9.99f &&
                    enemy.CountermeasureReadyIn > 23.99f,
                    "aircraft flares immediately cut live seeker/warnings and protect for ten seconds with 24-second cooldown");
                player.ResupplyExplosives(); player.TryEquipAntiAirLauncher(); Aim(player, enemy);
                for (int i = 0; i < 60; i++) player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockProgress == 0f && !player.TryUseHeldEquipment() &&
                    player.AntiAirMissileCount == 2, "immune aircraft refuses lock/fire and spends no ammo");
                SetField(enemy, "countermeasureUntil", Time.time + .01f);
                player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockProgress == 0f, "immunity persists through the final hundredth of a second");
                SetField(enemy, "countermeasureUntil", Time.time - .01f);
                player.UpdateAntiAirLock(true, .1f);
                Check(player.AntiAirLockProgress < .03f && !player.AntiAirLocked,
                    "immunity expiry begins a fresh five-second lock rather than restoring old progress");
                Invoke(missile, "Step", .02f);
                Check(!missile.IsGuided, "a spoofed in-flight missile never reacquires after immunity expires");
                Lock(player, enemy);
                Check(!enemy.DeployCountermeasure(), "flare reuse respects the existing cooldown");
                // Cooldown prevents the second dispense, so test the actual
                // ground smoke lifetime on a separate untouched AA fixture.
                ground.SetAiControlled(true);
                Check(ground.DeployCountermeasure() && ground.LockImmunityRemaining > 4.99f &&
                    ground.LockImmunityRemaining < 5.01f, "ground smoke retains its existing five-second seeker protection");
                player.EquipFirearm(); player.SelectClass(PrototypeInfantryClass.Assault);
                Check(!player.TryEquipAntiAirLauncher(), "non-engineers cannot equip AA missiles");
                Debug.Log("IRONFRONT engineer anti-air PASS: distinct two-view SAM, real five-second continuous optical lock, " +
                    "airborne/enemy gate, aim/cover/pause/equipment/support reset, ammo/cooldown, modeled guided moving-target hull damage, " +
                    "target warning, immediate permanent flare seeker break, ten-second aircraft immunity, fresh reacquisition and five-second ground smoke");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT engineer anti-air FAIL: " + error); }
            finally
            {
                if (game != null)
                {
                    if (game.PauseOpen) Invoke(game, "SetPause", false);
                    game.SetSupportMenu(false);
                    foreach (GameObject item in objects)
                        if (item != null && item.GetComponent<PrototypeCombinedArmsVehicle>() is IPrototypeVehicle vehicle)
                            game.Vehicles.Remove(vehicle);
                }
                foreach (PrototypeVehicleProjectile missile in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                    if (!originalMissiles.Contains(missile)) UnityEngine.Object.DestroyImmediate(missile.gameObject);
                foreach (GameObject item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
                foreach (PrototypeSmokeCloud cloud in UnityEngine.Object.FindObjectsByType<PrototypeSmokeCloud>())
                    if (!originalClouds.Contains(cloud)) UnityEngine.Object.DestroyImmediate(cloud.gameObject);
                foreach (PrototypeVehicleCountermeasure flare in UnityEngine.Object.FindObjectsByType<PrototypeVehicleCountermeasure>())
                    if (!originalFlares.Contains(flare)) UnityEngine.Object.DestroyImmediate(flare.gameObject);
                if (player != null && loadout != null)
                {
                    player.ForceExitVehicle(); player.ApplyLoadout(loadout);
                    CharacterController controller = player.GetComponent<CharacterController>();
                    controller.enabled = false; player.transform.SetPositionAndRotation(oldPosition, oldRotation); controller.enabled = true;
                    if (player.IsFirstPerson != oldFirstPerson) player.TogglePerspective();
                    SetField(player, "pitch", 0f); SetField(player, "aiming", false);
                    SetField(player, "nextEquipmentUseAt", 0f);
                    Invoke(player, "PositionInfantryCamera", 1f);
                }
            }
        }
        private static void Lock(PrototypePlayer player, IPrototypeVehicle target)
        {
            player.ResetAntiAirLock(); Aim(player, target);
            for (int i = 0; i < 50; i++) player.UpdateAntiAirLock(true, .1f);
            Check(player.AntiAirLocked && ReferenceEquals(player.AntiAirLockTarget, target), "continuous five-second acquisition completes");
        }
        private static void Aim(PrototypePlayer player, IPrototypeVehicle target)
        {
            SetField(player, "aiming", true);
            player.ViewCamera.transform.position = player.transform.position + Vector3.up * 1.65f;
            player.ViewCamera.transform.rotation = Quaternion.LookRotation(target.VehicleTransform.position +
                Vector3.up * 1.7f - player.ViewCamera.transform.position);
            Physics.SyncTransforms();
        }
        private static PrototypeVehicleProjectile LatestPlayerMissile(PrototypePlayer player,
            HashSet<PrototypeVehicleProjectile> original)
        {
            foreach (PrototypeVehicleProjectile missile in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                if (!original.Contains(missile) && Field(missile, "owner") as Transform == player.transform)
                    return missile;
            return null;
        }
        private static PrototypeCombinedArmsVehicle Vehicle(PrototypeRuntime game, PrototypeTeam team,
            PrototypeVehicleKind kind, Vector3 position, List<GameObject> objects)
        {
            var root = new GameObject("SAM smoke " + kind); objects.Add(root);
            var vehicle = root.AddComponent<PrototypeCombinedArmsVehicle>();
            vehicle.Initialize(game, team, kind, new Vector2(position.x, position.z), false);
            vehicle.RespawnEnabled = false; vehicle.transform.position = position; game.Vehicles.Add(vehicle);
            return vehicle;
        }
        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
