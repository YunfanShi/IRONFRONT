using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    [InitializeOnLoad]
    public static class RemainingArtPlaySmoke
    {
        private const string Key = "ironfront.remainingArtSmoke";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static PrototypeRuntime game;
        private static PrototypePlayer player;
        private static MonoBehaviour[] targets;
        private static int phase, index, steps, lastFrame = -1;
        private static Vector3 start;
        private static Quaternion wheelStart, rotorStart;
        private static readonly List<string> checks = new List<string>();
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/vehicles/remaining-integration-2026-10-23"));
        static RemainingArtPlaySmoke() => EditorApplication.update += Tick;
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Dedicated batch only");
            SessionState.SetBool(Key, true); SessionState.SetString(Key + ".deadline", DateTime.UtcNow.AddMinutes(5).Ticks.ToString());
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity"); EditorApplication.isPlaying = true;
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (DateTime.UtcNow.Ticks > long.Parse(SessionState.GetString(Key + ".deadline", "0"))) { Finish(new TimeoutException()); return; }
            if (!EditorApplication.isPlaying || Time.frameCount < 15 || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (phase == 0)
                {
                    game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>(); if (game == null || game.Player == null) return;
                    player = game.Player; if (!game.MatchStarted) game.BeginBattle(); game.Frontend.Close(); game.SetSupportMenu(false);
                    player.enabled = false; foreach (var b in game.Bots) b.enabled = false;
                    foreach (var v in game.Vehicles) if (v is Behaviour b) b.enabled = false;
                    var list = new List<MonoBehaviour>();
                    foreach (var team in new[] { PrototypeTeam.Blue, PrototypeTeam.Red })
                    {
                        list.Add(UnityEngine.Object.FindObjectsByType<PrototypeScoutVehicle>().First(v => v.Team == team));
                        list.Add(UnityEngine.Object.FindObjectsByType<PrototypeTransportVehicle>().First(v => v.Team == team));
                        foreach (var kind in new[] { PrototypeVehicleKind.Motorcycle, PrototypeVehicleKind.ReconHelicopter, PrototypeVehicleKind.Jet })
                            list.Add(UnityEngine.Object.FindObjectsByType<PrototypeCombinedArmsVehicle>().First(v => v.Team == team && v.Kind == kind));
                    }
                    targets = list.ToArray();
                    for (int i = 0; i < targets.Length; i++)
                    {
                        SpawnCheck(targets[i]);
                        float x = -325f + i % 5 * 25f, z = -320f + i / 5 * 35f;
                        targets[i].transform.SetPositionAndRotation(new Vector3(x, PrototypeLayout.HeightAt(x, z) + .05f, z), Quaternion.identity);
                    }
                    // The previously completed vehicles must still bind after the shared rig changes.
                    foreach (var key in new[] { "H8", "A40", "T90", "V12" })
                        Check(game.Vehicles.Any(v => v.VehicleName.StartsWith(key) && ((MonoBehaviour)v).GetComponentInChildren<PrototypeVehicleArtRig>() != null), key + " existing art remains bound");
                    index = 0; phase = 1; return;
                }
                var target = targets[index]; var vehicle = (IPrototypeVehicle)target; var rig = Rig(target);
                if (phase == 1)
                {
                    Check((bool)Invoke(player, "EnterVehicle", vehicle), vehicle.VehicleName + " enters actual player seat");
                    start = target.transform.position; steps = 0;
                    wheelStart = rig.Wheels.Length > 0 ? rig.Wheels[0].localRotation : Quaternion.identity;
                    rotorStart = rig.MainRotors.Length > 0 ? rig.MainRotors[0].localRotation : Quaternion.identity;
                    player.ViewCamera.transform.rotation = Quaternion.Euler(-12f, target.transform.eulerAngles.y, 0);
                    phase = 2; return;
                }
                if (phase == 2)
                {
                    if (target is PrototypeCombinedArmsVehicle c)
                    { c.ApplyPlayerInput(1, .35f, 1, false, false, .05f); Invoke(c, "UpdateVisualMotion", .05f); }
                    else Invoke(target, "Drive", 1f, .35f, .05f);
                    if (++steps < 48) return;
                    Check(Vector3.Distance(start, target.transform.position) > 2, vehicle.VehicleName + " actual movement");
                    phase = 3; return;
                }
                if (phase == 3)
                {
                    if (rig.Wheels.Length > 0) Check(Quaternion.Angle(wheelStart, rig.Wheels[0].localRotation) > 2f, vehicle.VehicleName + " rolling wheels");
                    if (rig.SteeringAxles.Length > 0) Check(rig.SteeringAxles.All(t => Quaternion.Angle(t.localRotation, Quaternion.identity) > 2f), vehicle.VehicleName + " steering all LODs");
                    if (rig.MainRotors.Length > 0)
                    {
                        Check(Quaternion.Angle(rotorStart, rig.MainRotors[0].localRotation) > 2, "R3 powered rotor rotates");
                        var m = Model(target); Check(rig.MainRotors.All(t => Quaternion.Angle(t.localRotation, m.Rotor.localRotation) < .01f) && rig.TailRotors.All(t => Quaternion.Angle(t.localRotation, m.TailRotor.localRotation) < .01f), "R3 main/tail synchronization all LODs");
                    }
                    if (target is PrototypeCombinedArmsVehicle bike && bike.Kind == PrototypeVehicleKind.Motorcycle)
                    {
                        var m = Model(target);
                        Check(Quaternion.Angle(rig.VisualRoot.localRotation, m.MotorcycleBody.localRotation) < .01f, "M2 authored chassis leans with rider");
                        Check(Vector3.Distance(m.MotorcycleLeftGrip.position, rig.transform.TransformPoint(rig.Contact("LeftGrip"))) < .45f, "M2 grip calibrated to source handlebar");
                        Check(player.TrySwitchVehicleSeat(1) && !bike.SeatCanDrive && !bike.SeatCanFire, "M2 passenger authority retained");
                        Check(player.TrySwitchVehicleSeat(0), "M2 driver seat restored");
                        phase = 5; return;
                    }
                    if (target is PrototypeTransportVehicle) Check(player.TrySwitchVehicleSeat(1), "U8 driver transfers to gunner");
                    var aim = target.transform.TransformPoint(new Vector3(15, 8, 100));
                    for (int n = 0; n < 80; n++)
                        if (target is PrototypeCombinedArmsVehicle c) c.AimAt(aim, .05f);
                        else Invoke(target, target is PrototypeScoutVehicle ? "AimTurretAt" : "AimGunAt", aim, .05f);
                    phase = 4; return;
                }
                if (phase == 4)
                {
                    PoseCheck(target);
                    var before = new HashSet<PrototypeBallisticProjectile>(UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>());
                    var beforeShell = new HashSet<PrototypeVehicleProjectile>(UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>());
                    Transform muzzle = ((IPrototypeWeaponAim)target).WeaponMuzzle; Vector3 origin = muzzle.position, bore = muzzle.forward;
                    if (target is PrototypeCombinedArmsVehicle c) Check(c.TryFireWeapon(), vehicle.VehicleName + " actual weapon accepts fire");
                    else { Set(target, "nextShot", 0f); Invoke(target, "FirePlayerGun"); }
                    var round = UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().FirstOrDefault(p => !before.Contains(p));
                    var shell = UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>().FirstOrDefault(p => !beforeShell.Contains(p));
                    object projectile = (object)round ?? shell; Check(projectile != null, vehicle.VehicleName + " launched projectile");
                    Check(Vector3.Distance(((MonoBehaviour)projectile).transform.position, origin) < .01f && Vector3.Angle((Vector3)Get(projectile, "velocity"), bore) < .05f, vehicle.VehicleName + " projectile uses visible bore");
                    phase = 5; return;
                }
                if (phase == 5)
                {
                    Capture(target, 0, true); player.ForceExitVehicle();
                    if (++index < 5) { phase = 1; return; }
                    foreach (var t in targets)
                    {
                        if (t is PrototypeCombinedArmsVehicle c) { Invoke(c, "SetVisible", false); Check(!Rig(t).gameObject.activeSelf, c.VehicleName + " art hides"); Invoke(c, "SetVisible", true); }
                        var old = t.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Armored chassis" || r.name == "Rover chassis" || r.name == "Fuselage" || r.name == "Engine block");
                        Check(old.All(r => !r.enabled), ((IPrototypeVehicle)t).VehicleName + " prototype meshes stay hidden");
                        for (int lod = 0; lod < 3; lod++) Capture(t, lod, false);
                    }
                    Finish(null);
                }
            }
            catch (Exception e) { Finish(e); }
        }
        private static PrototypeVehicleArtRig Rig(MonoBehaviour t) => t.GetComponentInChildren<PrototypeVehicleArtRig>(true);
        private static PrototypeCombinedVehicleVisual Model(object t) => (PrototypeCombinedVehicleVisual)Get(t, "model");
        private static void SpawnCheck(MonoBehaviour t)
        {
            var rig = Rig(t); Check(rig != null, ((IPrototypeVehicle)t).VehicleName + " authored spawn");
            var lods = rig.GetComponent<LODGroup>().GetLODs(); Check(lods.Length == 3 && lods.All(l => l.renderers.Length > 2), t.name + " complete LODs");
            string team = ((IPrototypeVehicle)t).Team.ToString();
            Check(lods[0].renderers.Any(r => r.sharedMaterials.Any(m => m != null && m.GetTexture("_BaseMap") != null && m.GetTexture("_BaseMap").name.Contains(team))), t.name + " team atlas " + team);
            Check(lods.All(l => l.renderers.All(r => r.sharedMaterials.All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit"))), t.name + " URP materials");
        }
        private static void PoseCheck(MonoBehaviour target)
        {
            var rig = Rig(target); Transform turret, barrel;
            if (target is PrototypeCombinedArmsVehicle) { turret = Model(target).Turret; barrel = Model(target).Barrel; }
            else { turret = (Transform)Get(target, target is PrototypeScoutVehicle ? "turret" : "gunMount"); barrel = (Transform)Get(target, "barrelPivot"); }
            Check(rig.Turrets.All(t => Quaternion.Angle(t.rotation, turret.rotation) < .05f && Vector3.Distance(t.position, turret.position) < .004f), target.name + " yaw pivots aligned");
            Check(rig.Barrels.All(t => Quaternion.Angle(t.rotation, barrel.rotation) < .05f && Vector3.Distance(t.position, barrel.position) < .004f), target.name + " elevation pivots aligned");
            var tip = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Muzzle");
            Check(Vector3.Distance(tip.position, ((IPrototypeWeaponAim)target).WeaponMuzzle.position) < .006f, target.name + " art muzzle alignment");
        }
        private static void Capture(MonoBehaviour target, int lod, bool optic)
        {
            var otherVehicles = game.Vehicles.OfType<MonoBehaviour>().Where(v => v != target && v.gameObject.activeSelf).ToArray();
            foreach (var other in otherVehicles) other.gameObject.SetActive(false);
            var rig = Rig(target); var group = rig.GetComponent<LODGroup>(); group.ForceLOD(lod);
            var renderers = group.GetLODs()[lod].renderers; Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            var go = new GameObject("Remaining proof camera"); var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.20f, .24f, .28f);
            cam.nearClipPlane = .05f; cam.farClipPlane = 1000f; cam.fieldOfView = optic ? 72f : 45f;
            if (optic)
            {
                Invoke(player, "UpdateVehicleCamera");
                cam.transform.position = player.ViewCamera.transform.position;
                cam.transform.rotation = player.ViewCamera.transform.rotation;
            }
            else
            {
                cam.transform.position = bounds.center + target.transform.rotation * new Vector3(.7f, .43f, .75f).normalized * bounds.size.magnitude * 1.05f;
                cam.transform.LookAt(bounds.center);
            }
            var rt = new RenderTexture(1100, 800, 24); var image = new Texture2D(1100, 800, TextureFormat.RGB24, false);
            var old = RenderTexture.active;
            try
            {
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1100, 800), 0, 0); image.Apply();
                string key = ((IPrototypeVehicle)target).VehicleName.Split(' ')[0];
                string path = Path.Combine(Output, key + "_" + ((IPrototypeVehicle)target).Team + (optic ? "_Optic" : "_LOD" + lod) + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old; cam.targetTexture = null; rt.Release();
                UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
                group.ForceLOD(-1);
                foreach (var other in otherVehicles) other.gameObject.SetActive(true);
            }
        }
        private static object Get(object o, string name) => o.GetType().GetField(name, Hidden).GetValue(o);
        private static void Set(object o, string name, object value) => o.GetType().GetField(name, Hidden).SetValue(o, value);
        private static object Invoke(object o, string name, params object[] args) => o.GetType().GetMethod(name, Hidden).Invoke(o, args);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message); Debug.Log("REMAINING_CHECK " + message);
        }
        private static void Finish(Exception error)
        {
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "play-results.txt"), (error == null ? "PASS\n" : "FAIL " + error + "\n") + string.Join("\n", checks));
            if (error != null) Debug.LogException(error); else Debug.Log("REMAINING_ART_PLAY_PASS " + checks.Count + " assertions");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
