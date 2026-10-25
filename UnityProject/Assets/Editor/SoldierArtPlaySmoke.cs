using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    [InitializeOnLoad]
    public static class SoldierArtPlaySmoke
    {
        const string Key = "ironfront.soldierArtSmoke";
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        static IEnumerator routine;
        static int lastFrame = -1;
        static readonly List<string> checks = new List<string>();
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/characters/motion-refinement-2026-10-24"));
        static SoldierArtPlaySmoke() => EditorApplication.update += Tick;
        public static void Run()
        {
            SoldierArtAssetSetup.Build();
            SessionState.SetBool(Key, true);
            SessionState.SetString(Key + ".deadline", DateTime.UtcNow.AddMinutes(5).Ticks.ToString());
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity"); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (DateTime.UtcNow.Ticks > long.Parse(SessionState.GetString(Key + ".deadline", "0"))) { Finish(new Exception("timeout")); return; }
            if (!EditorApplication.isPlaying || Time.frameCount < 30) return;
            var game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>(); if (game == null || game.Player == null) return;
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            try { if (routine == null) routine = Verify(game); if (!routine.MoveNext()) Finish(null); }
            catch (Exception e) { Finish(e); }
        }
        static IEnumerator Verify(PrototypeRuntime game)
        {
                if (!game.MatchStarted) game.BeginBattle(); game.Frontend.Close(); game.Player.enabled = false;
                foreach (var b in game.Bots) b.enabled = false;
                foreach (var v in game.Vehicles.OfType<MonoBehaviour>()) v.gameObject.SetActive(false);
                var all = game.Bots.Select(b => b.GetComponent<PrototypeSoldierVisual>()).ToArray();
                Check(all.Length > 0 && all.All(s => s.ArtRig != null), "Every actual bot uses skinned art");
                Check(game.Player.GetComponent<PrototypeSoldierVisual>().ArtRig != null, "Actual local player uses skinned art");
                foreach (var s in all) s.gameObject.SetActive(false);
                foreach (var team in new[] { PrototypeTeam.Blue, PrototypeTeam.Red })
                {
                    var actor = game.Bots.First(b => b.Team == team); actor.gameObject.SetActive(true);
                    actor.transform.SetPositionAndRotation(new Vector3(-325, 20, -320), Quaternion.identity);
                    var visual = actor.GetComponent<PrototypeSoldierVisual>(); visual.enabled = false;
                    var rig = visual.ArtRig; var group = rig.GetComponent<LODGroup>();
                    Check(group.GetLODs().Length == 3, team + " has three skinned LODs");
                    Check(rig.Bindings.Length == 16, team + " gameplay bone map complete");
                    for (int lod = 0; lod < 3; lod++)
                    {
                        group.ForceLOD(lod);
                        foreach (var pose in new[] { "standing", "crouch", "prone", "run" })
                        {
                            Set(visual, "runWeight", pose == "run" ? 1f : 0f); Set(visual, "gait", 1.1f);
                            Invoke(visual, "Pose", pose == "crouch" ? 1f : 0f, pose == "prone" ? 1f : 0f, .891f, pose == "run" ? 1f : 0f);
                            yield return null; yield return null;
                            var weapon = visual.HeldWeapon.transform;
                            foreach (bool left in new[] { false, true })
                            {
                                var hand = (Transform)Get(visual, left ? "leftHand" : "rightHand");
                                Vector3 grip = weapon.TransformPoint(left ? new Vector3(0, -.16f, .37f) : new Vector3(0, -.18f, -.18f));
                                var weaponArt=weapon.GetComponent<PrototypeWeaponArtRig>();
                                if(weaponArt!=null)grip=left?weaponArt.SupportPosition:weaponArt.RightGrip.position;
                                Check(Vector3.Distance(hand.position, grip) < .035f, team + " " + pose + " " + (left ? "left" : "right") + " grip error=" + Vector3.Distance(hand.position, grip));
                            }
                            var points = BakedPoints(rig, lod); var bounds = new Bounds(points[0], Vector3.zero); foreach (var p in points) bounds.Encapsulate(p);
                            Check(points.All(p => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z)), team + " LOD" + lod + " " + pose + " finite skin");
                            Check(bounds.size.magnitude < 3.5f, team + " LOD" + lod + " " + pose + " no exploded skin " + bounds.size);
                            if (pose == "standing" || pose == "crouch")
                            {
                                var model = (GameObject)Get(visual, "model");
                                Check(Mathf.Abs(bounds.min.y - model.transform.position.y) < .03f, team + " " + pose + " feet contact=" + (bounds.min.y - model.transform.position.y));
                            }
                            if (pose == "standing") Check(bounds.size.y > 1.65f && bounds.size.y < 1.95f, team + " standing scale " + bounds.size.y);
                            Capture(visual, lod, pose, new Vector3(.7f, .18f, 1f));
                            if (lod == 0)
                            {
                                Capture(visual, lod, pose + "-front", Vector3.forward);
                                if (pose == "standing" || pose == "crouch") Capture(visual, lod, pose + "-hands", new Vector3(.5f,.25f,1));
                                if (pose == "standing") Capture(visual, lod, pose + "-boots", new Vector3(1,.4f,1));
                            }
                            if (lod == 0 && pose != "run")
                            { Capture(visual, lod, pose + "-profile", Vector3.right); Capture(visual, lod, pose + "-back", Vector3.back); }
                        }
                    }
                    visual.SetMotorcycleFirstPerson(true);
                    Check(rig.Bodies.All(r => !r.enabled) && rig.Arms.All(r => r.enabled), team + " FP hides body and preserves authored arms");
                    visual.SetMotorcycleFirstPerson(false);
                    Check(rig.Bodies.All(r => r.enabled), team + " TP restores body");
                    var fallback = (Renderer[])Get(visual, "fallbackBody");
                    Check(fallback.All(r => !r.enabled), team + " fallback stays hidden after perspective switch");
                    actor.gameObject.SetActive(false);
                }
                var player = game.Player; var rider = player.GetComponent<PrototypeSoldierVisual>();
                var bike = game.Vehicles.OfType<PrototypeCombinedArmsVehicle>().First(v => v.Kind == PrototypeVehicleKind.Motorcycle && v.Team == player.Team);
                bike.gameObject.SetActive(true); bike.enabled = false;
                bike.transform.SetPositionAndRotation(new Vector3(-325, PrototypeLayout.HeightAt(-325, -320), -320), Quaternion.identity);
                Check((bool)Invoke(player, "EnterVehicle", bike), "Real player enters M2");
                foreach (int seat in new[] { 0, 1 })
                {
                    if (seat == 1) Check(player.TrySwitchVehicleSeat(1), "Passenger seat switch");
                    foreach (float steer in new[] { -1f, 0f, 1f })
                    {
                        bike.ApplyPlayerInput(.1f, steer, 0, false, false, .05f);
                        Invoke(bike, "UpdateVisualMotion", .05f); Invoke(player, "UpdateVehicleCamera"); rider.RefreshPose(true);
                        yield return null; yield return null;
                        foreach (bool left in new[] { true, false })
                        {
                            var hand = (Transform)Get(rider, left ? "leftHand" : "rightHand"); var foot = (Transform)Get(rider, left ? "leftFoot" : "rightFoot");
                            float handError = Vector3.Distance(hand.position, bike.MotorcycleGripTarget(left, seat).position);
                            float footError = Vector3.Distance(foot.position - bike.MotorcyclePegTarget(left, seat).up * .14f, bike.MotorcyclePegTarget(left, seat).position);
                            Check(handError < .02f, "M2 seat " + seat + " steer " + steer + " hand=" + handError);
                            Check(footError < .02f, "M2 seat " + seat + " foot=" + footError);
                        }
                    }
                    Set(player, "vehicleFirstPerson", false); Invoke(player, "ApplyPerspectiveVisibility"); rider.RefreshPose(true);
                    yield return null; yield return null;
                    Capture(rider, 0, "M2-seat" + seat, new Vector3(1,.3f,1));
                    Set(player, "vehicleFirstPerson", true); Invoke(player, "ApplyPerspectiveVisibility"); Invoke(player, "UpdateVehicleCamera");
                    Check(rider.ArtRig.FirstPerson && rider.ArtRig.Bodies.All(r => !r.enabled), "M2 FP uses authored arms only");
                }
                player.ForceExitVehicle();
                Check(!rider.ArtRig.FirstPerson && rider.ArtRig.Bodies.All(r => r.enabled), "M2 exit restores authored full body");
                foreach (var weaponId in new[] { PrototypeWeaponId.Carbine, PrototypeWeaponId.Pistol })
                {
                    rider.SetVisible(false); bike.gameObject.SetActive(false);
                    ((GameObject)Get(player, "firstPersonWeapon")).SetActive(false);
                    player.ViewCamera.transform.SetPositionAndRotation(new Vector3(0, 100, 0), Quaternion.identity);
                    var fp = PrototypeWeaponVisual.Build(player.ViewCamera.transform, weaponId, game, true, player.Team);
                    fp.transform.localPosition = new Vector3(.30f, -.29f, .74f); fp.transform.localScale = Vector3.one * .55f;
                    var viewRig = fp.GetComponentInChildren<PrototypeSoldierArtRig>();
                    Check(viewRig != null, weaponId + " authored first person arms");
                    foreach (var arm in viewRig.Arms)
                        foreach (var w in arm.sharedMesh.boneWeights)
                        {
                            int[] indices = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                            float[] amounts = { w.weight0, w.weight1, w.weight2, w.weight3 };
                            for (int i = 0; i < 4; i++) if (amounts[i] > 0 && (arm.bones[indices[i]].name == "Torso" || arm.bones[indices[i]].name == "Pelvis" || arm.bones[indices[i]].name == "Head")) throw new Exception("FP retains hidden torso weights");
                        }
                    Check(true, weaponId + " FP mesh has no hidden torso influences");
                    yield return null; yield return null;
                    CaptureCamera(player.ViewCamera, "FP-" + weaponId);
                    UnityEngine.Object.Destroy(fp); yield return null;
                }
                var remote = new GameObject("Remote soldier verification").AddComponent<PrototypeRemotePlayer>();
                remote.Initialize(game, 999, PrototypeTeam.Red);
                Check(remote.GetComponent<PrototypeSoldierVisual>().ArtRig != null, "Network replica uses authored red soldier");
                UnityEngine.Object.Destroy(remote.gameObject);
        }
        public static Vector3[] BakedPoints(PrototypeSoldierArtRig rig, int lod)
        {
            var points = new List<Vector3>();
            foreach (var r in rig.GetComponent<LODGroup>().GetLODs()[lod].renderers.Cast<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh(); r.BakeMesh(mesh); points.AddRange(mesh.vertices.Select(v => r.transform.TransformPoint(v))); UnityEngine.Object.DestroyImmediate(mesh);
            }
            return points.ToArray();
        }
        static void Capture(PrototypeSoldierVisual target, int lod, string pose, Vector3 direction)
        {
            var points = BakedPoints(target.ArtRig, lod); var bounds = new Bounds(points[0], Vector3.zero); foreach (var p in points) bounds.Encapsulate(p);
            var go = new GameObject("Soldier proof camera"); var cam = go.AddComponent<Camera>();
            var lightGo = new GameObject("Review fill"); var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f; lightGo.transform.rotation = Quaternion.Euler(30, 210, 0);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .21f, .24f);
            cam.nearClipPlane = .03f; cam.farClipPlane = 8; cam.orthographic = true; cam.orthographicSize = 1.08f;
            Vector3 focus = bounds.center;
            if (pose.EndsWith("-hands")) { focus = (((Transform)Get(target,"leftHand")).position + ((Transform)Get(target,"rightHand")).position) * .5f; cam.orthographicSize = .38f; }
            if (pose.EndsWith("-boots")) { focus = (((Transform)Get(target,"leftFoot")).position + ((Transform)Get(target,"rightFoot")).position) * .5f; cam.orthographicSize = .37f; }
            cam.transform.position = focus + direction.normalized * 4f; cam.transform.LookAt(focus);
            var rt = new RenderTexture(900, 900, 24); var image = new Texture2D(900, 900, TextureFormat.RGB24, false); var old = RenderTexture.active;
            try
            {
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Output, (target.GetComponent<PrototypeBot>() != null ? target.GetComponent<PrototypeBot>().Team.ToString() : "Player") + "-LOD" + lod + "-" + pose + ".png"), image.EncodeToPNG());
            }
            finally { RenderTexture.active = old; cam.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(lightGo); }
        }
        static void CaptureCamera(Camera cam, string label)
        {
            var rt = new RenderTexture(1280,800,24); var image = new Texture2D(1280,800,TextureFormat.RGB24,false);
            var old = RenderTexture.active; var previous = cam.targetTexture;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0,0,1280,800),0,0); image.Apply(); File.WriteAllBytes(Path.Combine(Output,label+".png"),image.EncodeToPNG()); }
            finally { RenderTexture.active = old; cam.targetTexture = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt); }
        }
        static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
        static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);
        static object Invoke(object o, string n, params object[] args) => o.GetType().GetMethod(n, Hidden).Invoke(o, args);
        static void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks.Add(label); }
        static void Finish(Exception error)
        {
            SessionState.SetBool(Key, false);
            File.WriteAllText(Path.Combine(Output, "play-results.txt"), (error == null ? "PASS\n" : "FAIL " + error + "\n") + string.Join("\n", checks));
            if (error != null) Debug.LogException(error); else Debug.Log("SOLDIER_ART_PLAY_PASS " + checks.Count);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
