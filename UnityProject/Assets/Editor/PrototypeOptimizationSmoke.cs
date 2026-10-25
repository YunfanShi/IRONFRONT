using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeOptimizationSmoke
    {
        private static PrototypeTracerPool pool;
        private static GameObject fixture;
        private static float originalScale;
        private static double phaseStart;
        private static int phase;
        private static Material material;

        [MenuItem("IRONFRONT/Verify Navigation Optimization")]
        public static void Navigation()
        {
            var navigation = new PrototypeNavigation();
            var random = new System.Random(542);
            var from = new Vector2[120]; var to = new Vector2[120];
            for (int i = 0; i < from.Length; i++)
            {
                from[i] = new Vector2(random.Next(-330, 331), random.Next(-330, 331));
                to[i] = new Vector2(random.Next(-330, 331), random.Next(-330, 331));
            }
            ulong hash = 14695981039346656037UL;
            int reachable = 0;
            for (int repeat = 0; repeat < 3; repeat++)
                for (int i = 0; i < from.Length; i++)
                {
                    var paths = new[] { navigation.Find(from[i], to[i]), PrototypeVehicleRoute.Find(from[i], to[i], 3.15f) };
                    foreach (var path in paths)
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
            // Captured from the unmodified 0.54.1 development player, including
            // empty routes and every waypoint in order (not just endpoints).
            Check(reachable == 636 && hash == 6173766864656313763UL, "720 paths exactly match the baseline");

            var result = new List<Vector2>(256);
            Vector2 a = PrototypeLayout.BlueBase, b = PrototypeLayout.RedBase;
            navigation.Find(a, b, result);
            Check(result.Count > 0, "base-to-base infantry path exists");
            Vector2[] saved = result.ToArray();
            navigation.Find(b, a);
            Check(result.Count == saved.Length, "caller route retains ownership");
            for (int i = 0; i < saved.Length; i++) Check(result[i] == saved[i], "later search cannot overwrite another route");
            navigation.Find(new Vector2(1000, 1000), b, result);
            Check(result.Count == 0, "failed search clears stale waypoints");
            foreach (float radius in new[] { 1.85f, 2.1f, 3.15f, 3.5f })
            {
                PrototypeVehicleRoute.Find(a, b, radius, result);
                Check(result.Count > 0, "vehicle base route exists at radius " + radius);
                Vector2 position = a;
                foreach (Vector2 point in result)
                {
                    Check(!PrototypeLayout.CirclePathBlocked(position, point, radius), "vehicle hull clears every returned segment");
                    position = point;
                }
            }
            // Warm all capacities and marker paths before counting allocations.
            navigation.Find(a, b, result); PrototypeVehicleRoute.Find(a, b, 3.15f, result);
            using (var sanity = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 64,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                GC.KeepAlive(new byte[128]); sanity.Stop();
                Check(sanity.Valid && sanity.Count > 0, "allocation recorder detects the positive control");
            }
            int allocations;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 4096,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                for (int i = 0; i < 12; i++)
                { navigation.Find(a, b, result); PrototypeVehicleRoute.Find(a, b, 3.15f, result); }
                recorder.Stop(); allocations = recorder.Count;
            }
            Check(allocations == 0, "warmed caller-buffer route searches allocate zero objects: " + allocations);
            Debug.Log("IRONFRONT navigation optimization PASS: 720 baseline paths, independent result ownership, failed-route clearing, four vehicle clearances, positive-control-verified zero allocations for 24 warmed searches");
        }

        [MenuItem("IRONFRONT/Verify Tracer Pool")]
        public static void Tracers()
        {
            Check(EditorApplication.isPlaying, "Play mode is required");
            originalScale = Time.timeScale;
            Time.timeScale = 1f;
            material = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>().LampMaterial;
            fixture = new GameObject("Tracer pool verification");
            pool = fixture.AddComponent<PrototypeTracerPool>();
            phase = 0; phaseStart = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            try
            {
                Check(pool.RetainedCount == 64, "prewarmed capacity");
                Spawn(32);
                foreach (LineRenderer line in fixture.GetComponentsInChildren<LineRenderer>())
                {
                    Check(line.sharedMaterial == material && line.positionCount == 2 && line.useWorldSpace,
                        "shared material and world-space geometry preserved");
                    Check(line.GetPosition(0) == new Vector3(0, 1000, 0) &&
                        line.GetPosition(1) == new Vector3(0, 1000, 30) &&
                        Mathf.Approximately(line.startWidth, .035f) && Mathf.Approximately(line.endWidth, .012f),
                        "endpoints and widths preserved");
                }
                Check(pool.ActiveCount == 32, "all shots rendered");
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || pool == null) throw new InvalidOperationException("Play session ended");
                double elapsed = EditorApplication.timeSinceStartup - phaseStart;
                if (elapsed > 5) throw new TimeoutException("Tracer expiry did not advance");
                if (elapsed < .25) return;
                if (phase == 0)
                {
                    Check(pool.ActiveCount == 0 && pool.RetainedCount == 64, "normal shots expire and return to pool");
                    int allocations;
                    using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1024,
                        ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
                    { Spawn(32); recorder.Stop(); allocations = recorder.Count; }
                    Check(allocations == 0 && fixture.GetComponentsInChildren<LineRenderer>(true).Length == 64,
                        "reuse creates neither managed allocations nor extra line objects: " + allocations);
                    Time.timeScale = 0f;
                }
                else if (phase == 1)
                {
                    Check(pool.ActiveCount == 32, "pause freezes the original scaled lifetime");
                    Time.timeScale = 1f;
                }
                else if (phase == 2)
                {
                    Check(pool.ActiveCount == 0, "resume expires paused shots");
                    Spawn(300);
                    Check(pool.ActiveCount == 300, "overflow never drops visible shots");
                }
                else
                {
                    Check(pool.ActiveCount == 0 && pool.RetainedCount == 256, "overflow returns to bounded idle storage");
                    Finish(null); return;
                }
                phase++; phaseStart = EditorApplication.timeSinceStartup;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Spawn(int count)
        {
            for (int i = 0; i < count; i++)
                pool.Show(new Vector3(0, 1000, 0), new Vector3(0, 1000, 30), material);
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            Time.timeScale = originalScale;
            if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            pool = null; fixture = null;
            if (error != null) Debug.LogError("IRONFRONT tracer pool FAIL: " + error);
            else Debug.Log("IRONFRONT tracer pool PASS: geometry/material parity, expiry, reuse without allocations or new objects, pause/resume, 300-shot burst, bounded retention and cleanup");
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
