using System;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeMatchRulesSmoke
    {
        [MenuItem("IRONFRONT/Preview Solo Breakthrough Defeat", true)]
        private static bool CanPreviewDefeat() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Preview Solo Breakthrough Defeat")]
        private static void PreviewDefeat()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null || !game.MatchStarted ||
                game.Match.Mode != PrototypeGameMode.Breakthrough)
                throw new InvalidOperationException(
                    "Start a solo Breakthrough match in Play mode first.");
            game.Match.SetNetworkTickets(0f, game.Match.RedTickets);
            Debug.Log("IRONFRONT solo result preview: Breakthrough defender victory");
        }

        [MenuItem("IRONFRONT/Verify Solo Match Rules")]
        private static void Verify()
        {
            var conquest = new PrototypeMatch();
            Check(conquest.Mode == PrototypeGameMode.Conquest &&
                conquest.Points[0].Owner == null, "Conquest starts neutral");
            conquest.RecordDeath(PrototypeTeam.Red);
            Check(Mathf.Approximately(conquest.RedTickets, 99f),
                "Conquest defender death consumes a ticket");

            var breakthrough = new PrototypeMatch();
            breakthrough.ConfigureSolo(PrototypeGameMode.Breakthrough, 100);
            Check(breakthrough.ActiveObjective == breakthrough.Points[0] &&
                breakthrough.Points.TrueForAll(p => p.Owner == PrototypeTeam.Red),
                "Breakthrough starts at A with all sectors held by Red");
            breakthrough.SetNetworkPoint("B", 100f, 0);
            breakthrough.Tick(.1f, null, Array.Empty<PrototypeBot>());
            Check(breakthrough.SectorIndex == 0,
                "an inactive sector cannot advance the front");
            breakthrough.SetNetworkPoint("B", -100f, 1);
            breakthrough.RecordDeath(PrototypeTeam.Red);
            breakthrough.RecordVehicleLoss(PrototypeTeam.Red);
            Check(Mathf.Approximately(breakthrough.RedTickets, 100f),
                "defender tickets are unlimited in Breakthrough");
            breakthrough.RecordDeath(PrototypeTeam.Blue);
            Check(Mathf.Approximately(breakthrough.BlueTickets, 99f),
                "attacker death consumes a ticket");
            for (int i = 0; i < breakthrough.Points.Count; i++)
            {
                string id = breakthrough.Points[i].Definition.Id;
                breakthrough.SetNetworkPoint(id, 100f, 0);
                breakthrough.Tick(.1f, null, Array.Empty<PrototypeBot>());
                Check(breakthrough.SectorIndex == i + 1,
                    "the captured sector advances exactly once: " + id);
                Check(breakthrough.BlueTickets <= breakthrough.TicketLimit,
                    "sector ticket reward respects the cap");
            }
            Check(breakthrough.Winner == PrototypeTeam.Blue,
                "capturing all five sectors wins for Blue");

            var defense = new PrototypeMatch();
            defense.ConfigureSolo(PrototypeGameMode.Breakthrough);
            defense.SetNetworkTickets(1f, 100f);
            defense.RecordDeath(PrototypeTeam.Blue);
            Check(defense.Winner == PrototypeTeam.Red,
                "exhausted attackers win for Red");
            VerifyCaptureThroughTicks();
            var overtime = new PrototypeMatch();
            overtime.Tick(900f, null, Array.Empty<PrototypeBot>());
            overtime.Tick(.1f, null, Array.Empty<PrototypeBot>());
            Check(overtime.BlueTickets < 100f && overtime.RedTickets < 100f,
                "Conquest overtime drains both teams after 900 seconds");
            overtime.SetNetworkPoint("A", -100f, 1);
            overtime.SetNetworkTickets(0f, 0f);
            Check(overtime.Winner == PrototypeTeam.Red,
                "Conquest tied tickets favor the team holding more points");
            Debug.Log("IRONFRONT solo match rules PASS: Conquest and Breakthrough");
        }

        private static void VerifyCaptureThroughTicks()
        {
            var marker = new GameObject("Solo match capture verifier");
            try
            {
                PrototypeBot attacker = marker.AddComponent<PrototypeBot>();
                var bots = new[] { attacker };
                var breakthrough = new PrototypeMatch();
                breakthrough.ConfigureSolo(PrototypeGameMode.Breakthrough);
                breakthrough.SetNetworkTickets(50f, 100f);
                Vector2 inactive = breakthrough.Points[1].Definition.Position;
                marker.transform.position = new Vector3(inactive.x, 0f, inactive.y);
                for (int i = 0; i < 400; i++)
                    breakthrough.Tick(.05f, null, bots);
                Check(breakthrough.SectorIndex == 0 &&
                    breakthrough.Points[1].Owner == PrototypeTeam.Red,
                    "standing on B cannot bypass active sector A");
                for (int sector = 0; sector < breakthrough.Points.Count; sector++)
                {
                    Vector2 location = breakthrough.Points[sector].Definition.Position;
                    marker.transform.position = new Vector3(location.x, 0f, location.y);
                    for (int i = 0; i < 400 && breakthrough.SectorIndex == sector; i++)
                        breakthrough.Tick(.05f, null, bots);
                    Check(breakthrough.SectorIndex == sector + 1,
                        "a present attacker captures sector " +
                        breakthrough.Points[sector].Definition.Id);
                    if (sector == 0)
                        Check(Mathf.Approximately(breakthrough.BlueTickets, 90f),
                            "first captured sector restores 40 tickets");
                }
                Check(breakthrough.Winner == PrototypeTeam.Blue,
                    "A through E capture ends a live Breakthrough simulation");

                var conquest = new PrototypeMatch();
                Vector2 point = conquest.Points[0].Definition.Position;
                marker.transform.position = new Vector3(point.x, 0f, point.y);
                for (int i = 0; i < 200; i++) conquest.Tick(.05f, null, bots);
                Check(conquest.Points[0].Owner == PrototypeTeam.Blue &&
                    conquest.RedTickets < 100f,
                    "Conquest capture starts opposing ticket bleed");
            }
            finally { UnityEngine.Object.DestroyImmediate(marker); }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
