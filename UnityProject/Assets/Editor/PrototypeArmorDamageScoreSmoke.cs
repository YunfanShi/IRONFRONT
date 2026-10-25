using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeArmorDamageScoreSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("IRONFRONT/Verify Vehicle Armor and Damage Scoring", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Verify Vehicle Armor and Damage Scoring")]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            var fixtures = new List<GameObject>();
            var pulses = new HashSet<PrototypePulseVisual>(UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>());
            PrototypeScoreSystem original = null;
            float blue = 0f, red = 0f; bool restore = false;
            try
            {
                Check(game != null && game.Player != null && !game.IsNetworkReplica, "authority and player exist");
                if (!game.MatchStarted) game.BeginBattle();
                Check(game.Player.Alive && !game.Match.Winner.HasValue, "live match");
                original = game.Scores; blue = game.Match.BlueTickets; red = game.Match.RedTickets; restore = true;
                float now = 100f;
                var scores = new PrototypeScoreSystem(game, () => now);
                typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, scores);
                game.Match.SetNetworkTickets(100f, 100f);
                var events = new List<PrototypeScoreSystem.Notice>(); scores.Awarded += events.Add;
                PrototypeTeam team = game.Player.Team, enemyTeam = team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                var vehicles = new List<IPrototypeVehicle>();
                for (int i = 0; i < 9; i++) vehicles.Add(Vehicle(game, enemyTeam, i, fixtures));
                foreach (IPrototypeVehicle vehicle in vehicles)
                {
                    Check(vehicle is IPrototypeArmoredVehicle && PrototypeVehicleArmor.Rating(vehicle) > 0,
                        "all nine hulls expose independent armor protection and maximum health");
                    float health = vehicle.Health;
                    foreach (PrototypeWeaponId id in Enum.GetValues(typeof(PrototypeWeaponId)))
                    {
                        var bullet = Track(PrototypeBallisticProjectile.LaunchBullet(game, team, game.Player.transform,
                            vehicle.VehicleTransform.position - Vector3.forward * 16f, Vector3.forward,
                            PrototypeInfantryRoles.GetWeapon(id), 10f), fixtures);
                        Collider hull = vehicle.VehicleTransform.GetComponent<Collider>();
                        Invoke(bullet, "ApplyBulletDamage", hull, 0f);
                        Check(Mathf.Approximately(vehicle.Health, health), "ordinary " + id + " cannot chip " + vehicle.VehicleName);
                        Physics.SyncTransforms();
                        Vector3 origin = hull.bounds.center - Vector3.forward * 16f;
                        object[] query = { origin, Vector3.forward, 32f, Vector3.zero, null };
                        Check((bool)InvokeArgs(bullet, "TryHit", query) &&
                            ReferenceEquals(PrototypeVehicleHit.Find((Collider)query[4]), vehicle),
                            "nonpenetrating rifle still impacts the physical hull instead of shooting through it");
                    }
                }
                Check(scores.Get(game.Player).Total == 0 && events.Count == 0,
                    "blocked ordinary hull hits award neither damage nor combat credits");
                IPrototypeVehicle light = vehicles[7], heavy = vehicles[0];
                float lightBefore = light.Health, heavyBefore = heavy.Health;
                var mounted = Track(PrototypeBallisticProjectile.LaunchMountedRound(game, team, game.Player.transform,
                    light.VehicleTransform.position, Vector3.forward, 500f, 200f, 40f), fixtures);
                Invoke(mounted, "ApplyBulletDamage", light.VehicleTransform.GetComponent<Collider>(), 0f);
                Invoke(mounted, "ApplyBulletDamage", heavy.VehicleTransform.GetComponent<Collider>(), 0f);
                Check(light.Health < lightBefore && Mathf.Approximately(heavy.Health, heavyBefore),
                    "mounted MG penetrates a light hull but fails heavy tank armor");
                foreach (IPrototypeVehicle vehicle in vehicles)
                {
                    float health = vehicle.Health;
                    var rocket = Track(PrototypeBallisticProjectile.LaunchRocket(game, team, game.Player.transform,
                        vehicle.VehicleTransform.position, Vector3.forward), fixtures);
                    Physics.SyncTransforms(); Invoke(rocket, "Explode",
                        vehicle.VehicleTransform.GetComponent<Collider>().bounds.center + Vector3.up * 3f);
                    Check(vehicle.Health < health, "actual anti-armor rocket penetrates " + vehicle.VehicleName);
                    // Each ammunition check needs a living target. A rocket can
                    // already destroy the U8; firing at its wreck proves nothing.
                    if (!vehicle.Alive) Debug.Log("Armor fixture: rocket destroyed " + vehicle.VehicleName + "; cannon uses a fresh hull.");
                    IPrototypeVehicle cannonTarget = Vehicle(game, enemyTeam, vehicles.IndexOf(vehicle), fixtures);
                    cannonTarget.VehicleTransform.position += Vector3.forward * 60f;
                    health = cannonTarget.Health;
                    Check(cannonTarget.Alive, "cannon fixture starts alive");
                    var shell = Track(PrototypeBallisticProjectile.LaunchShell(game, team, game.Player.transform,
                        cannonTarget.VehicleTransform.position, Vector3.forward, 170f, 230f), fixtures);
                    Physics.SyncTransforms(); Invoke(shell, "Explode", cannonTarget.VehicleTransform.GetComponent<Collider>().bounds.center);
                    Check(cannonTarget.Health < health, "actual cannon shell penetrates " + cannonTarget.VehicleName);
                }

                scores.Reset(); events.Clear();
                PrototypeBot victim = Bot(game, enemyTeam, new Vector3(1250f, 240f, 900f), fixtures);
                PrototypeBot helper = Bot(game, team, new Vector3(1280f, 240f, 900f), fixtures);
                Hit(scores, victim, game.Player, team, 6f); Hit(scores, victim, game.Player, team, 6f);
                Check(scores.Get(game.Player).Total == 1 && scores.Get(game.Player).DamagePoints == 1 &&
                    events.Count == 1 && events[0].Kind == PrototypeScoreNoticeKind.Damage && events[0].Points == 1,
                    "fractional real health losses accumulate before floor(damage / 10) awards an immediate point");
                Hit(scores, victim, game.Player, team, 88f);
                Check(scores.Get(game.Player).Total == 100 && scores.Get(game.Player).Kills == 1 &&
                    events[events.Count - 1].Kind == PrototypeScoreNoticeKind.Kill && events[events.Count - 1].Points == 90 &&
                    events[events.Count - 1].SettlementPoints == 100 && ReferenceEquals(events[events.Count - 1].Victim, victim),
                    "own kill totals exactly 100 including its recent ten damage points, with a typed 90-point top-up");
                int notices = events.Count; Hit(scores, victim, game.Player, team, 1000f);
                Check(scores.Get(game.Player).Total == 100 && events.Count == notices, "a dead victim cannot duplicate damage or settlement");
                Respawn(victim); scores.Reset(); events.Clear();
                Hit(scores, victim, game.Player, team, 40f); Hit(scores, victim, helper, team, 60f);
                Check(scores.Get(game.Player).Total == 54 && scores.Get(game.Player).Assists == 1 &&
                    scores.Get(helper).Total == 100 && Has(events, game.Player, PrototypeScoreNoticeKind.Assist, 50),
                    "four damage credits survive and teammate kill adds 50 assist, while the killer totals 100");
                Respawn(victim); scores.Reset(); events.Clear();
                Hit(scores, victim, game.Player, team, 20f); now += 12.01f;
                Hit(scores, victim, game.Player, team, 80f);
                Check(scores.Get(game.Player).Total == 102 && events[events.Count - 1].Points == 92,
                    "older damage credits stay earned; only the recent twelve-second credits reduce the kill top-up");
                Respawn(victim); scores.Reset(); events.Clear();
                Hit(scores, victim, game.Player, team, 50f); victim.HealTo(100f);
                Hit(scores, victim, helper, team, 50f); victim.HealTo(100f);
                Hit(scores, victim, game.Player, team, 50f); victim.HealTo(100f);
                Check(scores.Get(game.Player).DamagePoints + scores.Get(helper).DamagePoints == 10,
                    "the maximum-health budget is shared across actors and cannot be farmed by healing one victim life");
                Hit(scores, victim, helper, team, 1000f);
                Check(scores.Get(helper).Total == 100 && scores.Get(game.Player).Total == 55,
                    "capped damage still permits one genuine kill and a qualified extra assist");

                var vehicleVictim = (PrototypeCombinedArmsVehicle)vehicles[7];
                Invoke(vehicleVictim, "ResetVehicle"); scores.Reset(); events.Clear();
                HitVehicle(scores, vehicleVictim, game.Player, team, 40f);
                HitVehicle(scores, vehicleVictim, helper, team, 1000f);
                Check(scores.Get(game.Player).Total == 104 && scores.Get(game.Player).VehicleAssists == 1 &&
                    scores.Get(game.Player).Assists == 0 && scores.Get(helper).Total == 200 &&
                    scores.Get(helper).VehiclesDestroyed == 1 && scores.Get(helper).Kills == 0 &&
                    Has(events, game.Player, PrototypeScoreNoticeKind.VehicleAssist, 100),
                    "vehicle damage, destruction total 200 and extra vehicle assist 100 have distinct counters and notices");
                long previousSequence = 0;
                foreach (var notice in events)
                { Check(notice.Sequence > previousSequence, "notice identities increase exactly once per event"); previousSequence = notice.Sequence; }
                Check(scores.TrySpend(helper, 100) && scores.Refund(helper, 100) &&
                    Has(events, helper, PrototypeScoreNoticeKind.Spend, -100) && Has(events, helper, PrototypeScoreNoticeKind.Refund, 100),
                    "support debit and bounded refund emit typed budget deltas without changing earned total");
                Debug.Log("IRONFRONT armor and damage scoring PASS: all nine hull ratings, all seven ordinary guns blocked " +
                    "with physical impact, mounted penetration gate, real AT/cannon damage, fractional immediate damage credits, " +
                    "100/200 recent-window kill top-ups, extra 50/100 assists, shared life healing budget, typed notices/sequence/event and budget deltas");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT armor and damage scoring FAIL: " + error); }
            finally
            {
                if (restore && game != null)
                { typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, original); game.Match.SetNetworkTickets(blue, red); }
                foreach (GameObject fixture in fixtures) if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                foreach (PrototypePulseVisual pulse in UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>())
                    if (!pulses.Contains(pulse)) UnityEngine.Object.DestroyImmediate(pulse.gameObject);
            }
        }

        private static IPrototypeVehicle Vehicle(PrototypeRuntime game, PrototypeTeam team, int index, List<GameObject> fixtures)
        {
            var root = new GameObject("Armor Verification Hull " + index); fixtures.Add(root); root.transform.SetParent(game.transform);
            Vector3 position = new Vector3(900f + index * 35f, 240f, 900f); Vector2 map = new Vector2(position.x, position.z);
            IPrototypeVehicle vehicle;
            if (index == 0) { var actor = root.AddComponent<PrototypeTankVehicle>(); actor.Initialize(game, team, map, 0f); vehicle = actor; }
            else if (index == 1) { var actor = root.AddComponent<PrototypeScoutVehicle>(); actor.Initialize(game, team, map, false); vehicle = actor; }
            else if (index == 2) { var actor = root.AddComponent<PrototypeTransportVehicle>(); actor.Initialize(game, team, map, false); vehicle = actor; }
            else
            { var actor = root.AddComponent<PrototypeCombinedArmsVehicle>(); actor.Initialize(game, team, (PrototypeVehicleKind)(index - 3), map, false); vehicle = actor; }
            ((MonoBehaviour)vehicle).enabled = false; root.transform.position = position; return vehicle;
        }
        private static PrototypeBot Bot(PrototypeRuntime game, PrototypeTeam team, Vector3 position, List<GameObject> fixtures)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule); fixtures.Add(root); root.transform.SetParent(game.transform);
            var bot = root.AddComponent<PrototypeBot>(); bot.Initialize(game, game.Navigation, team,
                new Vector2(position.x, position.z), PrototypeInfantryClass.Assault); bot.enabled = false; root.transform.position = position; return bot;
        }
        private static PrototypeBallisticProjectile Track(PrototypeBallisticProjectile round, List<GameObject> fixtures)
        { Check(round != null, "real projectile launches"); round.enabled = false; fixtures.Add(round.gameObject); return round; }
        private static void Hit(PrototypeScoreSystem score, PrototypeBot target, object source, PrototypeTeam team, float amount) =>
            score.ApplyDamage(target, source, team, amount, () => target.TakeDamage(amount));
        private static void HitVehicle(PrototypeScoreSystem score, IPrototypeVehicle target, object source, PrototypeTeam team, float amount) =>
            score.ApplyDamage(target, source, team, amount, () => target.TakeDamage(amount));
        private static void Respawn(PrototypeBot target) => Invoke(target, "Respawn");
        private static bool Has(List<PrototypeScoreSystem.Notice> events, object actor, PrototypeScoreNoticeKind kind, int points) =>
            events.Exists(notice => ReferenceEquals(notice.Actor, actor) && notice.Kind == kind && notice.Points == points);
        private static object Invoke(object target, string name, params object[] args) => InvokeArgs(target, name, args);
        private static object InvokeArgs(object target, string name, object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
