using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeOptimizationBuild
    {
        // Isolated development build; normal release settings are not changed.
        public static void Build()
        {
            string[] args = Environment.GetCommandLineArgs();
            string output = null;
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-buildOutput") output = args[i + 1];
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("-buildOutput is required");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Optimization build failed: " + report.summary.result);
            Debug.Log("IRONFRONT optimization build PASS: " + report.summary.totalSize + " bytes; " + output);
        }
    }
}
