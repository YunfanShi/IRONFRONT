using System;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeVisionSmoke
    {
        private const string MenuPath = "IRONFRONT/Verify Infantry Vision";

        [MenuItem(MenuPath, true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem(MenuPath)]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null)
                throw new InvalidOperationException("Start SampleScene in Play mode first.");
            if (!game.MatchStarted)
            {
                game.ConfigureSoloTeams(8);
                game.Match.ConfigureSolo(PrototypeGameMode.Conquest);
                game.BlueCommander.Replan();
                game.RedCommander.Replan();
                game.BeginBattle();
                game.Frontend.Close();
            }
            Check(game.Bots.Count == 15, "8v8 match contains fifteen AI soldiers");

            PrototypeBot bot = game.Bots[0];
            Vector2 origin = bot.MapPosition;
            Vector2 direction = Vector2.zero;
            bool found = false;
            for (int yaw = 0; yaw < 360; yaw += 10)
            {
                float radians = yaw * Mathf.Deg2Rad;
                Vector2 candidate = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
                if (PrototypeLayout.LineBlocked(origin, origin + candidate * 8f, 0.1f) ||
                    PrototypeLayout.LineBlocked(origin, origin - candidate * 8f, 0.1f)) continue;
                direction = candidate;
                found = true;
                break;
            }
            if (!found) throw new InvalidOperationException("No clear front/back sight lines for test.");

            Vector2 front = origin + direction * 8f;
            Vector2 rear = origin - direction * 8f;
            Quaternion original = bot.transform.rotation;
            float forwardYaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            try
            {
                bot.transform.rotation = Quaternion.Euler(0f, forwardYaw, 0f);
                Check(bot.CanSeePosition(front) && bot.IsAimedAtPosition(front),
                    "front target is visible and aligned");
                Check(!bot.CanSeePosition(rear) && !bot.IsAimedAtPosition(rear),
                    "unturned soldier cannot see or fire at a rear target");

                bot.transform.rotation = Quaternion.Euler(0f, forwardYaw + 60f, 0f);
                Check(bot.CanSeePosition(front) && !bot.IsAimedAtPosition(front),
                    "peripheral target is visible but needs aiming before firing");
                bot.transform.rotation = Quaternion.Euler(0f, forwardYaw + 80f, 0f);
                Check(!bot.CanSeePosition(front), "outside sight cone is invisible");

                bot.transform.rotation = Quaternion.Euler(0f, forwardYaw + 180f, 0f);
                Check(bot.CanSeePosition(rear) && bot.IsAimedAtPosition(rear),
                    "turning around reveals and aligns the rear target");
                Debug.Log("IRONFRONT infantry vision PASS: front, peripheral, " +
                    "outside cone, rear blind spot and turn-to-reveal");
            }
            finally { bot.transform.rotation = original; }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
