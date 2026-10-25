using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Real game frames and public rescue commands drive this regression. No
    // private rescue clock or deadline is adjusted to manufacture a result.
    public static class PrototypeReviveTimerPauseSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private enum Phase { Rescue, PulseExpiry, MedicLeaves, MedicDies, NewLife, GuestPaused, GuestRunning, LegacyPacket }
        private static PrototypeRuntime game;
        private static PrototypePlayer player;
        private static PrototypeRemotePlayer remote;
        private static PrototypeBot localMedic, remoteMedic, alternateMedic;
        private static readonly Vector3 LocalPosition = new Vector3(900f, 240f, 900f);
        private static readonly Vector3 RemotePosition = new Vector3(940f, 240f, 900f);
        private static readonly List<GameObject> fixtures = new List<GameObject>();
        private static readonly Dictionary<Behaviour, bool> disabled = new Dictionary<Behaviour, bool>();
        private static readonly List<PrototypeBotDownNotice> oldFeed = new List<PrototypeBotDownNotice>();
        private static Phase phase;
        private static float phaseAt, localRemaining, remoteRemaining, lastFrame;
        private static double wallStarted;
        private static Vector3 oldPosition;
        private static Quaternion oldRotation;
        private static float oldHealth, oldBlue, oldRed, oldTimeScale;
        private static bool oldRuntimeEnabled, oldPause, oldSupport, oldNetwork, oldHost, oldPerspective;
        private static IPrototypeVehicle oldVehicle;
        private static int oldSeat;
        private static PrototypeScoreSystem oldScores, oldMatchScores;
        private static bool restore;

        [MenuItem("IRONFRONT/Verify Revive Timer Pause", true)]
        private static bool CanVerify() => EditorApplication.isPlaying && !EditorApplication.isPaused && game == null;

        [MenuItem("IRONFRONT/Verify Revive Timer Pause")]
        private static void Verify()
        {
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && !game.IsNetworkReplica,
                    "start in an authoritative solo or host Play session");
                if (!game.MatchStarted) game.BeginBattle();
                player = game.Player;
                Check(player.Alive && !game.Match.Winner.HasValue, "start with a living player in an active battle");
                oldPosition = player.transform.position; oldRotation = player.transform.rotation;
                oldHealth = player.Health; oldPerspective = player.IsFirstPerson;
                oldVehicle = player.CurrentVehicle;
                oldSeat = oldVehicle is IPrototypeSeatedVehicle seated ? seated.PlayerSeat : 0;
                oldRuntimeEnabled = game.enabled; oldPause = game.PauseOpen;
                oldSupport = game.SupportMenuOpen; oldTimeScale = Time.timeScale;
                oldNetwork = (bool)Field(game, "networkModeActive"); oldHost = (bool)Field(game, "networkHost");
                oldBlue = game.Match.BlueTickets; oldRed = game.Match.RedTickets;
                oldScores = game.Scores; oldMatchScores = game.Match.ScoreSystem;
                oldFeed.AddRange(game.BotDownFeed.Entries); restore = true;
                game.SetSupportMenu(false); Invoke(game, "SetPause", false); game.Frontend.Close();
                player.ForceExitVehicle();
                // Freeze unrelated combat and objective ticks while the actual
                // local Player.Update and temporary host Remote.Update continue.
                game.enabled = false;
                foreach (PrototypeBot bot in game.Bots) Disable(bot);
                foreach (PrototypeRemotePlayer actor in game.RemotePlayers) Disable(actor);
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                    if (vehicle is Behaviour behaviour) Disable(behaviour);
                foreach (PrototypeBallisticProjectile round in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>()) Disable(round);
                foreach (PrototypeVehicleProjectile round in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>()) Disable(round);
                var scores = new PrototypeScoreSystem(game);
                typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, scores);
                game.Match.ScoreSystem = scores; game.Match.SetNetworkTickets(100f, 100f);
                Relocate(player, LocalPosition);
                GameObject remoteRoot = new GameObject("Revive Timer Verification Host Remote");
                fixtures.Add(remoteRoot); remoteRoot.transform.SetParent(game.transform);
                remote = remoteRoot.AddComponent<PrototypeRemotePlayer>();
                remote.Initialize(game, 9998, player.Team); remote.transform.position = RemotePosition;
                localMedic = Medic(LocalPosition + Vector3.right * 2f);
                remoteMedic = Medic(RemotePosition + Vector3.right * 2f);
                alternateMedic = Medic(LocalPosition + Vector3.forward * 2f);
                DownBoth(true);
                Check(player.DownedRemaining > 0f && player.DownedRemaining < 4f &&
                    remote.DownedRemaining > 0f && remote.DownedRemaining < 4f,
                    "actual give-up commands prepare both casualties with less than four seconds left");
                Pulse(); Begin(Phase.Rescue);
                wallStarted = EditorApplication.timeSinceStartup; lastFrame = -1f;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
                Debug.Log("IRONFRONT revive timer verification running: real frames, near-expiry local/host rescue and guest snapshot transitions");
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.isPlaying && game != null && player != null && remote != null,
                    "Play continues during the asynchronous verification");
                Check(EditorApplication.timeSinceStartup - wallStarted < 25d, "real-frame verification completes without pausing Play");
                if (Time.time == lastFrame) return;
                lastFrame = Time.time;
                float elapsed = Time.time - phaseAt;
                switch (phase)
                {
                    case Phase.Rescue:
                        Pulse();
                        if (elapsed < 3.9f)
                        {
                            Check(player.IsDowned && remote.IsDowned && player.IsBeingRevived && remote.IsBeingRevived,
                                "neither near-expiry casualty dies or revives before four seconds");
                            Timers(localRemaining, remoteRemaining, .09f, "continuous medical work freezes both remaining timers");
                            Check(!player.TryRevive(alternateMedic), "a second medic cannot steal a fresh continuous rescue");
                        }
                        if (!player.Alive || !remote.Alive)
                        { Check(elapsed < 5.5f, "both patients receive their full four-second rescue"); return; }
                        Check(elapsed >= 3.99f && !player.IsDowned && !remote.IsDowned &&
                            !player.IsBeingRevived && !remote.IsBeingRevived && player.ReviveProgress == 0f && remote.ReviveProgress == 0f,
                            "actual local and host-remote rescues succeed beyond the original bleedout deadline and clear tracking");
                        DownBoth(false); Pulse(); Begin(Phase.PulseExpiry); break;
                    case Phase.PulseExpiry:
                        if (elapsed <= .20f)
                            Timers(localRemaining, remoteRemaining, .08f, "a fresh pulse remains paused during its grace period");
                        if (elapsed < .70f) return;
                        Check(!player.IsBeingRevived && !remote.IsBeingRevived && player.ReviveProgress == 0f && remote.ReviveProgress == 0f,
                            "a missing pulse ends rescue and clears continuous progress");
                        Timers(localRemaining - (elapsed - .25f), remoteRemaining - (elapsed - .25f), .10f,
                            "pulse expiry resumes precisely the preserved remainder instead of restarting thirty seconds");
                        Pulse();
                        Check(player.ReviveProgress < .03f && remote.ReviveProgress < .03f, "reacquisition starts a new four-second rescue");
                        Begin(Phase.MedicLeaves);
                        localMedic.transform.position += Vector3.right * 20f; remoteMedic.transform.position += Vector3.right * 20f;
                        Check(!player.IsBeingRevived && !remote.IsBeingRevived, "leaving the rescue radius cancels pause immediately"); break;
                    case Phase.MedicLeaves:
                        if (elapsed < .40f) return;
                        Timers(localRemaining - elapsed, remoteRemaining - elapsed, .10f, "remaining time resumes while medics are out of range");
                        Check(!player.TryRevive(localMedic) && !remote.TryRevive(remoteMedic), "out-of-range medics cannot pulse rescue");
                        localMedic.transform.position = LocalPosition + Vector3.right * 2f;
                        remoteMedic.transform.position = RemotePosition + Vector3.right * 2f;
                        Pulse(); Begin(Phase.MedicDies);
                        localMedic.TakeDamage(1000f); remoteMedic.TakeDamage(1000f);
                        Check(!player.IsBeingRevived && !remote.IsBeingRevived, "a medic death immediately ends the pause"); break;
                    case Phase.MedicDies:
                        if (elapsed < .40f) return;
                        Timers(localRemaining - elapsed, remoteRemaining - elapsed, .10f, "remaining time resumes after each medic dies");
                        Check(!player.TryRevive(localMedic) && !remote.TryRevive(remoteMedic), "dead medics cannot continue rescue");
                        Invoke(localMedic, "Respawn"); Invoke(remoteMedic, "Respawn");
                        localMedic.transform.position = LocalPosition + Vector3.right * 2f;
                        remoteMedic.transform.position = RemotePosition + Vector3.right * 2f;
                        Pulse(); player.GiveUpImmediately(); remote.GiveUpImmediately();
                        Check(!player.IsDowned && !remote.IsDowned && !player.IsBeingRevived && !remote.IsBeingRevived &&
                            player.DownedRemaining == 0f && remote.DownedRemaining == 0f &&
                            !player.TryRevive(localMedic) && !remote.TryRevive(remoteMedic),
                            "explicit give-up eliminates both patients despite a valid medic and cancels tracking");
                        RespawnBoth(); DownBoth(false); Begin(Phase.NewLife); break;
                    case Phase.NewLife:
                        if (elapsed < .45f) return;
                        Check(!player.IsBeingRevived && !remote.IsBeingRevived && player.ReviveProgress == 0f && remote.ReviveProgress == 0f,
                            "new lives inherit no medical pause or rescue progress");
                        Timers(localRemaining - elapsed, remoteRemaining - elapsed, .10f, "a fresh downed life counts down normally without pulses");
                        SetField(game, "networkModeActive", true); SetField(game, "networkHost", false);
                        Check(game.IsNetworkReplica, "guest packet fixture uses replica authority");
                        Packet(false, .40f, true, .50f); Begin(Phase.GuestPaused); break;
                    case Phase.GuestPaused:
                        if (elapsed < .70f) return;
                        Check(player.IsDowned && player.IsBeingRevived && Mathf.Abs(player.DownedRemaining - .40f) < .01f &&
                            Mathf.Abs(player.ReviveProgress - .50f) < .01f,
                            "one paused host packet holds guest bleedout and progress beyond its local deadline");
                        Packet(false, .80f, false, 0f); Begin(Phase.GuestRunning); break;
                    case Phase.GuestRunning:
                        if (elapsed < .35f) return;
                        Check(player.IsDowned && !player.IsBeingRevived && player.ReviveProgress == 0f &&
                            Mathf.Abs(player.DownedRemaining - Mathf.Max(0f, .80f - elapsed)) < .06f,
                            "running host packet resumes its exact remaining time between packets");
                        Packet(true, 0f, false, 0f);
                        Check(player.Alive && !player.IsDowned && !player.IsBeingRevived && player.DownedRemaining == 0f && player.ReviveProgress == 0f,
                            "host revival packet clears the guest freeze cache");
                        // Omit both new optional fields to exercise old packets.
                        player.ApplyNetworkState(LocalPosition, 0f, 0f, false,
                            player.Kit.Ammo, player.Kit.Reserve, 0f, .80f, false);
                        Begin(Phase.LegacyPacket); break;
                    case Phase.LegacyPacket:
                        if (elapsed < .35f) return;
                        Check(!player.IsBeingRevived && player.ReviveProgress == 0f &&
                            Mathf.Abs(player.DownedRemaining - Mathf.Max(0f, .80f - elapsed)) < .06f,
                            "old packets without new fields retain the normal running countdown");
                        Packet(true, 0f, false, 0f); Finish(null); break;
                }
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Begin(Phase next)
        { phase = next; phaseAt = Time.time; localRemaining = player.DownedRemaining; remoteRemaining = remote.DownedRemaining; }
        private static void Pulse()
        { player.TryRevive(localMedic); remote.TryRevive(remoteMedic); }
        private static void Timers(float local, float host, float tolerance, string label) =>
            Check(Mathf.Abs(player.DownedRemaining - local) <= tolerance && Mathf.Abs(remote.DownedRemaining - host) <= tolerance, label);
        private static void DownBoth(bool nearExpiry)
        {
            player.TakeDamage(1000f); remote.TakeDamage(1000f);
            Check(player.IsDowned && remote.IsDowned, "actual lethal damage enters both downed states");
            if (!nearExpiry) return;
            for (int i = 0; i < 20 && player.DownedRemaining > 2.2f; i++) player.GiveUp(.15f);
            for (int i = 0; i < 20 && remote.DownedRemaining > 2.2f; i++) remote.GiveUp(.15f);
        }
        private static void Packet(bool alive, float remaining, bool paused, float progress) =>
            player.ApplyNetworkState(LocalPosition, 0f, alive ? 55f : 0f, alive,
                player.Kit.Ammo, player.Kit.Reserve, 0f, remaining, false, paused, progress);
        private static void RespawnBoth()
        {
            typeof(PrototypePlayer).GetMethod("Respawn", Private, null, Type.EmptyTypes, null).Invoke(player, null);
            typeof(PrototypeRemotePlayer).GetMethod("Respawn", Private, null, Type.EmptyTypes, null).Invoke(remote, null);
            Relocate(player, LocalPosition); remote.transform.position = RemotePosition; game.Frontend.Close();
        }
        private static PrototypeBot Medic(Vector3 position)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Revive Timer Verification Medic"; fixtures.Add(root); root.transform.SetParent(game.transform);
            var medic = root.AddComponent<PrototypeBot>();
            medic.Initialize(game, game.Navigation, player.Team, new Vector2(position.x, position.z), PrototypeInfantryClass.Medic);
            medic.enabled = false; root.transform.position = position; return medic;
        }
        private static void Disable(Behaviour behaviour)
        { if (behaviour != null && !disabled.ContainsKey(behaviour)) { disabled.Add(behaviour, behaviour.enabled); behaviour.enabled = false; } }
        private static void Relocate(PrototypePlayer subject, Vector3 position)
        {
            CharacterController controller = subject.GetComponent<CharacterController>();
            bool enabled = controller.enabled; controller.enabled = false; subject.transform.position = position; controller.enabled = enabled;
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            try
            {
                if (restore && game != null && player != null)
                {
                    SetField(game, "networkModeActive", oldNetwork); SetField(game, "networkHost", oldHost);
                    typeof(PrototypePlayer).GetMethod("Respawn", Private, null, Type.EmptyTypes, null).Invoke(player, null);
                    typeof(PrototypePlayer).GetProperty("Health").SetValue(player, oldHealth);
                    Relocate(player, oldPosition); player.transform.rotation = oldRotation;
                    if (player.IsFirstPerson != oldPerspective) player.TogglePerspective();
                    typeof(PrototypeRuntime).GetProperty("Scores").SetValue(game, oldScores);
                    game.Match.ScoreSystem = oldMatchScores; game.Match.SetNetworkTickets(oldBlue, oldRed);
                    game.Frontend.Close();
                    if (oldVehicle != null && oldVehicle.Alive)
                        Invoke(player, "EnterVehicleSeat", oldVehicle, oldSeat);
                    game.enabled = oldRuntimeEnabled;
                    Invoke(game, "SetPause", oldPause); if (oldSupport) game.SetSupportMenu(true);
                    Time.timeScale = oldTimeScale;
                    var feed = (List<PrototypeBotDownNotice>)Field(game.BotDownFeed, "entries");
                    feed.Clear(); feed.AddRange(oldFeed);
                }
            }
            catch (Exception cleanup) { error = error ?? cleanup; }
            finally
            {
                foreach (KeyValuePair<Behaviour, bool> state in disabled)
                    if (state.Key != null) state.Key.enabled = state.Value;
                foreach (GameObject fixture in fixtures)
                    if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                fixtures.Clear(); disabled.Clear(); oldFeed.Clear(); restore = false;
                game = null; player = null; remote = null; localMedic = remoteMedic = alternateMedic = null;
            }
            if (error != null) Debug.LogError("IRONFRONT revive timer pause FAIL: " + error);
            else Debug.Log("IRONFRONT revive timer pause PASS: real near-expiry local/host-remote four-second rescue, " +
                "frozen remaining time, pulse expiry exact resume, medic departure/death, single rescuer, explicit give-up, " +
                "new-life reset, paused guest snapshot beyond local deadline, running/alive recovery and legacy packets");
        }
        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, Private).Invoke(target, arguments);
        private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
    }
}
