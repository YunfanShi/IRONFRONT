#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Opt-in only. No objects, settings, or counters are touched in normal play.
    public sealed class PrototypeOptimizationBenchmark : MonoBehaviour
    {
        private const int Samples = 600;
        private static readonly string[] CounterNames = {
            "Main Thread", "Render Thread", "Gfx.WaitForPresentOnGfxThread",
            "WaitForTargetFPS", "GC Allocated In Frame", "Draw Calls Count",
            "SetPass Calls Count", "Triangles Count", "GC.Collect"
        };
        private string output;
        private PrototypeRuntime game;
        private readonly Vector2[] from = new Vector2[120];
        private readonly Vector2[] to = new Vector2[120];
        private readonly long[,] counters = new long[Samples, CounterNames.Length];
        private readonly float[] frameMs = new float[Samples];
        private ProfilerRecorder[] recorders;
        private int errors;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-ironfrontBenchmark")
                {
                    var probe = new GameObject("Optimization benchmark").AddComponent<PrototypeOptimizationBenchmark>();
                    probe.output = Path.GetFullPath(args[i + 1]);
                    return;
                }
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += Observe;
            Application.runInBackground = true;
            Directory.CreateDirectory(output);
            while ((game = FindAnyObjectByType<PrototypeRuntime>()) == null || game.Player == null)
                yield return null;
            UnityEngine.Random.InitState(542);
            game.ConfigureSoloTeams(8);
            game.Match.ConfigureSolo(PrototypeGameMode.Conquest);
            game.BlueCommander.Replan(); game.RedCommander.Replan();
            game.BeginBattle(); game.Frontend.Close();
            game.Player.enabled = false;
            Camera camera = game.Player.ViewCamera;
            camera.transform.position = new Vector3(20f, 65f, -110f);
            camera.transform.LookAt(new Vector3(0f, 0f, 20f));
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = 1f / 60f;

            var random = new System.Random(542);
            for (int i = 0; i < from.Length; i++)
            {
                from[i] = new Vector2(random.Next(-330, 331), random.Next(-330, 331));
                to[i] = new Vector2(random.Next(-330, 331), random.Next(-330, 331));
            }
            // Warm edge caches/JIT before measuring the unchanged public APIs.
            NavigationBatch();
            string navigation = NavigationBatch();
            File.WriteAllText(Path.Combine(output, "navigation.json"), navigation);
            for (int frame = 0; frame < 300; frame++)
            { Tracers(frame); yield return null; }

            recorders = new ProfilerRecorder[CounterNames.Length];
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            for (int i = 0; i < recorders.Length; i++)
                foreach (var handle in handles)
                    if (ProfilerRecorderHandle.GetDescription(handle).Name == CounterNames[i])
                    { recorders[i] = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately); break; }
            yield return null; // Discard recorder-start setup allocations.
            for (int frame = 0; frame < Samples; frame++)
            {
                Tracers(frame);
                yield return null;
                frameMs[frame] = Time.unscaledDeltaTime * 1000f;
                for (int j = 0; j < recorders.Length; j++)
                    counters[frame, j] = recorders[j].Valid ? recorders[j].LastValue : -1;
            }
            for (int i = 0; i < recorders.Length; i++) recorders[i].Dispose();
            recorders = null;
            var csv = new StringBuilder("frame,wall_ms," + string.Join(",", CounterNames) + "\n");
            for (int i = 0; i < Samples; i++)
            {
                csv.Append(i).Append(',').Append(frameMs[i].ToString("F4", CultureInfo.InvariantCulture));
                for (int j = 0; j < CounterNames.Length; j++) csv.Append(',').Append(counters[i, j]);
                csv.Append('\n');
            }
            File.WriteAllText(Path.Combine(output, "frames.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(output, "device.txt"),
                Application.unityVersion + "\n" + SystemInfo.deviceModel + "\n" + SystemInfo.graphicsDeviceName +
                "\n" + Screen.width + "x" + Screen.height + "\n" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                "\nbots=" + game.Bots.Count + ";frames=" + Samples + ";errors=" + errors);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "battlefield.png"));
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log("IRONFRONT benchmark " + (errors == 0 ? "PASS" : "FAIL") + ": " + output);
            Application.Quit(errors == 0 ? 0 : 1);
        }

        private string NavigationBatch()
        {
            ulong hash = 14695981039346656037UL;
            int reachable = 0;
            long calibration = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(new byte[128]);
            bool allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() > calibration;
            long before = GC.GetAllocatedBytesForCurrentThread();
            long begin = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int repeat = 0; repeat < 3; repeat++)
                for (int i = 0; i < from.Length; i++)
                {
                    var infantry = game.Navigation.Find(from[i], to[i]);
                    var vehicle = PrototypeVehicleRoute.Find(from[i], to[i], 3.15f);
                    foreach (var path in new[] { infantry, vehicle })
                    {
                        if (path.Count > 0) reachable++;
                        hash = (hash ^ (uint)path.Count) * 1099511628211UL;
                        foreach (Vector2 point in path)
                        {
                            hash = (hash ^ (uint)Mathf.RoundToInt(point.x * 1000f)) * 1099511628211UL;
                            hash = (hash ^ (uint)Mathf.RoundToInt(point.y * 1000f)) * 1099511628211UL;
                        }
                    }
                }
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - begin) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            long bytes = allocationCounterAvailable ? GC.GetAllocatedBytesForCurrentThread() - before : -1;
            return "{\"queries\":720,\"reachable\":" + reachable + ",\"pathHash\":\"" + hash +
                "\",\"milliseconds\":" + ms.ToString("F4", CultureInfo.InvariantCulture) + ",\"allocatedBytes\":" + bytes + "}";
        }

        private void Tracers(int frame)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 start = new Vector3(-30f + i * 18f, 2f, -18f);
                game.ShowTracer(start, start + new Vector3(5f, 0f, 35f),
                    (frame + i) % 2 == 0 ? PrototypeTeam.Blue : PrototypeTeam.Red);
            }
        }

        private void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }

        private void OnDestroy()
        {
            Application.logMessageReceived -= Observe;
            if (recorders != null) foreach (var recorder in recorders) recorder.Dispose();
        }
    }
}
#endif
