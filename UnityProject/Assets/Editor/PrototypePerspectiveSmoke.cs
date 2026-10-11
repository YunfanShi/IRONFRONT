using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypePerspectiveSmoke
    {
        private const string MenuPath = "IRONFRONT/Verify Player and Vehicle Cameras";
        private static int previewStep;

        [MenuItem(MenuPath, true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem(MenuPath)]
        private static void Verify()
        {
            try
            {
                PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null, "SampleScene has a player");
                if (!game.MatchStarted) game.BeginBattle();
                game.Frontend?.Close();
                PrototypePlayer player = game.Player;
                Check(player.Alive && player.CurrentVehicle == null, "player is on foot");
                Check(Vector3.Dot(player.ViewCamera.transform.position -
                    player.transform.position, player.transform.forward) < -1f,
                    "foot camera follows behind the soldier");
                PrototypeSoldierVisual visual = player.GetComponent<PrototypeSoldierVisual>();
                Check(visual != null && visual.HeldWeapon != null &&
                    visual.HeldWeapon.activeSelf, "third-person soldier carries active weapon");

                MethodInfo fire = typeof(PrototypePlayer).GetMethod("Fire",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Check(fire != null, "infantry fire exists");
                int before = UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>(
                    FindObjectsSortMode.None).Length;
                fire.Invoke(player, null);
                PrototypeBallisticProjectile bullet = null;
                foreach (PrototypeBallisticProjectile projectile in
                    UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>(
                        FindObjectsSortMode.None))
                    if (projectile.name == "Ballistic round" &&
                        Vector3.Distance(projectile.transform.position,
                            player.transform.position) < 3f)
                    { bullet = projectile; break; }
                Check(UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>(
                    FindObjectsSortMode.None).Length == before + 1 && bullet != null,
                    "shot leaves the soldier's weapon rather than the camera");
                Check(Vector3.Distance(bullet.transform.position,
                    player.ViewCamera.transform.position) > 1f,
                    "third-person shot does not start at the camera");

                MethodInfo enter = typeof(PrototypePlayer).GetMethod("EnterVehicle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo update = typeof(PrototypePlayer).GetMethod("UpdateVehicleCamera",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Check(enter != null && update != null, "vehicle camera transition exists");
                int checkedVehicles = 0;
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                {
                    if (vehicle.Team != player.Team || !(vehicle is PrototypeTankVehicle ||
                        vehicle is PrototypeScoutVehicle || vehicle is PrototypeTransportVehicle))
                        continue;
                    Check((bool)enter.Invoke(player, new object[] { vehicle }),
                        vehicle.VehicleName + " accepts the player");
                    update.Invoke(player, null);
                    Check(Vector3.Distance(player.ViewCamera.transform.position,
                        vehicle.CameraPosition) < .02f,
                        vehicle.VehicleName + " uses its first-person eye point");
                    Check(!visual.HeldWeapon.activeSelf,
                        vehicle.VehicleName + " hides the infantry weapon");
                    if (vehicle is PrototypeTransportVehicle transport)
                    {
                        Check(transport.TrySetPlayerSeat(player, 1),
                            "U8 changes to gunner seat");
                        update.Invoke(player, null);
                        Check(Vector3.Distance(player.ViewCamera.transform.position,
                            transport.CameraPosition) < .02f,
                            "U8 gunner has a first-person sight");
                    }
                    player.ForceExitVehicle();
                    Check(player.CurrentVehicle == null && visual.HeldWeapon.activeSelf,
                        vehicle.VehicleName + " exits to visible third-person infantry");
                    checkedVehicles++;
                }
                Check(checkedVehicles == 3, "T90, R4 and U8 cameras were checked");
                Debug.Log("IRONFRONT perspective PASS: visible shoulder-view infantry, " +
                    "weapon-origin shot, first-person T90/R4/U8 seats and exit transition");
            }
            catch (Exception error)
            {
                Debug.LogError("IRONFRONT perspective FAIL: " + error);
            }
        }

        [MenuItem("IRONFRONT/Preview Next Vehicle Sight", true)]
        private static bool CanPreview() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Preview Next Vehicle Sight")]
        private static void PreviewNext() => Preview(previewStep++ % 4);

        [MenuItem("IRONFRONT/Preview U8 Gunner Sight", true)]
        private static bool CanPreviewGunner() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Preview U8 Gunner Sight")]
        private static void PreviewGunner() => Preview(3);

        private static void Preview(int step)
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null || game.Player == null) return;
            if (!game.MatchStarted) game.BeginBattle();
            game.Frontend?.Close();
            PrototypePlayer player = game.Player;
            if (!player.Alive)
            {
                typeof(PrototypePlayer).GetMethod("Respawn",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null).Invoke(player, null);
                game.Frontend?.Close();
            }
            player.ForceExitVehicle();
            foreach (IPrototypeVehicle vehicle in game.Vehicles)
            {
                if (vehicle.Team != player.Team || !vehicle.Alive ||
                    (step == 0 && !(vehicle is PrototypeTankVehicle)) ||
                    (step == 1 && !(vehicle is PrototypeScoutVehicle)) ||
                    (step >= 2 && !(vehicle is PrototypeTransportVehicle))) continue;
                MethodInfo enter = typeof(PrototypePlayer).GetMethod("EnterVehicle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (!(bool)enter.Invoke(player, new object[] { vehicle })) return;
                if (step == 3 && vehicle is PrototypeTransportVehicle transport)
                    transport.TrySetPlayerSeat(player, 1);
                typeof(PrototypePlayer).GetMethod("UpdateVehicleCamera",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
                Debug.Log("IRONFRONT sight preview: " + vehicle.VehicleName +
                    (step == 3 ? " gunner" : " driver"));
                return;
            }
        }

        private static void Check(bool value, string expectation)
        {
            if (!value) throw new InvalidOperationException(expectation);
        }
    }
}
