#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public sealed class PrototypeDamageFeedbackProbe : MonoBehaviour
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        string output; int errors; float now = 10f;
        PrototypeRuntime game; PrototypePlayer player; PrototypeDamageFeedback feedback;
        readonly List<string> checks = new List<string>();
        readonly List<string> samples = new List<string> { "stage,frame,health,flash,directions,angle" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-ironfrontDamageProbe")
                new GameObject("Incoming damage verification").AddComponent<PrototypeDamageFeedbackProbe>().output = Path.GetFullPath(args[i + 1]);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.runInBackground = true; Time.captureDeltaTime = 1f / 30f;
            if (!Application.isBatchMode) Screen.SetResolution(960, 640, false);
            Application.logMessageReceived += Observe;
            IEnumerator work = Verify(); Exception failure = null;
            while (true)
            {
                bool more = false; object current = null;
                try { more = work.MoveNext(); if (more) current = work.Current; } catch (Exception e) { failure = e; }
                if (!more || failure != null) break;
                yield return current;
            }
            bool pass = failure == null && errors == 0;
            File.WriteAllText(Path.Combine(output, "results.txt"), (pass ? "PASS" : "FAIL") + "\nVersion=" + Application.version +
                "\nRuntimeErrors=" + errors + "\n" + string.Join("\n", checks) + (failure == null ? "" : "\n" + failure));
            File.WriteAllLines(Path.Combine(output, "samples.csv"), samples);
            Debug.Log("IRONFRONT incoming damage " + (pass ? "PASS" : "FAIL")); Application.logMessageReceived -= Observe;
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(pass ? 0 : 1);
            #else
            Application.Quit(pass ? 0 : 1);
            #endif
        }
        IEnumerator Verify()
        {
            for (int i = 0; i < 600; i++)
            { game = FindAnyObjectByType<PrototypeRuntime>(); if (game != null && game.Player != null) break; yield return null; }
            Check(game != null && game.DamageFeedback != null, "Runtime creates incoming damage UI");
            game.BeginBattle(); game.Frontend.Close(); game.SetSupportMenu(false);
            player = game.Player; player.enabled = false;
            foreach (var bot in game.Bots) { bot.enabled = false; bot.gameObject.SetActive(false); }
            foreach (var vehicle in game.Vehicles.OfType<MonoBehaviour>()) vehicle.gameObject.SetActive(false);
            if (!player.IsFirstPerson) player.TogglePerspective();
            player.transform.rotation = Quaternion.identity;
            player.ViewCamera.transform.localRotation = Quaternion.identity;
            player.GetComponent<PrototypeSoldierVisual>().enabled = false;
            feedback = game.DamageFeedback; feedback.Initialize(game, player, () => now);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
            yield return null; yield return null;
            Check(feedback.Root != null && feedback.Root.pickingMode == UnityEngine.UIElements.PickingMode.Ignore,
                "Non-interactive overlay does not intercept controls");
            ResetHealth(); feedback.Tick(); Capture("baseline/0000");
            yield return null; yield return null;

            Vector3[] sources = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left, new Vector3(1, 0, 1) };
            string[] names = { "front", "right", "rear", "left", "diagonal" };
            float[] angles = { 0, 90, 180, -90, 45 };
            for (int i = 0; i < sources.Length; i++)
            {
                ResetHealth(); Bullet(sources[i]);
                Check(player.Health < 100 && feedback.FlashOpacity > .3f && feedback.DirectionCount == 1,
                    names[i] + " actual projectile health loss triggers flash and one direction");
                Check(Mathf.Abs(Mathf.DeltaAngle(FirstAngle(), angles[i])) < .1f, names[i] + " points toward incoming source");
                for (int frame = 0; frame < 45; frame++)
                {
                    feedback.Tick(); Record(names[i], frame); Capture(names[i] + "/" + frame.ToString("D4"));
                    yield return null; now += 1f / 30f;
                }
                Check(feedback.FlashOpacity == 0 && feedback.DirectionCount == 0, names[i] + " flash and marker expire");
            }
            ResetHealth(); Bullet(Vector3.forward);
            for (int frame = 0; frame < 45; frame++)
            {
                if (frame == 4) Bullet(Vector3.right);
                if (frame == 8) Bullet(Vector3.left);
                if (frame == 10) Check(feedback.DirectionCount == 3 && feedback.FlashOpacity <= .68f, "Burst from three directions stays bounded and retains three markers");
                feedback.Tick(); Record("burst", frame); Capture("burst/" + frame.ToString("D4"));
                yield return null; now += 1f / 30f;
            }
            ResetHealth(); Bullet(Vector3.right);
            for (int frame = 0; frame < 45; frame++)
            {
                float yaw = Mathf.Clamp01(frame / 24f) * 90f;
                player.ViewCamera.transform.rotation = Quaternion.Euler(0, yaw, 0);
                feedback.Tick(); Record("turn", frame); Capture("turn/" + frame.ToString("D4"));
                if (frame == 24) Check(Mathf.Abs(FirstAngle()) < .1f, "Turning toward a right-hand attacker moves the marker to the front");
                yield return null; now += 1f / 30f;
            }
            player.ViewCamera.transform.rotation = Quaternion.identity;
            ResetHealth(); player.TakeDamage(15);
            Check(feedback.FlashOpacity > 0 && feedback.DirectionCount == 0, "Unknown-source damage flashes without a false direction");
            for (int frame = 0; frame < 45; frame++)
            {
                feedback.Tick(); Record("unknown", frame); Capture("unknown/" + frame.ToString("D4"));
                yield return null; now += 1f / 30f;
            }
            ResetHealth(); int count = feedback.HitsObserved;
            player.TakeDamage(0); player.TakeDamage(-5); player.TakeDamage(float.NaN);
            Bullet(Vector3.forward, true);
            Check(player.Health == 100 && feedback.HitsObserved == count, "Zero, invalid and friendly projectile damage do not flash");
            Bullet(Vector3.forward); Bullet(Vector3.forward);
            Check(feedback.DirectionCount == 1, "Repeated hits from the same direction merge");
            Invoke(game, "SetPause", true); feedback.Tick();
            Check(feedback.DirectionCount == 0 && feedback.FlashOpacity == 0, "Pause clears feedback");
            Invoke(game, "SetPause", false); ResetHealth(); Bullet(Vector3.forward);
            game.SetSupportMenu(true); feedback.Tick(); game.SetSupportMenu(false);
            Check(feedback.DirectionCount == 0, "Support menu does not retain stale damage");

            ResetHealth();
            var source = player.transform.position + Vector3.left * 2;
            var shell = PrototypeBallisticProjectile.LaunchShell(game, Enemy, null, source, Vector3.right, 100f, 100f);
            shell.enabled = false; Invoke(shell, "Explode", source); Destroy(shell.gameObject);
            Check(player.Health < 100 && Mathf.Abs(Mathf.DeltaAngle(FirstAngle(), -90)) < .1f,
                "Real shell explosion reports its blast centre");
            ResetHealth();
            var remoteRoot = new GameObject("Incoming damage peer fixture");
            var remote = remoteRoot.AddComponent<PrototypeRemotePlayer>(); remote.Initialize(game, 9997, Enemy); remote.enabled = false;
            remote.TakeDamageFrom(12, source);
            var message = JsonUtility.FromJson<PrototypeIncomingDamage>(JsonUtility.ToJson(remote.LastIncomingDamage.Snapshot()));
            Check(message.sequence > 0 && message.Source.HasValue && message.amount == 12, "Host peer damage survives JSON snapshot serialization");
            feedback.ReceiveSnapshot(message, 100, 88);
            Check(feedback.DirectionCount == 0, "First network snapshot seeds state without replaying old damage");
            message.sequence++; feedback.ReceiveSnapshot(message, 88, 76);
            Check(feedback.DirectionCount == 1, "New guest health loss uses host-supplied direction");
            count = feedback.HitsObserved; feedback.ReceiveSnapshot(message, 88, 76); message.sequence--;
            feedback.ReceiveSnapshot(message, 88, 76);
            Check(feedback.HitsObserved == count, "Duplicate and older damage packets cannot replay feedback");
            feedback.Clear(); feedback.ReceiveSnapshot(null, 76, 64);
            Check(feedback.FlashOpacity > 0 && feedback.DirectionCount == 0, "Old hosts without optional direction data still flash on health loss");
            feedback.ReceiveSnapshot(message, 0, 100);
            Check(feedback.FlashOpacity == 0 && feedback.DirectionCount == 0, "Guest respawn clears feedback");
            Destroy(remoteRoot);
            ResetHealth(); player.TakeDamageFrom(10000, source); feedback.Tick();
            Check(!player.Alive && feedback.FlashOpacity == 0 && feedback.DirectionCount == 0, "Downed state clears combat overlay");
            Invoke(player, "Respawn", player.MapPosition); game.Frontend.Close(); feedback.Tick();
            Check(player.Alive && feedback.FlashOpacity == 0 && feedback.DirectionCount == 0, "Local respawn restores a clean view");
            yield return null; yield return null;
        }
        PrototypeTeam Enemy => player.Team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
        void ResetHealth()
        {
            typeof(PrototypePlayer).GetProperty("Health").SetValue(player, 100f);
            player.Kit.ResetForSpawn(); feedback.Clear(); now += 2f;
        }
        void Bullet(Vector3 direction, bool friendly = false)
        {
            Vector3 origin = player.transform.position + direction.normalized * 20f + Vector3.up;
            var bullet = PrototypeBallisticProjectile.LaunchBullet(game, friendly ? player.Team : Enemy, null,
                origin, -direction.normalized, PrototypeInfantryRoles.GetWeapon(PrototypeWeaponId.Carbine), .6f);
            bullet.enabled = false; Invoke(bullet, "ApplyBulletDamage", player.GetComponent<Collider>(), 0f); Destroy(bullet.gameObject);
        }
        float FirstAngle()
        { for (int i = 0; i < 4; i++) if (feedback.DirectionAlpha(i) > 0) return feedback.DirectionAngle(i); return float.NaN; }
        void Record(string stage, int frame) => samples.Add($"{stage},{frame},{player.Health:F3},{feedback.FlashOpacity:F4},{feedback.DirectionCount},{FirstAngle():F2}");
        void Capture(string path)
        {
            if (Application.isBatchMode) return;
            string full = Path.Combine(output, path + ".png"); Directory.CreateDirectory(Path.GetDirectoryName(full));
            ScreenCapture.CaptureScreenshot(full);
        }
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add(message); }
        void Observe(string message, string stack, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors++; }
        static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethods(Hidden)
            .First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
    }
}
#endif
