using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Runs against the live prototype in Play mode without a second computer.
    public static class PrototypeStabilitySmoke
    {
        private const string MenuPath = "IRONFRONT/Verify Reconnect and Red Vehicles";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem(MenuPath, true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem(MenuPath)]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            Check(game != null && game.Player != null, "start SampleScene in Play mode");

            FieldInfo activeField = typeof(PrototypeRuntime).GetField("networkModeActive", PrivateInstance);
            Check(activeField != null && !(bool)activeField.GetValue(game),
                "run the smoke check in an offline session");
            game.BeginBattle();
            game.Frontend.Close();

            PrototypeLanClient lan = (PrototypeLanClient)typeof(PrototypeRuntime)
                .GetField("lan", PrivateInstance).GetValue(game);
            PropertyInfo roomProperty = typeof(PrototypeLanClient).GetProperty("LastRoom");
            MethodInfo syncRoster = typeof(PrototypeRuntime)
                .GetMethod("SyncRemoteRoster", PrivateInstance);
            Check(lan != null && roomProperty != null && syncRoster != null,
                "room test hooks are available");
            PrototypeLanRoom originalRoom = lan.LastRoom;
            try
            {
                var member = new PrototypeLanRosterMember {
                    id = 79, name = "Reconnect smoke", team = "blue", connected = true
                };
                roomProperty.SetValue(lan, new PrototypeLanRoom {
                    roster = new[] { member }
                });
                syncRoster.Invoke(game, null);
                Check(game.RemotePlayers.Count == 1, "connected guest has one actor");
                PrototypeRemotePlayer actor = game.RemotePlayers[0];
                actor.TakeDamage(23f);
                actor.transform.position += Vector3.right * 2f;
                float health = actor.Health;
                Vector3 position = actor.transform.position;

                member.connected = false;
                syncRoster.Invoke(game, null);
                Check(game.RemotePlayers.Count == 1 &&
                    ReferenceEquals(game.RemotePlayers[0], actor) && !actor.IsConnected,
                    "disconnected guest retains the same actor");
                actor.ApplyInput(1f, 0f, 0f, 0f, false, false, 0.1f);
                Check(actor.transform.position == position,
                    "disconnected guest cannot move");

                member.connected = true;
                syncRoster.Invoke(game, null);
                Check(game.RemotePlayers.Count == 1 &&
                    ReferenceEquals(game.RemotePlayers[0], actor) && actor.IsConnected &&
                    Mathf.Approximately(actor.Health, health) &&
                    actor.transform.position == position,
                    "resumed guest keeps actor, health and position");

                game.Player.SetTeam(PrototypeTeam.Red);
                PrototypeScoutVehicle scout = null;
                PrototypeTransportVehicle transport = null;
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                {
                    if (vehicle.Team != PrototypeTeam.Red) continue;
                    if (vehicle is PrototypeScoutVehicle redScout) scout = redScout;
                    if (vehicle is PrototypeTransportVehicle redTransport) transport = redTransport;
                }
                Check(scout != null && transport != null, "red R4 and U8 exist");
                Check(scout.TrySetDriver(game.Player) && scout.Driver == game.Player,
                    "red player takes over AI R4");
                scout.RemoveDriver(game.Player);
                Check(scout.Driver == null, "R4 returns to AI after exit");
                Check(transport.TrySetPlayerSeat(game.Player, 0) &&
                    transport.Occupant == game.Player,
                    "red player takes over AI U8 driver seat");
                transport.RemoveOccupant(game.Player);
                Check(transport.TrySetPlayerSeat(game.Player, 1) &&
                    transport.Occupant == game.Player,
                    "red player takes over AI U8 gunner seat");
                transport.RemoveOccupant(game.Player);
                Debug.Log("IRONFRONT stability smoke PASS: guest reconnect state and red R4/U8 takeover");
            }
            catch (Exception exception)
            {
                Debug.LogError("IRONFRONT stability smoke FAIL: " + exception);
            }
            finally
            {
                roomProperty.SetValue(lan, new PrototypeLanRoom {
                    roster = Array.Empty<PrototypeLanRosterMember>()
                });
                syncRoster.Invoke(game, null);
                roomProperty.SetValue(lan, originalRoom);
            }
        }

        private static void Check(bool condition, string expectation)
        {
            if (!condition) throw new InvalidOperationException(expectation);
        }
    }
}
