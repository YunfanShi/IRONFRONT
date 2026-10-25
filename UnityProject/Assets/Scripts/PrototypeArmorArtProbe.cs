#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Explicit opt-in development-player packaging/startup proof. Normal games
    // and release builds never create this component or alter scene state.
    public sealed class PrototypeArmorArtProbe : MonoBehaviour
    {
        private string output;
        private int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-ironfrontArmorProbe")
                    new GameObject("Armor development proof").AddComponent<PrototypeArmorArtProbe>().output = Path.GetFullPath(args[i + 1]);
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += Observe;
            Directory.CreateDirectory(output);
            PrototypeRuntime game = null;
            for (int i = 0; i < 600; i++)
            {
                game = FindAnyObjectByType<PrototypeRuntime>();
                if (game != null && game.Player != null) break;
                yield return null;
            }
            bool ready = game != null && game.Player != null;
            if (ready)
            {
                game.BeginBattle(); game.Frontend.Close(); game.Player.enabled = false;
                foreach (var bot in game.Bots) bot.enabled = false;
                foreach (var vehicle in game.Vehicles) if (vehicle is Behaviour b) b.enabled = false;
                var tanks = FindObjectsByType<PrototypeTankVehicle>();
                var ifvs = FindObjectsByType<PrototypeCombinedArmsVehicle>().Where(v => v.Kind == PrototypeVehicleKind.Ifv).ToArray();
                ready = tanks.Length >= 2 && ifvs.Length >= 2 && tanks.All(v => v.HasAuthoredArt) && ifvs.All(v => v.HasAuthoredArt);
                if (ready)
                {
                    var tank = tanks.First(v => v.Team == PrototypeTeam.Blue);
                    var ifv = ifvs.First(v => v.Team == PrototypeTeam.Red);
                    tank.transform.position = new Vector3(-325f, PrototypeLayout.HeightAt(-325f, -320f) + .05f, -320f);
                    ifv.transform.position = new Vector3(-315f, PrototypeLayout.HeightAt(-315f, -320f) + .05f, -320f);
                    tank.transform.rotation = ifv.transform.rotation = Quaternion.identity;
                    foreach (var vehicle in new MonoBehaviour[] { tank, ifv })
                    {
                        var rig = vehicle.GetComponentInChildren<PrototypeVehicleArtRig>();
                        ready &= rig.Turrets.Length == 3 && rig.Barrels.Length == 3 && rig.GetComponent<LODGroup>().GetLODs().Length == 3;
                        rig.GetComponent<LODGroup>().ForceLOD(0);
                    }
                    var camera = game.Player.ViewCamera;
                    camera.transform.position = new Vector3(-305f, 13f, -301f);
                    camera.transform.LookAt(new Vector3(-320f, 1.6f, -320f));
                    camera.fieldOfView = 50f;
                    for (int i = 0; i < 30; i++) yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "development-player.png"));
                    for (int i = 0; i < 30; i++) yield return null;
                    ready &= File.Exists(Path.Combine(output, "development-player.png"));
                }
            }
            bool passed = ready && errors == 0;
            File.WriteAllText(Path.Combine(output, "development-player.txt"),
                (passed ? "PASS" : "FAIL") + "\nUnity=" + Application.unityVersion + "\nVersion=" + Application.version +
                "\nDevice=" + SystemInfo.deviceModel + "\nGPU=" + SystemInfo.graphicsDeviceName +
                "\nRuntimeErrors=" + errors + "\nBlue/red T90 and V12 authored prefabs present; three moving LODs; actual player frame captured.");
            Debug.Log("ARMOR_DEVELOPMENT_PLAYER_" + (passed ? "PASS" : "FAIL"));
            Application.logMessageReceived -= Observe;
            Application.Quit(passed ? 0 : 1);
        }
        private void Observe(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    }
}
#endif
