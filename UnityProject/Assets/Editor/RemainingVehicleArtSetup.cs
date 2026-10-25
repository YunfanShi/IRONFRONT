using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static partial class VehicleArtAssetSetup
    {
        private static bool IsRemaining(string key) => key == "R4" || key == "U8" || key == "M2" || key == "R3" || key == "J20";
        [MenuItem("IRONFRONT/Art/Build Remaining Vehicle Prefabs")]
        public static void BuildRemaining()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Build("R4", "R4Scout"); Build("U8", "U8Rover"); Build("M2", "M2Courier");
            Build("R3", "R3Firefly"); Build("J20", "J20Stratus");
            Debug.Log("IRONFRONT remaining vehicle prefabs PASS");
        }

        private static void BuildRemainingTeamPrefab(string key, string sourceRoot, string[] fbx,
            string team, Texture2D baseColor, Texture2D normal, Texture2D mask, Material joint)
        {
            Material pbr = BuildPbrMaterial(sourceRoot, key, team, baseColor, normal, mask);
            EnsureFolder(ResourcesRoot);
            GameObject root = new GameObject(key + "_" + team);
            try
            {
                Transform visual = new GameObject("VisualRoot").transform; visual.SetParent(root.transform, false);
                var rig = root.AddComponent<PrototypeVehicleArtRig>(); rig.VisualRoot = visual;
                var turrets = new List<Transform>(); var barrels = new List<Transform>();
                var main = new List<Transform>(); var tail = new List<Transform>(); var radars = new List<Transform>();
                var wheels = new List<Transform>(); var steering = new List<Transform>(); var radii = new List<float>();
                var lods = new LOD[3]; int previous = int.MaxValue;
                for (int lod = 0; lod < 3; lod++)
                {
                    Transform levelRoot = new GameObject("LOD" + lod).transform; levelRoot.SetParent(visual, false);
                    var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx[lod]), levelRoot);
                    source.name = "SourceFBX"; source.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
                    Transform steer = null;
                    if (key == "R4" || key == "U8" || key == "R3" || key == "J20")
                    {
                        var t = WrapPivot(FindPart(source.transform, "Turret"), levelRoot, "Turret"); turrets.Add(t);
                        barrels.Add(WrapPivot(FindPart(levelRoot, "Barrels"), t, "Barrels"));
                        if (lod == 0)
                        {
                            rig.TurretAnchor = t.position; rig.BarrelAnchor = barrels[0].position;
                            rig.MuzzleAnchor = FindPart(levelRoot, "Muzzle").position;
                        }
                    }
                    if (key == "M2")
                    {
                        steer = WrapPivot(FindPart(source.transform, "Steering"), levelRoot, "Steering"); steering.Add(steer);
                        if (lod == 0)
                        {
                            string[] names = { "DriverSeat", "PassengerSeat", "DriverEye", "PassengerEye", "LeftGrip", "RightGrip", "LeftPeg", "RightPeg", "PassengerLeftGrip", "PassengerRightGrip", "PassengerLeftPeg", "PassengerRightPeg" };
                            rig.ContactNames = names; rig.ContactAnchors = names.Select(n => FindPart(levelRoot, n).position).ToArray();
                            rig.SteeringAnchor = steer.position;
                        }
                    }
                    if (key == "R3")
                    {
                        main.Add(WrapPivot(FindPart(source.transform, "MainRotor"), levelRoot, "MainRotor"));
                        tail.Add(WrapPivot(FindPart(source.transform, "TailRotor"), levelRoot, "TailRotor"));
                        radars.Add(WrapPivot(FindPart(source.transform, "Radar"), levelRoot, "Radar"));
                    }
                    if (lod == 0 && (key == "R3" || key == "J20"))
                    {
                        rig.PilotEye = FindPart(levelRoot, "PilotEye").position;
                        rig.PilotSeat = FindPart(levelRoot, "PilotSeat").position;
                        rig.MuzzleAnchor = FindPart(levelRoot, "Muzzle").position;
                    }
                    Transform[] wheelParts = levelRoot.GetComponentsInChildren<Transform>(true)
                        .Where(t => t.name.StartsWith("Wheel_", StringComparison.Ordinal)).ToArray();
                    int count = key == "R4" || key == "U8" ? 4 : key == "M2" ? 2 : key == "J20" ? 3 : 0;
                    if (wheelParts.Length != count) throw new InvalidOperationException(key + " wheel count " + wheelParts.Length);
                    foreach (var wheel in wheelParts)
                    {
                        var bounds = wheel.GetComponent<Renderer>().bounds;
                        float radius = Mathf.Max(bounds.size.y, bounds.size.z) * .5f;
                        if (radius < .15f || radius > 1f) throw new InvalidOperationException(key + " wheel radius " + radius);
                        Transform parent = levelRoot;
                        if (key == "M2" && wheel.name.Contains("Front")) parent = steer;
                        if ((key == "R4" || key == "U8") && wheel.name.Contains("_0_LOD"))
                        {
                            parent = new GameObject("Steer_" + wheel.name).transform;
                            parent.SetParent(levelRoot, false); parent.position = wheel.position; steering.Add(parent);
                        }
                        wheels.Add(WrapPivot(wheel, parent, wheel.name)); radii.Add(radius);
                    }
                    Renderer[] renderers = levelRoot.GetComponentsInChildren<Renderer>(true);
                    foreach (var renderer in renderers)
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && m.name.Contains("JointSteel") ? joint : pbr).ToArray();
                    int triangles = levelRoot.GetComponentsInChildren<MeshFilter>(true).Sum(m => m.sharedMesh.triangles.Length / 3);
                    if (triangles <= 0 || triangles >= previous) throw new InvalidOperationException(key + " LOD order"); previous = triangles;
                    lods[lod] = new LOD(LodThresholds[lod], renderers);
                    if (lod == 0)
                    {
                        var hull = renderers.Single(r => r.name.StartsWith("Hull_LOD", StringComparison.Ordinal)).bounds;
                        rig.HullCenter = hull.center; rig.HullSize = hull.size;
                        // Exclude rotor discs, mirror/handlebar overhang and jet wings from the collision hull.
                        if (key == "M2") rig.HullSize = new Vector3(.8f, hull.size.y, 2.55f);
                        if (key == "R3") rig.HullSize = new Vector3(2.7f, 3.8f, 9.8f);
                        if (key == "J20") rig.HullSize = new Vector3(3.2f, 3.4f, hull.size.z);
                        if (key == "R4" || key == "U8")
                        { float h = rig.BarrelAnchor.y + .35f; rig.HullSize = new Vector3(hull.size.x, h, hull.size.z); rig.HullCenter = new Vector3(hull.center.x, h / 2, hull.center.z); }
                        var all = renderers[0].bounds; foreach (var r in renderers.Skip(1)) all.Encapsulate(r.bounds);
                        float length = key == "M2" ? 2.55f : key == "R3" ? 10.91f : key == "J20" ? 12.87f : key == "R4" ? 5.96f : 7.02f;
                        if (Mathf.Abs(all.size.z - length) > .15f) throw new InvalidOperationException(key + " axis/scale " + all.size);
                    }
                    Debug.Log(key + " " + team + " LOD" + lod + ": " + triangles + " triangles");
                }
                rig.Turrets = turrets.ToArray(); rig.Barrels = barrels.ToArray(); rig.MainRotors = main.ToArray();
                rig.TailRotors = tail.ToArray(); rig.Radars = radars.ToArray(); rig.Wheels = wheels.ToArray();
                rig.SteeringAxles = steering.ToArray(); rig.WheelRadii = radii.ToArray();
                if (radii.Count > 0) rig.WheelRadius = radii.Average();
                var group = root.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, ResourcesRoot + "/" + key + "_" + team + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
