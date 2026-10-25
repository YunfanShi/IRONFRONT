using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class SoldierArtAssetSetup
    {
        public static readonly string[] BoneNames = { "Pelvis", "Torso", "Head", "UpperArm_L", "UpperArm_R", "Forearm_L", "Forearm_R", "Hand_L", "Hand_R", "Thigh_L", "Thigh_R", "Shin_L", "Shin_R", "Foot_L", "Foot_R", "Spine" };
        [MenuItem("IRONFRONT/Art/Build Runtime Soldiers")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Resources/Soldiers");
            foreach (string team in new[] { "Blue", "Red" }) BuildTeam(team);
            AssetDatabase.SaveAssets();
            Debug.Log("SOLDIER_ASSET_SETUP_PASS");
        }
        private static Mesh BuildViewMesh(SkinnedMeshRenderer renderer, string folder)
        {
            var mesh = UnityEngine.Object.Instantiate(renderer.sharedMesh); mesh.name = renderer.name + "_FirstPerson";
            // World-body seams contain a few chest influences. A camera-only
            // arm must not drag those vertices toward the hidden rest torso.
            string side = renderer.name.EndsWith("_ArmL") ? "_R" : "_L";
            int upper = Array.FindIndex(renderer.bones, b => b.name == "UpperArm" + side);
            if (upper < 0) throw new InvalidOperationException("Missing view arm bone");
            var weights = mesh.boneWeights; var armAmount = new float[weights.Length];
            for (int v = 0; v < weights.Length; v++)
            {
                var w = weights[v]; int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                float[] values = { w.weight0, w.weight1, w.weight2, w.weight3 };
                var merged = new Dictionary<int, float>();
                for (int j = 0; j < 4; j++)
                {
                    if (values[j] <= 0) continue;
                    string name = renderer.bones[ids[j]].name;
                    bool arm = name.EndsWith(side) && (name.StartsWith("UpperArm") || name.StartsWith("Forearm") || name.StartsWith("Hand") || name.StartsWith("Fingers"));
                    if (arm) armAmount[v] += values[j]; else ids[j] = upper;
                    if (!merged.ContainsKey(ids[j])) merged[ids[j]] = 0;
                    merged[ids[j]] += values[j];
                }
                var pairs = merged.OrderByDescending(p => p.Value).ToArray(); var n = new BoneWeight();
                if (pairs.Length > 0) { n.boneIndex0 = pairs[0].Key; n.weight0 = pairs[0].Value; }
                if (pairs.Length > 1) { n.boneIndex1 = pairs[1].Key; n.weight1 = pairs[1].Value; }
                if (pairs.Length > 2) { n.boneIndex2 = pairs[2].Key; n.weight2 = pairs[2].Value; }
                if (pairs.Length > 3) { n.boneIndex3 = pairs[3].Key; n.weight3 = pairs[3].Value; }
                weights[v] = n;
            }
            mesh.boneWeights = weights;
            var source = mesh.triangles; var triangles = new List<int>();
            for (int i = 0; i < source.Length; i += 3)
                if ((armAmount[source[i]] + armAmount[source[i + 1]] + armAmount[source[i + 2]]) / 3f > .92f)
                { triangles.Add(source[i]); triangles.Add(source[i + 1]); triangles.Add(source[i + 2]); }
            mesh.triangles = triangles.ToArray();
            string path = folder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(mesh); return existing;
        }
        private static void BuildTeam(string team)
        {
            string lower = team.ToLowerInvariant();
            string folder = "Assets/Art/Characters/" + team + "Soldier";
            string destination = folder + "/rigged"; Directory.CreateDirectory(destination);
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/characters/" + lower + "-soldier/rigged-v02"));
            for (int i = 0; i < 3; i++) File.Copy(Path.Combine(source, lower + "-soldier-skinned-lod" + i + ".fbx"), destination + "/" + lower + "-soldier-skinned-lod" + i + ".fbx", true);
            AssetDatabase.Refresh();
            var root = new GameObject(team + "Soldier");
            try
            {
                var rig = root.AddComponent<PrototypeSoldierArtRig>();
                var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + team + "Soldier_URP.mat");
                if (material == null) throw new InvalidOperationException("Missing soldier PBR");
                var lods = new LOD[3]; var bodies = new List<SkinnedMeshRenderer>(); var arms = new List<SkinnedMeshRenderer>();
                Dictionary<string, Transform> skeleton = null;
                for (int i = 0; i < 3; i++)
                {
                    string path = destination + "/" + lower + "-soldier-skinned-lod" + i + ".fbx";
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    importer.globalScale = 1; importer.useFileUnits = true; importer.bakeAxisConversion = false;
                    importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = false;
                    importer.optimizeGameObjects = false; importer.isReadable = true;
                    importer.skinWeights = ModelImporterSkinWeights.Custom; importer.maxBonesPerVertex = 4;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.SaveAndReimport();
                    var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
                    instance.name = "LOD" + i;
                    instance.transform.localRotation = Quaternion.identity;
                    // FBX handedness reflects X. Map source sides to their actual
                    // Unity side while preserving the forward-facing model.
                    foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.EndsWith("_L")) t.name = t.name.Substring(0, t.name.Length - 2) + "_R";
                        else if (t.name.EndsWith("_R")) t.name = t.name.Substring(0, t.name.Length - 2) + "_L";
                    }
                    var animator = instance.GetComponent<Animator>(); if (animator != null) UnityEngine.Object.DestroyImmediate(animator);
                    var rs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (rs.Length != 3) throw new InvalidOperationException("Expected body + two arms: " + rs.Length);
                    if (i == 0)
                    {
                        skeleton = instance.GetComponentsInChildren<Transform>(true).Where(t => BoneNames.Contains(t.name) || t.name.StartsWith("Fingers_")).ToDictionary(t => t.name);
                        var bindings = new List<PrototypeSoldierArtRig.Binding>();
                        for (int n = 0; n < BoneNames.Length; n++)
                        {
                            var bone = skeleton[BoneNames[n]]; Quaternion direction = Quaternion.identity;
                            string child = n == 3 ? "Forearm_L" : n == 4 ? "Forearm_R" : n == 5 ? "Hand_L" : n == 6 ? "Hand_R" : n == 9 ? "Shin_L" : n == 10 ? "Shin_R" : n == 11 ? "Foot_L" : n == 12 ? "Foot_R" : null;
                            if (child != null) direction = Quaternion.FromToRotation(Vector3.down, skeleton[child].position - bone.position);
                            Quaternion grip = n == 7 ? Quaternion.Euler(0, -90, 0) : n == 8 ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
                            bindings.Add(new PrototypeSoldierArtRig.Binding { Bone = bone, Driver = n, RotationOffset = grip * Quaternion.Inverse(direction) * bone.rotation });
                            Debug.Log(team + " bone " + bone.name + " pos=" + bone.position.ToString("F4") + " rot=" + bone.eulerAngles);
                        }
                        rig.Bindings = bindings.ToArray();
                        rig.Fingers = new[] { skeleton["Fingers_L"], skeleton["Fingers_R"] };
                        rig.FingerRest = rig.Fingers.Select(t => t.localRotation).ToArray();
                    }
                    foreach (var r in rs)
                    {
                        r.sharedMaterial = material; r.quality = SkinQuality.Bone4;
                        if (i > 0) { r.bones = r.bones.Select(b => skeleton[b.name]).ToArray(); r.rootBone = skeleton["Pelvis"]; }
                        // Fixed conservative bounds cover prone, running and seated poses.
                        r.localBounds = new Bounds(Vector3.zero, Vector3.one * 5f);
                        r.updateWhenOffscreen = false;
                        if (r.name.EndsWith("_Body")) bodies.Add(r); else arms.Add(r);
                        if (r.sharedMesh.boneWeights.Length != r.sharedMesh.vertexCount) throw new InvalidOperationException("Missing skin");
                    }
                    if (i > 0)
                    {
                        var unused = instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Pelvis");
                        if (unused != null) UnityEngine.Object.DestroyImmediate(unused.gameObject);
                    }
                    lods[i] = new LOD(new[] { .45f, .16f, .015f }[i], rs);
                    Debug.Log(team + " skinned LOD" + i + " triangles=" + rs.Sum(r => r.sharedMesh.triangles.Length / 3));
                }
                rig.Bodies = bodies.ToArray(); rig.Arms = arms.ToArray();
                rig.FullArmMeshes = arms.Select(r => r.sharedMesh).ToArray();
                rig.ViewArmMeshes = arms.Select(r => BuildViewMesh(r, destination)).ToArray();
                var group = root.AddComponent<LODGroup>(); group.SetLODs(lods);
                group.localReferencePoint = new Vector3(0, .9f, 0); group.size = 1.8f;
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Soldiers/" + team + "Soldier.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
