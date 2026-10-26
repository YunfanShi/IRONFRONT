using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class EquipmentArtAssetSetup
    {
        private static readonly string[] Names = { "SupplyBag", "Beacon", "FragGrenade", "MedicalKit", "RepairTool", "SmokeGrenade" };
        private static readonly float[] SourceHeights = { .294434f, .419936f, .114989f, .280064f, .212456f, .145012f };
        private static readonly Vector3[] SourceSizes = {
            new Vector3(.500051f,.294434f,.181925f), new Vector3(.348762f,.419936f,.111232f),
            new Vector3(.076813f,.114989f,.066332f), new Vector3(.254288f,.280064f,.136087f),
            new Vector3(.076222f,.212456f,.340017f), new Vector3(.059584f,.145012f,.048809f) };
        private const string Art = "Assets/Art/Equipment/";
        private const string ResourcesFolder = "Assets/Resources/Equipment";

        public static void BuildPlayer()
        {
            Build();
            PlayerSettings.bundleVersion = "0.64.0";
            AssetDatabase.SaveAssets();
            PrototypeOptimizationBuild.Build();
        }

        [MenuItem("IRONFRONT/Art/Build Six Equipment Prefabs")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(ResourcesFolder); AssetDatabase.Refresh();
            var lines = new List<string>();
            for (int i = 0; i < Names.Length; i++) BuildOne(i, lines);
            AssetDatabase.SaveAssets();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/equipment/integration-2026-10-26/import.txt"));
            File.WriteAllLines(output, lines);
            Debug.Log("IRONFRONT equipment art imported: six prefabs, three LODs, URP PBR; " + output);
        }

        private static void BuildOne(int index, List<string> report)
        {
            string name = Names[index], key = (index + 1).ToString("00"), folder = Art + name;
            string tex = folder + "/Textures/Asset_" + key;
            Texture2D color = Texture(tex + "_BaseColor.png", true, false, false);
            Texture2D normal = Texture(tex + "_Normal.png", false, true, false);
            Texture2D mask = Texture(tex + "_MetallicSmoothness.png", false, false, true);
            string materialPath = folder + "/" + name + "_PBR.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", color);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP"); material.SetTexture("_MetallicGlossMap", mask);
            material.SetFloat("_Metallic", 1f); material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_WorkflowMode", 1f); material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.DisableKeyword("_SPECULAR_SETUP");
            material.SetFloat("_Surface", 0f); material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);
            var root = new GameObject(name);
            try
            {
                var marker = root.AddComponent<PrototypeEquipmentArt>(); marker.AssetKey = name;
                var lods = new List<LOD>();
                Quaternion orientation = Quaternion.identity;
                float normalization = 1f;
                Vector3 offset = Vector3.zero;
                for (int level = 0; level < 3; level++)
                {
                    string path = folder + "/Models/Asset_" + key + "_LOD" + level + ".fbx";
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    importer.globalScale = 1f; importer.useFileUnits = true; importer.bakeAxisConversion = false;
                    importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
                    importer.importCameras = false; importer.importLights = false; importer.isReadable = false;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.meshCompression = ModelImporterMeshCompression.Off;
                    importer.SaveAndReimport();
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance = UnityEngine.Object.Instantiate(source, root.transform, false);
                    instance.name = "LOD" + level;
                    if (level == 0)
                    {
                        // Select the axis correction from the known authored dimensions;
                        // do not assume that every FBX exporter uses the same root basis.
                        float best = float.MaxValue;
                        foreach (Quaternion candidate in new[] { Quaternion.identity, Quaternion.Euler(-90f, 0f, 0f) })
                        {
                            instance.transform.localRotation = candidate;
                            Vector3 size = BoundsOf(instance).size;
                            float error = (size / size.y - SourceSizes[index] / SourceHeights[index]).sqrMagnitude;
                            if (error < best) { best = error; orientation = candidate; }
                        }
                        if (best > .01f) throw new InvalidOperationException(name + " FBX axis ratios differ: " + best);
                        instance.transform.localRotation = orientation;
                        Bounds bounds = BoundsOf(instance);
                        bool throwable = index == 2 || index == 5;
                        float displayHeight = index == 2 ? .27f : index == 5 ? .30f : SourceHeights[index];
                        normalization = displayHeight / bounds.size.y;
                        offset = new Vector3(-bounds.center.x, throwable ? -bounds.center.y : -bounds.min.y, -bounds.center.z) * normalization;
                    }
                    instance.transform.localRotation = orientation;
                    instance.transform.localScale = Vector3.one * normalization;
                    instance.transform.localPosition = offset;
                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    foreach (Renderer renderer in renderers) renderer.sharedMaterials = new[] { material };
                    if (level == 0) marker.LocalBounds = BoundsOf(instance);
                    lods.Add(new LOD(level == 0 ? .24f : level == 1 ? .065f : .006f, renderers));
                    int triangles = instance.GetComponentsInChildren<MeshFilter>().Sum(m => (int)m.sharedMesh.GetIndexCount(0) / 3);
                    report.Add(name + " LOD" + level + " triangles=" + triangles + " bounds=" + BoundsOf(instance) +
                        " rotation=" + orientation.eulerAngles + " importScale=" + normalization);
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(lods.ToArray()); group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, ResourcesFolder + "/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Bounds BoundsOf(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Texture2D Texture(string path, bool srgb, bool normal, bool alpha)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.maxTextureSize = 2048; importer.isReadable = false;
            importer.mipmapEnabled = true; importer.streamingMipmaps = true;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
