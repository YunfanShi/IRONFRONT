using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Synchronous real Player.Update calls isolate the lifecycle deadline from
    // manual input. Original player, accounts, frontend and match remain intact.
    public static class PrototypeScoreboardPlayerLifecycleSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string LoadoutKey = "ironfront.unity.loadout";
        private static bool waitingForClock;
        private static double clockWaitStarted;

        [MenuItem("IRONFRONT/Verify Scoreboard Player Lifecycle", true)]
        private static bool CanRun() => EditorApplication.isPlaying && !waitingForClock;

        [MenuItem("IRONFRONT/Verify Scoreboard Player Lifecycle")]
        public static void Verify()
        {
            // The first warm Play callback can precede its first simulated
            // frame. Wait for the actual clock instead of inventing elapsed time.
            if (Time.time <= 0f || Time.deltaTime <= 0f)
            {
                if (!waitingForClock) clockWaitStarted = EditorApplication.timeSinceStartup;
                waitingForClock = true;
                EditorApplication.update -= WaitForClock; EditorApplication.update += WaitForClock;
                Debug.Log("IRONFRONT scoreboard lifecycle awaiting real Play clock: time=" + Time.time + " dt=" + Time.deltaTime);
                return;
            }
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            PrototypePlayer originalPlayer = game?.Player;
            PrototypeMatch originalMatch = game?.Match;
            PrototypeScoreSystem originalScores = game?.Scores;
            PrototypeFrontend originalFrontend = game?.Frontend;
            PrototypeScoreboard originalBoard = game?.Scoreboard;
            bool oldStarted = game != null && game.MatchStarted, oldPause = game != null && game.PauseOpen;
            bool oldSupport = game != null && game.SupportMenuOpen, oldBoardOpen = game != null && game.ScoreboardOpen;
            float blue = originalMatch?.BlueTickets ?? 0f, red = originalMatch?.RedTickets ?? 0f;
            float volume = AudioListener.volume, scale = Time.timeScale;
            CursorLockMode cursor = Cursor.lockState; bool cursorVisible = Cursor.visible;
            bool hadLoadout = PlayerPrefs.HasKey(LoadoutKey); string savedLoadout = PlayerPrefs.GetString(LoadoutKey, "");
            GameObject root = null;
            PrototypePlayer subject = null;
            PrototypeCombinedArmsVehicle vehicle = null;
            PrototypeFrontend temporaryFrontend = null;
            bool replaced = false;
            Exception failure = null;
            try
            {
                Check(game != null && originalPlayer != null && originalMatch != null && !game.IsNetworkReplica,
                    "authoritative Play runtime exists");
                Check(Time.time > 0f, "real Play clock is positive: time=" + Time.time + " dt=" + Time.deltaTime);
                var match = new PrototypeMatch(); match.ConfigureSolo(PrototypeGameMode.Conquest, 100);
                var scores = new PrototypeScoreSystem(game); match.ScoreSystem = scores;
                temporaryFrontend = new PrototypeFrontend(game);
                Set(game, "<Match>k__BackingField", match); Set(game, "<Scores>k__BackingField", scores);
                Set(game, "<Frontend>k__BackingField", temporaryFrontend);
                Set(game, "<Scoreboard>k__BackingField", new PrototypeScoreboard(game));
                Set(game, "<MatchStarted>k__BackingField", true); Set(game, "<PauseOpen>k__BackingField", false);
                Set(game, "<SupportMenuOpen>k__BackingField", false); Set(game, "<ScoreboardOpen>k__BackingField", false);
                replaced = true;
                root = new GameObject("EDITOR ONLY scoreboard player lifecycle fixture");
                var actor = new GameObject("Independent actual local player"); actor.transform.SetParent(root.transform);
                actor.AddComponent<CharacterController>(); subject = actor.AddComponent<PrototypePlayer>();
                Set(game, "<Player>k__BackingField", subject); subject.Initialize(game); subject.enabled = false;
                foreach (Camera camera in actor.GetComponentsInChildren<Camera>()) camera.enabled = false;
                foreach (AudioListener listener in actor.GetComponentsInChildren<AudioListener>()) listener.enabled = false;
                temporaryFrontend.Close();
                Check(subject.Alive && subject.CanUseHeldEquipment, "closed-overlay living fixture allows equipment");
                Check(game.SetScoreboard(true) && !subject.CanUseHeldEquipment, "real scoreboard command blocks held equipment");
                CharacterController controller = subject.GetComponent<CharacterController>();
                controller.enabled = false; subject.transform.position = new Vector3(900f, 240f, 900f); controller.enabled = true;
                Set(subject, "verticalSpeed", -10f); Physics.SyncTransforms();
                Vector3 airborne = subject.transform.position;
                for (int update = 0; update < 16; update++) InvokeUpdate(subject);
                float fallingSpeed = (float)typeof(PrototypePlayer).GetField("verticalSpeed", Private).GetValue(subject);
                Check(subject.transform.position.y < airborne.y && fallingSpeed < -10f &&
                    Mathf.Abs(subject.transform.position.x - airborne.x) < .001f &&
                    Mathf.Abs(subject.transform.position.z - airborne.z) < .001f && game.ScoreboardOpen && subject.Alive,
                    "scoreboard-open actual Player.Update accelerates and moves an airborne controller downward without manual horizontal movement: time=" +
                    Time.time + " dt=" + Time.deltaTime + " y=" + airborne.y + "->" + subject.transform.position.y);
                subject.TakeDamage(10000f);
                Check(!subject.Alive && subject.IsDowned && subject.DownedRemaining > 29f && game.ScoreboardOpen,
                    "real lethal damage enters downed state while standings remain open");
                Set(subject, "downedUntil", Time.time * .5f);
                InvokeUpdate(subject);
                Check(!subject.Alive && !subject.IsDowned && subject.DownedRemaining == 0f &&
                    subject.RespawnRemaining > 4f && match.BlueTickets == 99f && game.ScoreboardOpen,
                    "scoreboard-open actual Update eliminates an expired casualty and settles its ticket once");
                InvokeUpdate(subject);
                Check(match.BlueTickets == 99f, "later scoreboard-open Updates cannot repeat elimination settlement");

                typeof(PrototypePlayer).GetMethod("Respawn", Private, null, Type.EmptyTypes, null).Invoke(subject, null);
                temporaryFrontend.Close();
                var hull = new GameObject("Independent actual motorcycle"); hull.transform.SetParent(root.transform);
                vehicle = hull.AddComponent<PrototypeCombinedArmsVehicle>();
                vehicle.Initialize(game, subject.Team, PrototypeVehicleKind.Motorcycle, new Vector2(-325f, -320f), false);
                vehicle.RespawnEnabled = false; vehicle.enabled = false; game.Vehicles.Add(vehicle);
                Enter(subject, vehicle);
                Check(ReferenceEquals(subject.CurrentVehicle, vehicle) && !controller.enabled && game.ScoreboardOpen,
                    "real occupied fixture seat disables the controller while standings are open");
                // A hull invalidated outside this player's damage path must be
                // handled by Update, even with no manual controls available.
                Set(vehicle, "<Health>k__BackingField", 0f);
                InvokeUpdate(subject);
                Check(subject.Alive && subject.CurrentVehicle == null && vehicle.Occupant == null && controller.enabled &&
                    !subject.CanUseHeldEquipment && game.ScoreboardOpen,
                    "scoreboard-open Update exits a dead hull and restores the living infantry controller");
                Set(vehicle, "<Health>k__BackingField", vehicle.MaxHealth);
                Enter(subject, vehicle);
                var seats = (PrototypeVehicleSeats)typeof(PrototypeCombinedArmsVehicle).GetField("seats", Private).GetValue(vehicle);
                seats.RemovePlayer(subject);
                Check(subject.CurrentVehicle != null && vehicle.Occupant == null && !controller.enabled,
                    "a real seat-ledger invalidation prepares stale occupancy independently of hull health");
                InvokeUpdate(subject);
                Check(subject.CurrentVehicle == null && controller.enabled && subject.Alive && game.ScoreboardOpen,
                    "scoreboard-open Update also cleans an invalidated seat without disabling infantry movement afterward");
                Check(game.SetScoreboard(false) && subject.CanUseHeldEquipment,
                    "closing the actual overlay restores actions after both lifecycle transitions");
                Check(originalMatch.BlueTickets == blue && originalMatch.RedTickets == red,
                    "isolated lifecycle settlement never changes the source match tickets");
                Check(PlayerPrefs.HasKey(LoadoutKey) == hadLoadout && PlayerPrefs.GetString(LoadoutKey, "") == savedLoadout,
                    "fixture lifecycle does not rewrite saved player equipment");
            }
            catch (Exception error) { failure = error.GetBaseException(); }
            finally
            {
                try
                {
                    if (subject != null && subject.CurrentVehicle != null) subject.ForceExitVehicle();
                    if (game != null && vehicle != null) game.Vehicles.Remove(vehicle);
                    if (root != null) UnityEngine.Object.DestroyImmediate(root);
                    temporaryFrontend?.Dispose();
                }
                catch (Exception error) { failure = failure ?? error.GetBaseException(); }
                finally
                {
                    if (replaced && game != null)
                    {
                        Set(game, "<Player>k__BackingField", originalPlayer); Set(game, "<Match>k__BackingField", originalMatch);
                        Set(game, "<Scores>k__BackingField", originalScores); Set(game, "<Frontend>k__BackingField", originalFrontend);
                        Set(game, "<Scoreboard>k__BackingField", originalBoard); Set(game, "<MatchStarted>k__BackingField", oldStarted);
                        Set(game, "<PauseOpen>k__BackingField", oldPause); Set(game, "<SupportMenuOpen>k__BackingField", oldSupport);
                        Set(game, "<ScoreboardOpen>k__BackingField", oldBoardOpen);
                    }
                    if (hadLoadout) PlayerPrefs.SetString(LoadoutKey, savedLoadout); else PlayerPrefs.DeleteKey(LoadoutKey);
                    PlayerPrefs.Save(); AudioListener.volume = volume; Time.timeScale = scale;
                    Cursor.lockState = cursor; Cursor.visible = cursorVisible;
                }
            }
            if (failure != null) Debug.LogError("IRONFRONT scoreboard player lifecycle FAIL: time=" + Time.time + " dt=" + Time.deltaTime + " " + failure);
            else Debug.Log("IRONFRONT scoreboard player lifecycle PASS: actual SetScoreboard equipment gate, continued airborne gravity/controller descent, lethal damage/downed entry, expired real Player.Update elimination and single ticket settlement, dead-hull/stale-seat ForceExit/controller restoration; independent player/match/accounts/frontend fixtures and all original state restored");
        }

        private static void WaitForClock()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - clockWaitStarted > 10d)
            {
                EditorApplication.update -= WaitForClock; waitingForClock = false;
                Debug.LogError("IRONFRONT scoreboard player lifecycle FAIL: real Play clock did not advance; time=" + Time.time + " dt=" + Time.deltaTime);
                return;
            }
            if (Time.time <= 0f || Time.deltaTime <= 0f) return;
            EditorApplication.update -= WaitForClock; waitingForClock = false; Verify();
        }

        private static void Enter(PrototypePlayer player, IPrototypeVehicle vehicle)
        {
            Check((bool)typeof(PrototypePlayer).GetMethod("EnterVehicle", Private).Invoke(player, new object[] { vehicle }),
                "actual local seat accepts the independent runtime player");
        }
        private static void InvokeUpdate(PrototypePlayer player) => typeof(PrototypePlayer).GetMethod("Update", Private).Invoke(player, null);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
        private static void Check(bool value, string description) { if (!value) throw new InvalidOperationException(description); }
    }
}
