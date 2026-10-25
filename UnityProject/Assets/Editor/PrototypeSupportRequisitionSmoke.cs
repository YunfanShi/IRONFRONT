using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeSupportRequisitionSmoke
    {
        [MenuItem("IRONFRONT/Verify Score Vehicle Support", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Score Vehicle Support")]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            PrototypeSupportRequisition support = null;
            GameObject obstruction = null;
            Vector3 oldPosition = Vector3.zero;
            Quaternion oldRotation = Quaternion.identity;
            IPrototypeVehicle oldVehicle = null;
            int oldSeat = -1;
            float blue = 0f, red = 0f;
            bool oldPause = false, oldNetwork = false;
            FieldInfo networkField = typeof(PrototypeRuntime).GetField("networkModeActive",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo enter = typeof(PrototypePlayer).GetMethod("EnterVehicleSeat",
                BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                Check(game != null && game.Player != null, "runtime and player exist");
                if (!game.MatchStarted)
                {
                    if (game.SoloTeamSize != 8) game.ConfigureSoloTeams(8);
                    game.BeginBattle();
                }
                game.Frontend?.Close();
                Check(game.Player.Alive && !game.Match.Winner.HasValue,
                    "verify in a live match with a living local player");
                oldPosition = game.Player.transform.position;
                oldRotation = game.Player.transform.rotation;
                oldVehicle = game.Player.CurrentVehicle;
                oldSeat = oldVehicle is IPrototypeSeatedVehicle occupied ? occupied.PlayerSeat : 0;
                game.Player.ForceExitVehicle();
                oldPause = game.PauseOpen; oldNetwork = (bool)networkField.GetValue(game);
                SetPause(game, false); networkField.SetValue(game, false);
                blue = game.Match.BlueTickets; red = game.Match.RedTickets;
                var originalIds = new List<IPrototypeVehicle>(game.Vehicles);
                // A separate fixture ledger funds the requisitions without
                // changing the player's real earned score. Combat/capture
                // earning is verified by the score smoke test separately.
                var ledger = new PrototypeScoreSystem(game);
                PrototypeScoreSystem.Entry wallet = ledger.Get(game.Player);
                support = new PrototypeSupportRequisition(game, ledger);
                Check(support.Catalog.Count == 5 && support.ActiveCount(game.Player.Team) == 0,
                    "ground, anti-air and both helicopter offers exist");
                Check(!support.TryRequest(PrototypeSupportKind.Motorcycle, out _) && wallet.Credits == 0,
                    "insufficient funds never create or debit a request");
                typeof(PrototypeScoreSystem.Entry).GetProperty("Credits").SetValue(wallet, 3000);
                typeof(PrototypeScoreSystem.Entry).GetProperty("Total").SetValue(wallet, 3000);
                SetPause(game, true);
                Check(!support.TryRequest(PrototypeSupportKind.Motorcycle, out _) && wallet.Credits == 3000,
                    "paused request is rejected without debit");
                SetPause(game, false); networkField.SetValue(game, true);
                Check(!support.TryRequest(PrototypeSupportKind.Motorcycle, out _) && wallet.Credits == 3000,
                    "LAN requests are rejected while dynamic vehicle IDs lack a network descriptor");
                networkField.SetValue(game, false);
                Check(!support.TryRequest((PrototypeSupportKind)999, out _) && wallet.Credits == 3000,
                    "invalid catalog ID cannot spend credits");

                obstruction = BlockSupportBays(game.Player.Team);
                Check(!support.TryRequest(PrototypeSupportKind.Motorcycle, out _) && wallet.Credits == 3000,
                    "runtime physical obstacles in every bay reject before debit");
                UnityEngine.Object.DestroyImmediate(obstruction); obstruction = null;

                Check(support.TryRequest(PrototypeSupportKind.Motorcycle, out _) && wallet.Credits == 2800 &&
                    wallet.Total == 3000 && support.Pending, "one order debits budget while total score survives");
                Check(!support.TryRequest(PrototypeSupportKind.Motorcycle, out _) && wallet.Credits == 2800,
                    "repeated order does not debit twice");
                support.Tick(5.9f);
                Check(support.Pending && game.Vehicles.Count == originalIds.Count,
                    "physical delivery waits the full six seconds");
                SetPause(game, true); float pendingSeconds = support.DeliveryRemaining;
                support.Tick(10f);
                Check(support.Pending && Mathf.Approximately(support.DeliveryRemaining, pendingSeconds),
                    "pause does not advance delivery or cooldown");
                SetPause(game, false); support.Tick(.1f);
                var motorcycle = support.LastDelivered as PrototypeCombinedArmsVehicle;
                Check(!support.Pending && motorcycle != null && motorcycle.Alive &&
                    motorcycle.Team == game.Player.Team && motorcycle.Kind == PrototypeVehicleKind.Motorcycle &&
                    motorcycle.SeatCount == 2 && !motorcycle.RespawnEnabled,
                    "ground support is a living, correct-team seated vehicle with no free respawn");
                Check(game.Vehicles.Count == originalIds.Count + 1 &&
                    ReferenceEquals(game.Vehicles[originalIds.Count], motorcycle), "new support appends one stable ID");
                Check((bool)enter.Invoke(game.Player, new object[] { motorcycle, 0 }) &&
                    ReferenceEquals(game.Player.CurrentVehicle, motorcycle) && motorcycle.SeatCanDrive,
                    "real player boarding controls the delivered vehicle");
                int creditsOnBoard = wallet.Credits;
                Check(!support.TryRequest(PrototypeSupportKind.Ifv, out _) && wallet.Credits == creditsOnBoard,
                    "seated player cannot order a second support vehicle");
                game.Player.ForceExitVehicle();
                Check(!support.TryRequest(PrototypeSupportKind.Ifv, out _) && wallet.Credits == 2800,
                    "personal cooldown rejects without debit");
                support.Tick(60f);

                Check(support.TryRequest(PrototypeSupportKind.ReconHelicopter, out _) && wallet.Credits == 2200,
                    "aircraft can be requested after cooldown");
                obstruction = BlockSupportBays(game.Player.Team);
                support.Tick(6f);
                Check(!support.Pending && wallet.Credits == 2800 && wallet.Total == 3000 &&
                    support.CooldownReadyIn == 0f && game.Vehicles.Count == originalIds.Count + 1,
                    "late blocked delivery refunds exactly once and spawns no vehicle");
                support.Tick(6f);
                Check(wallet.Credits == 2800, "failed delivery cannot repeatedly refund");
                UnityEngine.Object.DestroyImmediate(obstruction); obstruction = null;
                Check(support.TryRequest(PrototypeSupportKind.ReconHelicopter, out _),
                    "refunded order may be retried immediately");
                support.Tick(6f);
                var helicopter = support.LastDelivered as PrototypeCombinedArmsVehicle;
                Check(helicopter != null && helicopter.Alive && helicopter.Team == game.Player.Team &&
                    helicopter.Kind == PrototypeVehicleKind.ReconHelicopter && helicopter.SeatCount == 3 &&
                    helicopter.ReconRadar != null && !helicopter.RespawnEnabled,
                    "air support has the real three-seat radar helicopter systems");
                support.Tick(60f); int cappedCredits = wallet.Credits;
                Check(support.ActiveCount(game.Player.Team) == 2 &&
                    !support.TryRequest(PrototypeSupportKind.Ifv, out _) && wallet.Credits == cappedCredits,
                    "two active support vehicles per team limit cannot debit a third");
                motorcycle.TakeDamage(10000f); support.Tick(1f);
                typeof(PrototypeCombinedArmsVehicle).GetField("respawnAt",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(motorcycle, -1f);
                typeof(PrototypeCombinedArmsVehicle).GetMethod("Update",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(motorcycle, null);
                Check(!motorcycle.Alive && !motorcycle.enabled && support.ActiveCount(game.Player.Team) == 1,
                    "destroyed paid vehicle never autorespawns and releases the active cap");
                Check(support.TryRequest(PrototypeSupportKind.Ifv, out _), "destroyed support frees a request slot");
                support.Reset();
                Check(!support.Pending && wallet.Credits == cappedCredits && support.CooldownReadyIn == 0f &&
                    support.LastDelivered == null && !helicopter.Alive && !helicopter.gameObject.activeSelf,
                    "reset refunds pending support and retires already delivered support");
                for (int i = 0; i < originalIds.Count; i++)
                    Check(ReferenceEquals(originalIds[i], game.Vehicles[i]), "all existing IDs survive support lifecycle");
                Check(game.Vehicles.Count == originalIds.Count + 2,
                    "retired support leaves stable dead IDs until scene cleanup");
                Debug.Log("IRONFRONT support PASS: budget debit once, full six-second delivery, pause, " +
                    "actual ground/air models and boarding, safe bay and late refund, solo eligibility, " +
                    "sixty-second cooldown, two-vehicle team cap, no autorespawn, reset and stable vehicle IDs");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT support FAIL: " + error); }
            finally
            {
                if (obstruction != null) UnityEngine.Object.DestroyImmediate(obstruction);
                support?.Dispose();
                if (game != null && game.Player != null)
                {
                    game.Player.ForceExitVehicle();
                    game.Player.transform.SetPositionAndRotation(oldPosition, oldRotation);
                    if (oldVehicle != null && oldVehicle.Alive && oldSeat >= 0)
                        enter.Invoke(game.Player, new object[] { oldVehicle, oldSeat });
                    networkField.SetValue(game, oldNetwork); SetPause(game, oldPause);
                    game.Match.SetNetworkTickets(blue, red);
                }
            }
        }

        private static GameObject BlockSupportBays(PrototypeTeam team)
        {
            Vector2 origin = PrototypeSupportRequisition.BaseDropPosition(team);
            float sign = team == PrototypeTeam.Blue ? 1f : -1f;
            Vector2 center = origin + new Vector2(sign * 36f, sign * 9f);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "Support smoke temporary bay obstruction";
            cover.transform.position = new Vector3(center.x,
                PrototypeLayout.HeightAt(center.x, center.y) + 4f, center.y);
            cover.transform.localScale = new Vector3(105f, 8f, 50f);
            Physics.SyncTransforms(); return cover;
        }
        private static void SetPause(PrototypeRuntime game, bool value) =>
            typeof(PrototypeRuntime).GetProperty("PauseOpen").SetValue(game, value);
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); }
    }
}
