using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeSmokeScreenSmoke
    {
        private static PrototypeSmokeCloud previewCloud;
        [MenuItem("IRONFRONT/Verify Smoke Screen and Grenade Models", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Smoke Screen and Grenade Models")]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            Check(game != null && game.Player != null, "Play mode runtime is present");
            if (!game.MatchStarted)
            {
                game.ConfigureSoloTeams(8);
                game.Match.ConfigureSolo(PrototypeGameMode.Conquest);
                game.BeginBattle();
                game.Frontend.Close();
            }
            var props = new GameObject("Smoke verification props");
            var clouds = new List<PrototypeSmokeCloud>();
            PrototypeBot observer = game.Bots.Count > 0 ? game.Bots[0] : null;
            Vector3 oldPosition = observer != null ? observer.transform.position : Vector3.zero;
            Quaternion oldRotation = observer != null ? observer.transform.rotation : Quaternion.identity;
            try
            {
                foreach (PrototypeThrowableId kind in Enum.GetValues(typeof(PrototypeThrowableId)))
                {
                    GameObject model = PrototypeThrowableVisual.Build(props.transform, kind, game);
                    Check(model.transform.Find("Canister") != null && model.transform.Find("Fuse cap") != null &&
                        model.transform.Find("Safety lever") != null && model.transform.Find("Safety pin") != null,
                        kind + " contains a shaped body, fuse, safety lever and pin");
                    Transform ring = model.transform.Find("Pull ring");
                    Check(ring != null && ring.GetComponent<MeshFilter>().sharedMesh.vertexCount > 100,
                        kind + " uses a hollow mesh pull ring");
                    foreach (Vector3 vertex in ring.GetComponent<MeshFilter>().sharedMesh.vertices)
                        Check(new Vector2(vertex.x, vertex.y).magnitude > .015f, "pull ring preserves its center hole");
                    foreach (Collider collider in model.GetComponentsInChildren<Collider>())
                        Check(!collider.enabled, "visual parts never obstruct physics");
                    PrototypeThrowableVisual.RemoveSafety(model);
                    Check(!model.transform.Find("Safety lever").gameObject.activeSelf &&
                        !ring.gameObject.activeSelf, "released grenade removes its safety pieces");
                }

                Vector2 map = new Vector2(-322f, -315f);
                Vector3 center = new Vector3(map.x, PrototypeLayout.HeightAt(map.x, map.y), map.y);
                Vector3 left = center + Vector3.up * 1.65f - Vector3.right * 10f;
                Vector3 right = center + Vector3.up * 1.65f + Vector3.right * 10f;
                Check(!PrototypeLayout.LineBlocked(new Vector2(left.x, left.z),
                    new Vector2(right.x, right.z), .1f), "test uses a clear physical corridor");
                PrototypeSmokeCloud cloud = PrototypeSmokeCloud.Spawn(game, center);
                clouds.Add(cloud);
                Transform groundedCan = cloud.transform.Find("Smoke grenade model");
                Check(groundedCan != null && Mathf.Abs(MinimumVisibleY(groundedCan) - center.y) < .025f,
                    "a spent canister rests on the actual ground without floating");
                Check(cloud.DensityAtAge(0f) == 0f && cloud.DensityAtAge(1f) < cloud.DensityAtAge(3f),
                    "cloud builds progressively rather than appearing at full opacity");
                Check(cloud.RadiusAtAge(1f) < cloud.RadiusAtAge(4f), "cloud expands from the emitter");
                Check(cloud.DensityAtAge(15f) > .95f && cloud.DensityAtAge(21f) < .6f &&
                    cloud.DensityAtAge(24f) == 0f, "smoke holds, fades and expires after 24 seconds");
                Check(cloud.SegmentOpticalDepth(left, right, 0f) == 0f &&
                    cloud.SegmentOpticalDepth(left, right, 4f) > .85f &&
                    cloud.SegmentOpticalDepth(left, right, 24f) == 0f,
                    "optical obstruction follows the same timed cloud");
                cloud.PreviewAtAge(4f);
                Check(PrototypeSmokeCloud.SegmentBlocked(left, right), "mature smoke blocks a ground eye line");
                Check(!PrototypeSmokeCloud.SegmentBlocked(left + Vector3.up * 12f, right + Vector3.up * 12f),
                    "smoke does not block an aircraft line above the cloud");
                Check(!PrototypeSmokeCloud.SegmentBlocked(left + Vector3.forward * 12f, right + Vector3.forward * 12f),
                    "a line outside the smoke remains visible");
                Check(PrototypeSmokeCloud.DensityAt(center + Vector3.up * 1.65f) > .9f,
                    "a player camera inside smoke receives dense fog");
                Check(!PrototypeLayout.LineBlocked(new Vector2(left.x, left.z),
                    new Vector2(right.x, right.z), .1f), "smoke never changes movement or ballistic clearance");
                Check(PrototypeLayout.SightBlocked(new Vector2(left.x, left.z),
                    new Vector2(right.x, right.z), .1f), "optical sight query includes smoke");
                if (observer != null)
                {
                    observer.transform.position = left - Vector3.up * .65f;
                    observer.transform.rotation = Quaternion.LookRotation(Vector3.right);
                    Check(!observer.CanSeePosition(new Vector2(right.x, right.z)), "AI cannot acquire infantry through smoke");
                }
                ParticleSystem[] particles = cloud.GetComponentsInChildren<ParticleSystem>();
                Check(particles.Length == 2, "cloud has a rolling body and a release jet");
                int limit = 0;
                foreach (ParticleSystem system in particles)
                {
                    limit += system.main.maxParticles;
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    Check(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.isSupported &&
                        renderer.sharedMaterial.renderQueue >= 3000 && renderer.sortMode == ParticleSystemSortMode.Distance,
                        "smoke uses a supported sorted transparent particle shader");
                    Check(!system.collision.enabled, "particles never block people or bullets");
                }
                Check(limit == cloud.ParticleLimit && limit <= 288, "per-cloud particle cost is bounded");
                PrototypeSmokeCloud vehicle = PrototypeSmokeCloud.Spawn(game, center + Vector3.forward * 25f, true);
                clouds.Add(vehicle);
                Check(vehicle.Duration < cloud.Duration && vehicle.RadiusAtAge(4f) > cloud.RadiusAtAge(4f),
                    "vehicle screen builds a wider, shorter defensive cloud");
                Check(vehicle.transform.Find("Smoke grenade model") == null, "vehicle screen does not spawn an infantry canister");
                GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = "Smoke canister support crate";
                crate.transform.SetParent(props.transform, false);
                Vector3 crateFloor = center + Vector3.forward * 35f;
                crate.transform.position = crateFloor + Vector3.up * 1.5f;
                crate.transform.localScale = Vector3.one * 3f;
                float crateTop = crateFloor.y + 3f;
                Vector3 restingSphereCenter = new Vector3(crateFloor.x, crateTop + .13f, crateFloor.z);
                // Destroy(projectile) is deferred until frame end in the live
                // detonation path. Its remaining sphere must be ignored here.
                GameObject oldProjectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                oldProjectile.name = "Deferred old smoke projectile";
                oldProjectile.transform.SetParent(props.transform, false);
                oldProjectile.transform.position = restingSphereCenter;
                oldProjectile.transform.localScale = Vector3.one * .26f;
                oldProjectile.AddComponent<Rigidbody>().isKinematic = true;
                PrototypeSmokeCloud elevated = PrototypeSmokeCloud.Spawn(game, restingSphereCenter);
                clouds.Add(elevated);
                Transform elevatedCan = elevated.transform.Find("Smoke grenade model");
                Check(elevatedCan != null && Mathf.Abs(MinimumVisibleY(elevatedCan) - crateTop) < .01f,
                    "a box-top canister preserves its support height and ignores the old physics sphere");
                Debug.Log("IRONFRONT smoke screen PASS: shaped frag/smoke bodies, hollow pin rings, released safety parts, " +
                    "gradual 24-second smoke, real AI optical cover, clear aircraft altitude, camera density, " +
                    "no physics obstruction, two transparent sorted emitters, bounded 288-particle cloud and " +
                    "spent canister ground/box-top support without deferred-projectile interference");
            }
            finally
            {
                if (observer != null) { observer.transform.position = oldPosition; observer.transform.rotation = oldRotation; }
                foreach (PrototypeSmokeCloud cloud in clouds)
                    if (cloud != null) { cloud.enabled = false; UnityEngine.Object.Destroy(cloud.gameObject); }
                UnityEngine.Object.Destroy(props);
            }
        }

        [MenuItem("IRONFRONT/Preview Smoke Grenade", true)]
        private static bool CanPreview() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Preview Smoke Grenade")]
        private static void Preview()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null || game.Player == null || !game.MatchStarted) return;
            CreateMaturePreview(game);
            EditorApplication.isPaused = true;
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            Debug.Log("IRONFRONT mature smoke preview: six-second cloud, Editor paused for visual review");
        }

        private static void CreateMaturePreview(PrototypeRuntime game)
        {
            if (previewCloud != null)
            {
                previewCloud.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(previewCloud.gameObject);
            }
            Camera camera = Camera.main;
            Vector3 point = game.Player.transform.position +
                (camera != null ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized :
                    game.Player.transform.forward) * 7f;
            point.y = PrototypeLayout.HeightAt(point.x, point.z) + .06f;
            previewCloud = PrototypeSmokeCloud.Spawn(game, point);
            previewCloud.gameObject.name = "Editor mature smoke preview";
            previewCloud.PreviewAtAge(6f);
        }

        [MenuItem("IRONFRONT/Capture Smoke Preview", true)]
        private static bool CanCapture() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Capture Smoke Preview")]
        private static void Capture()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null || game.Player == null || !game.MatchStarted || game.Player.ViewCamera == null)
                throw new InvalidOperationException("Start a match in SampleScene Play mode before capturing smoke.");
            CreateMaturePreview(game);
            EditorApplication.isPaused = true;
            Camera camera = game.Player.ViewCamera;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            var target = new RenderTexture(1200, 800, 24, RenderTextureFormat.ARGB32)
                { name = "Smoke evidence camera target" };
            var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = 1.5f;
                Physics.SyncTransforms();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 1200f, 800f), 0, 0);
                pixels.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification048"));
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "smoke.png");
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log("IRONFRONT smoke capture: " + path);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }

        private static float MinimumVisibleY(Transform model)
        {
            float minimum = float.PositiveInfinity;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                minimum = Mathf.Min(minimum, renderer.bounds.min.y);
            return minimum;
        }
    }
}
