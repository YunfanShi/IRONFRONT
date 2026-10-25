using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeHeldEquipmentSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("IRONFRONT/Verify Held Equipment", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Held Equipment")]
        public static void Verify()
        {
            PrototypeRuntime game = null;
            PrototypePlayer player = null;
            PrototypeLoadout previousLoadout = null;
            GameObject obstacle = null;
            GameObject rocketWall = null;
            PrototypeBallisticProjectile wallRocket = null;
            PrototypeBallisticProjectile rocket = null;
            PrototypeBot enemy = null;
            PrototypeBot friendly = null;
            Vector3 enemyPosition = Vector3.zero, friendlyPosition = Vector3.zero;
            float enemyHealth = 0f, friendlyHealth = 0f;
            bool botsSaved = false;
            bool previousFirstPerson = false;
            bool previousCrouch = false, previousProne = false;
            Vector3 playerPosition = Vector3.zero;
            Quaternion playerRotation = Quaternion.identity;
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && !game.IsNetworkReplica,
                    "local authority has a player");
                if (!game.MatchStarted)
                {
                    game.Match.ConfigureSolo(PrototypeGameMode.Conquest);
                    game.BeginBattle();
                }
                game.Frontend.Close();
                if (game.PauseOpen) Invoke(game, "SetPause", false);
                player = game.Player;
                if (player.CurrentVehicle != null) player.ForceExitVehicle();
                Check(player.Alive && !game.Match.Winner.HasValue, "living player is on foot");
                previousLoadout = player.Loadout.Copy();
                previousFirstPerson = player.IsFirstPerson;
                previousCrouch = (bool)Field(player, "crouched");
                previousProne = (bool)Field(player, "prone");
                playerPosition = player.transform.position;
                playerRotation = player.transform.rotation;
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Engineer,
                    primary = PrototypeWeaponId.Carbine, secondary = PrototypeWeaponId.Pistol,
                    throwable = PrototypeThrowableId.Frag });
                if (player.IsFirstPerson) player.TogglePerspective();
                Check(player.TryEquipLauncher(), "engineer takes out the launcher");
                Check(player.HeldEquipment == PrototypeHeldEquipment.Launcher &&
                    player.HeldEquipmentVisual.activeInHierarchy &&
                    player.HeldEquipmentVisual.transform.Find("Launcher tube") != null &&
                    player.HeldEquipmentVisual.transform.Find("Muzzle collar")?.childCount == 12 &&
                    player.HeldEquipmentVisual.transform.Find("Optical sight") != null,
                    "a persistent shoulder tube has a hollow collar and optical sight");
                Check(!((GameObject)Field(player, "rifle")).activeInHierarchy &&
                    !((GameObject)Field(player, "firstPersonWeapon")).activeInHierarchy,
                    "launcher replaces both firearm visuals");
                SetField(player, "aiming", true);
                Check(player.IsAiming, "launcher accepts right-button aiming state");
                PrototypeSoldierVisual soldier = player.GetComponent<PrototypeSoldierVisual>();
                foreach (bool firstPerson in new[] { false, true })
                {
                    if (player.IsFirstPerson != firstPerson) player.TogglePerspective();
                    foreach (int stance in new[] { 0, 1, 2 })
                    {
                        SetField(player, "crouched", stance == 1);
                        SetField(player, "prone", stance == 2);
                        SetField(player, "pitch", 45f);
                        soldier.RefreshPose(true);
                        Invoke(player, "PositionInfantryCamera", 1f);
                        player.RefreshHeldPresentation();
                        float floor = PrototypeLayout.HeightAt(player.HeldEquipmentVisual.transform.position.x,
                            player.HeldEquipmentVisual.transform.position.z);
                        foreach (Renderer renderer in player.HeldEquipmentVisual.GetComponentsInChildren<Renderer>())
                            Check(renderer.bounds.min.y >= floor + .04f,
                                "held launcher stays above terrain in both views and all three stances");
                        Check(!((GameObject)Field(player, "rifle")).activeInHierarchy &&
                            !((GameObject)Field(player, "firstPersonWeapon")).activeInHierarchy,
                            "view switch does not reveal an extra firearm");
                    }
                }
                SetField(player, "prone", false);
                SetField(player, "pitch", 0f);
                soldier.RefreshPose(true);
                Invoke(player, "PositionInfantryCamera", 1f);
                player.RefreshHeldPresentation();
                int rocketsBefore = player.RocketCount;
                int ammoBefore = player.Ammo;
                int projectilesBefore = UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().Length;
                Check(player.TryUseHeldEquipment(), "left-button equipment use fires one rocket");
                Check(player.RocketCount == rocketsBefore - 1 && player.Ammo == ammoBefore &&
                    UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().Length == projectilesBefore + 1,
                    "rocket use consumes one rocket and creates one physical projectile without rifle ammo");
                Check(!player.TryUseHeldEquipment() && player.RocketCount == rocketsBefore - 1,
                    "duplicate use cannot fire again during the equipment cooldown");
                foreach (PrototypeBallisticProjectile projectile in
                    UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    if ((bool)Field(projectile, "rocket") && Field(projectile, "owner") as Transform == player.transform)
                    { rocket = projectile; break; }
                Check(rocket != null, "the player's rocket keeps the existing blast implementation");
                foreach (PrototypeBot bot in game.Bots)
                    if (bot.Alive && !bot.IsPassenger && bot.Health > 80f)
                    {
                        if (bot.Team != player.Team && enemy == null) enemy = bot;
                        if (bot.Team == player.Team && friendly == null) friendly = bot;
                    }
                Check(enemy != null && friendly != null, "damage checks have opposing and friendly soldiers");
                enemyPosition = enemy.transform.position;
                friendlyPosition = friendly.transform.position;
                enemyHealth = enemy.Health;
                friendlyHealth = friendly.Health;
                botsSaved = true;
                Vector3 center = player.transform.position + player.transform.forward * 30f;
                center.y = PrototypeLayout.HeightAt(center.x, center.z) + .7f;
                enemy.transform.position = center + Vector3.right * 3f;
                friendly.transform.position = center + Vector3.left * 3f;
                Physics.SyncTransforms();
                Invoke(rocket, "Explode", center);
                rocket.enabled = false;
                Check(enemy.Health < enemyHealth && enemy.Alive && Mathf.Approximately(friendly.Health, friendlyHealth),
                    "launcher retains rocket blast damage and friendly-fire protection");
                enemy.HealTo(enemyHealth);
                Invoke(player, "ApplyFragDamage", center);
                Check(enemy.Health < enemyHealth && enemy.Alive && Mathf.Approximately(friendly.Health, friendlyHealth),
                    "held frag retains its existing falloff and friendly-fire protection");
                rocketWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rocketWall.name = "Held rocket thin cover";
                Vector3 eye = player.transform.position + Vector3.up * 1.65f;
                rocketWall.transform.position = eye + player.transform.forward * .45f;
                rocketWall.transform.rotation = player.transform.rotation;
                rocketWall.transform.localScale = new Vector3(3f, 4f, .08f);
                Physics.SyncTransforms();
                Vector3 visualMuzzle = player.HeldEquipmentVisual.transform.TransformPoint(new Vector3(0f, 0f, .76f));
                Check(rocketWall.transform.InverseTransformPoint(visualMuzzle).z > .04f,
                    "thin-cover test puts the long visual muzzle beyond the wall");
                var existingRounds = new HashSet<PrototypeBallisticProjectile>();
                foreach (PrototypeBallisticProjectile projectile in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    existingRounds.Add(projectile);
                SetField(player, "nextEquipmentUseAt", -1f);
                Check(player.TryUseHeldEquipment(), "held launcher actually fires while its visual muzzle penetrates thin cover");
                foreach (PrototypeBallisticProjectile projectile in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    if (!existingRounds.Contains(projectile) &&
                        (bool)Field(projectile, "rocket") && Field(projectile, "owner") as Transform == player.transform)
                    { wallRocket = projectile; break; }
                Check(wallRocket != null && rocketWall.transform.InverseTransformPoint(wallRocket.transform.position).z < -.04f,
                    "actual rocket starts on the player's side of the wall rather than at the clipped visual muzzle");
                object[] sweep = { wallRocket.transform.position, ((Vector3)Field(wallRocket, "velocity")).normalized,
                    .5f, Vector3.zero, null };
                Check((bool)Invoke(wallRocket, "TryHit", sweep) &&
                    (sweep[4] as Collider) == rocketWall.GetComponent<Collider>(),
                    "the actual projectile sweep hits the cover instead of passing through it");
                wallRocket.enabled = false;
                rocketWall.GetComponent<Collider>().enabled = false;
                VerifySmokeLanding(game);
                player.EquipFirearm();
                Check(player.HeldEquipment == PrototypeHeldEquipment.Firearm &&
                    player.HeldEquipmentVisual == null && !player.ThrowableTrajectoryVisible,
                    "firearm switch removes equipment and trajectory");
                Check(player.TryEquipThrowable(), "3 takes out the selected frag");
                player.RefreshHeldPresentation();
                Check(player.ThrowableTrajectoryVisible && player.TrajectoryPointCount > 1 &&
                    player.HeldEquipmentVisual.transform.Find("Canister") != null,
                    "a held grenade has a canister and dotted trajectory");
                SetField(player, "aiming", true);
                Check(!player.IsAiming, "grenade holding does not enable rifle or launcher zoom");
                var points = new List<Vector3>();
                Vector3 origin = new Vector3(900f, 240f, 900f);
                Vector3 velocity = Vector3.forward * PrototypeThrowableTrajectory.ThrowSpeed +
                    Vector3.up * PrototypeThrowableTrajectory.UpwardBoost;
                const float dt = .02f;
                float fuse = PrototypeThrowableTrajectory.FuseSeconds;
                Check(!PrototypeThrowableTrajectory.Predict(origin, velocity, Physics.gravity, dt,
                    fuse, player.transform, points), "an unobstructed trajectory has no false contact");
                Vector3 expected = origin + velocity * fuse + Physics.gravity * (fuse * (fuse + dt) * .5f);
                Check(Vector3.Distance(points[points.Count - 1], expected) < .01f,
                    "trajectory shares Rigidbody fixed-step speed, boost and gravity through the fuse");
                obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "Held equipment smoke wall";
                obstacle.transform.position = origin + Vector3.forward * 3f;
                obstacle.transform.localScale = new Vector3(3f, 6f, .3f);
                Check(PrototypeThrowableTrajectory.Predict(origin, velocity, Physics.gravity, dt,
                    fuse, player.transform, points) && points[points.Count - 1].z < origin.z + 3f,
                    "sphere prediction stops before the first wall at grenade radius");
                Invoke(game, "SetPause", true);
                player.RefreshHeldPresentation();
                Check(!player.ThrowableTrajectoryVisible && !player.HeldEquipmentVisual.activeInHierarchy,
                    "pause hides hand equipment and trajectory");
                Invoke(game, "SetPause", false);
                player.RefreshHeldPresentation();
                Check(player.ThrowableTrajectoryVisible, "resume rebuilds the current preview");
                game.Frontend.Open();
                player.RefreshHeldPresentation();
                Check(!player.ThrowableTrajectoryVisible && !player.HeldEquipmentVisual.activeInHierarchy,
                    "front-end screen hides hand equipment and trajectory");
                game.Frontend.Close();
                player.RefreshHeldPresentation();
                int grenadesBefore = player.ThrowableCount;
                SetField(player, "nextEquipmentUseAt", -1f);
                Check(player.TryUseHeldEquipment() && player.ThrowableCount == grenadesBefore - 1,
                    "left-button use throws exactly one selected grenade");
                Check(player.HeldEquipment == PrototypeHeldEquipment.Firearm && !player.ThrowableTrajectoryVisible,
                    "throwing returns to the firearm and clears the preview immediately");
                Invoke(player, "ClearThrownProjectiles");
                Check(player.TryEquipThrowable(), "the second grenade can be held");
                Invoke(player, "EnterDownedPose");
                Check(player.HeldEquipment == PrototypeHeldEquipment.Firearm && !player.ThrowableTrajectoryVisible,
                    "downed entry clears equipment and trajectory");
                Invoke(player, "LeaveDownedPose");
                Check(player.TryEquipThrowable(), "revived presentation can hold equipment again");
                IPrototypeVehicle friendlyVehicle = null;
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                    if (vehicle.Alive && vehicle.Team == player.Team && vehicle.Occupant == null &&
                        !(vehicle is IPrototypeAircraftVehicle aircraft && aircraft.IsAircraft))
                    { friendlyVehicle = vehicle; break; }
                Check(friendlyVehicle != null && (bool)Invoke(player, "EnterVehicleSeat", friendlyVehicle, 0),
                    "a friendly vehicle accepts the player during equipment holding");
                Check(player.HeldEquipment == PrototypeHeldEquipment.Firearm && !player.ThrowableTrajectoryVisible,
                    "vehicle entry clears equipment and trajectory");
                player.ForceExitVehicle();
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Assault });
                Check(!player.TryEquipLauncher(), "other infantry classes cannot equip engineer rockets");
                Debug.Log("IRONFRONT held equipment PASS: persistent launcher/frag models, exclusive " +
                    "two-view holding, standing/crouch/prone terrain clearance, launcher aiming, single-use " +
                    "ammo and cooldown, rocket/frag damage, thin-wall safe rocket release, wall-triggered short smoke fuse and continued falling physics, fixed-step collision-cut dotted trajectory, " +
                    "throw/switch/pause/menu/downed/vehicle cleanup and engineer restriction");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT held equipment FAIL: " + error); }
            finally
            {
                if (obstacle != null) UnityEngine.Object.Destroy(obstacle);
                if (rocketWall != null) UnityEngine.Object.Destroy(rocketWall);
                if (wallRocket != null) UnityEngine.Object.Destroy(wallRocket.gameObject);
                if (rocket != null) UnityEngine.Object.Destroy(rocket.gameObject);
                if (botsSaved && enemy != null) { enemy.transform.position = enemyPosition; enemy.HealTo(enemyHealth); }
                if (botsSaved && friendly != null) { friendly.transform.position = friendlyPosition; friendly.HealTo(friendlyHealth); }
                if (game != null && game.PauseOpen) Invoke(game, "SetPause", false);
                if (player != null && previousLoadout != null)
                {
                    if (player.CurrentVehicle != null) player.ForceExitVehicle();
                    Invoke(player, "ClearThrownProjectiles");
                    player.ApplyLoadout(previousLoadout);
                    SetField(player, "crouched", previousCrouch);
                    SetField(player, "prone", previousProne);
                    SetField(player, "pitch", 0f);
                    var controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    player.transform.position = playerPosition;
                    player.transform.rotation = playerRotation;
                    controller.enabled = true;
                    player.GetComponent<PrototypeSoldierVisual>().SetDowned(false);
                    if (player.IsFirstPerson != previousFirstPerson) player.TogglePerspective();
                    player.GetComponent<PrototypeSoldierVisual>().RefreshPose(true);
                    Invoke(player, "PositionInfantryCamera", 1f);
                    game.Frontend.Close();
                }
            }
        }

        private static void VerifySmokeLanding(PrototypeRuntime game)
        {
            Scene scene = SceneManager.CreateScene("Held smoke landing probe", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var props = new List<GameObject>();
            try
            {
                PhysicsScene physics = scene.GetPhysicsScene();
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Smoke landing floor";
                ground.transform.position = new Vector3(900f, 0f, 900f);
                ground.transform.localScale = new Vector3(20f, 1f, 20f);
                SceneManager.MoveGameObjectToScene(ground, scene);
                props.Add(ground);
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Smoke high throw wall";
                wall.transform.position = new Vector3(900f, 6f, 903f);
                wall.transform.localScale = new Vector3(10f, 12f, .3f);
                SceneManager.MoveGameObjectToScene(wall, scene);
                props.Add(wall);
                GameObject can = new GameObject("Airborne smoke probe");
                SceneManager.MoveGameObjectToScene(can, scene);
                can.transform.position = new Vector3(900f, 3f, 900f);
                can.AddComponent<SphereCollider>().radius = PrototypeThrowableTrajectory.CollisionRadius;
                Rigidbody body = can.AddComponent<Rigidbody>();
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearDamping = 0f;
                body.linearVelocity = new Vector3(0f, 10f, 17f);
                var fuse = can.AddComponent<PrototypeSmokeGrenadeFuse>();
                fuse.Initialize(game, game.Player.transform);
                props.Add(can);
                bool hitWall = false, checkedAirborneApex = false, landed = false;
                for (int i = 0; i < 600; i++)
                {
                    physics.Simulate(.02f);
                    if (body.position.z > 902.5f && body.position.y > 2f && body.linearVelocity.z < 1f)
                        hitWall = true;
                    if (hitWall && !checkedAirborneApex && body.position.y > 2f && Mathf.Abs(body.linearVelocity.y) < .8f)
                    {
                        // Reproduce the old edge case explicitly: contact with
                        // a high wall can leave zero speed for an airborne step.
                        body.linearVelocity = Vector3.zero;
                        body.WakeUp();
                        Check(!PrototypePlayer.CanReleaseSmoke(can),
                            "wall contact arms the fuse without releasing before the short collision delay");
                        Check(fuse.HasContact && fuse.ContactCollider == wall.GetComponent<Collider>(),
                            "the high wall is an actual accepted smoke fuse contact");
                        checkedAirborneApex = true;
                    }
                    if (body.position.y < .66f)
                    {
                        Check(body.position.y < .66f,
                            "wall contact keeps the canister's falling Rigidbody until it reaches the floor");
                        landed = true;
                        break;
                    }
                }
                Check(hitWall && checkedAirborneApex && landed,
                    "a high throw arms on a real wall hit and keeps falling after its airborne speed reaches zero");
            }
            finally
            {
                foreach (GameObject prop in props) UnityEngine.Object.Destroy(prop);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
        }

        [MenuItem("IRONFRONT/Equipment Preview/Launcher", true)]
        [MenuItem("IRONFRONT/Equipment Preview/Grenade", true)]
        [MenuItem("IRONFRONT/Equipment Preview/Firearm", true)]
        private static bool CanPreview() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Equipment Preview/Launcher")]
        private static void PreviewLauncher()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null || !game.Player.Alive || game.IsNetworkReplica) return;
            if (!game.MatchStarted) { game.Match.ConfigureSolo(PrototypeGameMode.Conquest); game.BeginBattle(); }
            game.Frontend.Close();
            if (game.PauseOpen) Invoke(game, "SetPause", false);
            if (game.Player.CurrentVehicle != null) game.Player.ForceExitVehicle();
            game.Player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Engineer,
                primary = PrototypeWeaponId.Carbine, secondary = PrototypeWeaponId.Pistol });
            game.Player.TryEquipLauncher();
        }

        [MenuItem("IRONFRONT/Equipment Preview/Grenade")]
        private static void PreviewGrenade()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null || !game.Player.Alive || game.IsNetworkReplica) return;
            if (!game.MatchStarted) { game.Match.ConfigureSolo(PrototypeGameMode.Conquest); game.BeginBattle(); }
            game.Frontend.Close();
            if (game.PauseOpen) Invoke(game, "SetPause", false);
            if (game.Player.CurrentVehicle != null) game.Player.ForceExitVehicle();
            if (game.Player.ThrowableCount == 0) game.Player.ApplyLoadout(game.Player.Loadout);
            game.Player.TryEquipThrowable();
        }

        [MenuItem("IRONFRONT/Equipment Preview/Firearm")]
        private static void PreviewFirearm() => UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>()?.Player.EquipFirearm();

        private static object Field(object value, string name) => value.GetType().GetField(name, Private).GetValue(value);
        private static void SetField(object value, string name, object setting) => value.GetType().GetField(name, Private).SetValue(value, setting);
        private static object Invoke(object value, string method, params object[] args) => value.GetType().GetMethod(method, Private).Invoke(value, args);
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
