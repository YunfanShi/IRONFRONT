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
    // Batch-owned SampleScene session; exercises real spawned vehicles and
    // player seat/weapon methods without modifying the saved scene.
    [InitializeOnLoad]
    public static class ArmorArtPlaySmoke
    {
        private const string Key = "ironfront.armorArtSmoke";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static PrototypeRuntime game;
        private static PrototypePlayer player;
        private static PrototypeTankVehicle tank;
        private static PrototypeCombinedArmsVehicle ifv;
        private static MonoBehaviour[] targets;
        private static int phase, steps, lastFrame = -1;
        private static Vector3 start;
        private static Quaternion wheelStart;
        private static float firedAt;
        private static readonly List<string> checks = new List<string>();
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../art/vehicles/armor-integration-2026-10-23"));
        static ArmorArtPlaySmoke() => EditorApplication.update += Tick;

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in a dedicated batch Editor.");
            SessionState.SetBool(Key, true);
            SessionState.SetString(Key + ".deadline", DateTime.UtcNow.AddMinutes(4).Ticks.ToString());
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (DateTime.UtcNow.Ticks > long.Parse(SessionState.GetString(Key + ".deadline", "0")))
            { Finish(new TimeoutException("Armor art check timed out")); return; }
            if (!EditorApplication.isPlaying || Time.frameCount < 10 || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (phase == 0)
                {
                    game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                    if (game == null || game.Player == null) return;
                    player = game.Player;
                    var allTanks = UnityEngine.Object.FindObjectsByType<PrototypeTankVehicle>();
                    var allIfvs = UnityEngine.Object.FindObjectsByType<PrototypeCombinedArmsVehicle>()
                        .Where(v => v.Kind == PrototypeVehicleKind.Ifv).ToArray();
                    if (allTanks.Length < 2 || allIfvs.Length < 2) return;
                    targets = new MonoBehaviour[] {
                        allTanks.First(v => v.Team == PrototypeTeam.Blue), allTanks.First(v => v.Team == PrototypeTeam.Red),
                        allIfvs.First(v => v.Team == PrototypeTeam.Blue), allIfvs.First(v => v.Team == PrototypeTeam.Red) };
                    tank = (PrototypeTankVehicle)targets[0]; ifv = (PrototypeCombinedArmsVehicle)targets[2];
                    if (!game.MatchStarted) game.BeginBattle();
                    game.Frontend.Close(); game.SetSupportMenu(false); player.enabled = false;
                    foreach (var bot in game.Bots) bot.enabled = false;
                    foreach (var v in game.Vehicles) if (v is Behaviour b) b.enabled = false;
                    for (int i = 0; i < targets.Length; i++)
                    {
                        ValidateSpawn(targets[i]);
                        targets[i].transform.position = new Vector3(-325f + i * 15f,
                            PrototypeLayout.HeightAt(-325f + i * 15f, -320f) + .05f, -320f);
                        targets[i].transform.rotation = Quaternion.identity;
                    }
                    Enter(tank); start = tank.transform.position; steps = 0; phase = 1; return;
                }
                if (phase == 1)
                {
                    Invoke(tank, "Drive", 1f, .3f, .05f);
                    if (++steps < 36) return;
                    Check(Vector3.Distance(start, tank.transform.position) > 2f && tank.Speed > 3f,
                        "T90 real driver movement and turning");
                    foreach (var target in targets)
                    {
                        Vector3 aim = target.transform.TransformPoint(new Vector3(35f, 25f, 90f));
                        for (int i = 0; i < 80; i++)
                            if (target is PrototypeTankVehicle t) t.GetComponent<PrototypeTankVisual>().AimAt(aim, .05f);
                            else ((PrototypeCombinedArmsVehicle)target).AimAt(aim, .05f);
                    }
                    phase = 2; return;
                }
                if (phase == 2)
                {
                    foreach (var target in targets) ValidatePose(target);
                    var before = new HashSet<PrototypeBallisticProjectile>(UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>());
                    Vector3 origin = tank.WeaponMuzzle.position, bore = tank.WeaponMuzzle.forward;
                    Set(tank, "nextShot", 0f); Invoke(tank, "FireCannon");
                    var shell = UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().Single(p => !before.Contains(p));
                    Check(Vector3.Distance(shell.transform.position, origin) < .001f &&
                        Vector3.Angle((Vector3)Get(shell, "velocity"), bore) < .01f, "T90 fires from calibrated real bore");
                    firedAt = Time.time; phase = 3; return;
                }
                if (phase == 3)
                {
                    if (Time.time - firedAt < .10f) return;
                    var rig = Rig(tank); var model = tank.GetComponent<PrototypeTankVisual>();
                    Check(model.BarrelPivot.localPosition.z < rig.BarrelAnchor.z - rig.TurretAnchor.z - .12f,
                        "T90 gameplay recoil moves the calibrated barrel");
                    Check(Vector3.Distance(rig.Barrels[0].position, model.BarrelPivot.position) < .001f,
                        "T90 visible cannon follows recoil without pivot drift");
                    player.ForceExitVehicle(); Enter(ifv); start = ifv.transform.position;
                    wheelStart = Rig(ifv).Wheels[0].localRotation; steps = 0; phase = 4; return;
                }
                if (phase == 4)
                {
                    ifv.ApplyPlayerInput(1f, .6f, 0f, false, false, .05f);
                    if (++steps < 30) return;
                    phase = 5; return;
                }
                if (phase == 5)
                {
                    var rig = Rig(ifv);
                    Check(Vector3.Distance(start, ifv.transform.position) > 2f && ifv.Speed > 3f,
                        "V12 real player-input driving");
                    Check(Quaternion.Angle(wheelStart, rig.Wheels[0].localRotation) > 5f &&
                        rig.SteeringAxles.All(a => Mathf.Abs(Mathf.DeltaAngle(0f, a.localEulerAngles.y)) > 10f),
                        "V12 wheel travel and front-two-axle steering on all LODs");
                    var before = new HashSet<PrototypeVehicleProjectile>(UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>());
                    Vector3 origin = ifv.WeaponMuzzle.position, bore = ifv.WeaponMuzzle.forward;
                    Check(ifv.TryFireWeapon(), "V12 real driver weapon accepts fire");
                    var shell = UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>().Single(p => !before.Contains(p));
                    Check(Vector3.Distance(shell.transform.position, origin) < .001f &&
                        Vector3.Angle((Vector3)Get(shell, "velocity"), bore) < .01f, "V12 projectile uses calibrated bore");
                    Check(player.TrySwitchVehicleSeat(2), "V12 passenger seat transfer");
                    Vector3 position = ifv.transform.position;
                    ifv.ApplyPlayerInput(1f, 1f, 0f, false, false, .05f);
                    Check(Vector3.Distance(position, ifv.transform.position) < .001f && !ifv.TryFireWeapon(),
                        "passenger cannot drive or fire main weapon");
                    player.ForceExitVehicle(); firedAt = Time.time; phase = 6; return;
                }
                if (phase == 6)
                {
                    if (Time.time - firedAt < .6f) return;
                    foreach (var target in targets)
                    {
                        // Destruction/respawn visibility must not resurrect the
                        // old prototype meshes or bypass LOD selection.
                        Invoke(target, "SetVisible", false);
                        Check(!Rig(target).gameObject.activeSelf, target.name + " art hides");
                        Invoke(target, "SetVisible", true);
                        Check(Rig(target).gameObject.activeInHierarchy, target.name + " art restores");
                        var old = target.GetComponentsInChildren<Renderer>().Where(r =>
                            r.name == "Cast turret armor" || r.name == "Eight-wheel armored chassis");
                        Check(old.All(r => !r.enabled), target.name + " keeps replaced primitives hidden");
                        ValidatePose(target);
                        for (int lod = 0; lod < 3; lod++) Capture(target, lod, false);
                    }
                    Enter(tank); Invoke(player, "UpdateVehicleCamera");
                    for (int i = 0; i < 80; i++) tank.GetComponent<PrototypeTankVisual>().AimAt(
                        player.ViewCamera.transform.position + player.ViewCamera.transform.forward * 200f, .05f);
                    phase = 7; return;
                }
                if (phase == 7)
                {
                    Capture(tank, 0, true); player.ForceExitVehicle();
                    Enter(ifv); Invoke(player, "UpdateVehicleCamera");
                    for (int i = 0; i < 80; i++) ifv.AimAt(
                        player.ViewCamera.transform.position + player.ViewCamera.transform.forward * 150f, .05f);
                    phase = 8; return;
                }
                if (phase == 8)
                {
                    Capture(ifv, 0, true); player.ForceExitVehicle(); Finish(null);
                }
            }
            catch (Exception e) { Finish(e); }
        }

        private static PrototypeVehicleArtRig Rig(MonoBehaviour target) => target.GetComponentInChildren<PrototypeVehicleArtRig>(true);
        private static void Enter(IPrototypeVehicle vehicle) => Check((bool)Invoke(player, "EnterVehicle", vehicle), vehicle.VehicleName + " real seat entry");
        private static void ValidateSpawn(MonoBehaviour target)
        {
            var rig = Rig(target); Check(rig != null && rig.Turrets.Length == 3 && rig.Barrels.Length == 3, target.name + " authored spawn and three moving LODs");
            var group = rig.GetComponent<LODGroup>(); var lods = group.GetLODs();
            Check(lods.Length == 3 && lods.All(l => l.renderers.Length >= 4), target.name + " complete LODGroup");
            string team = ((IPrototypeVehicle)target).Team.ToString();
            Check(lods[0].renderers.Any(r => r.sharedMaterials.Any(m => m.GetTexture("_BaseMap") != null &&
                m.GetTexture("_BaseMap").name.IndexOf(team, StringComparison.OrdinalIgnoreCase) >= 0)), target.name + " " + team + " atlas");
            Check(lods.All(l => l.renderers.All(r => r.sharedMaterials.All(m => m != null &&
                m.shader.name == "Universal Render Pipeline/Lit"))), target.name + " URP materials");
            if (target is PrototypeCombinedArmsVehicle)
                Check(rig.Wheels.Length == 24 && rig.SteeringAxles.Length == 12 && rig.WheelRadii.All(r => r > .55f && r < .75f),
                    "V12 eight wheels with measured radii and four steerable wheels per LOD");
        }
        private static void ValidatePose(MonoBehaviour target)
        {
            var rig = Rig(target); Transform turret, barrel, muzzle;
            if (target is PrototypeTankVehicle)
            {
                var m = target.GetComponent<PrototypeTankVisual>(); turret = m.Turret; barrel = m.BarrelPivot; muzzle = m.CannonMuzzle;
                Check(m.HullCollider.size.x > 5f && m.HullCollider.size.z > 7f, "T90 calibrated hull collider");
            }
            else
            {
                var m = (PrototypeCombinedVehicleVisual)Get(target, "model"); turret = m.Turret; barrel = m.Barrel; muzzle = m.Muzzle;
                Check(m.Hull.size.x > 4.5f && m.Hull.size.z > 5f, "V12 calibrated hull collider");
            }
            Check(rig.Turrets.All(t => Quaternion.Angle(t.rotation, turret.rotation) < .05f &&
                Vector3.Distance(t.position, turret.position) < .003f), target.name + " turret pose and real pivot");
            Check(rig.Barrels.All(b => Quaternion.Angle(b.rotation, barrel.rotation) < .05f &&
                Vector3.Distance(b.position, barrel.position) < .003f), target.name + " elevation pose and real pivot");
            Transform artMuzzle = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == "Muzzle");
            Check(Vector3.Distance(artMuzzle.position, muzzle.position) < .004f, target.name + " rendered barrel tip matches gameplay muzzle");
        }
        private static void Capture(MonoBehaviour target, int lod, bool optic)
        {
            var rig = Rig(target); var group = rig.GetComponent<LODGroup>(); group.ForceLOD(lod);
            var renderers = group.GetLODs()[lod].renderers; Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            var go = new GameObject("Armor proof camera"); var cam = go.AddComponent<Camera>();
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
                string key = target is PrototypeTankVehicle ? "T90" : "V12";
                string path = Path.Combine(Output, key + "_" + ((IPrototypeVehicle)target).Team + (optic ? "_Optic" : "_LOD" + lod) + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old; cam.targetTexture = null; rt.Release();
                UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
                group.ForceLOD(-1);
            }
        }
        private static object Get(object o, string name) => o.GetType().GetField(name, Hidden).GetValue(o);
        private static void Set(object o, string name, object value) => o.GetType().GetField(name, Hidden).SetValue(o, value);
        private static object Invoke(object o, string name, params object[] args) => o.GetType().GetMethod(name, Hidden).Invoke(o, args);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message); Debug.Log("ARMOR_CHECK " + message);
        }
        private static void Finish(Exception error)
        {
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "play-results.txt"), (error == null ? "PASS\n" : "FAIL " + error + "\n") + string.Join("\n", checks));
            if (error != null) Debug.LogException(error); else Debug.Log("ARMOR_ART_PLAY_PASS " + checks.Count + " assertions");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
