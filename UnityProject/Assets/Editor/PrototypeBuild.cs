using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeBuild
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MenuPath = "IRONFRONT/Build macOS Standalone";

        private static readonly string[] RequiredScripts =
        {
            "PrototypeRuntime.cs",
            "PrototypeMusicDirector.cs",
            "PrototypeBotRoster.cs",
            "PrototypeScoreSystem.cs",
            "PrototypeScoreboard.cs",
            "PrototypePlayerHealthHud.cs",
            "PrototypeScorePopup.cs",
            "PrototypeScoreIcons.cs",
            "PrototypeHitFeedback.cs",
            "PrototypePlayerAudio.cs",
            "PrototypeVehicleArmor.cs",
            "PrototypeAmmoSupplySystem.cs",
            "PrototypeAmmoSupplyPoint.cs",
            "PrototypeSupportRequisition.cs",
            "PrototypeVehicleEngineAudio.cs",
            "PrototypeVehicleImpactAudio.cs",
            "PrototypeReconRadar.cs",
            "PrototypeAntiAirLock.cs",
            "PrototypeAntiAirHud.cs",
            "PrototypeSmokeCloud.cs",
            "PrototypeSmokeGrenadeFuse.cs",
            "PrototypeThrowableVisual.cs",
            "PrototypeThrowableTrajectory.cs",
            "PrototypeLayout.cs",
            "PrototypeNavigation.cs",
            "PrototypePathWorkspace.cs",
            "PrototypeTracerPool.cs",
            "PrototypeMatch.cs",
            "PrototypePlayer.cs",
            "PrototypeBot.cs",
            "PrototypeCommander.cs",
            "PrototypeTactics.cs",
            "PrototypeFrontend.cs",
            "PrototypeStartMenuView.cs",
            "PrototypeLoadout.cs",
            "PrototypeEquipmentIcons.cs",
            "PrototypeMenuPreview.cs",
            "PrototypeWeaponMenuPreview.cs",
            "PrototypeInfantryRoles.cs",
            "PrototypeBallisticProjectile.cs",
            "PrototypeDeployment.cs",
            "IPrototypeVehicle.cs",
            "PrototypeScoutVehicle.cs",
            "PrototypeTransportVehicle.cs",
            "PrototypeLanClient.cs",
            "PrototypeRoomMenu.cs",
            "PrototypeRemotePlayer.cs",
            "PrototypeSoldierVisual.cs",
            "PrototypeEquipmentVisual.cs",
            "PrototypeCombatFeedback.cs",
            "PrototypeTankVisual.cs",
            "PrototypeTankVehicle.cs",
            "PrototypeTankSightHud.cs",
            "PrototypeVehicleSeats.cs",
            "PrototypeVehicleDefinition.cs",
            "PrototypeCombinedArmsVehicle.cs",
            "PrototypeCombinedVehicleVisual.cs",
            "PrototypeVehicleArtRig.cs",
            "PrototypeVehicleProjectile.cs",
            "PrototypeVehicleRoute.cs",
            "PrototypeVehicleCountermeasure.cs",
            "PrototypeWorldVisual.cs"
        };

        [MenuItem(MenuPath)]
        public static void BuildMacOS() => BuildMacOSAt(false);

        [MenuItem("IRONFRONT/Build macOS Versioned")]
        public static void BuildMacOSVersioned() => BuildMacOSAt(true);

        private static void BuildMacOSAt(bool versioned)
        {
            try
            {
                EnsurePrototypeMaterials();
                string[] scenes = ValidateProject();
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                SyncBundleVersion(projectRoot);
                string outputPath = Path.Combine(projectRoot, "Builds",
                    versioned ? "macOS-" + PlayerSettings.bundleVersion : "macOS", "IRONFRONT.app");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                Debug.Log("IRONFRONT macOS build starting. Output: " + outputPath);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.None
                });

                if (report == null || report.summary.result != BuildResult.Succeeded)
                {
                    string result = report == null ? "no report" : report.summary.result.ToString();
                    int errors = report == null ? -1 : report.summary.totalErrors;
                    throw new InvalidOperationException(
                        "IRONFRONT macOS build failed: " + result + ", errors: " + errors +
                        ". See the Unity Editor log. Output path: " + outputPath);
                }

                if (!Directory.Exists(outputPath))
                    throw new InvalidOperationException(
                        "Unity reported success, but the .app bundle is missing: " + outputPath);

                Debug.Log("IRONFRONT macOS build succeeded. Output: " + outputPath +
                          "; size: " + report.summary.totalSize + " bytes");
            }
            catch (Exception exception)
            {
                Debug.LogError("IRONFRONT macOS build could not complete: " + exception);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                else
                    EditorUtility.DisplayDialog("IRONFRONT build failed", exception.Message, "OK");
                throw;
            }
        }

        [Serializable]
        private sealed class PackageVersion
        {
            public string version;
        }

        private static void SyncBundleVersion(string projectRoot)
        {
            // A copied UnityProject can build on its own. In this repository,
            // package.json is the release version shared with the LAN server.
            string packagePath = Path.GetFullPath(Path.Combine(projectRoot, "..", "package.json"));
            if (!File.Exists(packagePath)) return;
            PackageVersion package = JsonUtility.FromJson<PackageVersion>(
                File.ReadAllText(packagePath));
            if (package == null || string.IsNullOrWhiteSpace(package.version))
                throw new InvalidOperationException("package.json has no release version.");
            if (PlayerSettings.bundleVersion == package.version) return;
            PlayerSettings.bundleVersion = package.version;
            Debug.Log("IRONFRONT macOS bundle version: " + package.version);
        }

        private static string[] ValidateProject()
        {
            if (UnityEditor.EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for Unity script compilation to finish.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Required scene is missing or not imported: " + ScenePath);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (!scenes.Contains(ScenePath))
                throw new InvalidOperationException(
                    "Enable " + ScenePath + " in File > Build Profiles > Scene List.");

            foreach (string scene in scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                    throw new InvalidOperationException("An enabled build scene is missing: " + scene);
            }

            foreach (string scriptName in RequiredScripts)
            {
                string path = "Assets/Scripts/" + scriptName;
                if (AssetDatabase.LoadAssetAtPath<MonoScript>(path) == null)
                    throw new InvalidOperationException("Required prototype script is missing: " + path);
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Resources/Materials/PrototypeLit.mat") == null ||
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Resources/Materials/PrototypeUnlit.mat") == null)
                throw new InvalidOperationException("Prototype URP material assets are missing.");

            foreach (string music in new[] { "lobby", "victory-near", "defeat-near" })
            {
                string path = "Assets/Resources/Audio/Music/" + music + ".wav";
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                    throw new InvalidOperationException("Required background music is missing: " + path);
            }

            foreach (string engine in new[] { "heavy-diesel", "light-diesel", "motorcycle", "rotor", "jet" })
                foreach (string layer in new[] { "idle", "load" })
                {
                    string path = "Assets/Resources/Audio/Engines/" + engine + "-" + layer + ".wav";
                    if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                        throw new InvalidOperationException("Required vehicle engine loop is missing: " + path);
                }
            foreach (string impact in new[] { "Ricochet", "MachineGun", "HeavyImpact" })
            {
                string path = "Assets/Resources/Audio/VehicleImpacts/" + impact + ".wav";
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                    throw new InvalidOperationException("Required vehicle impact sound is missing: " + path);
            }
            Shader smokeShader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Resources/Effects/PrototypeSmoke.shader");
            if (smokeShader == null || ShaderUtil.ShaderHasError(smokeShader))
                throw new InvalidOperationException("Prototype smoke shader is missing or failed to compile.");

            return scenes;
        }

        private static void EnsurePrototypeMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
                AssetDatabase.CreateFolder("Assets/Resources", "Materials");
            EnsureMaterial("PrototypeLit", "Universal Render Pipeline/Lit");
            EnsureMaterial("PrototypeUnlit", "Universal Render Pipeline/Unlit");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureMaterial(string assetName, string shaderName)
        {
            string path = "Assets/Resources/Materials/" + assetName + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException("Required URP shader is missing: " + shaderName);
            AssetDatabase.CreateAsset(new Material(shader) { name = assetName }, path);
        }
    }
}
