using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeAmmoSupplySmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("IRONFRONT/Preview Fixed Ammo Point", true)]
        private static bool CanPreviewFixed() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Preview Fixed Ammo Point")]
        private static void PreviewFixed()
        {
            try
            {
                PrototypeRuntime game = PreparePreview();
                PrototypeAmmoSupplyPoint point = null;
                foreach (PrototypeAmmoSupplyPoint candidate in game.Supplies.Points)
                    if (candidate.Alive && !candidate.IsDeployable) { point = candidate; break; }
                Check(point != null, "a fixed supply cabinet exists");
                Move(game.Player, point.MapPosition + Vector2.down * 2.1f, 0f);
                PreviewCamera(game.Player);
                Debug.Log("IRONFRONT fixed ammo preview ready: actual E fills 8/20 primary and 3/10 secondary, " +
                    "then shows the shared 30-second cooldown. V changes view; Stop Play discards the fixture.");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT fixed ammo preview FAIL: " + error); }
        }
        [MenuItem("IRONFRONT/Preview Ammo Crate Placement", true)]
        private static bool CanPreviewPlacement() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Preview Ammo Crate Placement")]
        private static void PreviewPlacement()
        {
            try
            {
                PrototypeRuntime game = PreparePreview();
                Move(game.Player, new Vector2(-130f, -280f), 0f);
                PreviewCamera(game.Player);
                Check(game.Supplies.NearestInteraction(game.Player.transform.position) == null,
                    "preview site is clear of fixed supply interactions");
                Debug.Log("IRONFRONT crate placement preview ready: actual 6 places a neutral crate ahead, " +
                    "then actual E refills both weapon slots. V changes view; Stop Play discards the fixture.");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT crate placement preview FAIL: " + error); }
        }
        private static PrototypeRuntime PreparePreview()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            Check(game != null && game.Player != null && game.Supplies != null && !game.IsNetworkSession,
                "preview needs the solo runtime and supplies");
            if (game.Match.Winner.HasValue) game.Match.ConfigureSolo(game.Match.Mode, Mathf.RoundToInt(game.Match.TicketLimit));
            if (!game.MatchStarted)
            {
                if (game.SoloTeamSize != 8) game.ConfigureSoloTeams(8);
                game.BeginBattle();
            }
            game.Frontend.Close(); game.SetSupportMenu(false);
            if (game.PauseOpen) Invoke(game, "SetPause", false);
            game.Player.ForceExitVehicle();
            if (!game.Player.Alive) Invoke(game.Player, "Respawn");
            game.Player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Assault,
                primary = PrototypeWeaponId.Carbine, secondary = PrototypeWeaponId.Pistol });
            Set(game.Player, "crouched", false); Set(game.Player, "prone", false);
            Set(game.Player, "slideEndsAt", 0f);
            game.Supplies.Reset();
            game.Player.Kit.ApplyNetworkAmmo(8, 20);
            ((PrototypeInfantryKit)Field(game.Player, "secondaryKit")).ApplyNetworkAmmo(3, 10);
            return game;
        }
        private static void PreviewCamera(PrototypePlayer player)
        {
            if (!player.IsFirstPerson) player.TogglePerspective();
            Set(player, "pitch", 24f);
            player.ViewCamera.transform.localRotation = Quaternion.Euler(24f, 0f, 0f);
            player.RefreshHeldPresentation();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Physics.SyncTransforms();
        }
        [MenuItem("IRONFRONT/Verify Ammo Supplies", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Verify Ammo Supplies")]
        public static void Verify()
        {
            PrototypeRuntime game = null; PrototypePlayer player = null;
            PrototypeAmmoSupplySystem supplies = null;
            PrototypeLoadout loadout = null; PrototypeInfantryKit primary = null, secondary = null;
            Vector3 oldPosition = Vector3.zero; Quaternion oldRotation = Quaternion.identity;
            PrototypeTeam oldTeam = PrototypeTeam.Blue;
            GameObject obstruction = null;
            var rounds = new List<GameObject>();
            var oldFields = new Dictionary<string, object>();
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && !game.IsNetworkSession,
                    "local solo runtime exists");
                if (!game.MatchStarted) game.BeginBattle();
                game.Frontend.Close(); game.SetSupportMenu(false);
                if (game.PauseOpen) Invoke(game, "SetPause", false);
                player = game.Player; Check(player.Alive, "verify with a living player");
                player.ForceExitVehicle();
                oldPosition = player.transform.position; oldRotation = player.transform.rotation; oldTeam = player.Team;
                loadout = player.Loadout.Copy(); primary = player.Kit;
                secondary = (PrototypeInfantryKit)Field(player, "secondaryKit");
                foreach (string field in new[] { "throwableCount", "rocketCount", "antiAirMissileCount", "secondaryEquipped" })
                    oldFields[field] = Field(player, field);
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Assault,
                    primary = PrototypeWeaponId.Carbine, secondary = PrototypeWeaponId.Pistol });
                supplies = new PrototypeAmmoSupplySystem(game);
                Check(supplies.Points.Count == 7, "both bases and all five sectors have fixed stations");
                foreach (PrototypeAmmoSupplyPoint station in supplies.Points)
                {
                    Check(station.Alive && !station.IsDeployable && !PrototypeLayout.Collides(station.MapPosition, .9f) &&
                        station.transform.Find("Fixed cabinet protective roof") != null,
                        "fixed stations use safe, reachable fortified cabinet models");
                    station.TakeDamage(10000f);
                    Check(station.Alive && station.Health == PrototypeAmmoSupplyPoint.MaxHealth,
                        "fixed world stations are indestructible");
                }
                PrototypeAmmoSupplyPoint fixedPoint = supplies.Points[0];
                Move(player, fixedPoint.MapPosition + Vector2.down * 1.8f, 0f);
                Check(!supplies.TryResupply(player, out _) && supplies.ResupplyReadyIn(player) == 0f,
                    "full inventory does not consume global resupply cooldown");

                PrototypeInfantryKit firstKit = player.Kit;
                firstKit.ApplyNetworkAmmo(firstKit.Weapon.Magazine - 3, firstKit.Weapon.Reserve - 20);
                Set(player, "throwableCount", 1); Set(player, "rocketCount", 1); Set(player, "antiAirMissileCount", 1);
                Invoke(player, "UseSupport");
                Check(firstKit.Reserve == firstKit.Weapon.Reserve && firstKit.Ammo == firstKit.Weapon.Magazine - 3 &&
                    player.ThrowableCount == 1 && player.RocketCount == 1 && player.AntiAirMissileCount == 1 &&
                    firstKit.GadgetCharges == 1, "X ammo pack refills ordinary reserve only, without explosives");
                float gadgetDeadline = firstKit.GadgetReadyAt;
                Check(supplies.TryResupply(player, out _) && firstKit.Ammo == firstKit.Weapon.Magazine &&
                    firstKit.Reserve == firstKit.Weapon.Reserve && firstKit.GadgetCharges == 2 &&
                    Mathf.Approximately(firstKit.GadgetReadyAt, gadgetDeadline) && player.ThrowableCount == 2 &&
                    player.RocketCount == 2 && player.AntiAirMissileCount == 2 && supplies.ResupplyReadyIn(player) > 29f,
                    "E full supply replenishes ammo, explosives and gadgets without bypassing gadget cooldown");
                Move(player, supplies.Points[1].MapPosition + Vector2.down * 1.8f, 0f);
                firstKit.ApplyNetworkAmmo(1, 0);
                Check(!supplies.TryResupply(player, out _) && firstKit.Ammo == 1 && firstKit.Reserve == 0,
                    "switching fixed stations cannot bypass the player's global 30-second cooldown");
                Expire(supplies, "nextResupplyAt", player);
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Engineer,
                    primary = PrototypeWeaponId.BattleRifle, secondary = PrototypeWeaponId.Pistol });
                var secondKit = (PrototypeInfantryKit)Field(player, "secondaryKit");
                player.Kit.ApplyNetworkAmmo(1, 0); secondKit.ApplyNetworkAmmo(0, 0);
                Set(player, "throwableCount", 0); Set(player, "rocketCount", 0); Set(player, "antiAirMissileCount", 0);
                typeof(PrototypeInfantryKit).GetProperty("GadgetCharges").SetValue(player.Kit, 0);
                float health = player.Health, armor = player.Kit.Armor;
                Check(supplies.TryResupply(player, out _) && player.Kit.Ammo == player.Kit.Weapon.Magazine &&
                    secondKit.Ammo == secondKit.Weapon.Magazine && secondKit.Reserve == secondKit.Weapon.Reserve &&
                    player.ThrowableCount == 2 && player.RocketCount == 2 && player.AntiAirMissileCount == 2 &&
                    player.Kit.GadgetCharges == 2 && player.Health == health && player.Kit.Armor == armor,
                    "a full crate refill includes both engineer weapon slots and AT/SAM, but no healing or armor");
                Check(!supplies.TryDeployCrate(player, out _) && supplies.DeployReadyIn(player) == 0f,
                    "non-support class cannot place a crate or consume deployment cooldown");
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Assault,
                    primary = PrototypeWeaponId.Carbine, secondary = PrototypeWeaponId.Pistol });
                Move(player, new Vector2(-130f, -280f), 0f);
                obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstruction.transform.position = player.transform.position + Vector3.forward * 2.15f + Vector3.up * .45f;
                obstruction.transform.localScale = new Vector3(2f, 1f, 2f); Physics.SyncTransforms();
                Check(!supplies.TryDeployCrate(player, out _) && supplies.DeployReadyIn(player) == 0f,
                    "physical blocked placement does not spend the 90-second cooldown");
                UnityEngine.Object.DestroyImmediate(obstruction); obstruction = null;
                obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstruction.name = "Ammo crate smoke thin wall between player and clear placement";
                obstruction.transform.position = player.transform.position + Vector3.forward * 1.05f + Vector3.up * .45f;
                obstruction.transform.localScale = new Vector3(2f, 1.1f, .06f); Physics.SyncTransforms();
                Check(!supplies.TryDeployCrate(player, out _) && supplies.DeployReadyIn(player) == 0f,
                    "a real thin wall blocks release path even when the destination itself is clear");
                UnityEngine.Object.DestroyImmediate(obstruction); obstruction = null;
                Check(supplies.TryDeployCrate(player, out _) && supplies.OwnedCount(player) == 1 &&
                    supplies.DeployReadyIn(player) > 89f, "support deploys one real neutral crate with 90-second cooldown");
                PrototypeAmmoSupplyPoint first = LatestOwned(supplies, player);
                Check(first != null && first.Alive && first.Health == 120f &&
                    first.GetComponent<BoxCollider>().enabled && first.transform.Find("Neutral ammunition case") != null,
                    "placed crate has a visible modeled 120 HP collision hull");
                Check(!supplies.TryDeployCrate(player, out _) && supplies.OwnedCount(player) == 1,
                    "repeated placement during cooldown adds no box");

                PrototypeTeam sourceTeam = first.SourceTeam;
                player.SetTeam(sourceTeam == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue);
                Move(player, first.MapPosition + Vector2.down * 1.8f, 0f); Expire(supplies, "nextResupplyAt", player);
                player.Kit.ApplyNetworkAmmo(1, 0);
                Check(player.Team != first.SourceTeam && supplies.TryUseNearest(player, out _) &&
                    player.Kit.Ammo == player.Kit.Weapon.Magazine && player.Kit.Reserve == player.Kit.Weapon.Reserve,
                    "an enemy of the crate's source team can use neutral supplies");
                Move(player, fixedPoint.MapPosition + Vector2.down * 1.8f, 0f);
                player.Kit.ApplyNetworkAmmo(1, 0);
                Check(supplies.TryUseNearest(player, out _) && player.Kit.Ammo == 1,
                    "cooling E is handled and cannot chain a fixed point or trigger vehicle entry");

                Expire(supplies, "nextDeployAt", player); Move(player, new Vector2(-120f, -280f), 0f);
                Check(supplies.TryDeployCrate(player, out _), "second owned crate deploys after cooldown");
                PrototypeAmmoSupplyPoint second = LatestOwned(supplies, player);
                Expire(supplies, "nextDeployAt", player); Move(player, new Vector2(-110f, -280f), 0f);
                Check(supplies.TryDeployCrate(player, out _) && supplies.OwnedCount(player) == 2 && !first.Alive &&
                    !first.GetComponent<BoxCollider>().enabled, "a successful third crate retires the oldest and preserves the two-box cap");
                PrototypeAmmoSupplyPoint third = LatestOwned(supplies, player);
                int score = game.Scores.Get(player).Total;
                Physics.SyncTransforms();
                Vector3 origin = second.transform.position + Vector3.back * 5f + Vector3.up * .35f;
                for (int i = 0; i < 2; i++)
                {
                    var bullet = PrototypeBallisticProjectile.LaunchMountedRound(game, player.Team,
                        player.transform, origin, Vector3.forward, 700f, 40f, 80f); rounds.Add(bullet.gameObject);
                    object[] hitArgs = { origin, Vector3.forward, 10f, Vector3.zero, null };
                    Check((bool)typeof(PrototypeBallisticProjectile).GetMethod("TryHit", Private).Invoke(bullet, hitArgs),
                        "real ray sweep hits a deployed ammo crate");
                    Check(((Collider)hitArgs[4]).GetComponentInParent<PrototypeAmmoSupplyPoint>() == second,
                        "sweep resolves the intended neutral crate hull");
                    Invoke(bullet, "ApplyBulletDamage", hitArgs[4], 5f);
                    if (i == 0) Check(second.Health == 40f && second.Alive, "bullet applies exact neutral crate health damage");
                }
                Check(!second.Alive && game.Scores.Get(player).Total == score,
                    "actual bullet destruction gives no kill, assist or vehicle score");
                supplies.Tick(.01f);
                Check(supplies.OwnedCount(player) == 1 && !new List<PrototypeAmmoSupplyPoint>(supplies.Points).Contains(second),
                    "destroyed boxes are removed from the supply interaction list");
                var shell = PrototypeVehicleProjectile.Launch(game, player.Team, player.transform,
                    third.transform.position + Vector3.back * 5f + Vector3.up * .35f,
                    Vector3.forward, 200f, 30f, 145f, 0f, 3f, 1f); rounds.Add(shell.gameObject);
                for (int i = 0; i < 8 && shell.enabled; i++) Invoke(shell, "Step", .02f);
                Check(shell.HitCollider != null && !third.Alive && game.Scores.Get(player).Total == score,
                    "actual explosive impact destroys neutral box without combat credit");
                supplies.Tick(.01f);
                Check(supplies.OwnedCount(player) == 0 && supplies.Points.Count == 7,
                    "fixed stations remain after deployed crates are destroyed");
                supplies.Reset();
                Check(supplies.Points.Count == 7 && supplies.ResupplyReadyIn(player) == 0f && supplies.DeployReadyIn(player) == 0f,
                    "new-battle reset clears personal cooldowns and keeps seven fixed stations");
                Debug.Log("IRONFRONT ammo supplies PASS: seven safe fortified stations, lightweight ammo-pack separation, " +
                    "full primary/secondary/explosive/gadget refill, neutral enemy access, 30-second global cooldown, " +
                    "90-second deployment cooldown, two-box replacement, failed/full no cooldown, real bullet/explosive " +
                    "destruction without score, fixed station protection and lifecycle cleanup");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT ammo supplies FAIL: " + error); }
            finally
            {
                if (obstruction != null) UnityEngine.Object.DestroyImmediate(obstruction);
                foreach (GameObject round in rounds) if (round != null) UnityEngine.Object.DestroyImmediate(round);
                supplies?.Dispose();
                if (player != null && loadout != null)
                {
                    player.SetTeam(oldTeam); player.ApplyLoadout(loadout);
                    Set(player, "kit", primary); Set(player, "secondaryKit", secondary);
                    foreach (KeyValuePair<string, object> field in oldFields) Set(player, field.Key, field.Value);
                    Invoke(player, "BuildWeaponVisual");
                    Move(player, new Vector2(oldPosition.x, oldPosition.z), oldRotation.eulerAngles.y);
                    player.transform.position = oldPosition;
                }
            }
        }
        private static PrototypeAmmoSupplyPoint LatestOwned(PrototypeAmmoSupplySystem supplies, PrototypePlayer owner)
        {
            PrototypeAmmoSupplyPoint last = null;
            foreach (PrototypeAmmoSupplyPoint point in supplies.Points)
                if (point.Alive && point.IsDeployable && point.Owner == owner) last = point;
            return last;
        }
        private static void Expire(PrototypeAmmoSupplySystem supplies, string field, PrototypePlayer player)
        { ((Dictionary<PrototypePlayer, float>)Field(supplies, field))[player] = Time.time - .01f; }
        private static void Move(PrototypePlayer player, Vector2 position, float yaw)
        {
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            player.transform.position = new Vector3(position.x, PrototypeLayout.HeightAt(position.x, position.y) + .3f, position.y);
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f); controller.enabled = true; Physics.SyncTransforms();
        }
        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] values) => target.GetType().GetMethod(name, Private).Invoke(target, values);
        private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
    }
}
