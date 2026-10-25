#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Ironfront.UnityPrototype
{
    public sealed class PrototypeRemainingArtProbe : MonoBehaviour
    {
        private string output; private int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-ironfrontRemainingProbe")
                new GameObject("Remaining vehicle development proof").AddComponent<PrototypeRemainingArtProbe>().output = Path.GetFullPath(args[i + 1]);
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true; Application.logMessageReceived += Observe; Directory.CreateDirectory(output);
            PrototypeRuntime game = null;
            for (int i = 0; i < 600; i++) { game = FindAnyObjectByType<PrototypeRuntime>(); if (game != null && game.Player != null) break; yield return null; }
            bool ready = game != null && game.Player != null;
            string[] keys = { "H8", "A40", "T90", "V12", "R4", "U8", "M2", "R3", "J20" };
            if (ready)
            {
                game.BeginBattle(); game.Frontend.Close(); game.Player.enabled = false;
                foreach (var bot in game.Bots) bot.enabled = false;
                foreach (var vehicle in game.Vehicles) if (vehicle is Behaviour b) b.enabled = false;
                foreach (string key in keys) foreach (var team in new[] { PrototypeTeam.Blue, PrototypeTeam.Red })
                {
                    var v = game.Vehicles.FirstOrDefault(v => v.Team == team && v.VehicleName.StartsWith(key));
                    var rig = v == null ? null : ((MonoBehaviour)v).GetComponentInChildren<PrototypeVehicleArtRig>();
                    ready &= rig != null && rig.GetComponent<LODGroup>().GetLODs().Length == 3;
                }
                foreach (string key in keys.Skip(4))
                {
                    var vehicle = game.Vehicles.First(v => v.Team == PrototypeTeam.Blue && v.VehicleName.StartsWith(key));
                    var target = (MonoBehaviour)vehicle;
                    foreach (var v in game.Vehicles.OfType<MonoBehaviour>()) v.gameObject.SetActive(v == target);
                    target.transform.SetPositionAndRotation(new Vector3(-325, PrototypeLayout.HeightAt(-325, -320) + .05f, -320), Quaternion.identity);
                    var group = target.GetComponentInChildren<LODGroup>(); group.ForceLOD(0);
                    var rs = group.GetLODs()[0].renderers; Bounds bounds = rs[0].bounds; foreach (var r in rs.Skip(1)) bounds.Encapsulate(r.bounds);
                    var camera = game.Player.ViewCamera; camera.fieldOfView = 45;
                    camera.transform.position = bounds.center + new Vector3(.7f, .43f, .75f).normalized * bounds.size.magnitude * 1.05f;
                    camera.transform.LookAt(bounds.center);
                    for (int i = 0; i < 20; i++) yield return null;
                    string file = Path.Combine(output, "development-" + key + ".png"); ScreenCapture.CaptureScreenshot(file);
                    for (int i = 0; i < 20; i++) yield return null;
                    ready &= File.Exists(file);
                }
            }
            bool passed = ready && errors == 0;
            File.WriteAllText(Path.Combine(output, "development-player.txt"), (passed ? "PASS" : "FAIL") + "\nUnity=" + Application.unityVersion + "\nVersion=" + Application.version + "\nDevice=" + SystemInfo.deviceModel + "\nGPU=" + SystemInfo.graphicsDeviceName + "\nRuntimeErrors=" + errors + "\nAll nine blue/red vehicles bind authored art with three LODs; five actual player frames captured.");
            Debug.Log("REMAINING_DEVELOPMENT_PLAYER_" + (passed ? "PASS" : "FAIL")); Application.logMessageReceived -= Observe; Application.Quit(passed ? 0 : 1);
        }
        private void Observe(string condition, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    }
}
#endif
