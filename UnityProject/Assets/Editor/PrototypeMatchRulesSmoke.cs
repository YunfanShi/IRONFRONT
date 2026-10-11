using System;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeMatchRulesSmoke
    {
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
            Debug.Log("IRONFRONT solo match rules PASS: Conquest and Breakthrough");
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
