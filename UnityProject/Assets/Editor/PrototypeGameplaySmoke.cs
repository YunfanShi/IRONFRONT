using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Run in Play mode after importing SampleScene. Exercises the actual
    // downed/deploy path and tank, without altering a saved scene or build.
    public static class PrototypeGameplaySmoke
    {
        private const string MenuPath = "IRONFRONT/Verify Tank and Respawn";
        private static PrototypeRuntime game;
        private static PrototypeTankVehicle tank;
        private static float readyAt;

        [MenuItem(MenuPath, true)]
        private static bool CanVerify() => UnityEditor.EditorApplication.isPlaying;

        [MenuItem(MenuPath)]
        private static void Verify()
        {
            game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null || game.Player == null)
                throw new InvalidOperationException("Start SampleScene in Play mode first.");
            game.BeginBattle();
            game.Frontend.Close();
            int tankIndex = -1;
            for (int i = 0; i < game.Vehicles.Count; i++)
                if (game.Vehicles[i] is PrototypeTankVehicle candidate &&
                    candidate.Team == game.Player.Team)
                { tank = candidate; tankIndex = i; break; }
            Check(tank != null && tank.Alive, "friendly T90 exists");
            Check(!game.Player.TryDeploy("VEHICLE-" + tankIndex,
                PrototypeInfantryClass.Assault), "living player cannot deploy");

            float tickets = game.Match.BlueTickets;
            game.Player.TakeDamage(1000f);
            Check(game.Player.IsDowned, "lethal infantry damage enters downed state");
            Check(Mathf.Approximately(game.Match.BlueTickets, tickets),
                "downed state does not consume a ticket");
            game.Player.RequestRescue();
            Check(game.Player.RescueCalled, "rescue call is recorded");
            game.Player.GiveUpImmediately();
            Check(!game.Player.IsDowned && !game.Player.Alive,
                "give up enters deployment state");
            Check(Mathf.Approximately(game.Match.BlueTickets, tickets - 1f),
                "elimination consumes exactly one ticket");
            Check(!game.Player.TryDeploy("VEHICLE-" + tankIndex,
                PrototypeInfantryClass.Assault), "early deployment is rejected");
            readyAt = Time.time + 4.6f;
            EditorApplication.update -= Finish;
            EditorApplication.update += Finish;
            Debug.Log("IRONFRONT smoke: downed, rescue, ticket and deployment wait passed");
        }

        private static void Finish()
        {
            if (EditorApplication.isPlaying && Time.time < readyAt) return;
            EditorApplication.update -= Finish;
            if (!EditorApplication.isPlaying || game == null || tank == null) return;
            try
            {
                int tankIndex = game.Vehicles.IndexOf(tank);
                Check(game.Player.TryDeploy("VEHICLE-" + tankIndex,
                    PrototypeInfantryClass.Assault), "tank seat deployment succeeds");
                Check(ReferenceEquals(game.Player.CurrentVehicle, tank) &&
                    tank.Driver == game.Player,
                    "player occupies tank driver seat");
                Vector2 start = tank.MapPosition;
                MethodInfo drive = typeof(PrototypeTankVehicle).GetMethod("Drive",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Check(drive != null, "tank drive control exists");
                for (int i = 0; i < 12; i++)
                    drive.Invoke(tank, new object[] { 1f, 0f, 0.05f });
                Check(Vector2.Distance(start, tank.MapPosition) > .1f,
                    "tank moves under throttle");
                float ticketsBeforeLoss = game.Match.BlueTickets;
                tank.TakeDamage(820f);
                Check(!tank.Alive && game.Player.CurrentVehicle == null,
                    "destroyed tank ejects driver");
                Check(!game.Player.Alive && !game.Player.IsDowned,
                    "tank destruction eliminates its driver");
                Check(Mathf.Approximately(game.Match.BlueTickets,
                    ticketsBeforeLoss - 1.5f),
                    "driver death and tank loss consume the expected tickets");
                Debug.Log("IRONFRONT smoke PASS: downed, manual tank deployment, drive and ejection");
            }
            catch (Exception exception)
            {
                Debug.LogError("IRONFRONT smoke FAIL: " + exception);
            }
        }

        private static void Check(bool condition, string expectation)
        {
            if (!condition) throw new InvalidOperationException(expectation);
        }
    }
}
