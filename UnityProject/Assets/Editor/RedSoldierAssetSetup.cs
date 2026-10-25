using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Creates a static art-review prefab. Gameplay integration still needs skinning.
    public static class RedSoldierAssetSetup
    {
        private const string Root = "Assets/Art/Characters/RedSoldier";

        [MenuItem("IRONFRONT/Art/Build Red Soldier Static Preview")]
        public static void Generate()
        {
            Texture2D baseColor = LoadTexture("red-soldier-basecolor-2k.png");
            Texture2D normal = LoadTexture("red-soldier-normal-2k.png");
            Texture2D roughness = LoadTexture("red-soldier-roughness-2k.png");
            Texture2D metallic = LoadTexture("red-soldier-metallic-2k.png");
            ConfigureTexture(normal, TextureImporterType.NormalMap, false, false);
            ConfigureTexture(roughness, TextureImporterType.Default, false, true);
            ConfigureTexture(metallic, TextureImporterType.Default, false, true);
            roughness = LoadTexture("red-soldier-roughness-2k.png");
            metallic = LoadTexture("red-soldier-metallic-2k.png");

            string maskPath = Root + "/textures/red-soldier-metallic-smoothness-2k.png";
            Color32[] roughPixels = roughness.GetPixels32();
            Color32[] metalPixels = metallic.GetPixels32();
            if (roughness.width != metallic.width || roughness.height != metallic.height)
                throw new InvalidOperationException("Metallic and roughness sizes differ.");
            Color32[] maskPixels = new Color32[roughPixels.Length];
            for (int i = 0; i < maskPixels.Length; i++)
                maskPixels[i] = new Color32(metalPixels[i].r, 0, 0,
                    (byte)(255 - roughPixels[i].r));
            Texture2D mask = new Texture2D(roughness.width, roughness.height,
                TextureFormat.RGBA32, false, true);
            mask.SetPixels32(maskPixels);
            mask.Apply(false, false);
            File.WriteAllBytes(Path.Combine(Application.dataPath,
                "Art/Characters/RedSoldier/textures/red-soldier-metallic-smoothness-2k.png"),
                mask.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(mask);
            AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceUpdate);
            ConfigureTexture(AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath),
                TextureImporterType.Default, false, false);
            ConfigureTexture(roughness, TextureImporterType.Default, false, false);
            ConfigureTexture(metallic, TextureImporterType.Default, false, false);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader not found.");
            string materialPath = Root + "/RedSoldier_URP.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", baseColor);
            material.SetTexture("_BumpMap", LoadTexture("red-soldier-normal-2k.png"));
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);

            GameObject root = new GameObject("RedSoldier_StaticLODPreview");
            LOD[] lods = new LOD[3];
            float[] thresholds = { .55f, .18f, .02f };
            int[] expectedTriangles = { 19984, 6000, 2000 };
            for (int i = 0; i < 3; i++)
            {
                string path = Root + "/exports/red-soldier-lod" + i + ".fbx";
                ConfigureModel(path);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) throw new InvalidOperationException("Missing FBX: " + path);
                GameObject child = UnityEngine.Object.Instantiate(model, root.transform);
                child.name = "LOD" + i;
                child.transform.localPosition = Vector3.zero;
                // Unity keeps this Blender FBX's mesh Z-up; rotate it to Unity Y-up.
                child.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                child.transform.localScale = Vector3.one;
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                    renderer.sharedMaterial = material;
                int triangles = 0;
                foreach (MeshFilter filter in child.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null)
                        triangles += filter.sharedMesh.triangles.Length / 3;
                if (triangles != expectedTriangles[i] || renderers.Length == 0)
                    throw new InvalidOperationException("LOD" + i + " import mismatch: " + triangles + " triangles.");
                Bounds visualBounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++)
                    visualBounds.Encapsulate(renderers[r].bounds);
                if (Mathf.Abs(visualBounds.size.y - 1.8f) > .03f)
                    throw new InvalidOperationException("LOD" + i + " Unity height is " + visualBounds.size.y + " m.");
                Debug.Log("Red Soldier LOD" + i + ": " + triangles +
                    " triangles, " + visualBounds.size.y + " m high");
                lods[i] = new LOD(thresholds[i], renderers);
            }
            LODGroup group = root.AddComponent<LODGroup>();
            group.SetLODs(lods);
            group.RecalculateBounds();
            string prefabPath = Root + "/RedSoldier_StaticLODPreview.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Red Soldier static art preview generated: " + prefabPath);
        }

        private static Texture2D LoadTexture(string filename)
        {
            string path = Root + "/textures/" + filename;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Missing texture: " + path);
            return texture;
        }

        private static void ConfigureTexture(Texture2D texture,
            TextureImporterType type, bool sRgb, bool readable)
        {
            if (texture == null) throw new InvalidOperationException("Texture is missing.");
            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("TextureImporter missing: " + path);
            if (importer.textureType == type && importer.sRGBTexture == sRgb &&
                importer.isReadable == readable) return;
            importer.textureType = type;
            importer.sRGBTexture = sRgb;
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }

        private static void ConfigureModel(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("ModelImporter missing: " + path);
            if (Mathf.Approximately(importer.globalScale, 100f) &&
                importer.useFileUnits && !importer.bakeAxisConversion) return;
            // Unity reads this FBX at 0.01x; compensate in the model import settings.
            importer.globalScale = 100f;
            importer.useFileUnits = true;
            importer.bakeAxisConversion = false;
            importer.SaveAndReimport();
        }
    }
}
