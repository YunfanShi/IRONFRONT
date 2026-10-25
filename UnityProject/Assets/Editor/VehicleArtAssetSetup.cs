using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Rebuilds the runtime prefabs from the Blender FBX exports and 2K atlas.
    // Run after copying each asset's exports and textures into Assets/Art/Vehicles.
    public static partial class VehicleArtAssetSetup
    {
        private const string ResourcesRoot = "Assets/Resources/Vehicles";
        private static readonly float[] LodThresholds = { .52f, .18f, .025f };

        [MenuItem("IRONFRONT/Art/Build H8 Vehicle Prefabs")]
        public static void BuildH8() => Build("H8", "H8Kestrel");

        [MenuItem("IRONFRONT/Art/Build A40 Vehicle Prefabs")]
        public static void BuildA40() => Build("A40", "A40Watchtower");

        [MenuItem("IRONFRONT/Art/Build T90 Vehicle Prefabs")]
        public static void BuildT90() => Build("T90", "T90Bastion");

        [MenuItem("IRONFRONT/Art/Build V12 Vehicle Prefabs")]
        public static void BuildV12() => Build("V12", "V12Lancer");

        [MenuItem("IRONFRONT/Art/Build T90 and V12 Prefabs")]
        public static void BuildArmor()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildT90();
            BuildV12();
        }

        [MenuItem("IRONFRONT/Art/Build All Vehicle Prefabs")]
        public static void BuildAll()
        {
            BuildH8();
            BuildA40();
            BuildT90();
            BuildV12();
            BuildRemaining();
        }

        private static void Build(string key, string folder)
        {
            string sourceRoot = "Assets/Art/Vehicles/" + folder;
            string[] fbx = Enumerable.Range(0, 3).Select(lod => FindFbx(sourceRoot, lod)).ToArray();
            foreach (string path in fbx) ConfigureModel(path, key);
            Texture2D blue = FindTexture(sourceRoot, "blue", "basecolor", true);
            Texture2D red = FindTexture(sourceRoot, "red", "basecolor", false);
            Texture2D normal = FindTexture(sourceRoot, null, "normal", false);
            Texture2D metalRough = FindTexture(sourceRoot, null, "metalrough", false);
            string bluePath = AssetDatabase.GetAssetPath(blue);
            string redPath = red != null ? AssetDatabase.GetAssetPath(red) : null;
            string normalPath = normal != null ? AssetDatabase.GetAssetPath(normal) : null;
            string metalRoughPath = metalRough != null ? AssetDatabase.GetAssetPath(metalRough) : null;
            ConfigureTexture(blue, TextureImporterType.Default, true, false);
            if (red != null) ConfigureTexture(red, TextureImporterType.Default, true, false);
            if (normal != null) ConfigureTexture(normal, TextureImporterType.NormalMap, false, false);
            if (metalRough != null)
                ConfigureTexture(metalRough, TextureImporterType.Default, false, true);
            blue = AssetDatabase.LoadAssetAtPath<Texture2D>(bluePath);
            red = redPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(redPath) : null;
            normal = normalPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null;
            metalRough = metalRoughPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(metalRoughPath) : null;

            Texture2D mask = metalRough != null ? BuildMetallicSmoothness(sourceRoot, key, metalRough) : null;
            if (metalRough != null)
                ConfigureTexture(metalRough, TextureImporterType.Default, false, false);
            Material graphite = key == "H8" ? BuildGraphiteMaterial(sourceRoot) : null;
            if (key == "T90" || key == "V12" || IsRemaining(key)) graphite = BuildJointMaterial(sourceRoot, key);
            BuildTeamPrefab(key, sourceRoot, fbx, "Blue", blue, normal, mask, graphite);
            if (red != null)
                BuildTeamPrefab(key, sourceRoot, fbx, "Red", red, normal, mask, graphite);
            else Debug.Log(key + " has no red basecolor atlas; red vehicles retain prototype visuals.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildTeamPrefab(string key, string sourceRoot, string[] fbx,
            string team, Texture2D baseColor, Texture2D normal, Texture2D mask, Material graphite)
        {
            if (IsRemaining(key)) { BuildRemainingTeamPrefab(key, sourceRoot, fbx, team, baseColor, normal, mask, graphite); return; }
            Material pbr = BuildPbrMaterial(sourceRoot, key, team, baseColor, normal, mask);
            EnsureFolder(ResourcesRoot);
            GameObject root = new GameObject(key + "_" + team);
            try
            {
                Transform visualRoot = new GameObject("VisualRoot").transform;
                visualRoot.SetParent(root.transform, false);
                PrototypeVehicleArtRig rig = root.AddComponent<PrototypeVehicleArtRig>();
                rig.VisualRoot = visualRoot;
                List<Transform> turrets = new List<Transform>();
                List<Transform> barrels = new List<Transform>();
                List<Transform> mainRotors = new List<Transform>();
                List<Transform> tailRotors = new List<Transform>();
                List<Transform> radars = new List<Transform>();
                List<Transform> wheels = new List<Transform>();
                List<Transform> steeringAxles = new List<Transform>();
                List<float> wheelRadii = new List<float>();
                LOD[] lods = new LOD[3];
                int previousTriangles = int.MaxValue;

                for (int lod = 0; lod < 3; lod++)
                {
                    GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx[lod]);
                    if (fbxAsset == null) throw new InvalidOperationException("Missing FBX: " + fbx[lod]);
                    Transform levelRoot = new GameObject("LOD" + lod).transform;
                    levelRoot.SetParent(visualRoot, false);
                    GameObject level = UnityEngine.Object.Instantiate(fbxAsset, levelRoot);
                    level.name = "SourceFBX";
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(level))
                        PrefabUtility.UnpackPrefabInstance(level, PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction);
                    level.transform.localPosition = Vector3.zero;
                    level.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    level.transform.localScale = Vector3.one;

                    if (key == "H8")
                    {
                        mainRotors.Add(WrapPivot(FindPart(level.transform, "MainRotor"),
                            levelRoot, "MainRotor"));
                        tailRotors.Add(WrapPivot(FindPart(level.transform, "TailRotor"),
                            levelRoot, "TailRotor"));
                        Vector3 main = root.transform.InverseTransformPoint(mainRotors[lod].position);
                        Vector3 tail = root.transform.InverseTransformPoint(tailRotors[lod].position);
                        if (main.y < 5f || main.y > 8f || tail.y < 5f || tail.y > 8f ||
                            tail.z > -7f || tail.z < -12f)
                            throw new InvalidOperationException(fbx[lod] +
                                " rotor pivots are not Unity Y-up / +Z-forward metres: " + main + ", " + tail);
                    }
                    else
                    {
                        Transform artTurret = FindPart(level.transform, "Turret");
                        Transform artBarrels = FindPart(level.transform, "Barrels");
                        Transform artRadar = key == "A40" ? FindPart(level.transform, "Radar") : null;
                        Transform turretDriver = WrapPivot(artTurret, levelRoot, "Turret");
                        turrets.Add(turretDriver);
                        barrels.Add(WrapPivot(artBarrels, turretDriver, "Barrels"));
                        if (artRadar != null) radars.Add(WrapPivot(artRadar, turretDriver, "Radar"));
                        Vector3 turret = root.transform.InverseTransformPoint(turrets[lod].position);
                        if (Mathf.Abs(turret.y - 2f) > .5f || Mathf.Abs(turret.z + .1f) > .5f)
                            throw new InvalidOperationException(fbx[lod] +
                                " turret pivot is not Unity Y-up / +Z-forward metres: " + turret);
                        Transform[] wheelParts = level.GetComponentsInChildren<Transform>(true)
                            .Where(item => item != level.transform &&
                                item.name.StartsWith("Wheel_", StringComparison.OrdinalIgnoreCase))
                            .ToArray();
                        int expectedWheels = key == "T90" ? 0 : 8;
                        if (wheelParts.Length != expectedWheels)
                            throw new InvalidOperationException(fbx[lod] +
                                " requires " + expectedWheels + " independently pivoted Wheel_ nodes; found " + wheelParts.Length);
                        if (lod == 0 && wheelParts.Length > 0) rig.WheelRadius = MeasureWheelRadius(wheelParts);
                        foreach (Transform wheel in wheelParts)
                        {
                            float radius = MeasureWheelRadius(new[] { wheel });
                            Transform parent = levelRoot;
                            if (key == "V12" && (wheel.name.Contains("_0_LOD") || wheel.name.Contains("_1_LOD")))
                            {
                                parent = new GameObject("Steer_" + wheel.name).transform;
                                parent.SetParent(levelRoot, false); parent.position = wheel.position;
                                steeringAxles.Add(parent);
                            }
                            wheels.Add(WrapPivot(wheel, parent, wheel.name));
                            wheelRadii.Add(radius);
                        }
                        if (lod == 0 && (key == "T90" || key == "V12"))
                        {
                            rig.TurretAnchor = turret;
                            rig.BarrelAnchor = root.transform.InverseTransformPoint(barrels[lod].position);
                            rig.MuzzleAnchor = root.transform.InverseTransformPoint(FindPart(levelRoot, "Muzzle").position);
                            Renderer hullRenderer = levelRoot.GetComponentsInChildren<Renderer>(true)
                                .Single(item => item.name.StartsWith("Hull_LOD", StringComparison.Ordinal));
                            Bounds hullBounds = hullRenderer.bounds;
                            float height = rig.BarrelAnchor.y + .5f;
                            rig.HullCenter = new Vector3(hullBounds.center.x, height * .5f, hullBounds.center.z);
                            rig.HullSize = new Vector3(hullBounds.size.x, height, hullBounds.size.z);
                        }
                    }

                    Renderer[] renderers = levelRoot.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) throw new InvalidOperationException(fbx[lod] + " has no renderers.");
                    foreach (Renderer renderer in renderers)
                    {
                        Material[] materials = renderer.sharedMaterials;
                        for (int slot = 0; slot < materials.Length; slot++)
                            materials[slot] = graphite != null && materials[slot] != null &&
                                (materials[slot].name.IndexOf("RotorGraphite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 materials[slot].name.IndexOf("JointSteel", StringComparison.OrdinalIgnoreCase) >= 0) ?
                                graphite : pbr;
                        renderer.sharedMaterials = materials;
                    }
                    int triangles = levelRoot.GetComponentsInChildren<MeshFilter>(true)
                        .Where(filter => filter.sharedMesh != null)
                        .Sum(filter => filter.sharedMesh.triangles.Length / 3);
                    if (triangles <= 0 || triangles >= previousTriangles)
                        throw new InvalidOperationException(key + " LOD" + lod +
                            " triangle count is not lower than the previous LOD: " + triangles);
                    previousTriangles = triangles;
                    lods[lod] = new LOD(LodThresholds[lod], renderers);
                    Debug.Log(key + " " + team + " LOD" + lod + ": " + triangles + " triangles");
                    if (lod == 0) ValidateSize(key, renderers);
                }

                rig.Turrets = turrets.ToArray();
                rig.Barrels = barrels.ToArray();
                rig.MainRotors = mainRotors.ToArray();
                rig.TailRotors = tailRotors.ToArray();
                rig.Radars = radars.ToArray();
                rig.Wheels = wheels.ToArray();
                rig.SteeringAxles = steeringAxles.ToArray();
                if (key == "V12") rig.WheelRadii = wheelRadii.ToArray();
                LODGroup group = root.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();
                string prefabPath = ResourcesRoot + "/" + key + "_" + team + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("Vehicle art prefab generated: " + prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string FindFbx(string root, int lod)
        {
            string[] matches = AssetDatabase.FindAssets("t:Model", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                    path.IndexOf("lod" + lod, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException(root + " requires exactly one LOD" + lod +
                    " FBX; found " + matches.Length);
            return matches[0];
        }

        private static void ConfigureModel(string path, string key)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Model importer missing: " + path);
            float scale = key == "H8" ? 100f : 1f;
            if (Mathf.Approximately(importer.globalScale, scale) && importer.useFileUnits &&
                !importer.bakeAxisConversion && !importer.importAnimation) return;
            // The H8 FBX records centimetre units while A40 records metres.
            // Both retain Blender axes; the source root rotates to Unity Y-up
            // and driven parts are reparented under neutral Unity-axis pivots.
            importer.globalScale = scale;
            importer.useFileUnits = true;
            importer.bakeAxisConversion = false;
            importer.importAnimation = false;
            importer.SaveAndReimport();
        }

        private static Texture2D FindTexture(string root, string team, string kind, bool required)
        {
            string[] aliases = kind == "basecolor" ? new[] { "basecolor", "albedo" } :
                kind == "metalrough" ? new[] { "metalrough", "metallicroughness", "orm" } :
                new[] { kind };
            string[] candidates = AssetDatabase.FindAssets("t:Texture2D", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => aliases.Any(alias => Path.GetFileNameWithoutExtension(path)
                    .IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0) &&
                    path.IndexOf("_Mask", StringComparison.OrdinalIgnoreCase) < 0)
                .ToArray();
            if (team != null)
                candidates = candidates.Where(path => Path.GetFileName(path)
                    .IndexOf(team, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            else
            {
                string[] shared = candidates.Where(path =>
                    path.IndexOf("red", StringComparison.OrdinalIgnoreCase) < 0).ToArray();
                if (shared.Length > 0) candidates = shared;
            }
            if (candidates.Length > 1)
                candidates = candidates.OrderBy(path => path.Length).Take(1).ToArray();
            if (candidates.Length == 0)
            {
                if (required) throw new InvalidOperationException(root + " lacks " + team + " " + kind + " texture.");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(candidates[0]);
        }

        private static Transform FindPart(Transform root, string semantic)
        {
            Transform[] matches = root.GetComponentsInChildren<Transform>(true)
                .Where(item => item != root &&
                    (item.name.Equals(semantic, StringComparison.OrdinalIgnoreCase) ||
                     item.name.StartsWith(semantic + ".", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException(root.name + " requires one " + semantic +
                    " node; found " + matches.Length);
            return matches[0];
        }

        private static Transform WrapPivot(Transform part, Transform parent, string semantic)
        {
            Transform wrapper = new GameObject("Driven_" + semantic).transform;
            wrapper.SetParent(parent, false);
            wrapper.position = part.position;
            part.SetParent(wrapper, true);
            return wrapper;
        }

        private static float MeasureWheelRadius(Transform[] wheels)
        {
            float sum = 0f;
            foreach (Transform wheel in wheels)
            {
                Renderer[] renderers = wheel.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException(wheel.name + " has no tire renderer.");
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                sum += Mathf.Max(bounds.size.y, bounds.size.z) * .5f;
            }
            float radius = sum / wheels.Length;
            if (radius < .55f || radius > 1.1f)
                throw new InvalidOperationException("Vehicle wheel radius is outside metre-scale range: " + radius);
            if (wheels.Length > 1) Debug.Log("Measured average wheel radius: " + radius.ToString("F3") + " m");
            return radius;
        }

        private static void ValidateSize(string key, Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Vector3 size = bounds.size;
            bool plausible = key == "H8" ?
                size.x > 10f && size.x < 16f && size.y > 3f && size.y < 11f && size.z > 9f && size.z < 24f :
                size.x > 3.5f && size.x < 6f && size.y > 2f && size.y < 8f && size.z > 5.5f && size.z < (key == "T90" ? 11f : 10f);
            if (!plausible)
                throw new InvalidOperationException(key + " imported FBX bounds " + size +
                    " do not match Unity Y-up metre scale. Check FBX axis and importer settings.");
            Debug.Log(key + " imported bounds: center " + bounds.center + ", size " + size);
        }

        private static Material BuildPbrMaterial(string root, string key, string team,
            Texture2D baseColor, Texture2D normal, Texture2D mask)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            string path = root + "/" + key + "_" + team + "_URP.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", baseColor);
            material.SetColor("_BaseColor", Color.white);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                if (key == "T90" || key == "V12" || IsRemaining(key)) material.SetFloat("_BumpScale", .65f);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
            }
            if (mask != null)
            {
                material.SetTexture("_MetallicGlossMap", mask);
                // The source glTF atlas retains its detail, but prototype
                // outdoor light makes full-scale specular values sparkle on
                // the normal-map microdetail. Keep both painted hulls matte.
                material.SetFloat("_Metallic", key == "H8" ? .10f : .12f);
                material.SetFloat("_Smoothness", key == "H8" ? .24f : .30f);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BuildGraphiteMaterial(string root)
        {
            string path = root + "/H8_RotorGraphite_URP.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(.13f, .15f, .17f));
            material.SetFloat("_Metallic", .55f);
            material.SetFloat("_Smoothness", .38f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BuildJointMaterial(string root, string key)
        {
            string path = root + "/" + key + "_JointSteel_URP.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(.055f, .067f, .075f));
            material.SetFloat("_Metallic", .45f); material.SetFloat("_Smoothness", .28f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D BuildMetallicSmoothness(string root, string key, Texture2D metalRough)
        {
            // glTF ORM convention: roughness in G and metallic in B.
            Color32[] pixels = metalRough.GetPixels32();
            Color32[] packed = new Color32[pixels.Length];
            for (int i = 0; i < packed.Length; i++)
                packed[i] = new Color32(pixels[i].b, 0, 0, (byte)(255 - pixels[i].g));
            Texture2D output = new Texture2D(metalRough.width, metalRough.height,
                TextureFormat.RGBA32, false, true);
            output.SetPixels32(packed);
            output.Apply(false, false);
            string assetPath = root + "/" + key + "_MetallicSmoothness_Mask.png";
            string filePath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            File.WriteAllBytes(filePath, output.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(output);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            ConfigureTexture(texture, TextureImporterType.Default, false, false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void ConfigureTexture(Texture2D texture, TextureImporterType type,
            bool sRgb, bool readable)
        {
            if (texture == null) return;
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Texture importer missing: " + texture.name);
            if (importer.textureType == type && importer.sRGBTexture == sRgb && importer.isReadable == readable)
                return;
            importer.textureType = type;
            importer.sRGBTexture = sRgb;
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
