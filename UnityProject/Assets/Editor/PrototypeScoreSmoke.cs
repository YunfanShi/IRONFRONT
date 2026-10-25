using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeScoreSmoke
    {
        [MenuItem("IRONFRONT/Verify Battle Scoring", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Battle Scoring")]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            var fixtures = new List<GameObject>();
            PrototypeScoreSystem originalScores = null;
            PrototypeGameMode originalMode = PrototypeGameMode.Conquest;
            float blueTickets = 0f, redTickets = 0f;
            bool restore = false;
            try
            {
                Check(game != null && game.Player != null, "runtime and local player exist");
                if (!game.MatchStarted) game.BeginBattle();
                Check(!game.IsNetworkReplica && !game.Match.Winner.HasValue,
                    "verify scoring in a living solo or host battle");
                originalScores = game.Scores;
                originalMode = game.Match.Mode;
                blueTickets = game.Match.BlueTickets; redTickets = game.Match.RedTickets;
                restore = true;
                float now = 100f;
                var scores = new PrototypeScoreSystem(game, () => now);
                typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, scores);
                PrototypeTeam team = game.Player.Team;
                PrototypeTeam enemyTeam = team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                PrototypeBot victim = CreateBot(game, enemyTeam, fixtures, 0);
                PrototypeBot helper = CreateBot(game, team, fixtures, 1);
                PrototypeBot friendly = CreateBot(game, team, fixtures, 2);

                Hit(scores, victim, helper, team, 25f);
                Hit(scores, victim, game.Player, team, 75f);
                Check(!victim.Alive && scores.Get(game.Player).Kills == 1 &&
                    scores.Get(game.Player).Total == 100 && scores.Get(game.Player).Credits == 100,
                    "real lethal damage pays the shooter once");
                Check(scores.Get(helper).Assists == 1 && scores.Get(helper).Total == 52,
                    "25 actual damage earns two damage points and another killer grants an extra 50 assist points");
                Hit(scores, victim, game.Player, team, 1000f);
                scores.ResolveDefeat(victim, enemyTeam);
                Check(scores.Get(game.Player).Kills == 1 && scores.Get(helper).Assists == 1,
                    "repeated dead damage and duplicate death callbacks cannot pay twice");
                Respawn(victim);
                Hit(scores, victim, helper, team, 19f);
                Hit(scores, victim, game.Player, team, 81f);
                Check(scores.Get(game.Player).Kills == 2 && scores.Get(helper).Assists == 1,
                    "new target life can pay again; damage below the assist threshold cannot");
                Respawn(victim);
                Hit(scores, victim, helper, team, 25f);
                now += PrototypeScoreSystem.AssistWindow + .01f;
                Hit(scores, victim, game.Player, team, 75f);
                Check(scores.Get(helper).Assists == 1, "expired assists cannot earn points");
                Respawn(victim);
                Hit(scores, victim, helper, team, 25f);
                now += PrototypeScoreSystem.AssistWindow + .01f;
                Hit(scores, victim, helper, team, 1f);
                Hit(scores, victim, game.Player, team, 74f);
                Check(scores.Get(helper).Assists == 1,
                    "a fresh tiny hit does not revive old damage outside the 12 second window");
                Respawn(victim);
                var armorKit = new PrototypeInfantryKit(PrototypeInfantryClass.Engineer);
                Check(armorKit.TryApplyArmor(now, victim.Kit, 0f), "test armor is applied");
                Hit(scores, victim, helper, team, 25f);
                Check(Mathf.Approximately(victim.Health, 100f), "armor absorbs the whole assisting hit");
                Hit(scores, victim, game.Player, team, 1000f);
                Check(scores.Get(helper).Assists == 1, "absorbed damage does not count as an assist");

                Respawn(victim);
                GameObject vehicleRoot = new GameObject("Score Verification Vehicle");
                fixtures.Add(vehicleRoot); vehicleRoot.transform.SetParent(game.transform);
                var vehicle = vehicleRoot.AddComponent<PrototypeCombinedArmsVehicle>();
                vehicle.Initialize(game, enemyTeam, PrototypeVehicleKind.Motorcycle,
                    new Vector2(348f, 330f), false);
                vehicle.enabled = false;
                var seats = (PrototypeVehicleSeats)typeof(PrototypeCombinedArmsVehicle)
                    .GetField("seats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(vehicle);
                Check(seats.TryReserve(0, victim), "real vehicle has a live enemy crew member");
                int beforeCrewKill = scores.Get(game.Player).Kills;
                int beforeVehicleReward = scores.Get(game.Player).Total;
                scores.ApplyDamage(vehicle, game.Player, team, 1000f, () => vehicle.TakeDamage(1000f));
                Check(!vehicle.Alive && !victim.Alive && scores.Get(game.Player).VehiclesDestroyed == 1 &&
                    scores.Get(game.Player).Kills == beforeCrewKill + 1 &&
                    scores.Get(game.Player).Total == beforeVehicleReward + 300,
                    "real vehicle destruction pays 200 and nested crew death pays its 100 to the same attacker");
                scores.ApplyDamage(vehicle, game.Player, team, 1000f, () => vehicle.TakeDamage(1000f));
                Check(scores.Get(game.Player).VehiclesDestroyed == 1,
                    "a destroyed hull cannot pay again");

                Respawn(victim);
                typeof(PrototypeMatch).GetProperty("Mode").SetValue(game.Match, PrototypeGameMode.Conquest);
                game.Match.SetNetworkTickets(enemyTeam == PrototypeTeam.Blue ? 1f : 100f,
                    enemyTeam == PrototypeTeam.Red ? 1f : 100f);
                int beforeFinalKill = scores.Get(game.Player).Total;
                Hit(scores, victim, game.Player, team, 1000f);
                Check(game.Match.Winner == team && scores.Get(game.Player).Total == beforeFinalKill + 100,
                    "the actual final ticket kill still pays after TakeDamage ends the match");
                typeof(PrototypeMatch).GetProperty("Mode").SetValue(game.Match, originalMode);
                game.Match.SetNetworkTickets(blueTickets, redTickets);

                int previousKills = scores.Get(game.Player).Kills;
                Hit(scores, friendly, game.Player, team, 1000f);
                Hit(scores, helper, helper, team, 1000f);
                Check(scores.Get(game.Player).Kills == previousKills && scores.Get(helper).Kills == 0,
                    "friendly fire and self kills earn no kill points");
                Respawn(helper);
                Respawn(victim);
                scores.RecordDamage(victim, game.Player, team, float.NaN);
                scores.RecordDamage(victim, game.Player, team, -100f);
                scores.ResolveDefeat(victim, enemyTeam);
                Check(scores.Get(game.Player).Assists == 0, "invalid or negative damage cannot fabricate assists");

                // Use real match Tick and live actor positions, so the check covers
                // capture radius, contention, objective activation and full ownership.
                scores.Reset();
                var captureMatch = new PrototypeMatch { ScoreSystem = scores };
                captureMatch.ConfigureSolo(PrototypeGameMode.Conquest);
                PrototypeCapturePoint point = captureMatch.Points[0];
                Position(helper, point.Definition.Position);
                captureMatch.Tick(4.99f, null, new[] { helper });
                Check(scores.Get(helper).Total == 0, "less than five seconds of progress earns no tick yet");
                captureMatch.Tick(.02f, null, new[] { helper });
                Check(scores.Get(helper).Total == 20, "five seconds of real progress earns 20 points");
                captureMatch.Tick(10f, null, new[] { helper });
                Check(point.Owner == team && scores.Get(helper).Captures == 1 && scores.Get(helper).Total == 170,
                    "actual full capture pays 150 once plus its effective progress time");
                int ownedScore = scores.Get(helper).Total;
                captureMatch.Tick(100f, null, new[] { helper });
                Check(scores.Get(helper).Total == ownedScore, "holding an already full friendly objective cannot farm points");
                Position(helper, point.Definition.Position + Vector2.right * 40f);
                captureMatch.Tick(10f, null, new[] { helper });
                Check(scores.Get(helper).Total == ownedScore, "actors outside the 25 meter capture circle earn no points");

                scores.Reset();
                captureMatch.ConfigureSolo(PrototypeGameMode.Conquest);
                Position(helper, point.Definition.Position);
                Position(victim, point.Definition.Position);
                captureMatch.Tick(20f, null, new[] { helper, victim });
                Check(point.Contested && scores.Get(helper).Total == 0 && scores.Get(victim).Total == 0,
                    "contested objectives pay neither team");
                Position(victim, point.Definition.Position + Vector2.right * 40f);
                captureMatch.Tick(4f, null, new[] { helper, victim });
                Position(helper, point.Definition.Position + Vector2.right * 40f);
                captureMatch.Tick(1f, null, new[] { helper, victim });
                Position(helper, point.Definition.Position);
                captureMatch.Tick(1.01f, null, new[] { helper, victim });
                Check(scores.Get(helper).Total == 0, "leaving the objective resets incomplete progress reward timing");

                scores.Reset();
                captureMatch.ConfigureSolo(PrototypeGameMode.Breakthrough);
                PrototypeBot blueActor = team == PrototypeTeam.Blue ? helper : victim;
                PrototypeBot redActor = team == PrototypeTeam.Red ? helper : victim;
                Respawn(blueActor); Respawn(redActor);
                Position(blueActor, captureMatch.Points[1].Definition.Position);
                Position(redActor, point.Definition.Position + Vector2.right * 40f);
                captureMatch.Tick(20f, null, new[] { blueActor, redActor });
                Check(scores.Get(blueActor).Total == 0 && captureMatch.SectorIndex == 0,
                    "future Breakthrough sectors award no capture score");
                Position(blueActor, point.Definition.Position);
                captureMatch.Tick(20f, null, new[] { blueActor, redActor });
                Check(captureMatch.SectorIndex == 1 && scores.Get(blueActor).Captures == 1 &&
                    scores.Get(blueActor).Total == 210,
                    "active Breakthrough capture advances the sector and pays only its real contributor");
                Check(scores.Get(redActor).Total == 0, "distant defenders do not share the attacker's score");

                int total = scores.Get(blueActor).Total;
                Check(scores.TrySpend(blueActor, 200, "support") &&
                    scores.Get(blueActor).Credits == total - 200 && scores.Get(blueActor).Total == total,
                    "support spending deducts personal credits but preserves lifetime match score");
                Check(!scores.TrySpend(blueActor, 200) && !scores.TrySpend(blueActor, -1),
                    "insufficient or negative support purchases cannot spend");
                Check(scores.Refund(blueActor, 200) && !scores.Refund(blueActor, 200) &&
                    scores.Get(blueActor).Credits == total && scores.Get(blueActor).Total == total,
                    "delivery failure can refund its exact debit once without minting score");
                Respawn(blueActor);
                Check(scores.Get(blueActor).Total == total && scores.Get(blueActor).Credits == total,
                    "respawn preserves earned score and credits");
                scores.Reset();
                Check(scores.Get(blueActor).Total == 0 && scores.Get(blueActor).Credits == 0 &&
                    scores.Notices.Count == 0, "a new match clears scores, contributions, credits and notices");
                Debug.Log("IRONFRONT battle scoring PASS: real lethal damage, 100 kill / 200 vehicle / 50 assist, actual " +
                    "health damage, 20 damage / 12 second assist window, one reward per life, no friendly/self reward, " +
                    "nested real crew death attribution and final ticket reward, " +
                    "real Conquest and active Breakthrough captures, 150 capture / 20 per five seconds of progress, " +
                    "contested/owned/distant farm prevention, atomic personal spending/refund, respawn preservation and match reset");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT battle scoring FAIL: " + error); }
            finally
            {
                if (restore && game != null)
                {
                    typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, originalScores);
                    typeof(PrototypeMatch).GetProperty("Mode").SetValue(game.Match, originalMode);
                    game.Match.SetNetworkTickets(blueTickets, redTickets);
                    game.BotDownFeed.Clear();
                }
                foreach (GameObject fixture in fixtures)
                    if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        private static PrototypeBot CreateBot(PrototypeRuntime game, PrototypeTeam team,
            List<GameObject> fixtures, int index)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Score Verification Actor " + index;
            fixtures.Add(root);
            root.transform.SetParent(game.transform);
            var bot = root.AddComponent<PrototypeBot>();
            bot.Initialize(game, game.Navigation, team, new Vector2(335f + index * 4f, 330f),
                PrototypeInfantryClass.Assault);
            bot.enabled = false;
            return bot;
        }

        private static void Hit(PrototypeScoreSystem scores, PrototypeBot target, object source,
            PrototypeTeam team, float amount) =>
            scores.ApplyDamage(target, source, team, amount, () => target.TakeDamage(amount));

        private static void Respawn(PrototypeBot bot) => typeof(PrototypeBot)
            .GetMethod("Respawn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bot, null);

        private static void Position(PrototypeBot bot, Vector2 position) =>
            bot.transform.position = new Vector3(position.x,
                PrototypeLayout.HeightAt(position.x, position.y) + 1f, position.y);

        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); }
    }
}
