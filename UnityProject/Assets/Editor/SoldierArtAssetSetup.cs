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
        public static void BuildPlayer() { Build(); PrototypeOptimizationBuild.Build(); }
        public static readonly string[] BoneNames = { "Pelvis", "Torso", "Head", "UpperArm_L", "UpperArm_R", "Forearm_L", "Forearm_R", "Hand_L", "Hand_R", "Thigh_L", "Thigh_R", "Shin_L", "Shin_R", "Foot_L", "Foot_R", "Spine" };
        [MenuItem("IRONFRONT/Art/Build Runtime Soldiers")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Resources/Soldiers");
            foreach (string team in new[] { "Blue", "Red" }) BuildTeam(team, false);
            AssetDatabase.SaveAssets();
            Debug.Log("SOLDIER_ASSET_SETUP_PASS");
        }
        public static void BuildHandCandidates()
        {
            Directory.CreateDirectory("Assets/Resources/Soldiers/HandCandidate");
            foreach (string team in new[] { "Blue", "Red" }) BuildTeam(team, true);
            AssetDatabase.SaveAssets();
            Debug.Log("HAND_CANDIDATE_IMPORT_PASS");
        }
        public static void BuildHandCandidatePlayer() { BuildHandCandidates(); PrototypeOptimizationBuild.Build(); }
        static bool IsDigit(string name) => new[] { "Index", "Middle", "Ring", "Pinky", "Thumb", "Fingers" }.Any(name.StartsWith);
        private static Mesh BuildViewMesh(SkinnedMeshRenderer renderer, string folder)
        {
            var mesh = UnityEngine.Object.Instantiate(renderer.sharedMesh); mesh.name = renderer.name + "_FirstPerson";
            // World-body seams contain a few chest influences. A camera-only
            // arm must not drag those vertices toward the hidden rest torso.
            string side = renderer.name.EndsWith("_ArmL") ? "_R" : "_L";
            int upper = Array.FindIndex(renderer.bones, b => b.name == "UpperArm" + side);
            if (upper < 0) throw new InvalidOperationException("Missing view arm bone");
            var weights = mesh.boneWeights;
            for (int v = 0; v < weights.Length; v++)
            {
                var w = weights[v]; int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                float[] values = { w.weight0, w.weight1, w.weight2, w.weight3 };
                var merged = new Dictionary<int, float>();
                for (int j = 0; j < 4; j++)
                {
                    if (values[j] <= 0) continue;
                    string name = renderer.bones[ids[j]].name;
                    bool arm = name.EndsWith(side) && (name.StartsWith("UpperArm") || name.StartsWith("Forearm") || name.StartsWith("Hand") || IsDigit(name));
                    if (!arm) ids[j] = upper;
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
            // Arm objects already contain only arm geometry. Deleting triangles
            // with a small torso influence punched holes through the elbow and
            // sleeve. Remap those weights above and retain the complete surface.
            string path = folder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(mesh); return existing;
        }
        private static void BuildTeam(string team, bool candidate)
        {
            string lower = team.ToLowerInvariant();
            string folder = "Assets/Art/Characters/" + team + "Soldier";
            string destination = folder + (candidate ? "/hand-candidate-v07" : "/rigged-v03"); Directory.CreateDirectory(destination);
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, candidate ? "../../art/characters/sleeve-width-2026-10-27/candidate/" + lower : "../../art/characters/" + lower + "-soldier/rigged-v03"));
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
                    importer.materialImportMode = candidate ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
                    if(candidate)importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
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
                        skeleton = instance.GetComponentsInChildren<Transform>(true).Where(t => BoneNames.Contains(t.name) || IsDigit(t.name)).ToDictionary(t => t.name);
                        var bindings = new List<PrototypeSoldierArtRig.Binding>();
                        for (int n = 0; n < BoneNames.Length; n++)
                        {
                            var bone = skeleton[BoneNames[n]]; Quaternion direction = Quaternion.identity;
                            string child = n == 3 ? "Forearm_L" : n == 4 ? "Forearm_R" : n == 5 ? "Hand_L" : n == 6 ? "Hand_R" : n == 9 ? "Shin_L" : n == 10 ? "Shin_R" : n == 11 ? "Foot_L" : n == 12 ? "Foot_R" : null;
                            if (child != null) direction = Quaternion.FromToRotation(Vector3.down, skeleton[child].position - bone.position);
                            Quaternion grip = n == 7 ? Quaternion.Euler(0, -90, 0) : n == 8 ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
                            Quaternion offset = grip * Quaternion.Inverse(direction) * bone.rotation;
                            if (candidate && (n == 7 || n == 8))
                            {
                                // A hand pose now has anatomical axes: +Z wrist-to-knuckles,
                                // +Y pinky-to-index. This removes the old mitten's arbitrary roll.
                                string side = n == 7 ? "_L" : "_R";
                                Vector3 distal = skeleton["Middle1" + side].position - bone.position;
                                Vector3 across = skeleton["Index1" + side].position - skeleton["Pinky1" + side].position;
                                offset = Quaternion.Inverse(Quaternion.LookRotation(distal, across)) * bone.rotation;
                            }
                            bindings.Add(new PrototypeSoldierArtRig.Binding { Bone = bone, Driver = n, RotationOffset = offset });
                            Debug.Log(team + " bone " + bone.name + " pos=" + bone.position.ToString("F4") + " rot=" + bone.eulerAngles);
                        }
                        rig.Bindings = bindings.ToArray();
                        rig.ForearmLength = Vector3.Distance(skeleton["Forearm_R"].position,skeleton["Hand_R"].position);
                        if (candidate)
                        {
                            rig.Fingers = Array.Empty<Transform>(); rig.FingerRest = Array.Empty<Quaternion>();
                            rig.Thumbs = Array.Empty<Transform>(); rig.ThumbRest = Array.Empty<Quaternion>();
                            var joints = new List<PrototypeSoldierArtRig.DigitJoint>();
                            string[] digits = { "Index", "Middle", "Ring", "Pinky", "Thumb" };
                            for (int side = 0; side < 2; side++)
                            for (int digit = 0; digit < digits.Length; digit++)
                            for (int joint = 0; joint < 3; joint++)
                            {
                                var t = skeleton[digits[digit] + (joint + 1) + (side == 0 ? "_L" : "_R")];
                                joints.Add(new PrototypeSoldierArtRig.DigitJoint { Bone = t, Rest = t.localRotation,
                                    Left = side == 0, Digit = digit, Joint = joint });
                            }
                            rig.Digits = joints.ToArray();
                        }
                        else
                        {
                            rig.Fingers = new[] { skeleton["Fingers_L"], skeleton["Fingers_R"] };
                            rig.FingerRest = rig.Fingers.Select(t => t.localRotation).ToArray();
                            rig.Thumbs = new[] { skeleton["Thumb_L"], skeleton["Thumb_R"] };
                            rig.ThumbRest = rig.Thumbs.Select(t => t.localRotation).ToArray();
                        }
                    }
                    foreach (var r in rs)
                    {
                        if (candidate && r.sharedMesh.subMeshCount > 1)
                        {
                            string glovePath = destination + "/HandGlove.mat";
                            var glove = AssetDatabase.LoadAssetAtPath<Material>(glovePath);
                            if (glove == null) { glove = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(glove, glovePath); }
                            glove.SetColor("_BaseColor", new Color(.11f,.125f,.14f)); glove.SetFloat("_Smoothness", .18f);glove.SetFloat("_Metallic",0f);
                            var candidateMaterials = new Material[] { material, glove, null, null };
                            for(int slot=2;slot<4;slot++)
                            {
                                string surfacePath=destination+(slot==2?"/Sleeve.mat":"/ForearmPad.mat");
                                var surface=AssetDatabase.LoadAssetAtPath<Material>(surfacePath);
                                if(surface==null){surface=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(surface,surfacePath);}
                                bool blue=team=="Blue";
                                surface.SetColor("_BaseColor",slot==2?(blue?new Color(.10f,.15f,.23f):new Color(.23f,.10f,.11f)):(blue?new Color(.31f,.41f,.56f):new Color(.48f,.24f,.25f)));
                                surface.SetFloat("_Smoothness",slot==2?.12f:.25f);surface.SetFloat("_Metallic",0f);
                                if(slot==2)
                                {
                                    string weavePath=destination+"/SleeveWeave.asset";
                                    var weave=AssetDatabase.LoadAssetAtPath<Texture2D>(weavePath);
                                    if(weave==null)
                                    {
                                        weave=new Texture2D(128,128,TextureFormat.RGB24,true,true);weave.name="Sleeve fabric weave";
                                        var pixels=new Color[128*128];
                                        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                                        {float value=.88f+.06f*Mathf.PerlinNoise(x*.16f,y*.16f)+(((x/2+y/2)%2==0)?.025f:-.025f);pixels[y*128+x]=new Color(value,value,value);}
                                        weave.SetPixels(pixels);weave.Apply();weave.wrapMode=TextureWrapMode.Repeat;AssetDatabase.CreateAsset(weave,weavePath);
                                    }
                                    surface.SetTexture("_BaseMap",weave);surface.SetTextureScale("_BaseMap",new Vector2(4,4));
                                }
                                candidateMaterials[slot]=surface;
                            }
                            Debug.Log("CANDIDATE_MATERIAL_SLOTS "+r.name+" "+string.Join(",",r.sharedMaterials.Select(m=>m==null?"null":m.name)));
                            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.StartsWith("HandGlove") ? glove : m != null && m.name.StartsWith("Candidate sleeve") ? candidateMaterials[2] : m != null && m.name.StartsWith("Candidate armor") ? candidateMaterials[3] : material).ToArray();
                        }
                        else r.sharedMaterial = material;
                        r.quality = SkinQuality.Bone4;
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
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Soldiers/" + (candidate ? "HandCandidate/" : "") + team + "Soldier.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
