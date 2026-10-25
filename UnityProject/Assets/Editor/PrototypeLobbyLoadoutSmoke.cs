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
    // The actual UIDocument renders to a cloned panel. No source scene is
    // deployed or changed, and every local preference is restored afterward.
    public static class PrototypeLobbyLoadoutSmoke
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly string[] RoleKeys = { "assault", "medic", "recon", "engineer" };
        private static readonly string[] WeaponKeys = { "carbine", "marksman", "smg", "lmg", "battlerifle", "sniper", "pistol" };
        private static readonly string[] Preferences = { "ironfront.unity.loadout", "ironfront.unity.soloMode",
            "ironfront.unity.masterVolume", "ironfront.unity.musicVolume", "ironfront.unity.mouseSensitivity", "ironfront.unity.crosshairSize" };
        private static readonly bool[] had = new bool[6];
        private static readonly float[] floats = new float[6];
        private static string loadoutJson;
        private static int soloMode;
        private static PrototypeRuntime game;
        private static PrototypeStartMenuView view;
        private static PrototypeMenuPreview preview;
        private static UIDocument document;
        private static PanelSettings panel;
        private static RenderTexture target;
        private static float originalVolume, originalMusic, originalSensitivity, originalCrosshair;
        private static CursorLockMode originalLock;
        private static bool originalCursor, saved, capture;
        private static int resolution, phase, waitingFrames, preparedFrame;
        private static double begun;

        [MenuItem("IRONFRONT/Verify Lobby Loadout", true)]
        [MenuItem("IRONFRONT/Capture Lobby and Loadout", true)]
        private static bool CanRun() => EditorApplication.isPlaying && view == null;
        [MenuItem("IRONFRONT/Verify Lobby Loadout")]
        public static void Verify() => Begin(false);
        [MenuItem("IRONFRONT/Capture Lobby and Loadout")]
        public static void Capture() => Begin(true);

        private static void Begin(bool captureImages)
        {
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && PrototypeStartMenuView.AssetsAvailable, "actual runtime/menu assets exist");
                SaveState();
                game.Frontend?.Close();
                view = new PrototypeStartMenuView(game, "Operator preview", new PrototypeLoadout(), originalVolume);
                document = (UIDocument)Field(view, "document"); preview = (PrototypeMenuPreview)Field(view, "preview");
                Check(preview.HasBackdrop && preview.BackgroundTexture != null, "generated hangar is a separate loaded background");
                VisualElement root = document.rootVisualElement;
                Check(root.Q<Image>("hangar-image").image == preview.LobbyBackgroundTexture &&
                    root.Q<Image>("operator-image").image == preview.Texture,
                    "real UI uses separate background and transparent 3D foreground layers");
                VerifyCallbacks(root);
                VerifyForegroundAndThumbnails();
                panel = UnityEngine.Object.Instantiate(document.panelSettings);
                panel.name = "EDITOR ONLY lobby/loadout capture panel";
                panel.sortingOrder = 2000;
                document.panelSettings = panel;
                capture = captureImages; resolution = phase = 0;
                begun = EditorApplication.timeSinceStartup;
                PreparePhase();
                EditorApplication.update -= Frame; EditorApplication.update += Frame;
                Debug.Log("IRONFRONT lobby/loadout verification prepared: actual callbacks, independent render layers, isolated operator rig; UI layout capture pending");
            }
            catch (Exception error) { Fail(error); }
        }

        private static void VerifyCallbacks(VisualElement root)
        {
            Click(root, "tab-loadout");
            foreach (PrototypeInfantryClass role in Enum.GetValues(typeof(PrototypeInfantryClass)))
            {
                Click(root, "class-" + RoleKeys[(int)role]); Click(root, "slot-primary");
                IReadOnlyList<PrototypeWeaponId> allowed = PrototypeInfantryRoles.AllowedPrimaryWeapons(role);
                Check(allowed.Count == 2, "class exposes its two primary options");
                for (int id = 0; id < WeaponKeys.Length; id++)
                    Check(OptionVisible(root, id) ==
                        PrototypeInfantryRoles.IsPrimaryWeaponAllowed(role, (PrototypeWeaponId)id),
                        "actual primary cards match class policy for " + role + "/" + WeaponKeys[id]);
                Click(root, "weapon-" + WeaponKeys[(int)allowed[1]]);
                Check(view.Loadout.primary == allowed[1] && preview.ShownClass == role && preview.ShownWeapon == allowed[1],
                    "actual class/weapon click changes inventory choice and the real held model");
                Check(root.Q<Label>("selection-title").text.Contains("主武器") &&
                    root.Q<Label>("stat-heading").text.Contains("PRIMARY"), "primary statistics identify their slot");
                PrototypeWeaponDefinition weapon = PrototypeInfantryRoles.GetWeapon(allowed[1]);
                Check(root.Q<Label>("stat-damage").text.Contains(weapon.Damage.ToString("0")) &&
                    root.Q<Label>("stat-magazine").text.Contains(weapon.Magazine.ToString()), "displayed weapon stats use selected definition");
                for (int id = 0; id < WeaponKeys.Length; id++)
                    if (!PrototypeInfantryRoles.IsPrimaryWeaponAllowed(role, (PrototypeWeaponId)id))
                    {
                        Click(root, "weapon-" + WeaponKeys[id]);
                        Check(view.Loadout.primary == allowed[1], "hidden forbidden option callback cannot bypass class policy");
                    }
                Click(root, "slot-secondary");
                for (int id = 0; id < WeaponKeys.Length; id++)
                    Check(OptionVisible(root, id) ==
                        (id == (int)PrototypeWeaponId.Pistol), "secondary UI exposes only the pistol");
                Click(root, "weapon-pistol");
                Check(view.Loadout.secondary == PrototypeWeaponId.Pistol && preview.ShownWeapon == PrototypeWeaponId.Pistol &&
                    root.Q<Label>("selection-title").text.Contains("副武器") &&
                    root.Q<Label>("stat-heading").text.Contains("SECONDARY"), "pistol slot switches real preview and identified statistics");
                Check(PrototypeLoadout.Load().role == role && PrototypeLoadout.Load().secondary == PrototypeWeaponId.Pistol,
                    "actual UI callbacks save the validated loadout");
            }
            Click(root, "slot-throwable"); Click(root, "throwable-smoke");
            Check(view.Loadout.throwable == PrototypeThrowableId.Smoke, "real throwable selection remains functional");
            Click(root, "browse-operation");
            foreach (PrototypeGameMode mode in new[] { PrototypeGameMode.Conquest, PrototypeGameMode.Breakthrough })
            {
                ClickModeCard(root, mode);
                Check(view.SelectedMode == mode && PlayerPrefs.GetInt(Preferences[1]) == (int)mode &&
                    root.Q<VisualElement>("mode-conquest").ClassListContains("selected") == (mode == PrototypeGameMode.Conquest) &&
                    root.Q<VisualElement>("mode-breakthrough").ClassListContains("selected") == (mode == PrototypeGameMode.Breakthrough),
                    "actual operation card ClickEvent selects and saves " + mode);
                Check(root.Q<Label>("operation-summary").text.Contains(mode == PrototypeGameMode.Conquest ? "征服" : "攻防"),
                    "lobby operation summary follows the actual selected mode");
            }
            Click(root, "size-8v8");
            Check(view.SelectedTeamSize == 8, "actual team-size click retains the supported 8v8 match");
            ClickModeCard(root, soloMode == 1 ? PrototypeGameMode.Breakthrough : PrototypeGameMode.Conquest);
            Click(root, "tab-settings");
            root.Q<Slider>("volume-slider").value = .35f;
            root.Q<Slider>("music-slider").value = .40f;
            root.Q<Slider>("crosshair-slider").value = 1.25f;
            Check(root.Q<Label>("volume-value").text == "35%" && Mathf.Abs(AudioListener.volume - .35f) < .001f &&
                Mathf.Abs(PlayerPrefs.GetFloat(Preferences[2]) - .35f) < .001f, "master percentage label and actual saved callback");
            Check(root.Q<Label>("music-value").text == "40%" && Mathf.Abs(game.Music.MusicVolume - .4f) < .001f &&
                Mathf.Abs(PlayerPrefs.GetFloat(Preferences[3]) - .4f) < .001f, "music percentage label and actual saved callback");
            Check(root.Q<Label>("crosshair-value").text == "125%" && game.CrosshairSize == 1.25f &&
                PlayerPrefs.GetFloat(Preferences[5]) == 1.25f, "crosshair percentage label and actual saved callback");
            Click(root, "tab-lobby");
            Check(preview.ShownWeapon == view.Loadout.primary && !preview.IsLoadoutComposition,
                "lobby returns to the actual selected primary and central composition");
            PrototypeInfantryClass before = game.Player.Class;
            Click(root, "solo-button");
            Check(view.DeployRequested && game.Player.Class == before, "start button requests deployment without mutating the scene fixture");
        }

        private static void VerifyForegroundAndThumbnails()
        {
            preview.SetOperator(PrototypeInfantryClass.Assault, PrototypeWeaponId.Carbine);
            preview.SetComposition(false);
            Check(Mathf.Abs(preview.OperatorViewportPoint.x - .56f) < .01f,
                "lobby operator is centered using the real camera right axis");
            preview.SetComposition(true);
            Check(Mathf.Abs(preview.OperatorViewportPoint.x - .63f) < .01f,
                "loadout operator moves to the actual right-hand side of the viewport");
            preview.SetComposition(false);
            Check(preview.Operator.GetComponent<PrototypeSoldierVisual>() != null,
                "foreground is the real articulated infantry geometry");
            Transform torso = preview.Operator.transform.Find("Armored infantry visual/Pelvis/Torso");
            PrototypeSoldierVisual rig = preview.Operator.GetComponent<PrototypeSoldierVisual>();
            Transform gun = rig.HeldWeapon.transform;
            var weaponArt=gun.GetComponent<PrototypeWeaponArtRig>();
            Vector3 support=weaponArt!=null?weaponArt.SupportPosition:gun.TransformPoint(new Vector3(0f,-.16f,.37f));
            Check(Vector3.Distance(torso.Find("Left arm/Elbow/Hand").position, support) < .02f &&
                Vector3.Distance(torso.Find("Right arm/Elbow/Hand").position, gun.TransformPoint(new Vector3(0f, -.18f, -.18f))) < .02f,
                "actual hands hold the current weapon grips");
            Texture2D foreground = Read((RenderTexture)preview.Texture);
            try
            {
                Color32[] pixels = foreground.GetPixels32();
                Check(pixels[0].a == 0 && pixels[pixels.Length - 1].a == 0,
                    "foreground leaves the hangar visible through clear alpha corners");
                int opaque = 0; foreach (Color32 pixel in pixels) if (pixel.a > 100) opaque++;
                Check(opaque > 10000 && opaque < pixels.Length / 3, "real operator has visible geometry without covering the environment");
            }
            finally { UnityEngine.Object.DestroyImmediate(foreground); }
            foreach (PrototypeWeaponId id in Enum.GetValues(typeof(PrototypeWeaponId)))
            {
                Texture2D thumbnail = preview.WeaponThumbnail(id);
                Check(thumbnail != null && thumbnail.width == 640 && thumbnail.height == 304 &&
                    thumbnail == preview.WeaponThumbnail(id), "actual weapon thumbnail is cached at the expected aspect");
                Color32[] pixels = thumbnail.GetPixels32(); int visible = 0;
                foreach (Color32 pixel in pixels) if (pixel.a > 100) visible++;
                Check(pixels[0].a == 0 && visible > 500 && visible < pixels.Length * .8f,
                    id + " renders real isolated gun geometry on clear alpha");
            }
        }

        private static void PreparePhase()
        {
            int width = resolution == 0 ? 960 : 1280;
            int height = resolution == 0 ? 600 : resolution == 1 ? 720 : 800;
            if (phase == 0)
            {
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.Create();
                panel.targetTexture = target;
            }
            VisualElement root = document.rootVisualElement;
            if (phase == 0) { Click(root, "class-assault"); Click(root, "slot-primary"); Click(root, "tab-lobby"); }
            else if (phase <= 4)
            {
                Click(root, "tab-loadout"); Click(root, "class-" + RoleKeys[phase - 1]); Click(root, "slot-primary");
                PrototypeWeaponId selected = PrototypeInfantryRoles.AllowedPrimaryWeapons((PrototypeInfantryClass)(phase - 1))[1];
                Click(root, "weapon-" + WeaponKeys[(int)selected]);
            }
            else if (phase == 5) { Click(root, "tab-loadout"); Click(root, "slot-secondary"); }
            else if (phase == 6) { Click(root, "tab-settings"); }
            else { Click(root, "browse-operation"); ClickModeCard(root, PrototypeGameMode.Conquest); Click(root, "size-8v8"); }
            preview.Render(); waitingFrames = 0; preparedFrame = Time.frameCount;
        }

        private static void Frame()
        {
            try
            {
                if (!EditorApplication.isPlaying) { End(); return; }
                if (EditorApplication.timeSinceStartup - begun > 75d) throw new InvalidOperationException("UI layout/capture timed out");
                if (++waitingFrames < 8 || Time.frameCount < preparedFrame + 3) return;
                ValidateLayout();
                if (capture) WriteCapture();
                phase++;
                if (phase > 7) { phase = 0; resolution++; }
                if (resolution >= (capture ? 3 : 1))
                {
                    string message = capture ? "IRONFRONT lobby/loadout actual UITK capture PASS: " : "IRONFRONT lobby/loadout PASS: ";
                    End();
                    Debug.Log(message + "actual UI callbacks, class weapon choices, primary/pistol previews, original three-dimensional geometry/transparent layers, " +
                        "live percentage preferences, resolved class/weapon/settings/operation cards and unobstructed deployment; preferences restored");
                    return;
                }
                PreparePhase();
            }
            catch (Exception error)
            {
                if (target != null && view != null)
                    try { WriteCapture(); } catch { /* Preserve the original layout error. */ }
                Fail(error);
            }
        }

        private static void ValidateLayout()
        {
            VisualElement root = document.rootVisualElement;
            if (phase >= 1 && phase <= 5)
            {
                Rect bounds = root.Q<VisualElement>("screen").worldBound;
                Check(Within(root.Q<VisualElement>("class-rail").worldBound, bounds) &&
                    Within(root.Q<VisualElement>("loadout-workbench").worldBound, bounds), "B class rail and bottom cards fit inside the panel");
                Check(Within(root.Q<VisualElement>("loadout-picker-panel").worldBound, bounds), "Actual equipment picker fits panel");
                Button done = root.Q<Button>("loadout-picker-done");
                Check(IsDescendant(root.panel.Pick(done.worldBound.center), done), "Actual picker done action receives picking");
                return;
            }
            Camera operatorCamera = (Camera)Field(preview, "camera");
            Image foreground = root.Q<Image>("operator-image");
            Check(foreground.scaleMode == ScaleMode.ScaleToFit, "actual operator image preserves the character's natural aspect");
            Rect box = foreground.worldBound;
            float fit = Mathf.Min(box.width / preview.Texture.width, box.height / preview.Texture.height);
            Rect fitted = new Rect(box.center.x - preview.Texture.width * fit * .5f,
                box.center.y - preview.Texture.height * fit * .5f, preview.Texture.width * fit, preview.Texture.height * fit);
            foreach (Renderer renderer in preview.Operator.GetComponentsInChildren<Renderer>())
            {
                if (renderer.name != "Helmet shell" && renderer.name != "Boot") continue;
                Bounds bounds = renderer.bounds;
                Vector2 guiMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 guiMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 world = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 point = operatorCamera.WorldToViewportPoint(world);
                    Check(point.z > 0f && point.x > .01f && point.x < .99f && point.y > .01f && point.y < .99f,
                        "real character helmet and both boots remain inside the transparent camera frame");
                    Vector2 gui = new Vector2(fitted.xMin + point.x * fitted.width, fitted.yMax - point.y * fitted.height);
                    guiMin = Vector2.Min(guiMin, gui); guiMax = Vector2.Max(guiMax, gui);
                }
                Rect actualImageBounds = Rect.MinMaxRect(guiMin.x, guiMin.y, guiMax.x, guiMax.y);
                Check(Within(actualImageBounds, box), "actual fitted UI image keeps the head/boots on screen");
                if (renderer.name == "Boot")
                    Check(!actualImageBounds.Overlaps(root.Q<Button>("solo-button").worldBound),
                        "actual fitted character boot is not covered by the start button");
            }
            VisualElement screen = root.Q<VisualElement>("screen");
            Rect screenBounds = screen.worldBound;
            Check(screenBounds.width > 500f && screenBounds.height > 400f, "actual UI panel layout resolved");
            Button start = root.Q<Button>("solo-button"); Rect startBounds = start.worldBound;
            Check(startBounds.width > 120f && startBounds.height > 35f && Within(startBounds, screenBounds), "start button is fully inside the panel");
            VisualElement picked = root.panel.Pick(startBounds.center);
            Check(IsDescendant(picked, start), "actual picking reaches the start button without a covering card/image");
            if (phase >= 1 && phase <= 5)
            {
                Rect rail = root.Q<VisualElement>("class-rail").worldBound;
                Rect bench = root.Q<VisualElement>("loadout-workbench").worldBound;
                Check(Within(rail, screenBounds) && Within(bench, screenBounds) && !rail.Overlaps(bench),
                    "class rail and loadout workbench are separate and inside the screen");
                Check(!bench.Overlaps(root.Q<VisualElement>("deployment-actions").worldBound),
                    "loadout cards stay clear of deployment actions");
                Rect primary = root.Q<Button>("slot-primary").worldBound;
                Rect secondary = root.Q<Button>("slot-secondary").worldBound;
                Check(primary.width > 90f && secondary.width > 70f && !primary.Overlaps(secondary), "actual primary/secondary cards are legible and distinct");
            }
            else if (phase == 6)
            {
                var rows = new[] { root.Q<VisualElement>(className: "audio-card"), root.Q<VisualElement>(className: "music-card"),
                    root.Q<VisualElement>(className: "sensitivity-card"), root.Q<VisualElement>(className: "crosshair-card"), root.Q<VisualElement>(className: "controls-card") };
                for (int i = 0; i < rows.Length; i++)
                {
                    Check(Within(rows[i].worldBound, screenBounds) && rows[i].worldBound.height > 35f &&
                        !rows[i].worldBound.Overlaps(startBounds), "actual settings card stays visible and clear of start action");
                    if (i > 0) Check(rows[i].worldBound.yMin >= rows[i - 1].worldBound.yMax - .1f, "actual settings rows do not overlap");
                }
            }
            else if (phase == 7)
            {
                var cards = new[] { root.Q<VisualElement>(className: "map-card"), root.Q<VisualElement>("mode-conquest"),
                    root.Q<VisualElement>("mode-breakthrough"), root.Q<Button>("size-8v8") };
                for (int i = 0; i < cards.Length; i++)
                {
                    Check(cards[i] != null && cards[i].worldBound.width > 100f && cards[i].worldBound.height > 30f &&
                        Within(cards[i].worldBound, screenBounds) && !cards[i].worldBound.Overlaps(startBounds),
                        "actual map/mode/team-size card is visible and clear of the start action");
                    for (int j = 0; j < i; j++)
                        Check(!cards[i].worldBound.Overlaps(cards[j].worldBound),
                            "actual map, mode and team-size cards do not overlap");
                }
                Image map = root.Q<Image>("map-thumb");
                Check(map.image != null && map.worldBound.width > 100f && map.worldBound.height > 50f &&
                    Within(map.worldBound, cards[0].worldBound), "actual tactical map remains inside its operation card");
                Check(view.SelectedMode == PrototypeGameMode.Conquest && view.SelectedTeamSize == 8 &&
                    PlayerPrefs.GetInt(Preferences[1]) == (int)view.SelectedMode,
                    "captured operation page retains the actual saved mode and supported team size");
            }
        }

        private static void WriteCapture()
        {
            Texture2D image = Read(target);
            try
            {
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Verification053")); Directory.CreateDirectory(directory);
                string name = phase == 0 ? "lobby" : phase <= 4 ? "loadout-" + RoleKeys[phase - 1] : phase == 5 ? "loadout-pistol" : phase == 6 ? "settings" : "operations";
                string path = Path.Combine(directory, name + "-" + target.width + "x" + target.height + ".png");
                int bright = 0; foreach (Color32 pixel in image.GetPixels32())
                    if (pixel.a > 0 && (pixel.r > 60 || pixel.g > 60 || pixel.b > 60)) bright++;
                Check(bright > 5000, "the actual UI capture contains visible rendered background/menu content");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("IRONFRONT lobby/loadout actual capture: " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
        private static Texture2D Read(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0); image.Apply(); return image;
            }
            finally { RenderTexture.active = previous; }
        }
        private static bool Within(Rect child, Rect parent) => child.xMin >= parent.xMin - 1f && child.yMin >= parent.yMin - 1f &&
            child.xMax <= parent.xMax + 1f && child.yMax <= parent.yMax + 1f;
        private static bool IsDescendant(VisualElement element, VisualElement ancestor)
        { for (; element != null; element = element.parent) if (element == ancestor) return true; return false; }
        private static object Field(object instance, string name) => instance.GetType().GetField(name, Flags).GetValue(instance);
        private static void Click(VisualElement root, string name)
        {
            Button button = root.Q<Button>(name);
            // Forbidden options may be removed from the live tree. Exercise
            // their registered callbacks as well to verify the handler guard.
            if (button == null && name.StartsWith("weapon-", StringComparison.Ordinal))
            {
                int id = Array.IndexOf(WeaponKeys, name.Substring("weapon-".Length));
                if (id >= 0) button = ((Button[])Field(view, "weaponButtons"))[id];
            }
            Check(button != null, "actual UI button exists: " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }
        private static void ClickModeCard(VisualElement root, PrototypeGameMode mode)
        {
            VisualElement card = root.Q<VisualElement>(mode == PrototypeGameMode.Conquest ? "mode-conquest" : "mode-breakthrough");
            Check(card != null && card.panel != null, "actual operation card is attached to the UI panel");
            using (ClickEvent click = ClickEvent.GetPooled())
            {
                click.target = card;
                card.SendEvent(click);
            }
        }
        private static bool OptionVisible(VisualElement root, int id)
        {
            Button option = root.Q<Button>("weapon-" + WeaponKeys[id]);
            return option != null && option.style.display.value != DisplayStyle.None;
        }
        private static void SaveState()
        {
            originalVolume = AudioListener.volume; originalMusic = game.Music.MusicVolume;
            originalSensitivity = game.MouseSensitivity; originalCrosshair = game.CrosshairSize;
            originalLock = Cursor.lockState; originalCursor = Cursor.visible;
            for (int i = 0; i < Preferences.Length; i++)
            { had[i] = PlayerPrefs.HasKey(Preferences[i]); if (i >= 2) floats[i] = PlayerPrefs.GetFloat(Preferences[i]); }
            loadoutJson = PlayerPrefs.GetString(Preferences[0], ""); soloMode = PlayerPrefs.GetInt(Preferences[1]); saved = true;
        }
        private static void End()
        {
            EditorApplication.update -= Frame;
            view?.Dispose(); view = null; preview = null; document = null;
            if (panel != null) UnityEngine.Object.DestroyImmediate(panel); panel = null;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); } target = null;
            if (saved)
            {
                AudioListener.volume = originalVolume; game.Music.SetMusicVolume(originalMusic);
                game.SetMouseSensitivity(originalSensitivity); game.SetCrosshairSize(originalCrosshair);
                for (int i = 0; i < Preferences.Length; i++)
                    if (!had[i]) PlayerPrefs.DeleteKey(Preferences[i]);
                    else if (i == 0) PlayerPrefs.SetString(Preferences[i], loadoutJson);
                    else if (i == 1) PlayerPrefs.SetInt(Preferences[i], soloMode);
                    else PlayerPrefs.SetFloat(Preferences[i], floats[i]);
                PlayerPrefs.Save(); Cursor.lockState = originalLock; Cursor.visible = originalCursor;
            }
            saved = false; game = null;
        }
        private static void Fail(Exception error) { End(); Debug.LogError("IRONFRONT lobby/loadout FAIL: " + error.GetBaseException()); }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
