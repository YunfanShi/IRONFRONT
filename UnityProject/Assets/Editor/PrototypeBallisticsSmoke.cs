using System;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Play-mode check of real projectile movement and swept collision.
    public static class PrototypeBallisticsSmoke
    {
        private const string MenuPath = "IRONFRONT/Verify Ballistics";
        private static PrototypeBallisticProjectile pistol;
        private static PrototypeBallisticProjectile shell;
        private static PrototypeBallisticProjectile blocked;
        private static GameObject obstacle;
        private static float startedAt;

        [MenuItem(MenuPath, true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem(MenuPath)]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null) throw new InvalidOperationException("Start SampleScene in Play mode first.");
            if (!game.MatchStarted) game.BeginBattle();
            game.Frontend.Close();
            PrototypeWeaponDefinition sidearm = PrototypeInfantryRoles.GetWeapon(PrototypeWeaponId.Pistol);
            PrototypeWeaponDefinition sniper = PrototypeInfantryRoles.GetWeapon(PrototypeWeaponId.Sniper);
            Check(sidearm.MuzzleVelocity > 0f &&
                sniper.MuzzleVelocity > sidearm.MuzzleVelocity,
                "weapons have distinct positive muzzle velocities");
            Vector3 start = new Vector3(0f, 70f, 0f);
            pistol = PrototypeBallisticProjectile.LaunchBullet(game, PrototypeTeam.Blue,
                null, start, Vector3.forward, sidearm);
            shell = PrototypeBallisticProjectile.LaunchShell(game, PrototypeTeam.Blue,
                null, start + Vector3.right * 5f, Vector3.forward, 170f, 230f);
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Ballistics smoke obstacle";
            obstacle.transform.position = start + Vector3.left * 5f + Vector3.forward * 12f;
            blocked = PrototypeBallisticProjectile.LaunchBullet(game, PrototypeTeam.Blue,
                null, start + Vector3.left * 5f, Vector3.forward, sidearm);
            startedAt = Time.time;
            EditorApplication.update -= Finish;
            EditorApplication.update += Finish;
        }

        private static void Finish()
        {
            if (EditorApplication.isPlaying && Time.time - startedAt < .25f) return;
            EditorApplication.update -= Finish;
            try
            {
                Check(EditorApplication.isPlaying, "game is still running");
                Check(pistol != null && shell != null,
                    "unobstructed rounds remain in flight");
                Check(blocked == null, "swept collision stops a fast bullet at a thin obstacle");
                float elapsed = Time.time - startedAt;
                Check(pistol.transform.position.y < 69.75f &&
                    shell.transform.position.y < 69.75f,
                    "gravity lowers both bullet and shell trajectories");
                Check(shell.transform.position.z > pistol.transform.position.z + elapsed * 5f,
                    "projectiles advance according to muzzle velocity");
                Debug.Log("IRONFRONT ballistics smoke PASS: travel, drop, speed, swept impact");
            }
            catch (Exception exception)
            {
                Debug.LogError("IRONFRONT ballistics smoke FAIL: " + exception);
            }
            finally
            {
                if (pistol != null) UnityEngine.Object.Destroy(pistol.gameObject);
                if (shell != null) UnityEngine.Object.Destroy(shell.gameObject);
                if (blocked != null) UnityEngine.Object.Destroy(blocked.gameObject);
                if (obstacle != null) UnityEngine.Object.Destroy(obstacle);
            }
        }

        private static void Check(bool condition, string expectation)
        {
            if (!condition) throw new InvalidOperationException(expectation);
        }
    }
}
