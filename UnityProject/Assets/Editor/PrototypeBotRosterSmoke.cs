using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeBotRosterSmoke
    {
        [MenuItem("IRONFRONT/Verify Bot Names and Down Feed", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Bot Names and Down Feed")]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            PrototypeBot subject = null;
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            float health = 100f, blue = 0f, red = 0f;
            bool networkActive = false, receivedSnapshot = false;
            FieldInfo networkField = typeof(PrototypeRuntime).GetField("networkModeActive",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo receivedField = typeof(PrototypeBot).GetField("receivedNetworkState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                Check(game != null, "runtime exists");
                if (!game.MatchStarted)
                {
                    if (game.SoloTeamSize != 8) game.ConfigureSoloTeams(8);
                    game.BeginBattle();
                }
                var names = new HashSet<string>();
                foreach (PrototypeBot bot in game.Bots)
                    Check(!string.IsNullOrWhiteSpace(bot.DisplayName) && names.Add(bot.DisplayName),
                        "each bot has a unique display name");
                var queue = new PrototypeBotDownFeed();
                for (int i = 0; i < 8; i++) queue.Add("Bot " + i, PrototypeTeam.Blue, 0f);
                Check(queue.Entries.Count == 4 && queue.Entries[0].Name == "Bot 7" &&
                    queue.Entries[3].Name == "Bot 4", "burst is bounded and newest comes first");
                queue.Expire(7.99f);
                Check(queue.Entries.Count == 4, "notifications persist during their interval");
                queue.Expire(8f);
                Check(queue.Entries.Count == 0, "notifications expire at eight seconds");
                foreach (PrototypeBot bot in game.Bots)
                    if (bot.Alive && !bot.IsPassenger) { subject = bot; break; }
                Check(subject != null, "living infantry exists");
                position = subject.transform.position; rotation = subject.transform.rotation;
                health = subject.Health;
                networkActive = (bool)networkField.GetValue(game);
                receivedSnapshot = (bool)receivedField.GetValue(subject);
                blue = game.Match.BlueTickets; red = game.Match.RedTickets;
                string displayName = subject.DisplayName;
                game.BotDownFeed.Clear();
                subject.TakeDamage(1000f);
                Check(!subject.Alive && game.BotDownFeed.Entries.Count == 1 &&
                    game.BotDownFeed.Entries[0].Name == displayName &&
                    game.BotDownFeed.Entries[0].Team == subject.Team,
                    "real lethal damage reports the named bot and team once");
                subject.TakeDamage(1000f);
                Check(game.BotDownFeed.Entries.Count == 1, "repeated dead damage does not report twice");
                typeof(PrototypeBot).GetMethod("Respawn", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(subject, null);
                Check(subject.DisplayName == displayName && subject.Alive,
                    "respawn keeps the same personal identity");
                game.BotDownFeed.Clear();
                networkField.SetValue(game, true);
                receivedField.SetValue(subject, false);
                subject.ApplyNetworkState(position, 0f, 0f, false);
                Check(game.BotDownFeed.Entries.Count == 0,
                    "initial dead snapshot does not fabricate a new down notice");
                subject.ApplyNetworkState(position, 0f, health, true);
                subject.ApplyNetworkState(position, 0f, 0f, false);
                subject.ApplyNetworkState(position, 0f, 0f, false);
                Check(game.BotDownFeed.Entries.Count == 1,
                    "replica snapshots report a lethal transition exactly once");
                networkField.SetValue(game, networkActive);
                game.Frontend?.Close();
                Debug.Log("IRONFRONT bot roster PASS: unique personal names, actual lethal transition, " +
                    "one notice per life, respawn identity, replica transitions, bounded four-row queue and eight-second expiry");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT bot roster FAIL: " + error); }
            finally
            {
                if (subject != null)
                {
                    networkField.SetValue(game, networkActive);
                    receivedField.SetValue(subject, receivedSnapshot);
                    if (!subject.Alive) typeof(PrototypeBot).GetMethod("Respawn",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(subject, null);
                    subject.transform.SetPositionAndRotation(position, rotation);
                    typeof(PrototypeBot).GetProperty("Health").SetValue(subject, health);
                    game.Match.SetNetworkTickets(blue, red);
                }
            }
        }
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); }
    }
}
