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
            "PrototypeLayout.cs",
            "PrototypeNavigation.cs",
            "PrototypeMatch.cs",
            "PrototypePlayer.cs",
            "PrototypeBot.cs",
            "PrototypeCommander.cs",
            "PrototypeTactics.cs",
            "PrototypeFrontend.cs",
            "PrototypeInfantryRoles.cs",
            "IPrototypeVehicle.cs",
            "PrototypeScoutVehicle.cs",
            "PrototypeTransportVehicle.cs",
            "PrototypeLanClient.cs",
            "PrototypeRoomMenu.cs",
            "PrototypeRemotePlayer.cs",
            "PrototypeSoldierVisual.cs",
            "PrototypeTankVisual.cs",
            "PrototypeWorldVisual.cs"
        };

        [MenuItem(MenuPath)]
        public static void BuildMacOS()
        {
            try
            {
                EnsurePrototypeMaterials();
                string[] scenes = ValidateProject();
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string outputPath = Path.Combine(projectRoot, "Builds", "macOS", "IRONFRONT.app");
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
