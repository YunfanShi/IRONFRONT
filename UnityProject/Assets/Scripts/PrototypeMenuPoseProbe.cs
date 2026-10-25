#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Runs only when explicitly requested in a development player. Does not save
    // loadout/preferences; it exercises the isolated real preview directly.
    public sealed class PrototypeMenuPoseProbe : MonoBehaviour
    {
        private string output;
        private int errors;
        private float maxGrip, maxFoot, maxStep, minMotion = float.MaxValue;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-ironfrontMenuPoseProbe")
                    new GameObject("Menu pose verification only").AddComponent<PrototypeMenuPoseProbe>().output = Path.GetFullPath(args[i + 1]);
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.runInBackground = true;
            Application.logMessageReceived += Observe;
            IEnumerator work = Run(); Exception failure = null;
            while (true)
            {
                bool more = false; object current = null;
                try { more = work.MoveNext(); if (more) current = work.Current; } catch (Exception e) { failure = e; }
                if (!more || failure != null) break;
                yield return current;
            }
            Application.logMessageReceived -= Observe;
            bool pass = failure == null && errors == 0;
            File.WriteAllText(Path.Combine(output, "results.txt"), (pass ? "PASS" : "FAIL") + "\nVersion=" + Application.version +
                "\nRuntimeErrors=" + errors + "\nMaxGripErrorM=" + maxGrip + "\nMaxAnkleDriftM=" + maxFoot +
                "\nMaxLoopPositionStepM=" + maxStep + "\nMinChestMotionM=" + minMotion +
                "\nVerified=7 weapons; 201 samples per loop; live playback; two complete cycles at 30 fps" +
                (failure == null ? "" : "\n" + failure));
            Debug.Log("IRONFRONT menu pose " + (pass ? "PASS" : "FAIL"));
            Application.Quit(pass ? 0 : 1);
        }
        private IEnumerator Run()
        {
            PrototypeRuntime game = null;
            for (int i = 0; i < 600; i++)
            {
                game = FindAnyObjectByType<PrototypeRuntime>();
                if (game != null && game.Frontend != null) break;
                yield return null;
            }
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var view = (PrototypeStartMenuView)typeof(PrototypeFrontend).GetField("startMenu", flags)?.GetValue(game.Frontend);
            Check(view != null, "Menu exists");
            var preview = (PrototypeMenuPreview)typeof(PrototypeStartMenuView).GetField("preview", flags).GetValue(view);
            foreach (PrototypeWeaponId weapon in Enum.GetValues(typeof(PrototypeWeaponId)))
            {
                preview.SetOperator(PrototypeInfantryClass.Assault, weapon);
                var pose = preview.Operator.GetComponent<PrototypeMenuOperatorPose>(); pose.Automatic = false;
                Check(pose.HasSourceMotion, "CC0 source motion is loaded");
                var visual = preview.Operator.GetComponent<PrototypeSoldierVisual>();
                Transform model = preview.Operator.transform.Find("Armored infantry visual"), torso = model.Find("Pelvis/Torso");
                Transform leftFoot = model.Find("Pelvis/Left leg/Knee/Ankle"), rightFoot = model.Find("Pelvis/Right leg/Knee/Ankle");
                Transform leftHand = torso.Find("Left arm/Elbow/Hand"), rightHand = torso.Find("Right arm/Elbow/Hand");
                Transform gun = visual.HeldWeapon.transform;
                var art = gun.GetComponent<PrototypeWeaponArtRig>();
                pose.Evaluate(0); Vector3 left0 = leftFoot.position, right0 = rightFoot.position, chest0 = torso.position;
                float motion = 0, weaponGrip = 0;
                for (int f = 0; f <= 200; f++)
                {
                    pose.Evaluate(pose.LoopDuration * f / 200f);
                    bool pistol = weapon == PrototypeWeaponId.Pistol;
                    Vector3 left = art != null ? art.SupportPosition : gun.TransformPoint(pistol ? new Vector3(-.055f, -.16f, .015f) : new Vector3(0f, -.16f, .37f));
                    Vector3 right = art != null ? art.RightGrip.position : gun.TransformPoint(new Vector3(0f, -.18f, pistol ? -.045f : -.18f));
                    weaponGrip = Mathf.Max(weaponGrip, Vector3.Distance(left, leftHand.position), Vector3.Distance(right, rightHand.position));
                    maxGrip = Mathf.Max(maxGrip, weaponGrip);
                    maxFoot = Mathf.Max(maxFoot, Vector3.Distance(left0, leftFoot.position), Vector3.Distance(right0, rightFoot.position));
                    motion = Mathf.Max(motion, Vector3.Distance(chest0, torso.position));
                    Check(Vector3.Distance(torso.Find("Left arm").position, torso.Find("Left arm/Elbow").position) > .24f, "No collapsed upper arm");
                }
                File.AppendAllText(Path.Combine(output, "contacts.csv"), weapon + "," + weaponGrip + "\n");
                minMotion = Mathf.Min(minMotion, motion);
                pose.Evaluate(pose.LoopDuration - 1f / 120f); Vector3 before = torso.position;
                pose.Evaluate(1f / 120f); maxStep = Mathf.Max(maxStep, Vector3.Distance(before, torso.position));
                preview.Render(); yield return new WaitForEndOfFrame();
                SaveTexture((RenderTexture)preview.Texture, "operator-" + weapon + ".png");
                if (weapon == PrototypeWeaponId.Carbine)
                {
                    Quaternion home = preview.Operator.transform.localRotation;
                    foreach (float yaw in new[] { -65f, 65f })
                    {
                        preview.Operator.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                        pose.Evaluate(0f); preview.Render();
                        SaveTexture((RenderTexture)preview.Texture, "operator-side-" + yaw + ".png");
                    }
                    preview.Operator.transform.localRotation = home;
                }
            }
            Check(maxGrip < .008f, "Every weapon remains in both hands: " + maxGrip);
            Check(maxFoot < .0015f, "Planted ankles do not slide: " + maxFoot);
            Check(maxStep < .002f, "Loop boundary is continuous: " + maxStep);
            Check(minMotion > .001f, "Sampled body motion is visible and nonzero");
            preview.SetOperator(PrototypeInfantryClass.Assault, PrototypeWeaponId.Carbine);
            var moving = preview.Operator.GetComponent<PrototypeMenuOperatorPose>();
            Transform chest = preview.Operator.transform.Find("Armored infantry visual/Pelvis/Torso");
            Vector3 live0 = chest.position;
            yield return new WaitForSecondsRealtime(.9f);
            Check(Vector3.Distance(live0, chest.position) > .0005f, "Actual LateUpdate advances the loop");
            moving.Automatic = false; preview.SetComposition(false, true);
            int count = Mathf.RoundToInt(moving.LoopDuration * 2 * 30);
            Directory.CreateDirectory(Path.Combine(output, "frames"));
            for (int f = 0; f < count; f++)
            {
                moving.Evaluate(f / 30f);
                yield return new WaitForEndOfFrame();
                Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(output, "frames", f.ToString("D4") + ".jpg"), screen.EncodeToJPG(86));
                if (f == 0) File.WriteAllBytes(Path.Combine(output, "lobby-pose.png"), screen.EncodeToPNG());
                Destroy(screen);
            }
        }
        private void SaveTexture(RenderTexture target, string file)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
            Destroy(image); RenderTexture.active = previous;
        }
        private void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); }
    }
}
#endif
