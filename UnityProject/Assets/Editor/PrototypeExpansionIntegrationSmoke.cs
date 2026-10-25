using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeExpansionIntegrationSmoke
    {
        [MenuItem("IRONFRONT/Verify 0.48 Scene and Seat Deployment", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify 0.48 Scene and Seat Deployment")]
        private static void Verify()
        {
            try
            {
                PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null, "runtime and player exist");
                if (!game.MatchStarted) game.BeginBattle();
                game.Frontend?.Close();
                PrototypePlayer player = game.Player;
                player.ForceExitVehicle();
                Check(game.Vehicles.Count == 18, "each team has all nine vehicle kinds");
                foreach (PrototypeTeam team in new[] { PrototypeTeam.Blue, PrototypeTeam.Red })
                {
                    int count = 0, seats = 0;
                    foreach (IPrototypeVehicle vehicle in game.Vehicles)
                    {
                        if (vehicle.Team != team) continue;
                        count++;
                        Check(vehicle is IPrototypeSeatedVehicle, vehicle.VehicleName + " has seats");
                        IPrototypeSeatedVehicle seated = (IPrototypeSeatedVehicle)vehicle;
                        seats += seated.SeatCount;
                        float radius = vehicle is PrototypeCombinedArmsVehicle combined ?
                            combined.Clearance : vehicle is PrototypeTankVehicle ? 3.5f : 2.1f;
                        Check(!PrototypeLayout.Collides(vehicle.MapPosition, radius),
                            vehicle.VehicleName + " has a clear initial parking bay");
                    }
                    Check(count == 9 && seats == 28, team + " has nine vehicles and 28 seats");
                    foreach (PrototypeVehicleKind kind in Enum.GetValues(typeof(PrototypeVehicleKind)))
                    {
                        int matching = 0;
                        foreach (IPrototypeVehicle vehicle in game.Vehicles)
                            if (vehicle is PrototypeCombinedArmsVehicle combined &&
                                combined.Team == team && combined.Kind == kind) matching++;
                        Check(matching == 1, team + " has exactly one " + kind);
                    }
                }

                int selectedIndex = -1;
                PrototypeCombinedArmsVehicle selected = null;
                for (int i = 0; i < game.Vehicles.Count; i++)
                    if (game.Vehicles[i] is PrototypeCombinedArmsVehicle combined &&
                        combined.Team == player.Team && combined.Kind == PrototypeVehicleKind.Ifv)
                    { selectedIndex = i; selected = combined; break; }
                Check(selected != null, "friendly IFV exists");
                string passengerId = "VEHICLE-" + selectedIndex + "-2";
                Check(PrototypeDeployment.TryResolve(game, player.Team, passengerId,
                    true, out _, out IPrototypeVehicle resolved) && ReferenceEquals(resolved, selected),
                    "deployment resolves an explicit passenger seat");
                Check(PrototypeDeployment.TryResolve(game, player.Team,
                    "VEHICLE-" + selectedIndex, true, out _, out resolved),
                    "historical driver deployment IDs remain valid");
                Check(!PrototypeDeployment.TryResolve(game, player.Team,
                    "VEHICLE-" + selectedIndex + "-4", true, out _, out _),
                    "out-of-range seat deployment is rejected");

                // This check bypasses only the already-covered 4.5s deployment
                // timer; it exercises the actual loadout, respawn and seat path.
                player.TakeDamage(1000f);
                player.GiveUpImmediately();
                typeof(PrototypePlayer).GetField("respawnAt",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, Time.time - 1f);
                Check(player.TryDeploy(passengerId, PrototypeInfantryClass.Engineer),
                    "deployment accepts an available passenger seat");
                Check(ReferenceEquals(player.CurrentVehicle, selected) &&
                    selected.PlayerSeat == 2 && !selected.SeatCanDrive && !selected.SeatCanFire,
                    "passenger deployment cannot drive or fire");
                Check(Vector3.Distance(player.ViewCamera.transform.position,
                    selected.CameraPosition) < .05f || !player.IsFirstPerson,
                    "passenger camera follows the selected seat");
                Check(player.TrySwitchVehicleSeat(0) && selected.SeatCanDrive && selected.SeatCanFire,
                    "F1 returns the IFV passenger to driver and primary weapon");
                Check(selected.GetSeatOccupant(2) == null,
                    "seat transfer releases the former passenger slot");
                player.ForceExitVehicle();
                game.Frontend?.Close();
                Check(selected.Occupant == null && selected.GetSeatOccupant(0) == null,
                    "exit releases the deployed driver slot");
                Debug.Log("IRONFRONT 0.48 integration PASS: 18 vehicles, 56 seats, " +
                    "clear bays, explicit passenger deployment, permissions, transfer and exit");
            }
            catch (Exception error)
            {
                Debug.LogError("IRONFRONT 0.48 integration FAIL: " + error);
            }
        }

        private static void Check(bool value, string expectation)
        {
            if (!value) throw new InvalidOperationException(expectation);
        }
    }
}
