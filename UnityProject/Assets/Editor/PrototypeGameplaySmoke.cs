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
            // The UI Toolkit lobby can be present before the legacy frontend
            // controller has been created. The vehicle check does not need it.
            game.Frontend?.Close();
            int tankIndex = -1;
            for (int i = 0; i < game.Vehicles.Count; i++)
                if (game.Vehicles[i] is PrototypeTankVehicle candidate &&
                    candidate.Team == game.Player.Team)
                { tank = candidate; tankIndex = i; break; }
            Check(tank != null && tank.Alive,
                $"friendly T90 exists (player team {game.Player.Team}, " +
                $"vehicles {game.Vehicles.Count}, tank health {(tank == null ? -1f : tank.Health)})");
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
                for (int i = 0; i < 60; i++)
                    drive.Invoke(tank, new object[] { 1f, 0f, 1f / 240f });
                Check(tank.Speed > 0f && Vector2.Distance(start, tank.MapPosition) > .1f,
                    "tank starts moving from rest at 240 FPS");
                start = tank.MapPosition;
                for (int i = 0; i < 12; i++)
                    drive.Invoke(tank, new object[] { 1f, 0f, 0.05f });
                Check(Vector2.Distance(start, tank.MapPosition) > .1f,
                    "tank moves under throttle");
                MethodInfo controls = typeof(PrototypeTankVehicle).GetMethod(
                    "ApplyPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(controls != null, "tank player input control exists");
                Vector3 roadPosition = tank.transform.position;
                Quaternion roadRotation = tank.transform.rotation;
                Vector2 corner = new Vector2(-247.8f, -152.3f);
                Vector2 away = corner + Vector2.up * .15f;
                Check(!PrototypeLayout.Collides(corner, 3.5f) &&
                    !PrototypeLayout.Collides(away, 3.5f) &&
                    PrototypeLayout.LineBlocked(corner, away, 3.5f),
                    "corner reproduces the padded line false positive");
                tank.transform.position = new Vector3(corner.x,
                    PrototypeLayout.HeightAt(corner.x, corner.y) + .05f, corner.y);
                tank.transform.rotation = Quaternion.identity;
                controls.Invoke(tank, new object[] { 1f, 0f, false, false, .05f });
                Check(tank.MapPosition.y > corner.y + .01f,
                    "tank drives away from a clear corner with cursor unlocked");
                tank.transform.position = roadPosition;
                tank.transform.rotation = roadRotation;
                PrototypeTankVisual visual = tank.GetComponent<PrototypeTankVisual>();
                Check(visual != null && visual.Turret != null &&
                    visual.BarrelPivot != null && visual.CannonMuzzle != null,
                    "tank has independent turret, elevating barrel and muzzle");
                visual.Turret.localRotation = Quaternion.identity;
                visual.BarrelPivot.localRotation = Quaternion.identity;
                Quaternion beforeTurn = visual.Turret.rotation;
                Vector3 aim = visual.Turret.position + visual.Turret.right * 100f +
                    Vector3.up * 30f;
                visual.AimAt(aim, .05f);
                float yawStep = Quaternion.Angle(beforeTurn, visual.Turret.rotation);
                float pitchStep = Mathf.Abs(Mathf.DeltaAngle(0f,
                    visual.BarrelPivot.localEulerAngles.x));
                Check(yawStep > .1f && yawStep <= 2.11f &&
                    pitchStep > .1f && pitchStep <= 1.21f,
                    "turret and barrel slew independently within their speed limits");
                int shellsBefore = UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().Length;
                controls.Invoke(tank, new object[] { 0f, 0f, true, false, .05f });
                Check(UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().Length ==
                    shellsBefore + 1,
                    "first click with cursor unlocked launches a physical shell");
                controls.Invoke(tank, new object[] { 0f, 0f, true, false, .05f });
                Check(UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().Length ==
                    shellsBefore + 1 && tank.CannonReadyIn > 0f,
                    "cannon cooldown prevents duplicate shells and exposes reload time");
                PrototypeBallisticProjectile shell = null;
                foreach (PrototypeBallisticProjectile round in
                    UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    if (round.name == "T90 cannon shell" &&
                        Vector3.Distance(round.transform.position,
                            visual.CannonMuzzle.position) < .5f)
                    { shell = round; break; }
                FieldInfo velocity = typeof(PrototypeBallisticProjectile).GetField("velocity",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Check(shell != null && velocity != null &&
                    Vector3.Angle((Vector3)velocity.GetValue(shell),
                        visual.CannonMuzzle.forward) < .1f,
                    "shell leaves along the actual barrel direction");
                CheckMachineGunTraverse<PrototypeScoutVehicle>("turret",
                    "barrelPivot", "AimTurretAt", 4.76f, 3.51f);
                CheckMachineGunTraverse<PrototypeTransportVehicle>("gunMount",
                    "barrelPivot", "AimGunAt", 5.51f, 4.01f);
                float ticketsBeforeLoss = game.Match.BlueTickets;
                tank.TakeDamage(820f);
                Check(!tank.Alive && game.Player.CurrentVehicle == null,
                    "destroyed tank ejects driver");
                Check(!game.Player.Alive && !game.Player.IsDowned,
                    "tank destruction eliminates its driver");
                Check(Mathf.Approximately(game.Match.BlueTickets,
                    ticketsBeforeLoss - 1.5f),
                    "driver death and tank loss consume the expected tickets");
                Debug.Log("IRONFRONT smoke PASS: deployment, unlocked tank controls, corner drive, cannon cooldown and ejection");
            }
            catch (Exception exception)
            {
                Debug.LogError("IRONFRONT smoke FAIL: " + exception);
            }
        }

        private static void CheckMachineGunTraverse<T>(string mountField,
            string pivotField, string aimMethod, float maxYaw, float maxPitch)
            where T : Component, IPrototypeVehicle, IPrototypeWeaponAim
        {
            T vehicle = null;
            foreach (IPrototypeVehicle candidate in game.Vehicles)
                if (candidate is T gun && gun.Team == game.Player.Team)
                { vehicle = gun; break; }
            Check(vehicle != null && vehicle.WeaponMuzzle != null,
                typeof(T).Name + " has a muzzle");
            Type type = typeof(T);
            Transform mount = (Transform)type.GetField(mountField,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(vehicle);
            Transform pivot = (Transform)type.GetField(pivotField,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(vehicle);
            MethodInfo aimAt = type.GetMethod(aimMethod,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(mount != null && pivot != null && aimAt != null &&
                vehicle.WeaponMuzzle.IsChildOf(pivot),
                typeof(T).Name + " has an elevating weapon assembly");
            mount.localRotation = Quaternion.identity;
            pivot.localRotation = Quaternion.identity;
            Quaternion beforeTurn = mount.rotation;
            Vector3 aim = mount.position + mount.right * 100f + Vector3.up * 30f;
            aimAt.Invoke(vehicle, new object[] { aim, .05f });
            float yawStep = Quaternion.Angle(beforeTurn, mount.rotation);
            float pitchStep = Mathf.Abs(Mathf.DeltaAngle(0f, pivot.localEulerAngles.x));
            Check(yawStep > .1f && yawStep <= maxYaw &&
                pitchStep > .1f && pitchStep <= maxPitch,
                typeof(T).Name + " tracks aim with independent slew limits");
        }

        private static void Check(bool condition, string expectation)
        {
            if (!condition) throw new InvalidOperationException(expectation);
        }
    }
}
