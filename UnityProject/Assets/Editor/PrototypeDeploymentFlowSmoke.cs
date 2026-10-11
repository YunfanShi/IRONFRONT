using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Exercises consecutive deaths and each actual host deployment path in Play mode.
    // The temporary match changes are discarded when Play mode ends.
    public static class PrototypeDeploymentFlowSmoke
    {
        private const string MenuPath = "IRONFRONT/Verify Solo Deployment Flow";
        private static readonly FieldInfo RespawnAt = typeof(PrototypePlayer).GetField(
            "respawnAt", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo PlaceBeacon = typeof(PrototypePlayer).GetMethod(
            "PlaceBeacon", BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem(MenuPath, true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem(MenuPath)]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null || game.Player == null)
                throw new InvalidOperationException("Start SampleScene in Play mode first.");
            Check(RespawnAt != null && PlaceBeacon != null, "runtime test hooks exist");
            game.BeginBattle();
            game.Frontend?.Close();
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                // Freeze AI movement while the five deployment choices are checked.
                game.Match.ConfigureSolo(PrototypeGameMode.Conquest, 800);
                game.Match.SetNetworkPoint("A", 100f, 0);
                Check(!game.Player.TryDeploy("BASE", PrototypeInfantryClass.Assault),
                    "living player cannot deploy");

                DeployAfterDeath(game, "BASE", PrototypeInfantryClass.Assault);
                Check(Vector2.Distance(game.Player.MapPosition,
                    PrototypeLayout.BlueBase) < 35f, "base deployment position");

                DeployAfterDeath(game, "A", PrototypeInfantryClass.Medic);
                Check(game.Player.Class == PrototypeInfantryClass.Medic,
                    "point deployment applies selected class");

                string squad = FirstAvailable(game, "squad");
                DeployAfterDeath(game, squad, PrototypeInfantryClass.Recon);
                Check(game.Player.Class == PrototypeInfantryClass.Recon,
                    "squad deployment applies selected class");

                Vector2 beaconSite = PrototypeLayout.BlueBase + new Vector2(0f, 12f);
                Check(!PrototypeLayout.Collides(beaconSite, 1.5f), "beacon site is clear");
                PlaceBeacon.Invoke(game.Player, new object[] { beaconSite });
                Check(game.Player.BeaconAvailable, "recon beacon is available");
                DeployAfterDeath(game, "BEACON", PrototypeInfantryClass.Recon);
                Check(!game.Player.BeaconAvailable,
                    "beacon is consumed after deployment");

                string vehicle = FirstAvailable(game, "vehicle");
                DeployAfterDeath(game, vehicle, PrototypeInfantryClass.Engineer);
                Check(game.Player.CurrentVehicle != null &&
                    game.Player.CurrentVehicle.Occupant == game.Player,
                    "vehicle deployment occupies the driver seat");
                Debug.Log("IRONFRONT deployment flow PASS: five deaths, base, point, squad, beacon, vehicle");
            }
            catch (Exception error)
            {
                Debug.LogError("IRONFRONT deployment flow FAIL: " + error);
            }
            finally { Time.timeScale = previousTimeScale; }
        }

        private static void DeployAfterDeath(PrototypeRuntime game, string locationId,
            PrototypeInfantryClass selectedClass)
        {
            Check(game.Player.Alive, locationId + " starts alive");
            game.Player.TakeDamage(1000f);
            if (game.Player.IsDowned) game.Player.GiveUpImmediately();
            Check(!game.Player.Alive && !game.Player.IsDowned,
                locationId + " enters deployment state");
            Check(!game.Player.TryDeploy(locationId, selectedClass),
                locationId + " rejects early deployment");
            RespawnAt.SetValue(game.Player, Time.time - 1f);
            Check(!game.Player.TryDeploy("INVALID", selectedClass),
                "unknown deployment location is rejected");
            Check(game.Player.TryDeploy(locationId, selectedClass),
                locationId + " deployment succeeds");
            Check(game.Player.Alive, locationId + " restores player health");
        }

        private static string FirstAvailable(PrototypeRuntime game, string kind)
        {
            foreach (PrototypeDeployment.Location location in
                PrototypeDeployment.GetLocations(game, game.Player.Team, true))
                if (location.Kind == kind && location.Available)
                    return location.Id;
            throw new InvalidOperationException("No available " + kind + " deployment location");
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
