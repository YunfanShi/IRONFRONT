using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Exercises the actual scene-spawn path, not just the imported FBX prefabs.
    [InitializeOnLoad]
    public static class VehicleArtPlaySmoke
    {
        private const string SessionKey = "ironfront.vehicleArtPlaySmoke";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private static readonly FieldInfo ModelField = typeof(PrototypeCombinedArmsVehicle)
            .GetField("model", BindingFlags.Instance | BindingFlags.NonPublic);
        private static int phase;
        private static int targetFrame;
        private static PrototypeCombinedArmsVehicle[] targets;
        private static readonly Dictionary<PrototypeCombinedArmsVehicle, Quaternion> WheelStart =
            new Dictionary<PrototypeCombinedArmsVehicle, Quaternion>();

        static VehicleArtPlaySmoke() => EditorApplication.update += Tick;

        [MenuItem("IRONFRONT/Art/Verify Vehicle Art in Play Mode")]
        public static void Run() => Start(false);

        [MenuItem("IRONFRONT/Art/Verify H8 Art in Play Mode")]
        public static void RunH8() => Start(true);

        private static void Start(bool h8Only)
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Leave Play Mode before starting the vehicle art check.");
            SessionState.SetBool(SessionKey, true);
            SessionState.SetBool(SessionKey + ".h8Only", h8Only);
            SessionState.SetString(SessionKey + ".deadline",
                DateTime.UtcNow.AddMinutes(3).Ticks.ToString());
            phase = 0;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            if (long.TryParse(SessionState.GetString(SessionKey + ".deadline", "0"), out long deadline) &&
                DateTime.UtcNow.Ticks > deadline)
            {
                Finish(new TimeoutException("Vehicle art Play Mode check timed out."));
                return;
            }
            if (!EditorApplication.isPlaying || Time.frameCount < 10) return;
            try
            {
                if (phase == 0)
                {
                    targets = FindTargets();
                    if (targets == null) return;
                    foreach (PrototypeCombinedArmsVehicle vehicle in targets)
                    {
                        ValidateSpawn(vehicle);
                        PrototypeCombinedVehicleVisual model = Model(vehicle);
                        if (vehicle.Kind == PrototypeVehicleKind.AntiAir)
                        {
                            PrototypeVehicleArtRig art = vehicle.GetComponentInChildren<PrototypeVehicleArtRig>();
                            WheelStart[vehicle] = art.Wheels[0].localRotation;
                            model.Turret.localRotation = Quaternion.Euler(0f, 25f, 0f);
                            model.Barrel.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                            model.Radar.localRotation = Quaternion.Euler(0f, 18f, 0f);
                            vehicle.transform.position += vehicle.transform.forward * .5f;
                        }
                        else
                        {
                            model.Rotor.localRotation = Quaternion.Euler(0f, 37f, 0f);
                            model.TailRotor.localRotation = Quaternion.Euler(29f, 0f, 0f);
                        }
                    }
                    targetFrame = Time.frameCount + 2;
                    phase = 1;
                    return;
                }
                if (phase == 1 && Time.frameCount >= targetFrame)
                {
                    foreach (PrototypeCombinedArmsVehicle vehicle in targets)
                    {
                        ValidateDrivenParts(vehicle);
                        SavePreview(vehicle);
                    }
                    Debug.Log(SessionState.GetBool(SessionKey + ".h8Only", false) ?
                        "Vehicle art Play Mode check passed for blue/red H8." :
                        "Vehicle art Play Mode check passed for blue/red H8 and A40.");
                    Finish(null);
                }
            }
            catch (Exception error) { Finish(error); }
        }

        private static PrototypeCombinedArmsVehicle[] FindTargets()
        {
            PrototypeCombinedArmsVehicle[] all = UnityEngine.Object
                .FindObjectsByType<PrototypeCombinedArmsVehicle>();
            var result = new List<PrototypeCombinedArmsVehicle>();
            PrototypeVehicleKind[] kinds = SessionState.GetBool(SessionKey + ".h8Only", false) ?
                new[] { PrototypeVehicleKind.Helicopter } :
                new[] { PrototypeVehicleKind.Helicopter, PrototypeVehicleKind.AntiAir };
            foreach (PrototypeVehicleKind kind in kinds)
                foreach (PrototypeTeam team in new[] { PrototypeTeam.Blue, PrototypeTeam.Red })
                {
                    PrototypeCombinedArmsVehicle target = all.FirstOrDefault(item =>
                        item.Kind == kind && item.Team == team);
                    if (target == null) return null;
                    result.Add(target);
                }
            return result.ToArray();
        }

        private static PrototypeCombinedVehicleVisual Model(PrototypeCombinedArmsVehicle vehicle)
        {
            PrototypeCombinedVehicleVisual model = ModelField?.GetValue(vehicle) as PrototypeCombinedVehicleVisual;
            if (model == null) throw new InvalidOperationException(vehicle.name + " has no visual model.");
            return model;
        }

        private static void ValidateSpawn(PrototypeCombinedArmsVehicle vehicle)
        {
            PrototypeVehicleArtRig art = vehicle.GetComponentInChildren<PrototypeVehicleArtRig>();
            if (art == null || !art.gameObject.activeInHierarchy)
                throw new InvalidOperationException(vehicle.name + " did not instantiate authored art.");
            LODGroup group = art.GetComponent<LODGroup>();
            if (group == null || group.GetLODs().Length != 3 ||
                group.GetLODs().Any(lod => lod.renderers == null || lod.renderers.Length == 0))
                throw new InvalidOperationException(vehicle.name + " lacks complete LOD0/1/2.");
            Renderer[] renderers = art.GetComponentsInChildren<Renderer>(true);
            string side = vehicle.Team == PrototypeTeam.Blue ? "Blue" : "Red";
            if (!renderers.Any(renderer => renderer.sharedMaterials.Any(material =>
                material != null && material.GetTexture("_BaseMap") != null &&
                material.GetTexture("_BaseMap").name.IndexOf(side, StringComparison.OrdinalIgnoreCase) >= 0)))
                throw new InvalidOperationException(vehicle.name + " has no " + side + " PBR atlas.");
            if (renderers.Any(renderer => renderer.sharedMaterials.Any(material =>
                material == null || material.shader == null ||
                material.shader.name != "Universal Render Pipeline/Lit")))
                throw new InvalidOperationException(vehicle.name + " has a missing or non-URP material.");

            PrototypeCombinedVehicleVisual model = Model(vehicle);
            if (model.Hull == null || !model.Hull.enabled)
                throw new InvalidOperationException(vehicle.name + " has no live hull collider.");
            Vector3 muzzle = vehicle.transform.InverseTransformPoint(model.Muzzle.position);
            if (vehicle.Kind == PrototypeVehicleKind.Helicopter)
            {
                if (art.MainRotors.Length != 3 || art.TailRotors.Length != 3 ||
                    model.Hull.size.z < 15f || muzzle.z < 7.8f || muzzle.z > 8.8f ||
                    vehicle.transform.InverseTransformPoint(vehicle.GetSeatCameraPosition(0)).y < 3.3f ||
                    vehicle.transform.InverseTransformPoint(model.GunnerMuzzle.position).y < 1f)
                    throw new InvalidOperationException(vehicle.name +
                        " H8 pivot, collision, camera or fire point is not calibrated: muzzle " + muzzle);
            }
            else if (art.Turrets.Length != 3 || art.Barrels.Length != 3 ||
                     art.Radars.Length != 3 || art.Wheels.Length != 24 ||
                     muzzle.z < 2.2f || muzzle.z > 3f || model.Hull.size.y < 3f)
                throw new InvalidOperationException(vehicle.name +
                    " A40 pivot, wheel, collision or fire point is not calibrated: muzzle " + muzzle);
        }

        private static void ValidateDrivenParts(PrototypeCombinedArmsVehicle vehicle)
        {
            PrototypeVehicleArtRig art = vehicle.GetComponentInChildren<PrototypeVehicleArtRig>();
            PrototypeCombinedVehicleVisual model = Model(vehicle);
            if (vehicle.Kind == PrototypeVehicleKind.Helicopter)
            {
                AssertRotations(art.MainRotors, model.Rotor, vehicle.name + " main rotor");
                AssertRotations(art.TailRotors, model.TailRotor, vehicle.name + " tail rotor");
            }
            else
            {
                AssertRotations(art.Turrets, model.Turret, vehicle.name + " turret");
                AssertRotations(art.Barrels, model.Barrel, vehicle.name + " barrels");
                AssertRotations(art.Radars, model.Radar, vehicle.name + " radar");
                if (Quaternion.Angle(WheelStart[vehicle], art.Wheels[0].localRotation) < 20f)
                    throw new InvalidOperationException(vehicle.name + " wheel did not rotate after moving .5 m.");
            }
        }

        private static void AssertRotations(Transform[] artParts, Transform logic, string label)
        {
            foreach (Transform part in artParts)
                if (Quaternion.Angle(part.localRotation, logic.localRotation) > .1f)
                    throw new InvalidOperationException(label + " did not follow its gameplay pivot.");
        }

        private static void SavePreview(PrototypeCombinedArmsVehicle vehicle)
        {
            PrototypeVehicleArtRig art = vehicle.GetComponentInChildren<PrototypeVehicleArtRig>();
            LODGroup group = art.GetComponent<LODGroup>();
            group.ForceLOD(0);
            Renderer[] renderers = group.GetLODs()[0].renderers;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float distance = Mathf.Max(11f, bounds.size.magnitude * .95f);
            GameObject cameraObject = new GameObject("Vehicle art smoke camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.31f, .37f, .43f);
            camera.fieldOfView = 48f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 500f;
            Vector3 direction = vehicle.transform.rotation * new Vector3(.62f, .42f, .70f).normalized;
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center);
            Vector3 overviewPosition = camera.transform.position;
            Quaternion overviewRotation = camera.transform.rotation;
            RenderTexture target = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(1024, 768, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                image.Apply();
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
                string output = Path.Combine(project, "art/vehicles/unity-preview");
                Directory.CreateDirectory(output);
                string key = vehicle.Kind == PrototypeVehicleKind.Helicopter ? "H8" : "A40";
                string side = vehicle.Team == PrototypeTeam.Blue ? "Blue" : "Red";
                string path = Path.Combine(output, key + "_" + side + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("Vehicle art Play Mode scene preview: " + path);
                if (vehicle.Kind == PrototypeVehicleKind.Helicopter)
                {
                    camera.transform.position = vehicle.GetSeatCameraPosition(0);
                    camera.transform.rotation = vehicle.transform.rotation;
                    camera.fieldOfView = 72f;
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                    image.Apply();
                    string pilotPath = Path.Combine(output, key + "_" + side + "_Pilot.png");
                    File.WriteAllBytes(pilotPath, image.EncodeToPNG());
                    Debug.Log("Vehicle art pilot camera preview: " + pilotPath);
                }
                {
                    // A magenta isolation pass distinguishes an actual mesh
                    // opening from a painted or lit yellow fleck.
                    Transform[] artParts = art.GetComponentsInChildren<Transform>(true);
                    int[] previousLayers = artParts.Select(part => part.gameObject.layer).ToArray();
                    try
                    {
                        foreach (Transform part in artParts) part.gameObject.layer = 30;
                        camera.cullingMask = 1 << 30;
                        camera.backgroundColor = new Color(.7f, .1f, .7f);
                        camera.transform.position = overviewPosition;
                        camera.transform.rotation = overviewRotation;
                        camera.fieldOfView = 48f;
                        camera.Render();
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                        image.Apply();
                        string isolatedPath = Path.Combine(output, key + "_" + side + "_Isolated.png");
                        File.WriteAllBytes(isolatedPath, image.EncodeToPNG());
                        Debug.Log("Vehicle art isolated preview: " + isolatedPath);
                    }
                    finally
                    {
                        for (int i = 0; i < artParts.Length; i++)
                            artParts[i].gameObject.layer = previousLayers[i];
                    }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                group.ForceLOD(-1);
            }
        }

        private static void Finish(Exception error)
        {
            SessionState.SetBool(SessionKey, false);
            WheelStart.Clear();
            if (error != null) Debug.LogError("Vehicle art Play Mode check failed: " + error);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
