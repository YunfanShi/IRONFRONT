using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeBotVehicleCombatSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 Origin = new Vector3(780f, 300f, 780f);
        private static readonly Vector3 TargetPosition = Origin + Vector3.forward * 20f;

        [MenuItem("IRONFRONT/Verify Bot Anti-Vehicle", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Bot Anti-Vehicle")]
        public static void Verify()
        {
            SceneState state = null;
            try
            {
                PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && Time.deltaTime > 0f,
                    "a running SampleScene has a player and advancing Unity frame time");
                state = new SceneState(game);
                PrototypeTeam side = state.OriginalPlayer.Team;
                PrototypeTeam enemy = side == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                PrototypeBot engineer = Bot(state, side, PrototypeInfantryClass.Engineer, Origin);
                PrototypeCombinedArmsVehicle hull = Vehicle(state, enemy,
                    PrototypeVehicleKind.Ifv, TargetPosition, true);
                game.Bots.Add(engineer);
                game.Vehicles.Add(hull);
                Check(engineer.RocketCount == 2 && hull.IsActiveThreat && engineer.CanAttackVehicle(hull),
                    "fresh engineer has two effective rockets and sees the real hostile IFV; " + Describe(engineer, hull));

                // Spend the existing support equipment through its production
                // API. Rockets must remain available even after both plates go.
                Check(engineer.Kit.TryApplyArmor(Time.time, engineer.Kit, 0f), "first real armor support spends a gadget");
                Set(engineer.Kit, "nextGadgetAt", Time.time - .01f);
                Check(engineer.Kit.TryApplyArmor(Time.time, new PrototypeInfantryKit(PrototypeInfantryClass.Assault), 0f),
                    "second real armor support spends the final gadget");
                Check(engineer.Kit.GadgetCharges == 0 && engineer.RocketCount == 2,
                    "support gadget depletion leaves independent anti-armor ammunition intact");
                int rifleAmmo = engineer.Kit.Ammo;
                int rifleReserve = engineer.Kit.Reserve;
                float health = hull.Health;
                int armorRating = hull.ArmorRating;
                var before = OwnedRounds(engineer);
                Tick(engineer);
                Check(engineer.IsUsingLauncher && engineer.RocketCount == 2 && OwnedRounds(engineer).Count == before.Count &&
                    engineer.Kit.Ammo == rifleAmmo && engineer.RocketReadyAt > Time.time,
                    "real Update raises the launcher before firing, without spending rifle rounds; " + Describe(engineer, hull));
                GameObject launcher = (GameObject)Field(engineer, "heldLauncher");
                Check(launcher != null && launcher.activeInHierarchy && launcher.GetComponentsInChildren<Renderer>().Length > 2,
                    "engineer holds a visible actual launcher model");
                PrototypeBallisticProjectile first = FirePrepared(engineer, before);
                Check(engineer.RocketCount == 1 && engineer.Kit.Ammo == rifleAmmo && engineer.Kit.Reserve == rifleReserve,
                    "launch spends exactly one rocket and no ordinary ammunition; " + Describe(engineer, hull));
                Check(Mathf.Abs(engineer.RocketReadyAt - Time.time - 10f) < .03f,
                    "actual rocket launch sets a ten-second independent cooldown; ready=" + engineer.RocketReadyAt + " now=" + Time.time);
                Check(ReferenceEquals(Field(first, "scoreActor"), engineer) &&
                    !ReferenceEquals(Field(first, "scoreActor"), game.Player) && first.GetComponentsInChildren<Renderer>().Length > 2,
                    "the visible flying rocket caches its engineer as score owner");
                Fly(first);
                Check(hull.Health < health - 100f && hull.ArmorRating == armorRating &&
                    game.Scores.Get(engineer).Total > 0 && game.Scores.Get(game.Player).Total == 0 &&
                    game.Scores.Get(state.OriginalPlayer).Total == 0,
                    "production rocket travel/collision damages armored hull and credits only the bot; " + Describe(engineer, hull));
                health = hull.Health;
                int projectiles = OwnedRounds(engineer).Count;
                Tick(engineer);
                Tick(engineer);
                Check(OwnedRounds(engineer).Count == projectiles && engineer.RocketCount == 1 && engineer.Kit.Ammo == rifleAmmo &&
                    ReferenceEquals(SelectedVehicle(engineer), hull),
                    "during cooldown the engineer retains the useful vehicle target and never rifle-fires at its hull");
                Set(engineer, "nextRocketAt", Time.time - .01f);
                PrototypeBallisticProjectile second = FirePrepared(engineer, OwnedRounds(engineer));
                Fly(second);
                Check(engineer.RocketCount == 0 && hull.Health < health - 100f && engineer.Kit.GadgetCharges == 0,
                    "second actual rocket is finite and still independent from exhausted support gadgets; " + Describe(engineer, hull));
                int count = OwnedRounds(engineer).Count;
                for (int i = 0; i < 3; i++) Tick(engineer);
                Check(OwnedRounds(engineer).Count == count && engineer.Kit.Ammo == rifleAmmo && !engineer.CanAttackVehicle(hull),
                    "an exhausted engineer cannot regenerate rockets or substitute ineffective rifle fire");

                VerifyInfantryFallback(state, engineer, hull, enemy);
                VerifyInvalidThreats(state, engineer, hull);
                VerifyAuthorityAndLifecycle(state, engineer, hull);
                VerifyNearVehicleRetreat(state, engineer, hull);
                VerifyMedicRescue(state, hull, side);
                Debug.Log("IRONFRONT bot anti-vehicle PASS: actual Update equip/launch and swept rocket collision reduce armored hull health; " +
                    "bot-only attribution, independent two-shot inventory/10s cooldown, infantry fallback, friend/dead/parked/inactive/" +
                    "rear/3D altitude/near/cover/smoke rejection, solo pause/replica/lobby/winner/dead/passenger gating, " +
                    "actual close-vehicle retreat with loaded/empty launcher, respawn inventory and medic rescue preserved; isolated scene state restored");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT bot anti-vehicle FAIL: " + error); }
            finally { state?.Dispose(); }
        }

        private static void VerifyInfantryFallback(SceneState state, PrototypeBot engineer,
            PrototypeCombinedArmsVehicle hull, PrototypeTeam enemy)
        {
            PrototypeRuntime game = state.Game;
            hull.transform.position = Origin + Vector3.forward * 15f;
            SetHealth(hull, hull.MaximumHealth);
            hull.SetAiControlled(true);
            PrototypeBot infantry = Bot(state, enemy, PrototypeInfantryClass.Assault,
                Origin + Vector3.forward * 21f + Vector3.right * 7f);
            game.Bots.Add(infantry);
            engineer.transform.LookAt(new Vector3(infantry.transform.position.x,
                engineer.transform.position.y, infantry.transform.position.z));
            int ammo = engineer.Kit.Ammo;
            Set(engineer, "nextShot", Time.time - .01f);
            Select(engineer, out PrototypeBot selected, out IPrototypeVehicle selectedVehicle);
            Check(selected == infantry && selectedVehicle == null,
                "exhausted engineer selects a visible infantry enemy behind the closer hostile vehicle");
            var rounds = OwnedRounds(engineer);
            Tick(engineer);
            Check(engineer.Kit.Ammo == ammo - 1 && NewRound(engineer, rounds, false) != null && !engineer.IsUsingLauncher,
                "exhausted engineer's production Update fires its rifle only at the selected infantry enemy");
            foreach (PrototypeInfantryClass role in new[] {
                PrototypeInfantryClass.Assault, PrototypeInfantryClass.Medic, PrototypeInfantryClass.Recon })
            {
                PrototypeBot bot = Bot(state, engineer.Team, role, Origin + Vector3.right * 3f);
                state.Game.Bots.Remove(engineer);
                state.Game.Bots.Add(bot);
                bot.transform.LookAt(new Vector3(infantry.transform.position.x, bot.transform.position.y, infantry.transform.position.z));
                Select(bot, out selected, out selectedVehicle);
                Check(bot.RocketCount == 0 && !bot.CanAttackVehicle(hull) && selected == infantry && selectedVehicle == null,
                    role + " has no invented anti-vehicle equipment and keeps the infantry target");
                ammo = bot.Kit.Ammo;
                rounds = OwnedRounds(bot);
                Tick(bot);
                Check(bot.Kit.Ammo == ammo - 1 && NewRound(bot, rounds, false) != null && !bot.IsUsingLauncher,
                    role + " fires its real infantry gun without firing at the nearer armored hull");
                state.Game.Bots.Remove(bot);
                bot.GetComponent<Collider>().enabled = false;
                bot.transform.position = Origin + Vector3.right * 100f;
            }
            game.Bots.Remove(infantry);
            infantry.GetComponent<Collider>().enabled = false;
            infantry.transform.position = Origin + Vector3.right * 120f;
            if (!game.Bots.Contains(engineer)) game.Bots.Add(engineer);
            Reset(engineer);
            hull.transform.position = TargetPosition;
        }

        private static void VerifyInvalidThreats(SceneState state, PrototypeBot bot, PrototypeCombinedArmsVehicle hull)
        {
            PrototypeTeam hostile = hull.Team;
            SetProperty(hull, "Team", bot.Team);
            Reject(bot, hull, "friendly hull");
            SetProperty(hull, "Team", hostile);
            SetHealth(hull, 0f);
            Reject(bot, hull, "destroyed hull");
            SetHealth(hull, hull.MaximumHealth);
            hull.SetAiControlled(false);
            Reject(bot, hull, "parked unoccupied hull");
            hull.SetAiControlled(true);
            hull.gameObject.SetActive(false);
            Reject(bot, hull, "inactive vehicle GameObject");
            hull.gameObject.SetActive(true);
            Reset(bot);
            bot.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Reject(bot, hull, "rear target outside the real view cone", false);
            bot.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            Check(bot.CanAttackVehicle(hull) && !bot.IsAimedAtPosition(hull.MapPosition),
                "peripheral vehicle is visible but not yet aligned for firing");
            var before = OwnedRounds(bot);
            Tick(bot);
            Set(bot, "launcherReadyAt", Time.time - .01f);
            bot.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            bool fired = (bool)typeof(PrototypeBot).GetMethod("TryFireVehicleRocket", Private)
                .Invoke(bot, new object[] { hull });
            Check(!fired && OwnedRounds(bot).Count == before.Count && bot.RocketCount == 2,
                "peripheral target cannot fire before the real body and muzzle turn into alignment");
            Reset(bot);
            hull.transform.position = Origin + Vector3.forward * 5f;
            Reject(bot, hull, "inside launcher safety distance");
            hull.transform.position = Origin + Vector3.forward * 95f;
            Reject(bot, hull, "beyond rocket detection range");
            hull.transform.position = TargetPosition;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            state.Objects.Add(wall);
            wall.name = "Bot rocket verification thin real cover";
            wall.transform.position = Origin + Vector3.forward * 10f + Vector3.up;
            wall.transform.localScale = new Vector3(8f, 6f, .12f);
            Physics.SyncTransforms();
            Reject(bot, hull, "actual thin wall in the rocket ray");
            UnityEngine.Object.DestroyImmediate(wall);
            PrototypeCombinedArmsVehicle friend = Vehicle(state, bot.Team, PrototypeVehicleKind.Ifv,
                Origin + Vector3.forward * 10f, false);
            state.Game.Vehicles.Add(friend);
            Reject(bot, hull, "friendly vehicle physically screens the enemy");
            state.Game.Vehicles.Remove(friend);
            UnityEngine.Object.DestroyImmediate(friend.gameObject);

            Check(PrototypeSmokeCloud.ActiveCloudCount < PrototypeSmokeCloud.MaxActiveClouds,
                "smoke fixture has capacity without destroying an existing scene cloud");
            PrototypeSmokeCloud cloud = PrototypeSmokeCloud.Spawn(state.Game,
                Origin + Vector3.forward * 10f);
            state.Objects.Add(cloud.gameObject);
            Set(cloud, "bornAt", Time.time - 5f);
            Reject(bot, hull, "mature smoke intersects the actual elevated 3D eye-to-hull ray");
            UnityEngine.Object.DestroyImmediate(cloud.gameObject);
            PrototypeCombinedArmsVehicle jet = Vehicle(state, hull.Team, PrototypeVehicleKind.Jet,
                Origin + Vector3.forward * 20f + Vector3.up * 200f, true);
            state.Game.Vehicles.Remove(hull);
            state.Game.Vehicles.Add(jet);
            Reject(bot, jet, "high aircraft has near XZ coordinates but is beyond 3D range");
            state.Game.Vehicles.Remove(jet);
            UnityEngine.Object.DestroyImmediate(jet.gameObject);
            state.Game.Vehicles.Add(hull);
            Reset(bot);
        }

        private static void VerifyAuthorityAndLifecycle(SceneState state, PrototypeBot bot, PrototypeCombinedArmsVehicle hull)
        {
            PrototypeRuntime game = state.Game;
            Reset(bot);
            Tick(bot);
            Set(bot, "launcherReadyAt", Time.time - .01f);
            var before = OwnedRounds(bot);
            int ammo = bot.Kit.Ammo;
            SetProperty(game, "PauseOpen", true);
            float scale = Time.timeScale;
            Time.timeScale = 0f;
            Tick(bot);
            Time.timeScale = scale;
            SetProperty(game, "PauseOpen", false);
            Check(OwnedRounds(bot).Count == before.Count && bot.RocketCount == 2 && bot.Kit.Ammo == ammo,
                "true solo pause produces no actual rocket or ordinary gun fire");
            Set(game, "networkModeActive", true);
            Set(game, "networkHost", false);
            Check(game.IsNetworkReplica, "fixture is a real replica according to production authority logic");
            Tick(bot);
            Set(game, "networkModeActive", false);
            Check(OwnedRounds(bot).Count == before.Count && bot.RocketCount == 2,
                "replica bot cannot launch authoritative combat projectiles");
            SetProperty(game, "MatchStarted", false);
            Tick(bot);
            SetProperty(game, "MatchStarted", true);
            Check(OwnedRounds(bot).Count == before.Count && bot.RocketCount == 2,
                "lobby bot cannot launch combat projectiles");
            SetProperty(game.Match, "Winner", (PrototypeTeam?)hull.Team);
            Tick(bot);
            SetProperty(game.Match, "Winner", null);
            Check(OwnedRounds(bot).Count == before.Count && bot.RocketCount == 2,
                "resolved match cannot launch more combat projectiles");
            SetHealth(bot, 0f);
            Set(bot, "respawnAt", Time.time + 30f);
            Tick(bot);
            Check(OwnedRounds(bot).Count == before.Count && bot.RocketCount == 2,
                "dead engineer cannot fire before its respawn");
            Reset(bot);
            GameObject root = new GameObject("Bot rocket verification friendly U8");
            state.Objects.Add(root);
            PrototypeTransportVehicle transport = root.AddComponent<PrototypeTransportVehicle>();
            transport.Initialize(game, bot.Team, new Vector2(Origin.x, Origin.z), false);
            transport.enabled = false;
            transport.transform.position = Origin;
            Check(transport.TryBoardPassenger(bot) && bot.IsPassenger, "engineer actually reserves a U8 passenger seat");
            before = OwnedRounds(bot);
            Tick(bot);
            Check(OwnedRounds(bot).Count == before.Count && bot.RocketCount == 2,
                "seated passenger cannot shoulder-launch a rocket");
            bot.DisembarkTransport();
            UnityEngine.Object.DestroyImmediate(root);
            Reset(bot);
            Check(bot.RocketCount == 2 && bot.RocketReadyAt <= Time.time && bot.Kit.GadgetCharges == 2,
                "production respawn restores exactly two rockets and resets cooldown independently of gadgets");
        }

        private static void VerifyMedicRescue(SceneState state, PrototypeCombinedArmsVehicle hull, PrototypeTeam side)
        {
            PrototypeRuntime game = state.Game;
            foreach (PrototypeBot bot in game.Bots) bot.GetComponent<Collider>().enabled = false;
            game.Bots.Clear();
            PrototypeBot medic = Bot(state, side, PrototypeInfantryClass.Medic, Origin);
            game.Bots.Add(medic);
            game.Player.transform.position = Origin + Vector3.right * 2f;
            SetHealth(game.Player, 0f);
            Set(game.Player, "downedUntil", Time.time + 30f);
            Set(game.Player, "lastDownedClockAt", Time.time);
            Check(game.Player.IsDowned, "isolated friendly player is in the actual downed state");
            var before = OwnedRounds(medic);
            int ammo = medic.Kit.Ammo;
            Tick(medic);
            Check(medic.TacticalState == PrototypeTacticalState.Revive && game.Player.IsBeingRevived &&
                ReferenceEquals(Field(game.Player, "revivingMedic"), medic) &&
                OwnedRounds(medic).Count == before.Count && medic.Kit.Ammo == ammo && medic.RocketCount == 0,
                "real medic Update prioritizes a nearby casualty and pulses real revive tracking despite an enemy vehicle; " +
                "state=" + medic.TacticalState + " revived=" + game.Player.IsBeingRevived);
        }

        private static void VerifyNearVehicleRetreat(SceneState state, PrototypeBot original,
            PrototypeCombinedArmsVehicle hull)
        {
            // A valid navigable map position is essential here: an off-map
            // fixture would be unable to move in either direction and could
            // hide the original bug of advancing into a too-close vehicle.
            Vector2 map = new Vector2(-130f, -280f);
            Vector3 position = new Vector3(map.x, PrototypeLayout.HeightAt(map.x, map.y) + 1f, map.y);
            Check(!PrototypeLayout.Collides(map, .5f) &&
                !PrototypeLayout.Collides(map - Vector2.up * 4f, .5f),
                "close-vehicle retreat fixture has navigable ground behind the infantry");
            state.Game.Bots.Remove(original);
            original.GetComponent<Collider>().enabled = false;
            PrototypeBot bot = Bot(state, original.Team, PrototypeInfantryClass.Engineer, position);
            state.Game.Bots.Add(bot);
            Vector3 previousHull = hull.transform.position;
            hull.transform.position = new Vector3(map.x,
                PrototypeLayout.HeightAt(map.x, map.y + 7f) + .05f, map.y + 7f);
            hull.SetAiControlled(true);
            SetHealth(hull, hull.MaximumHealth);
            for (int loaded = 1; loaded >= 0; loaded--)
            {
                bot.transform.position = position;
                bot.transform.rotation = Quaternion.identity;
                bot.SetObjective(new Vector2(map.x, map.y + 35f), "objective beyond hostile vehicle " + loaded);
                Set(bot, "rocketCount", loaded == 1 ? 2 : 0);
                Set(bot, "nextScanAt", Time.time + 30f);
                Physics.SyncTransforms();
                Check(!bot.CanAttackVehicle(hull) && SelectedVehicle(bot) == null,
                    "close vehicle remains unsuitable for a rocket or rifle target");
                var rounds = OwnedRounds(bot);
                int ammo = bot.Kit.Ammo;
                Vector3 before = bot.transform.position;
                Vector2 previousMap = bot.MapPosition;
                float beforeDistance = Vector2.Distance(previousMap, hull.MapPosition);
                Tick(bot);
                float afterDistance = Vector2.Distance(bot.MapPosition, hull.MapPosition);
                Check(afterDistance > beforeDistance + Mathf.Max(.0005f, Time.deltaTime * .5f) &&
                    bot.MapPosition.y < previousMap.y && OwnedRounds(bot).Count == rounds.Count &&
                    bot.Kit.Ammo == ammo && bot.RocketCount == (loaded == 1 ? 2 : 0),
                    "production Update steps away from a visible close vehicle with " + (loaded == 1 ? "loaded" : "empty") +
                    " launcher rather than chasing the objective through its hull; before=" + before +
                    " after=" + bot.transform.position + " distances=" + beforeDistance + "/" + afterDistance +
                    " dt=" + Time.deltaTime + " state=" + bot.TacticalState);
            }
            state.Game.Bots.Remove(bot);
            UnityEngine.Object.DestroyImmediate(bot.gameObject);
            state.Game.Bots.Add(original);
            original.GetComponent<Collider>().enabled = true;
            hull.transform.position = previousHull;
            Physics.SyncTransforms();
        }

        private static void Reject(PrototypeBot bot, IPrototypeVehicle hull, string description, bool reset = true)
        {
            if (reset) Reset(bot);
            Physics.SyncTransforms();
            var before = OwnedRounds(bot);
            int ammo = bot.Kit.Ammo;
            Check(!bot.CanAttackVehicle(hull) && SelectedVehicle(bot) == null,
                description + " rejected by production target selection; " + Describe(bot, hull));
            Tick(bot);
            Check(OwnedRounds(bot).Count == before.Count && bot.Kit.Ammo == ammo && bot.RocketCount == 2,
                description + " causes no launcher/rifle ammunition consumption or projectile");
        }

        private static PrototypeBallisticProjectile FirePrepared(PrototypeBot bot,
            HashSet<PrototypeBallisticProjectile> before)
        {
            Tick(bot);
            Set(bot, "launcherReadyAt", Time.time - .01f);
            Tick(bot);
            PrototypeBallisticProjectile rocket = NewRound(bot, before, true);
            Check(rocket != null, "production Update actually launched an anti-armor rocket; " + Describe(bot, null));
            return rocket;
        }

        private static void Fly(PrototypeBallisticProjectile projectile)
        {
            Vector3 start = projectile.transform.position;
            for (int i = 0; i < 600 && projectile != null && projectile.enabled; i++)
                Invoke(projectile, "Update");
            Check(projectile != null && !projectile.enabled &&
                Vector3.Distance(start, projectile.transform.position) > 8f,
                "real ballistic Update moved and collided instead of direct fixture damage; " +
                "travel=" + (projectile != null ? Vector3.Distance(start, projectile.transform.position) : -1f));
        }

        private static PrototypeBot Bot(SceneState state, PrototypeTeam team,
            PrototypeInfantryClass role, Vector3 position)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            state.Objects.Add(root);
            root.name = "Anti-vehicle verification " + role;
            PrototypeBot bot = root.AddComponent<PrototypeBot>();
            bot.Initialize(state.Game, state.Game.Navigation, team, new Vector2(position.x, position.z), role);
            bot.enabled = false;
            root.GetComponent<PrototypeSoldierVisual>().enabled = false;
            root.transform.position = position;
            root.transform.rotation = Quaternion.identity;
            bot.SetObjective(new Vector2(position.x, position.z), "anti-vehicle fixture");
            return bot;
        }

        private static PrototypeCombinedArmsVehicle Vehicle(SceneState state, PrototypeTeam team,
            PrototypeVehicleKind kind, Vector3 position, bool active)
        {
            var root = new GameObject("Anti-vehicle verification " + kind);
            state.Objects.Add(root);
            var hull = root.AddComponent<PrototypeCombinedArmsVehicle>();
            hull.Initialize(state.Game, team, kind, new Vector2(position.x, position.z), active);
            hull.enabled = false;
            hull.RespawnEnabled = false;
            root.transform.position = position;
            return hull;
        }

        private static void Reset(PrototypeBot bot)
        {
            Invoke(bot, "Respawn");
            bot.transform.position = Origin;
            bot.transform.rotation = Quaternion.identity;
            bot.SetObjective(new Vector2(Origin.x, Origin.z), "anti-vehicle fixture");
            Set(bot, "nextScanAt", Time.time + 30f);
            Physics.SyncTransforms();
        }

        private static HashSet<PrototypeBallisticProjectile> OwnedRounds(PrototypeBot bot)
        {
            var result = new HashSet<PrototypeBallisticProjectile>();
            foreach (PrototypeBallisticProjectile round in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                if (ReferenceEquals(Field(round, "owner"), bot.transform)) result.Add(round);
            return result;
        }

        private static PrototypeBallisticProjectile NewRound(PrototypeBot bot,
            HashSet<PrototypeBallisticProjectile> before, bool rocket)
        {
            foreach (PrototypeBallisticProjectile round in OwnedRounds(bot))
                if (!before.Contains(round) && (bool)Field(round, "rocket") == rocket) return round;
            return null;
        }

        private static IPrototypeVehicle SelectedVehicle(PrototypeBot bot)
        { Select(bot, out _, out IPrototypeVehicle vehicle); return vehicle; }

        private static void Select(PrototypeBot bot, out PrototypeBot infantry, out IPrototypeVehicle vehicle)
        {
            object[] selected = { null, null, null, null };
            typeof(PrototypeBot).GetMethod("FindVisibleEnemy", Private).Invoke(bot, selected);
            infantry = (PrototypeBot)selected[0]; vehicle = (IPrototypeVehicle)selected[2];
        }

        private static void Tick(PrototypeBot bot) => Invoke(bot, "Update");
        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static void SetHealth(object target, float health) => SetProperty(target, "Health", health);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
        private static string Describe(PrototypeBot bot, IPrototypeVehicle vehicle) =>
            "role=" + bot.Class + " rockets=" + bot.RocketCount + " ammo=" + bot.Kit.Ammo +
            " launcher=" + bot.IsUsingLauncher + " ready=" + bot.RocketReadyAt + " time=" + Time.time +
            (vehicle is IPrototypeArmoredVehicle armor ? " hull=" + vehicle.VehicleName + " health=" +
                ((PrototypeCombinedArmsVehicle)vehicle).Health + " armor=" + armor.ArmorRating : "");
        private static void Check(bool value, string detail)
        { if (!value) throw new InvalidOperationException(detail); }

        private sealed class SceneState : IDisposable
        {
            public readonly PrototypeRuntime Game;
            public readonly PrototypePlayer OriginalPlayer;
            public readonly List<GameObject> Objects = new List<GameObject>();
            private readonly List<PrototypeBot> bots;
            private readonly List<IPrototypeVehicle> vehicles;
            private readonly List<PrototypeRemotePlayer> remotes;
            private readonly HashSet<PrototypeBallisticProjectile> rounds;
            private readonly HashSet<PrototypePulseVisual> pulses;
            private readonly HashSet<AudioSource> audio;
            private readonly UnityEngine.Random.State random;
            private readonly List<PrototypeBotDownNotice> feed;
            private readonly PrototypeScoreSystem scores, matchScores;
            private readonly float blueTickets, redTickets, timeScale;
            private readonly PrototypeTeam? winner;
            private readonly bool started, paused, network, host;

            public SceneState(PrototypeRuntime game)
            {
                Game = game; OriginalPlayer = game.Player;
                bots = new List<PrototypeBot>(game.Bots); vehicles = new List<IPrototypeVehicle>(game.Vehicles);
                remotes = new List<PrototypeRemotePlayer>(game.RemotePlayers);
                rounds = new HashSet<PrototypeBallisticProjectile>(UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>());
                pulses = new HashSet<PrototypePulseVisual>(UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>());
                audio = new HashSet<AudioSource>(UnityEngine.Object.FindObjectsByType<AudioSource>());
                random = UnityEngine.Random.state;
                feed = new List<PrototypeBotDownNotice>(game.BotDownFeed.Entries);
                scores = game.Scores; matchScores = game.Match.ScoreSystem;
                blueTickets = game.Match.BlueTickets; redTickets = game.Match.RedTickets; winner = game.Match.Winner;
                timeScale = Time.timeScale; started = game.MatchStarted; paused = game.PauseOpen;
                network = (bool)Field(game, "networkModeActive"); host = (bool)Field(game, "networkHost");
                game.Bots.Clear(); game.Vehicles.Clear(); game.RemotePlayers.Clear();
                Set(game, "networkModeActive", false);
                SetProperty(game, "MatchStarted", true); SetProperty(game, "PauseOpen", false);
                SetProperty(game.Match, "Winner", null); Time.timeScale = 1f;
                var root = new GameObject("Anti-vehicle verification isolated local player");
                Objects.Add(root);
                var player = root.AddComponent<PrototypePlayer>();
                player.enabled = false; root.GetComponent<CharacterController>().enabled = false;
                Set(player, "runtime", game); SetProperty(player, "Team", OriginalPlayer.Team);
                root.transform.position = Origin - Vector3.right * 100f;
                SetProperty(game, "Player", player);
                var isolated = new PrototypeScoreSystem(game);
                SetProperty(game, "Scores", isolated); game.Match.ScoreSystem = isolated;
            }

            public void Dispose()
            {
                Set(Game, "networkModeActive", false);
                foreach (PrototypeBallisticProjectile round in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    if (!rounds.Contains(round)) UnityEngine.Object.DestroyImmediate(round.gameObject);
                foreach (GameObject item in Objects)
                    if (item != null) UnityEngine.Object.DestroyImmediate(item);
                foreach (PrototypePulseVisual pulse in UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>())
                    if (!pulses.Contains(pulse)) UnityEngine.Object.DestroyImmediate(pulse.gameObject);
                foreach (AudioSource source in UnityEngine.Object.FindObjectsByType<AudioSource>())
                    if (!audio.Contains(source)) UnityEngine.Object.DestroyImmediate(source.gameObject);
                Game.Bots.Clear(); Game.Bots.AddRange(bots);
                Game.Vehicles.Clear(); Game.Vehicles.AddRange(vehicles);
                Game.RemotePlayers.Clear(); Game.RemotePlayers.AddRange(remotes);
                SetProperty(Game, "Player", OriginalPlayer); SetProperty(Game, "Scores", scores);
                Game.Match.ScoreSystem = matchScores;
                SetProperty(Game.Match, "BlueTickets", blueTickets); SetProperty(Game.Match, "RedTickets", redTickets);
                SetProperty(Game.Match, "Winner", winner);
                SetProperty(Game, "MatchStarted", started); SetProperty(Game, "PauseOpen", paused);
                Set(Game, "networkModeActive", network); Set(Game, "networkHost", host); Time.timeScale = timeScale;
                var entries = (List<PrototypeBotDownNotice>)Field(Game.BotDownFeed, "entries");
                entries.Clear(); entries.AddRange(feed);
                UnityEngine.Random.state = random;
                Physics.SyncTransforms();
            }
        }
    }
}
