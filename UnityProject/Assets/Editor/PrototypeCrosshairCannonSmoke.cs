using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeCrosshairCannonSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Preference = "ironfront.unity.crosshairSize";
        private static PrototypeRuntime captureGame;
        private static PrototypeStartMenuView captureView;
        private static UIDocument captureDocument;
        private static PanelSettings capturePanel;
        private static RenderTexture captureTarget;
        private static int captureFrame, captureUpdates;
        private static double captureStarted;
        private static float captureOriginalSize, captureOriginalPreference;
        private static bool captureHadPreference;

        [MenuItem("IRONFRONT/Verify Fixed Attack Cannon", true)]
        [MenuItem("IRONFRONT/Verify Crosshair Settings", true)]
        [MenuItem("IRONFRONT/Capture Fixed Attack Cannon", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Capture Crosshair Settings", true)]
        private static bool CanCaptureSettings() => EditorApplication.isPlaying && captureView == null;

        [MenuItem("IRONFRONT/Verify Fixed Attack Cannon")]
        public static void VerifyCannon()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            PrototypePlayer player = game?.Player;
            var objects = new List<GameObject>();
            var shells = new HashSet<PrototypeVehicleProjectile>(UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>());
            var bullets = new HashSet<PrototypeBallisticProjectile>(UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>());
            Vector3 savedPosition = player != null ? player.transform.position : Vector3.zero;
            Quaternion savedRotation = player != null ? player.transform.rotation : Quaternion.identity;
            try
            {
                Check(game != null && player != null && !game.IsNetworkReplica, "authoritative runtime exists");
                if (!game.MatchStarted) game.BeginBattle();
                game.Frontend.Close();
                Check(player.Alive && !game.Match.Winner.HasValue, "active living player");
                player.ForceExitVehicle();
                var attack = MakeVehicle(game, PrototypeVehicleKind.Helicopter, objects, 0f);
                var model = (PrototypeCombinedVehicleVisual)Field(attack, "model");
                Check(model.Turret.parent == model.Airframe && model.Barrel.parent == model.Turret,
                    "the cannon is attached to the visible airframe rather than an independent turret");
                attack.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
                model.Airframe.localRotation = Quaternion.Euler(-18f, 0f, 22f);
                Vector3 expected = model.Airframe.forward;
                foreach (Vector3 offset in new[] { Vector3.right * 100f, Vector3.left * 100f,
                    Vector3.back * 120f + Vector3.up * 70f, Vector3.down * 80f })
                    for (int i = 0; i < 30; i++) attack.AimAt(attack.transform.position + offset, .1f);
                Check(Quaternion.Angle(model.Turret.localRotation, Quaternion.identity) < .001f &&
                    Quaternion.Angle(model.Barrel.localRotation, Quaternion.identity) < .001f &&
                    Vector3.Angle(model.Muzzle.forward, expected) < .01f,
                    "camera targets cannot yaw/elevate the fixed cannon, including during airframe pitch and roll");
                Enter(player, attack);
                Check(attack.SelectWeapon(0) && attack.TryFireWeapon(), "the pilot actually fires a primary round");
                PrototypeVehicleProjectile launched = null;
                foreach (var shell in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                    if (!shells.Contains(shell)) launched = shell;
                Check(launched != null && Vector3.Angle((Vector3)Field(launched, "velocity"), expected) < .01f &&
                    Vector3.Distance(launched.transform.position, model.Muzzle.position) < .001f,
                    "actual cannon velocity and origin use the fixed visible bore");
                Check(attack.SelectWeapon(1) && attack.IsDownwardMachineGun, "pilot can still select belly MG");
                CheckBelly(attack);
                Vector3 beforeMG = model.GunnerMuzzle.forward;
                Check(attack.TryFireWeapon(), "actual belly MG still fires");
                PrototypeBallisticProjectile fired = null;
                foreach (var bullet in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    if (!bullets.Contains(bullet)) fired = bullet;
                Check(fired != null && Vector3.Angle((Vector3)Field(fired, "velocity"), beforeMG) < .5f,
                    "the actual MG round follows its independent downward bore");
                Check(player.TrySwitchVehicleSeat(1) && attack.IsDownwardMachineGun, "gunner seat still owns the belly MG");
                CheckBelly(attack);
                player.ForceExitVehicle();
                foreach (PrototypeVehicleKind kind in new[] { PrototypeVehicleKind.ReconHelicopter, PrototypeVehicleKind.Jet,
                    PrototypeVehicleKind.Ifv, PrototypeVehicleKind.AntiAir })
                {
                    var other = MakeVehicle(game, kind, objects, objects.Count * 30f);
                    var otherModel = (PrototypeCombinedVehicleVisual)Field(other, "model");
                    Quaternion before = otherModel.Turret.rotation;
                    other.AimAt(other.transform.position + new Vector3(35f, 10f, 100f), 1f);
                    Check(Quaternion.Angle(otherModel.Turret.rotation, before) > .1f,
                        kind + " keeps its existing steerable main weapon");
                }
                Debug.Log("IRONFRONT fixed attack cannon PASS: real airframe hierarchy, immutable main yaw/elevation, flight pitch/roll, " +
                    "actual launched origin/velocity, independent pilot/gunner belly MG limits and fired bore, unchanged recon/jet/IFV/AA steering");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT fixed attack cannon FAIL: " + error); }
            finally
            {
                if (player != null)
                {
                    player.ForceExitVehicle();
                    CharacterController cc = player.GetComponent<CharacterController>();
                    cc.enabled = false; player.transform.SetPositionAndRotation(savedPosition, savedRotation); cc.enabled = true;
                }
                foreach (var shell in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                    if (!shells.Contains(shell)) UnityEngine.Object.DestroyImmediate(shell.gameObject);
                foreach (var bullet in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    if (!bullets.Contains(bullet)) UnityEngine.Object.DestroyImmediate(bullet.gameObject);
                foreach (var item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [MenuItem("IRONFRONT/Verify Crosshair Settings")]
        public static void VerifyCrosshair()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            bool hadPreference = PlayerPrefs.HasKey(Preference);
            float savedPreference = PlayerPrefs.GetFloat(Preference, 1f), savedRuntime = game?.CrosshairSize ?? 1f;
            PrototypeStartMenuView view = null;
            CursorLockMode lockMode = Cursor.lockState; bool cursorVisible = Cursor.visible;
            try
            {
                Check(game != null && game.Player != null && PrototypeStartMenuView.AssetsAvailable, "runtime and editable menu assets exist");
                float spread = game.Player.CrosshairSpread, hip = game.Player.ActiveWeapon.HipSpread, ads = game.Player.ActiveWeapon.AdsSpread;
                PlayerPrefs.DeleteKey(Preference); Invoke(game, "LoadCrosshairSize");
                Check(game.CrosshairSize == 1f, "a missing saved setting loads the default 100 percent");
                game.SetCrosshairSize(-50f); Check(game.CrosshairSize == .5f, "minimum clamps to 50 percent");
                game.SetCrosshairSize(500f); Check(game.CrosshairSize == 2f, "maximum clamps to 200 percent");
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                { game.SetCrosshairSize(invalid); Check(game.CrosshairSize == 1f, "nonfinite input returns safe default"); }
                game.SetCrosshairSize(1.75f);
                Check(PlayerPrefs.GetFloat(Preference) == 1.75f, "setter writes the persistent preference");
                typeof(PrototypeRuntime).GetField("<CrosshairSize>k__BackingField", Private).SetValue(game, .5f);
                Invoke(game, "LoadCrosshairSize");
                Check(game.CrosshairSize == 1.75f, "the same startup loader restores the saved setting");
                view = new PrototypeStartMenuView(game, "Editor settings verification", game.Player.Loadout, AudioListener.volume);
                var document = (UIDocument)Field(view, "document");
                VisualElement root = document.rootVisualElement;
                Slider slider = root.Q<Slider>("crosshair-slider");
                Check(slider != null && slider.lowValue == .5f && slider.highValue == 2f && slider.value == 1.75f,
                    "real lobby slider initializes from saved runtime range and value");
                slider.value = 1.25f;
                Check(game.CrosshairSize == 1.25f && PlayerPrefs.GetFloat(Preference) == 1.25f &&
                    root.Q<Label>("crosshair-value").text == "125%", "actual UI Toolkit change callback updates runtime, label and persistent preference");
                VisualElement preview = root.Q<VisualElement>("crosshair-preview");
                Check(preview != null && preview.childCount == 5 &&
                    Mathf.Abs(preview[0].style.width.value.value - 8.75f) < .001f,
                    "the live crosshair preview immediately changes its rendered bar geometry");
                slider.value = .5f;
                Check(root.Q<Label>("crosshair-value").text == "50%" &&
                    Mathf.Abs(preview[0].style.width.value.value - 3.5f) < .001f,
                    "preview and numeric feedback also reflect the smallest size");
                Check(root.Q<Slider>("volume-slider") != null && root.Q<Slider>("music-slider") != null &&
                    root.Q<Slider>("sensitivity-slider") != null, "existing three controls remain available");
                Check(game.Player.CrosshairSpread == spread && game.Player.ActiveWeapon.HipSpread == hip &&
                    game.Player.ActiveWeapon.AdsSpread == ads, "cosmetic sizing never changes weapon accuracy or simulated spread");
                Debug.Log("IRONFRONT crosshair settings PASS: 50-200 percent/default/finite clamps, startup preference round-trip, " +
                    "actual UI Toolkit slider callback, instantaneous number/preview geometry, preserved volume/music/sensitivity and firearm accuracy; original preference restored");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT crosshair settings FAIL: " + error); }
            finally
            {
                view?.Dispose();
                if (game != null) game.SetCrosshairSize(savedRuntime);
                if (hadPreference) PlayerPrefs.SetFloat(Preference, savedPreference); else PlayerPrefs.DeleteKey(Preference);
                PlayerPrefs.Save(); Cursor.lockState = lockMode; Cursor.visible = cursorVisible;
            }
        }

        [MenuItem("IRONFRONT/Capture Crosshair Settings")]
        public static void CaptureSettings()
        {
            try
            {
                captureGame = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(captureGame != null && captureGame.Player != null, "Play runtime exists");
                captureOriginalSize = captureGame.CrosshairSize;
                captureHadPreference = PlayerPrefs.HasKey(Preference);
                captureOriginalPreference = PlayerPrefs.GetFloat(Preference, 1f);
                captureView = new PrototypeStartMenuView(captureGame, "Settings preview", captureGame.Player.Loadout, AudioListener.volume);
                captureDocument = (UIDocument)Field(captureView, "document");
                capturePanel = UnityEngine.Object.Instantiate(captureDocument.panelSettings);
                capturePanel.name = "EDITOR ONLY crosshair settings capture panel";
                captureTarget = new RenderTexture(960, 600, 24); captureTarget.Create();
                capturePanel.targetTexture = captureTarget;
                captureDocument.panelSettings = capturePanel;
                Invoke(captureView, "SetPage", 3);
                captureDocument.rootVisualElement.Q<Slider>("crosshair-slider").value = 2f;
                captureFrame = Time.frameCount; captureUpdates = 0; captureStarted = EditorApplication.timeSinceStartup;
                EditorApplication.update -= CaptureSettingsFrame; EditorApplication.update += CaptureSettingsFrame;
                Debug.Log("IRONFRONT crosshair settings capture prepared: real UIDocument on cloned 960x600 PanelSettings target texture, actual slider callback sets 200 percent preview; capture pending");
            }
            catch (Exception error) { EndSettingsCapture(); Debug.LogError("IRONFRONT crosshair settings capture FAIL: " + error); }
        }

        private static void CaptureSettingsFrame()
        {
            Texture2D texture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                if (!EditorApplication.isPlaying) { EndSettingsCapture(); return; }
                if (EditorApplication.timeSinceStartup - captureStarted > 10d)
                    throw new InvalidOperationException("The actual UI panel did not render within ten seconds.");
                if (++captureUpdates < 8 || Time.frameCount < captureFrame + 3) return;
                VisualElement root = captureDocument.rootVisualElement;
                var rows = new[] { root.Q<VisualElement>(className: "audio-card"), root.Q<VisualElement>(className: "music-card"),
                    root.Q<VisualElement>(className: "sensitivity-card"), root.Q<VisualElement>(className: "crosshair-card"),
                    root.Q<VisualElement>(className: "controls-card") };
                Rect footer = root.Q<VisualElement>(className: "bottom-bar").worldBound;
                Check(footer.height > 0f, "actual UI layout has resolved");
                for (int i = 0; i < rows.Length; i++)
                {
                    Rect row = rows[i].worldBound;
                    Check(row.width > 400f && row.height > 50f && row.yMax < footer.yMin,
                        "actual settings row remains above the persistent action bar");
                    if (i > 0) Check(row.yMin >= rows[i - 1].worldBound.yMax,
                        "actual neighboring settings cards do not overlap");
                }
                Check(root.Q<Label>("crosshair-value").text == "200%", "rendered callback value is 200 percent");
                RenderTexture.active = captureTarget;
                texture = new Texture2D(960, 600, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, 960f, 600f), 0, 0); texture.Apply();
                int visible = 0; foreach (Color32 pixel in texture.GetPixels32())
                    if (pixel.a > 0 && (pixel.r > 80 || pixel.g > 80 || pixel.b > 80)) visible++;
                Check(visible > 1000, "the actual target texture contains rendered menu content");
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Verification052"));
                Directory.CreateDirectory(folder); string path = Path.Combine(folder, "crosshair-settings-200.png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("IRONFRONT crosshair settings actual UITK capture PASS: " + path +
                    "; resolved five card world bounds clear of each other and action bar; original preference restored");
                EndSettingsCapture();
            }
            catch (Exception error) { EndSettingsCapture(); Debug.LogError("IRONFRONT crosshair settings capture FAIL: " + error); }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void EndSettingsCapture()
        {
            EditorApplication.update -= CaptureSettingsFrame;
            captureView?.Dispose(); captureView = null; captureDocument = null;
            if (capturePanel != null) UnityEngine.Object.Destroy(capturePanel); capturePanel = null;
            if (captureTarget != null) { captureTarget.Release(); UnityEngine.Object.Destroy(captureTarget); } captureTarget = null;
            if (captureGame != null) captureGame.SetCrosshairSize(captureOriginalSize); captureGame = null;
            if (captureHadPreference) PlayerPrefs.SetFloat(Preference, captureOriginalPreference); else PlayerPrefs.DeleteKey(Preference);
            PlayerPrefs.Save();
        }

        [MenuItem("IRONFRONT/Capture Fixed Attack Cannon")]
        public static void CaptureCannon()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            var objects = new List<GameObject>();
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = null; Texture2D texture = null;
            try
            {
                Check(game != null, "Play runtime exists");
                var attack = MakeVehicle(game, PrototypeVehicleKind.Helicopter, objects, 0f);
                var model = (PrototypeCombinedVehicleVisual)Field(attack, "model");
                model.Airframe.localRotation = Quaternion.Euler(-10f, 0f, 12f);
                attack.AimAt(attack.transform.position + new Vector3(-100f, 100f, -100f), 10f);
                GameObject cameraObject = new GameObject("EDITOR ONLY fixed cannon capture camera"); objects.Add(cameraObject);
                Camera camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.backgroundColor = new Color(.23f, .31f, .36f); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.fieldOfView = 40f; camera.nearClipPlane = .1f; camera.farClipPlane = 70f;
                Vector3 center = attack.transform.position + new Vector3(0f, 1.9f, .5f);
                camera.transform.position = center + new Vector3(14f, 4f, 11f); camera.transform.LookAt(center);
                target = new RenderTexture(1800, 1000, 24); camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1800, 1000, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, 1800f, 1000f), 0, 0); texture.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Verification052"));
                Directory.CreateDirectory(folder); string path = Path.Combine(folder, "fixed-attack-cannon.png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("IRONFRONT fixed cannon actual camera capture: " + path);
            }
            catch (Exception error) { Debug.LogError("IRONFRONT fixed cannon capture FAIL: " + error); }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                foreach (var item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static void CheckBelly(PrototypeCombinedArmsVehicle attack)
        {
            float yaw = 180f, pitch = -90f; attack.ClampWeaponView(ref yaw, ref pitch);
            Check(yaw == 75f && pitch == 15f, "existing MG camera limits remain 75 degrees yaw and 15-85 down");
            foreach (Vector3 offset in new[] { Vector3.up * 100f, Vector3.down * 100f, Vector3.back * 100f })
            {
                attack.AimAt(attack.transform.position + offset, 1f);
                Vector3 local = attack.transform.InverseTransformDirection(attack.WeaponMuzzle.forward);
                float elevation = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                float relativeYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                Check(elevation <= -14.99f && elevation >= -85.01f && Mathf.Abs(relativeYaw) <= 75.01f,
                    "independent MG continues to track within its existing cone");
            }
        }
        private static PrototypeCombinedArmsVehicle MakeVehicle(PrototypeRuntime game, PrototypeVehicleKind kind,
            List<GameObject> objects, float offset)
        {
            var item = new GameObject("EDITOR ONLY fixed cannon verification " + kind); objects.Add(item);
            var vehicle = item.AddComponent<PrototypeCombinedArmsVehicle>();
            vehicle.Initialize(game, game.Player.Team, kind, new Vector2(900f + offset, 900f), false);
            vehicle.transform.position = new Vector3(900f + offset, 80f, 900f); vehicle.RespawnEnabled = false;
            return vehicle;
        }
        private static void Enter(PrototypePlayer player, IPrototypeVehicle vehicle) =>
            Check((bool)Invoke(player, "EnterVehicle", vehicle), "player enters real vehicle");
        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static object Invoke(object target, string name, params object[] arguments) =>
            target.GetType().GetMethod(name, Private).Invoke(target, arguments);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
