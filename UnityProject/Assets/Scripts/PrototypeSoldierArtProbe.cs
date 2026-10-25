#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Explicit development-player probe. Normal players never instantiate it.
    public sealed class PrototypeSoldierArtProbe : MonoBehaviour
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        string output; int errors;
        readonly List<string> checks = new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-ironfrontSoldierProbe")
                new GameObject("Soldier development proof").AddComponent<PrototypeSoldierArtProbe>().output = Path.GetFullPath(args[i + 1]);
        }
        IEnumerator Start()
        {
            Application.runInBackground = true; Directory.CreateDirectory(output); Application.logMessageReceived += Observe;
            var work = Verify(); Exception failure = null;
            while (true)
            {
                bool more = false; object current = null;
                try { more = work.MoveNext(); if (more) current = work.Current; }
                catch (Exception e) { failure = e; }
                if (!more || failure != null) break;
                yield return current;
            }
            bool passed = failure == null && errors == 0;
            File.WriteAllText(Path.Combine(output, "development-player.txt"), (passed ? "PASS\n" : "FAIL\n") +
                "Version=" + Application.version + "\nUnity=" + Application.unityVersion + "\nDevice=" + SystemInfo.deviceModel + "\nGPU=" + SystemInfo.graphicsDeviceName +
                "\nRuntimeErrors=" + errors + "\n" + string.Join("\n", checks) + (failure == null ? "" : "\n" + failure));
            Debug.Log("SOLDIER_DEVELOPMENT_PLAYER_" + (passed ? "PASS" : "FAIL"));
            Application.logMessageReceived -= Observe; Application.Quit(passed ? 0 : 1);
        }
        IEnumerator Verify()
        {
            PrototypeRuntime game = null;
            for (int i = 0; i < 600; i++) { game = FindAnyObjectByType<PrototypeRuntime>(); if (game != null && game.Player != null) break; yield return null; }
            Check(game != null && game.Player != null, "Runtime starts");
            game.BeginBattle(); game.Frontend.Close(); game.SetSupportMenu(false);
            var player = game.Player; player.enabled = false;
            foreach (var b in game.Bots) { b.enabled = false; Check(b.GetComponent<PrototypeSoldierVisual>().ArtRig != null, "Bot " + b.Team + " bound"); b.gameObject.SetActive(false); }
            foreach (var v in game.Vehicles.OfType<MonoBehaviour>()) v.gameObject.SetActive(false);
            var camera = player.ViewCamera;
            var actors = new[] { game.Bots.First(b => b.Team == PrototypeTeam.Blue), game.Bots.First(b => b.Team == PrototypeTeam.Red) };
            Vector3 center = new Vector3(-325, PrototypeLayout.HeightAt(-325, -320), -320);
            for (int n = 0; n < actors.Length; n++)
            {
                var b = actors[n]; b.gameObject.SetActive(true); b.transform.SetPositionAndRotation(center + new Vector3((n * 2 - 1) * .75f, 1f, 0), Quaternion.identity);
                var visual = b.GetComponent<PrototypeSoldierVisual>(); visual.enabled = false; visual.SetWeapon(PrototypeWeaponId.Carbine, game);
                visual.ArtRig.GetComponent<LODGroup>().ForceLOD(0);
            }
            // Independently raycast the actual rendered pad mesh. This catches
            // decorative surfaces higher than the gameplay terrain equation.
            var padMesh=FindAnyObjectByType<PrototypeWorldVisual>().GetComponentsInChildren<MeshFilter>()
                .First(m=>{var c=m.GetComponent<Renderer>().sharedMaterial.color;return Mathf.Abs(c.r-.22f)<.01f&&Mathf.Abs(c.g-.43f)<.01f&&Mathf.Abs(c.b-.51f)<.01f;});
            var pad=padMesh.gameObject.AddComponent<MeshCollider>();pad.sharedMesh=padMesh.sharedMesh;Physics.SyncTransforms();
            Check(pad.Raycast(new Ray(center+Vector3.up*3,Vector3.down),out var padHit,5),"Rendered spawn pad has testable surface");
            float surface=padHit.point.y;
            camera.fieldOfView = 43;
            foreach (string pose in new[] { "standing", "crouch", "run", "prone" })
            {
                foreach (var b in actors)
                {
                    var visual = b.GetComponent<PrototypeSoldierVisual>(); Set(visual, "runWeight", pose == "run" ? 1f : 0f); Set(visual, "gait", 1.1f);
                    Invoke(visual, "Pose", pose == "crouch" ? 1f : 0f, pose == "prone" ? 1f : 0f, .89f, pose == "run" ? 1f : 0f);
                }
                camera.transform.position = center + new Vector3(2.7f, 2.2f, 4.6f); camera.transform.LookAt(center + Vector3.up * .95f);
                yield return Capture("soldiers-" + pose);
                foreach(var b in actors)
                {
                    var rig=b.GetComponent<PrototypeSoldierVisual>().ArtRig;float lowest=float.PositiveInfinity;
                    foreach(var renderer in rig.GetComponent<LODGroup>().GetLODs()[0].renderers.Cast<SkinnedMeshRenderer>())
                    {var mesh=new Mesh();renderer.BakeMesh(mesh);foreach(var v in mesh.vertices)lowest=Mathf.Min(lowest,renderer.transform.TransformPoint(v).y);Destroy(mesh);}
                    Check(lowest>=surface-.015f,b.Team+" "+pose+" clear of rendered pad="+(lowest-surface).ToString("F4"));
                    if(pose!="run")Check(lowest<surface+.035f,b.Team+" "+pose+" grounded on rendered pad");
                }
            }
            Destroy(pad);
            foreach (var b in actors) b.gameObject.SetActive(false);
            player.transform.position = center + Vector3.up * .30f; player.transform.rotation = Quaternion.identity;
            if (!player.IsFirstPerson) player.TogglePerspective();
            foreach (var weapon in new[] { PrototypeWeaponId.Carbine, PrototypeWeaponId.Pistol })
            {
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Assault, primary = weapon == PrototypeWeaponId.Pistol ? PrototypeWeaponId.Carbine : weapon, secondary = PrototypeWeaponId.Pistol, throwable = PrototypeThrowableId.Frag });
                // Preview the same weapon/arm prefab used by player inventory.
                var existing = (GameObject)Get(player, "firstPersonWeapon"); existing.SetActive(false);
                var fp = PrototypeWeaponVisual.Build(camera.transform, weapon, game, true, player.Team);
                fp.transform.localPosition = new Vector3(.30f, -.29f, .74f); fp.transform.localScale = Vector3.one * .55f;
                camera.transform.SetPositionAndRotation(center + Vector3.up * 1.65f, Quaternion.identity); camera.fieldOfView = 75;
                Check(fp.GetComponentInChildren<PrototypeSoldierArtRig>().FirstPerson, weapon + " first-person art active");
                yield return Capture("first-person-" + weapon); Destroy(fp); yield return null;
            }
            var bike = game.Vehicles.OfType<PrototypeCombinedArmsVehicle>().First(v => v.Kind == PrototypeVehicleKind.Motorcycle && v.Team == player.Team);
            bike.gameObject.SetActive(true); bike.enabled = false; bike.transform.SetPositionAndRotation(center, Quaternion.identity);
            Check((bool)Invoke(player, "EnterVehicle", bike), "M2 player enters");
            var rider = player.GetComponent<PrototypeSoldierVisual>();
            foreach (int seat in new[] { 0, 1 })
            {
                if (seat == 1) Check(player.TrySwitchVehicleSeat(1), "M2 passenger enters");
                foreach (bool fp in new[] { false, true })
                {
                    Set(player, "vehicleFirstPerson", fp); Invoke(player, "ApplyPerspectiveVisibility"); Invoke(player, "UpdateVehicleCamera"); rider.RefreshPose(true);
                    if (!fp) { camera.transform.position = bike.transform.position + new Vector3(3, 2.3f, 3.4f); camera.transform.LookAt(bike.transform.position + Vector3.up * 1.05f); camera.fieldOfView = 45; }
                    yield return Capture("motorcycle-seat" + seat + (fp ? "-FP" : "-TP"));
                    Check(rider.ArtRig.FirstPerson == fp, "M2 seat " + seat + " visibility " + fp);
                }
            }
            player.ForceExitVehicle(); Check(!rider.ArtRig.FirstPerson, "M2 exit restores soldier");
        }
        IEnumerator Capture(string label)
        {
            for (int i = 0; i < 12; i++) yield return null;
            string path = Path.Combine(output, label + ".png"); ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 12; i++) yield return null;
            Check(File.Exists(path), "Captured " + label);
        }
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
        void Observe(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
        static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);
        static object Invoke(object o, string n, params object[] args) => o.GetType().GetMethod(n, Hidden).Invoke(o, args);
    }
}
#endif
