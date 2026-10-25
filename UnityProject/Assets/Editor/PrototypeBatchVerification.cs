using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // CLI-only coordinator. Every check gets a fresh Play session, so a prior
    // damage fixture cannot remove the living actors required by the next one.
    [InitializeOnLoad]
    public static class PrototypeBatchVerification
    {
        private const string Key = "ironfront.batch.verification.";
        static PrototypeBatchVerification()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += Observe;
        }

        public static void Run()
        {
            // An explicit CLI check-list can also run in a test-owned GUI
            // Editor process to capture real GameView/OnGUI frames.
            string[] args = Environment.GetCommandLineArgs();
            string list = null;
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-ironfrontChecks") list = Path.GetFullPath(args[i + 1]);
            if (list == null || !File.Exists(list)) throw new InvalidOperationException("A -ironfrontChecks menu list is required.");
            SessionState.SetString(Key + "list", list);
            SessionState.SetString(Key + "output", Path.Combine(Path.GetDirectoryName(list), "batch-results.txt"));
            File.WriteAllText(SessionState.GetString(Key + "output", ""), "IRONFRONT actual Unity Play verification\n");
            SessionState.SetInt(Key + "index", 0);
            SessionState.SetInt(Key + "phase", 0);
            SessionState.SetInt(Key + "result", 0);
            SessionState.SetBool(Key + "active", true);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            RestartDeadline();
        }

        private static void Update()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            try
            {
                long deadline = long.Parse(SessionState.GetString(Key + "deadline", "0"));
                if (DateTime.UtcNow.Ticks > deadline) { Finish(false, "Timed out waiting for the current check or Play transition."); return; }
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                int phase = SessionState.GetInt(Key + "phase", 0);
                if (phase == 0)
                {
                    SessionState.SetInt(Key + "phase", 1);
                    SessionState.SetInt(Key + "frames", 0);
                    EditorApplication.isPaused = false;
                    EditorApplication.isPlaying = true;
                    RestartDeadline();
                    return;
                }
                if (phase == 1)
                {
                    if (!EditorApplication.isPlaying) return;
                    PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                    if (game == null || game.Player == null || game.Frontend == null) return;
                    int frames = SessionState.GetInt(Key + "frames", 0) + 1;
                    SessionState.SetInt(Key + "frames", frames);
                    if (frames < 8) return;
                    if (!game.MatchStarted)
                    {
                        // Use the same solo deployment setup as the real menu.
                        // BeginBattle alone starts the eight-bot LAN roster and
                        // omits the additional soldiers required for 8v8 solo.
                        if (!game.IsNetworkSession)
                        {
                            if (game.SoloTeamSize == 0)
                                game.ConfigureSoloTeams(game.Frontend.SelectedTeamSize);
                            game.Match.ConfigureSolo(game.Frontend.SelectedMode);
                            game.BlueCommander.Replan();
                            game.RedCommander.Replan();
                        }
                        game.BeginBattle();
                    }
                    game.Frontend.Close(); game.SetSupportMenu(false);
                    string[] lines = File.ReadAllLines(SessionState.GetString(Key + "list", ""));
                    int index = SessionState.GetInt(Key + "index", 0);
                    if (index >= lines.Length) { Finish(true, "All configured checks passed."); return; }
                    string[] parts = lines[index].Split('|');
                    if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                        throw new InvalidOperationException("Each check must specify menu path|expected completion log prefix.");
                    SessionState.SetString(Key + "menu", parts[0]);
                    SessionState.SetString(Key + "expected", parts[1]);
                    SessionState.SetString(Key + "detail", "");
                    SessionState.SetInt(Key + "result", 0);
                    SessionState.SetInt(Key + "phase", 2);
                    RestartDeadline();
                    Debug.Log("IRONFRONT batch running: " + parts[0]);
                    if (!EditorApplication.ExecuteMenuItem(parts[0])) Finish(false, "Menu did not execute: " + parts[0]);
                    return;
                }
                if (phase == 2)
                {
                    int result = SessionState.GetInt(Key + "result", 0);
                    if (result == 0) return;
                    string detail = SessionState.GetString(Key + "detail", "");
                    File.AppendAllText(SessionState.GetString(Key + "output", ""),
                        SessionState.GetString(Key + "menu", "") + "\n" + detail + "\n\n");
                    if (result < 0) { Finish(false, detail); return; }
                    SessionState.SetInt(Key + "phase", 3);
                    EditorApplication.isPaused = false;
                    EditorApplication.isPlaying = false;
                    RestartDeadline();
                    return;
                }
                if (phase == 3 && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetInt(Key + "index", SessionState.GetInt(Key + "index", 0) + 1);
                    string[] lines = File.ReadAllLines(SessionState.GetString(Key + "list", ""));
                    if (SessionState.GetInt(Key + "index", 0) >= lines.Length)
                        Finish(true, "All configured checks passed.");
                    else { SessionState.SetInt(Key + "phase", 0); RestartDeadline(); }
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void Observe(string condition, string stack, LogType type)
        {
            if (!SessionState.GetBool(Key + "active", false) || SessionState.GetInt(Key + "phase", 0) != 2) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                SessionState.SetInt(Key + "result", -1);
                SessionState.SetString(Key + "detail", condition + "\n" + stack);
            }
            else if (SessionState.GetInt(Key + "result", 0) >= 0 &&
                condition.StartsWith(SessionState.GetString(Key + "expected", ""), StringComparison.Ordinal))
            {
                SessionState.SetInt(Key + "result", 1);
                SessionState.SetString(Key + "detail", condition);
            }
        }

        private static void RestartDeadline() => SessionState.SetString(Key + "deadline", DateTime.UtcNow.AddSeconds(90).Ticks.ToString());
        private static void Finish(bool passed, string detail)
        {
            SessionState.SetBool(Key + "active", false);
            string output = SessionState.GetString(Key + "output", "");
            if (!string.IsNullOrEmpty(output)) File.AppendAllText(output, (passed ? "FINAL PASS: " : "FINAL FAIL: ") + detail + "\n");
            Debug.Log("IRONFRONT batch " + (passed ? "PASS: " : "FAIL: ") + detail);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
