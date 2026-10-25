using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Projectile travel and weapon inputs have separate smoke tests. This check
    // exercises the real impact/blast receivers and their score wrappers together.
    public static class PrototypeCombatScoreAttributionSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 InfantryPosition = new Vector3(750f, 350f, 750f);
        private static readonly Vector3 VehiclePosition = new Vector3(810f, 350f, 750f);

        [MenuItem("IRONFRONT/Verify Combat Score Attribution", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Combat Score Attribution")]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            var fixtures = new List<GameObject>();
            var oldPulses = new HashSet<PrototypePulseVisual>(
                UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>());
            var oldFeed = new List<PrototypeBotDownNotice>();
            PrototypeScoreSystem originalScores = null, originalMatchScores = null;
            float blueTickets = 0f, redTickets = 0f;
            bool restore = false;
            try
            {
                Check(game != null && game.Player != null, "runtime and player exist");
                if (!game.MatchStarted) game.BeginBattle();
                Check(!game.IsNetworkReplica && game.Player.Alive && !game.Match.Winner.HasValue,
                    "verify in a live solo or host battle");
                Check(game.Player.CurrentVehicle == null, "verify while the player is on foot");
                originalScores = game.Scores; originalMatchScores = game.Match.ScoreSystem;
                blueTickets = game.Match.BlueTickets; redTickets = game.Match.RedTickets;
                oldFeed.AddRange(game.BotDownFeed.Entries);
                restore = true;
                var scores = new PrototypeScoreSystem(game);
                typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, scores);
                game.Match.ScoreSystem = scores;
                game.Match.SetNetworkTickets(100f, 100f);
                PrototypePlayer player = game.Player;
                PrototypeTeam team = player.Team;
                PrototypeTeam enemyTeam = team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                PrototypeBot enemy = Bot(game, enemyTeam, InfantryPosition, fixtures);
                PrototypeBot helper = Bot(game, team, InfantryPosition - Vector3.right * 20f, fixtures);
                PrototypeBot friend = Bot(game, team, InfantryPosition + Vector3.right * 50f, fixtures);
                PrototypeRemotePlayer remote = Remote(game, enemyTeam,
                    InfantryPosition + Vector3.right * 30f, fixtures);
                var hull = Vehicle(game, enemyTeam, PrototypeVehicleKind.Motorcycle, VehiclePosition, fixtures);
                var samTarget = Vehicle(game, enemyTeam, PrototypeVehicleKind.ReconHelicopter,
                    VehiclePosition + Vector3.right * 60f, fixtures);
                var weaponVehicle = Vehicle(game, team, PrototypeVehicleKind.Ifv,
                    VehiclePosition + Vector3.right * 100f, fixtures);
                GameObject tankRoot = new GameObject("Score Impact Verification T90");
                fixtures.Add(tankRoot); tankRoot.transform.SetParent(game.transform);
                var tank = tankRoot.AddComponent<PrototypeTankVehicle>();
                tank.Initialize(game, team, new Vector2(960f, 750f), 0f);
                tank.enabled = false; tank.transform.position = new Vector3(960f, 350f, 750f);

                // A local rifle round contributes real health loss, and a different
                // bot's actual round gets the kill. No direct score API grants points.
                Rifle(game, player.transform, team, enemy.GetComponent<Collider>(), fixtures, 1f);
                Check(Mathf.Approximately(enemy.Health, 69f), "actual rifle impact removes 31 health");
                Rifle(game, helper.transform, team, enemy.GetComponent<Collider>(), fixtures, 3f);
                Check(!enemy.Alive && scores.Get(helper).Kills == 1 && scores.Get(helper).Total == 100 &&
                    scores.Get(player).Assists == 1 && scores.Get(player).Total == 53,
                    "real bullet wrappers credit the bot killer and local player's contribution assist");
                Respawn(enemy, InfantryPosition);
                Rifle(game, player.transform, team, enemy.GetComponent<Collider>(), fixtures, 4f);
                Check(!enemy.Alive && scores.Get(player).Kills == 1 && scores.Get(player).Total == 153,
                    "local infantry rifle kill credits its captured shooter");
                Rifle(game, player.transform, team, friend.GetComponent<Collider>(), fixtures, 4f);
                Check(friend.Alive && Mathf.Approximately(friend.Health, 100f) && scores.Get(player).Kills == 1,
                    "the real rifle receiver rejects friendly fire and reward");
                Rifle(game, player.transform, team, remote.GetComponent<Collider>(), fixtures, 4f);
                Check(!remote.Alive && scores.Get(player).Kills == 2,
                    "the real host remote-player rifle receiver awards its defeat");

                // Reserve the actual gunner slot without moving the player's body
                // or camera. A real ledger transfer and removal change live owner.
                Respawn(enemy, InfantryPosition);
                Check(tank.TrySetPlayerSeat(player, 1) && tank.Occupant == player,
                    "T90 gunner is the actual weapon operator");
                PrototypeBallisticProjectile mounted = Track(PrototypeBallisticProjectile.LaunchMountedRound(
                    game, team, tank.transform, tank.WeaponMuzzle.position, Vector3.forward,
                    500f, 200f, 100f), fixtures);
                Check(ReferenceEquals(Field(mounted, "scoreActor"), player), "mounted gun caches operator at launch");
                Check(tank.TrySetPlayerSeat(player, 0), "operator can transfer seats after launch");
                tank.RemoveDriver(player);
                Check(tank.Occupant == null &&
                    ReferenceEquals(PrototypeScoreSystem.ActorForShooter(tank.transform), tank),
                    "live vehicle shooter ownership changes after the operator exits");
                int kills = scores.Get(player).Kills;
                Invoke(mounted, "ApplyBulletDamage", enemy.GetComponent<Collider>(), 0f);
                Check(!enemy.Alive && scores.Get(player).Kills == kills + 1 && scores.Get(tank).Total == 0,
                    "mounted round remains attributed to the launch operator after transfer and exit");

                Respawn(enemy, InfantryPosition);
                Check(tank.TrySetPlayerSeat(player, 0), "T90 driver operates the shell launcher");
                PrototypeBallisticProjectile shell = Track(PrototypeBallisticProjectile.LaunchShell(
                    game, team, tank.transform, tank.WeaponMuzzle.position, Vector3.forward, 170f, 230f), fixtures);
                tank.RemoveDriver(player);
                kills = scores.Get(player).Kills;
                Physics.SyncTransforms(); Invoke(shell, "Explode", enemy.transform.position);
                Check(!enemy.Alive && scores.Get(player).Kills == kills + 1 && scores.Get(tank).Total == 0,
                    "real T90 shell blast receiver keeps launch-time infantry kill attribution");
                Check(tank.TrySetPlayerSeat(player, 0), "T90 driver can launch a separate shell");
                shell = Track(PrototypeBallisticProjectile.LaunchShell(game, team, tank.transform,
                    tank.WeaponMuzzle.position, Vector3.forward, 170f, 230f), fixtures);
                tank.RemoveDriver(player);
                int destroyed = scores.Get(player).VehiclesDestroyed;
                Physics.SyncTransforms(); Invoke(shell, "Explode", hull.GetComponent<BoxCollider>().bounds.center);
                Check(!hull.Alive && scores.Get(player).VehiclesDestroyed == destroyed + 1,
                    "real shell blast through a vehicle hull credits a destruction");

                ResetVehicle(hull, VehiclePosition);
                PrototypeBallisticProjectile rocket = Track(PrototypeBallisticProjectile.LaunchRocket(
                    game, team, player.transform, InfantryPosition + Vector3.up * 10f, Vector3.forward), fixtures);
                destroyed = scores.Get(player).VehiclesDestroyed;
                Physics.SyncTransforms(); Invoke(rocket, "Explode", hull.GetComponent<BoxCollider>().bounds.center);
                Check(!hull.Alive && scores.Get(player).VehiclesDestroyed == destroyed + 1,
                    "actual anti-armor rocket blast wrapper credits the launcher user");

                // The added vehicle projectile class has a separate damage path.
                // Test it using real IFV values and a captured departing gunner.
                Respawn(enemy, InfantryPosition);
                Rifle(game, player.transform, team, enemy.GetComponent<Collider>(), fixtures, 1f);
                Check(weaponVehicle.TrySetPlayerSeat(player, 1), "IFV gunner is seated at launch");
                PrototypeVehicleDefinition definition = PrototypeVehicleDefinition.Get(PrototypeVehicleKind.Ifv);
                PrototypeVehicleProjectile round = Track(PrototypeVehicleProjectile.Launch(game, team,
                    weaponVehicle.transform, weaponVehicle.WeaponMuzzle.position, Vector3.forward,
                    definition.ProjectileSpeed, definition.Range, definition.Damage, definition.Gravity,
                    definition.BlastRadius, definition.Armor), fixtures);
                Check(ReferenceEquals(Field(round, "scoreActor"), player), "added vehicle round caches its gunner");
                Check(weaponVehicle.TrySetPlayerSeat(player, 0), "added vehicle gunner can transfer after launch");
                weaponVehicle.RemoveDriver(player);
                kills = scores.Get(player).Kills;
                Physics.SyncTransforms(); Invoke(round, "Explode", enemy.transform.position, enemy.GetComponent<Collider>());
                Check(!enemy.Alive && scores.Get(player).Kills == kills + 1 && scores.Get(weaponVehicle).Total == 0,
                    "actual combined-arms impact stays credited to the gunner after leaving");

                round = Track(PrototypeVehicleProjectile.Launch(game, team, player.transform,
                    samTarget.transform.position - Vector3.forward * 20f, Vector3.forward,
                    170f, 500f, 220f, 0f, 3.5f, 1f, samTarget), fixtures);
                Check(round.IsGuided && ReferenceEquals(Field(round, "scoreActor"), player),
                    "engineer SAM ammunition keeps the launching player's identity");
                destroyed = scores.Get(player).VehiclesDestroyed;
                Physics.SyncTransforms();
                Invoke(round, "Explode", samTarget.GetComponent<BoxCollider>().bounds.center,
                    samTarget.GetComponent<BoxCollider>());
                Check(!samTarget.Alive && scores.Get(player).VehiclesDestroyed == destroyed + 1,
                    "actual guided missile hull receiver credits 200 to its launcher user");

                // Put two wounded real enemy receivers in one real grenade blast.
                // An extra collider verifies that overlap iteration cannot pay twice.
                Respawn(enemy, InfantryPosition);
                Respawn(remote, InfantryPosition + Vector3.right);
                friend.transform.position = InfantryPosition + Vector3.right * 2f;
                int total = scores.Get(player).Total;
                Rifle(game, player.transform, team, enemy.GetComponent<Collider>(), fixtures, 1f);
                Rifle(game, player.transform, team, remote.GetComponent<Collider>(), fixtures, 1f);
                BoxCollider extraHitbox = enemy.gameObject.AddComponent<BoxCollider>();
                extraHitbox.size = new Vector3(.7f, 1.8f, .7f);
                kills = scores.Get(player).Kills;
                Physics.SyncTransforms();
                Invoke(player, "ApplyFragDamageForTeam", InfantryPosition, team);
                Check(!enemy.Alive && !remote.Alive && friend.Alive &&
                    Mathf.Approximately(friend.Health, 100f) && scores.Get(player).Kills == kills + 2 &&
                    scores.Get(player).Total == total + 200,
                    "actual frag wrappers credit bot and remote kills once despite multiple colliders and reject friends");
                Invoke(player, "ApplyFragDamageForTeam", InfantryPosition, team);
                Check(scores.Get(player).Kills == kills + 2 && scores.Get(player).Total == total + 200,
                    "repeated blast callbacks cannot pay a defeated receiver again");
                Debug.Log("IRONFRONT combat score attribution PASS: actual infantry rifle, bot assist, remote-player " +
                    "damage, mounted round, T90 shell, anti-armor rocket, combined-arms round, guided engineer SAM " +
                    "and frag receivers; real health/death credit, launch-time operator retention across seat transfer/exit, " +
                    "vehicle destruction, friendly exclusion and multi-collider duplicate prevention");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT combat score attribution FAIL: " + error); }
            finally
            {
                if (restore && game != null)
                {
                    typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, originalScores);
                    game.Match.ScoreSystem = originalMatchScores;
                    game.Match.SetNetworkTickets(blueTickets, redTickets);
                    var feed = (List<PrototypeBotDownNotice>)typeof(PrototypeBotDownFeed)
                        .GetField("entries", Private).GetValue(game.BotDownFeed);
                    feed.Clear(); feed.AddRange(oldFeed);
                }
                foreach (GameObject fixture in fixtures)
                    if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                foreach (PrototypePulseVisual pulse in UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>())
                    if (!oldPulses.Contains(pulse)) UnityEngine.Object.DestroyImmediate(pulse.gameObject);
            }
        }

        private static PrototypeBot Bot(PrototypeRuntime game, PrototypeTeam team, Vector3 position,
            List<GameObject> fixtures)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Score Impact Verification Infantry"; fixtures.Add(root);
            root.transform.SetParent(game.transform);
            var bot = root.AddComponent<PrototypeBot>();
            bot.Initialize(game, game.Navigation, team, new Vector2(position.x, position.z), PrototypeInfantryClass.Assault);
            bot.enabled = false; root.transform.position = position; return bot;
        }

        private static PrototypeRemotePlayer Remote(PrototypeRuntime game, PrototypeTeam team,
            Vector3 position, List<GameObject> fixtures)
        {
            GameObject root = new GameObject("Score Impact Verification Remote");
            fixtures.Add(root); root.transform.SetParent(game.transform);
            var remote = root.AddComponent<PrototypeRemotePlayer>();
            remote.Initialize(game, 9999, team); remote.enabled = false;
            root.transform.position = position; return remote;
        }

        private static PrototypeCombinedArmsVehicle Vehicle(PrototypeRuntime game, PrototypeTeam team,
            PrototypeVehicleKind kind, Vector3 position, List<GameObject> fixtures)
        {
            GameObject root = new GameObject("Score Impact Verification " + kind);
            fixtures.Add(root); root.transform.SetParent(game.transform);
            var vehicle = root.AddComponent<PrototypeCombinedArmsVehicle>();
            vehicle.Initialize(game, team, kind, new Vector2(position.x, position.z), false);
            vehicle.enabled = false; vehicle.RespawnEnabled = false;
            root.transform.position = position; return vehicle;
        }

        private static void Rifle(PrototypeRuntime game, Transform source, PrototypeTeam team,
            Collider target, List<GameObject> fixtures, float scale)
        {
            PrototypeBallisticProjectile round = Track(PrototypeBallisticProjectile.LaunchBullet(game, team,
                source, InfantryPosition + Vector3.up * 10f, Vector3.forward,
                PrototypeInfantryRoles.GetWeapon(PrototypeWeaponId.Carbine), scale), fixtures);
            Invoke(round, "ApplyBulletDamage", target, 0f);
        }

        private static PrototypeBallisticProjectile Track(PrototypeBallisticProjectile projectile,
            List<GameObject> fixtures)
        { Check(projectile != null, "ballistic round launches"); projectile.enabled = false; fixtures.Add(projectile.gameObject); return projectile; }

        private static PrototypeVehicleProjectile Track(PrototypeVehicleProjectile projectile,
            List<GameObject> fixtures)
        { Check(projectile != null, "combined-arms round launches"); projectile.enabled = false; fixtures.Add(projectile.gameObject); return projectile; }

        private static void Respawn(PrototypeBot bot, Vector3 position)
        { Invoke(bot, "Respawn"); bot.transform.position = position; }

        private static void Respawn(PrototypeRemotePlayer remote, Vector3 position)
        {
            typeof(PrototypeRemotePlayer).GetMethod("Respawn", Private, null, Type.EmptyTypes, null).Invoke(remote, null);
            remote.transform.position = position;
        }

        private static void ResetVehicle(PrototypeCombinedArmsVehicle vehicle, Vector3 position)
        { Invoke(vehicle, "ResetVehicle"); vehicle.transform.position = position; }

        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static object Invoke(object target, string name, params object[] arguments) =>
            target.GetType().GetMethod(name, Private).Invoke(target, arguments);
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); }
    }
}
