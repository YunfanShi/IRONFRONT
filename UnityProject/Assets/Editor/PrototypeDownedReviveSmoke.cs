using System;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeDownedReviveSmoke
    {
        private const string MenuPath = "IRONFRONT/Verify Downed View and Revive";
        private static PrototypeRuntime game;
        private static float startedAt;
        private static bool firstPersonBefore;

        [MenuItem(MenuPath, true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem(MenuPath)]
        private static void Verify()
        {
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null, "SampleScene Play has a player");
                if (!game.MatchStarted)
                {
                    game.ConfigureSoloTeams(8);
                    game.Match.ConfigureSolo(PrototypeGameMode.Conquest);
                    game.BeginBattle();
                    game.Frontend.Close();
                }
                game.Player.ForceExitVehicle();
                firstPersonBefore = game.Player.IsFirstPerson;
                PrototypeBot medic = null;
                foreach (PrototypeBot bot in game.Bots)
                    if (bot.Team == game.Player.Team &&
                        bot.Class == PrototypeInfantryClass.Medic)
                    { medic = bot; break; }
                Check(medic != null, "friendly medic exists");
                medic.transform.position = game.Player.transform.position + Vector3.right * 2f;
                game.Player.TakeDamage(1000f);
                Check(game.Player.IsDowned && !game.Player.Alive,
                    "lethal damage enters the downed state");
                Check(Mathf.Abs(game.Player.ViewCamera.transform.localPosition.y - .42f) < .01f,
                    "downed camera is near ground level");
                game.Player.AdjustDownedView(200f, 0f);
                Check(Mathf.Abs(game.Player.DownedViewYaw - 85f) < .01f,
                    "right look is limited to 85 degrees");
                game.Player.AdjustDownedView(-400f, 0f);
                Check(Mathf.Abs(game.Player.DownedViewYaw + 85f) < .01f,
                    "left look is limited to 85 degrees");
                game.Player.AdjustDownedView(85f, 0f);
                Check(!game.Player.TryRevive(medic) && game.Player.IsDowned,
                    "medic cannot revive instantly");
                startedAt = Time.time;
                EditorApplication.update -= Finish;
                EditorApplication.update += Finish;
            }
            catch (Exception error) { Fail(error); }
        }

        private static void Finish()
        {
            try
            {
                Check(EditorApplication.isPlaying && game != null,
                    "Play continues during the revive check");
                float elapsed = Time.time - startedAt;
                if (elapsed < 3.8f)
                {
                    Check(game.Player.IsDowned,
                        "player stays down during the four second rescue");
                    return;
                }
                if (elapsed < 4.2f) return;
                if (!game.Player.Alive && elapsed < 5.5f) return;
                Check(game.Player.Alive && !game.Player.IsDowned,
                    "medic revives the player after roughly four seconds");
                Check(game.Player.IsFirstPerson == firstPersonBefore,
                    "revival preserves the infantry perspective");
                if (firstPersonBefore)
                    Check(Vector3.Distance(game.Player.ViewCamera.transform.position,
                        game.Player.transform.position + Vector3.up * 1.65f) < .1f,
                        "revived first-person camera returns to standing eye height");
                else
                    Check(Vector3.Distance(game.Player.ViewCamera.transform.position,
                        game.Player.transform.position) > 2f &&
                        Vector3.Dot(game.Player.ViewCamera.transform.position -
                            game.Player.transform.position,
                            game.Player.transform.forward) < -1f,
                        "revived third-person camera returns behind the soldier");
                Check(!game.Frontend.IsDownedScreen, "downed screen closes after revival");
                Debug.Log("IRONFRONT downed view and revive PASS: ground camera, " +
                    "170-degree look arc, continuous four-second medic rescue");
                EditorApplication.update -= Finish;
            }
            catch (Exception error) { Fail(error); }
        }

        private static void Fail(Exception error)
        {
            EditorApplication.update -= Finish;
            Debug.LogError("IRONFRONT downed view and revive FAIL: " + error);
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
