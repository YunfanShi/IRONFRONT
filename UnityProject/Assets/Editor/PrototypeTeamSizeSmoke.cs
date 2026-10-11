using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeTeamSizeSmoke
    {
        private const string MenuPath = "IRONFRONT/Verify Solo 8v8";

        [MenuItem(MenuPath, true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem(MenuPath)]
        private static void Verify()
        {
            try
            {
                PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.MatchStarted, "start a solo match from the menu");
                Check(game.SoloTeamSize == 8, "menu selected 8v8");
                Check(game.Player.Team == PrototypeTeam.Blue, "local player is Blue");
                Check(game.Bots.Count == 15, "15 AI exist");
                Check(game.BlueCommander.Squads.Count == 4 &&
                    game.RedCommander.Squads.Count == 4, "four squads per team");

                var registered = new HashSet<PrototypeBot>();
                CheckSquads(game.BlueCommander, PrototypeTeam.Blue, registered);
                CheckSquads(game.RedCommander, PrototypeTeam.Red, registered);
                int blue = 0, red = 0;
                foreach (PrototypeBot bot in game.Bots)
                {
                    Check(registered.Contains(bot), "every AI receives a squad");
                    Check(!PrototypeLayout.Collides(bot.SpawnPosition, .8f),
                        "AI spawn is clear: " + bot.name);
                    if (bot.Team == PrototypeTeam.Blue) blue++;
                    else red++;
                }
                Check(blue == 7 && red == 8, "Blue 1+7 versus Red 8");
                Check(registered.Count == 15, "no AI belongs to two squads");
                Check(game.BlueCommander.Squads[3].Members.Length == 1,
                    "Blue's unmatched AI receives a solo squad");
                Debug.Log("IRONFRONT 8v8 PASS: " + game.Match.Mode +
                    ", Blue 1 player + 7 AI in 4 squads, Red 8 AI in 4 squads; " +
                    "all 15 AI spawns clear");
            }
            catch (Exception error)
            {
                Debug.LogError("IRONFRONT 8v8 FAIL: " + error);
            }
        }

        private static void CheckSquads(PrototypeCommander commander,
            PrototypeTeam team, HashSet<PrototypeBot> registered)
        {
            foreach (PrototypeSquad squad in commander.Squads)
            {
                Check(squad.Team == team && squad.Members.Length >= 1,
                    "squad side and membership");
                foreach (PrototypeBot bot in squad.Members)
                    Check(bot.Team == team && registered.Add(bot),
                        "unique AI assignment in " + team + " squad " + squad.Id);
            }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
