#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ironfront.UnityPrototype
{
    public sealed class PrototypeLobbyProbe : MonoBehaviour
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private string output;
        private int errors;
        private readonly List<string> checks = new List<string>();
        private readonly List<string> layout = new List<string> { "screen,element,x,y,width,height" };
        private PrototypeRuntime game;
        private PrototypeStartMenuView view;
        private PrototypeMenuPreview preview;
        private string oldLoadout, oldName;
        private int oldMode;
        private bool hadLoadout, hadName, hadMode;
        private float originalVolume, originalMusic, originalSensitivity, originalCrosshair;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-ironfrontLobbyProbe")
                    new GameObject("Lobby verification only").AddComponent<PrototypeLobbyProbe>().output = Path.GetFullPath(args[i + 1]);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.runInBackground = true;
            hadLoadout = PlayerPrefs.HasKey("ironfront.unity.loadout"); oldLoadout = PlayerPrefs.GetString("ironfront.unity.loadout");
            hadName = PlayerPrefs.HasKey("ironfront.unity.playerName"); oldName = PlayerPrefs.GetString("ironfront.unity.playerName");
            hadMode = PlayerPrefs.HasKey("ironfront.unity.soloMode"); oldMode = PlayerPrefs.GetInt("ironfront.unity.soloMode");
            Application.logMessageReceived += Observe;
            IEnumerator work = Verify(); Exception failure = null;
            while (true)
            {
                bool more = false; object current = null;
                try { more = work.MoveNext(); if (more) current = work.Current; } catch (Exception error) { failure = error; }
                if (!more || failure != null) break;
                yield return current;
            }
            RestoreString("ironfront.unity.loadout", hadLoadout, oldLoadout);
            RestoreString("ironfront.unity.playerName", hadName, oldName);
            if (hadMode) PlayerPrefs.SetInt("ironfront.unity.soloMode", oldMode); else PlayerPrefs.DeleteKey("ironfront.unity.soloMode");
            PlayerPrefs.Save();
            bool pass = failure == null && errors == 0;
            File.WriteAllText(Path.Combine(output, "results.txt"), (pass ? "PASS" : "FAIL") + "\nVersion=" + Application.version +
                "\nRuntimeErrors=" + errors + "\n" + string.Join("\n", checks) + (failure == null ? "" : "\n" + failure));
            File.WriteAllLines(Path.Combine(output, "layout.csv"), layout);
            Debug.Log("IRONFRONT lobby upgrade " + (pass ? "PASS" : "FAIL")); Application.logMessageReceived -= Observe;
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(pass ? 0 : 1);
            #else
            Application.Quit(pass ? 0 : 1);
            #endif
        }

        private IEnumerator Verify()
        {
            for (int i = 0; i < 600; i++)
            { game = FindAnyObjectByType<PrototypeRuntime>(); if (game != null && game.Frontend != null) break; yield return null; }
            Check(game != null && !game.MatchStarted, "Real application opens to lobby before battle");
            Acquire();
            originalVolume = AudioListener.volume; originalMusic = game.Music.MusicVolume;
            originalSensitivity = game.MouseSensitivity; originalCrosshair = game.CrosshairSize;
            int[,] sizes = { { 960, 600 }, { 1280, 800 }, { 1600, 900 }, { 1920, 1080 }, { 2560, 1080 } };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                if (!Application.isBatchMode) Screen.SetResolution(sizes[size, 0], sizes[size, 1], false);
                for (int frame = 0; frame < 20; frame++) yield return null;
                Capture("lobby-" + Screen.width + "x" + Screen.height);
                yield return null; yield return null;
                ValidateLobby();
            }
            if (!Application.isBatchMode) Screen.SetResolution(1600, 900, false);
            for (int frame = 0; frame < 15; frame++) yield return null;
            SaveTexture((RenderTexture)preview.Portrait, "portrait.png");
            SaveTexture((RenderTexture)preview.Texture, "operator.png");
            Root.Q<TextField>("player-name").value = "铁锋测试 PLAYER 0611";
            Check(view.PlayerName == "铁锋测试 PLAYER 0611", "Calling card is bound to the actual editable player name");
            for (int i = 0; i < 6; i++) yield return null;
            Capture("calling-card-cjk"); yield return null;
            Root.Q<TextField>("player-name").value = game.Frontend.PlayerName;

            Click("browse-operation");
            for (int i = 0; i < 8; i++) yield return null;
            Check(Visible(Root.Q<VisualElement>("page-operations")), "Right mission action opens existing operation selector");
            ClickMode("mode-breakthrough");
            Check(view.SelectedMode == PrototypeGameMode.Breakthrough, "Breakthrough selection uses original mode callback");
            Capture("operations-existing"); yield return null;
            Click("operation-back");
            for (int i = 0; i < 8; i++) yield return null;
            Check(Root.Q<Label>("operation-summary").text.Contains("攻防"), "Return to lobby updates mission summary");
            ValidateLobby();

            Click("tab-loadout"); Click("class-assault"); Click("slot-primary"); Click("weapon-carbine"); Click("loadout-picker-done");
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                if (!Application.isBatchMode) Screen.SetResolution(sizes[size, 0], sizes[size, 1], false);
                for (int frame = 0; frame < 20; frame++) yield return null;
                Capture("loadout-" + Screen.width + "x" + Screen.height); yield return null;
                ValidateLoadout();
            }
            if (!Application.isBatchMode) Screen.SetResolution(1600, 900, false);
            string[] roles = { "assault", "medic", "recon", "engineer" };
            string[] weapons = { "carbine", "marksman", "smg", "lmg", "battlerifle", "sniper", "pistol" };
            foreach (PrototypeInfantryClass role in Enum.GetValues(typeof(PrototypeInfantryClass)))
            {
                Click("class-" + roles[(int)role]);
                for (int frame = 0; frame < 10; frame++) yield return null;
                ValidateLoadout(); Capture("loadout-" + roles[(int)role]); yield return null;
                Click("slot-primary");
                for (int frame = 0; frame < 6; frame++) yield return null;
                Check(Visible(Root.Q<VisualElement>("loadout-picker")), "Primary card opens selection panel");
                Check(!Root.Q<Button>("slot-primary").enabledInHierarchy && !Root.Q<Button>("loadout-complete").enabledInHierarchy,
                    "Modal blocks keyboard and pointer interaction with underlying equipment/footer");
                Check(Within(Root.Q<VisualElement>("loadout-picker-panel").worldBound, Root.Q<VisualElement>("screen").worldBound), "Picker panel stays on screen");
                foreach (PrototypeWeaponId id in Enum.GetValues(typeof(PrototypeWeaponId)))
                {
                    Button option = Root.Q<Button>("weapon-" + weapons[(int)id]);
                    Check(Visible(option) == PrototypeInfantryRoles.IsPrimaryWeaponAllowed(role, id), role + " shows only allowed primary weapons");
                }
                foreach (PrototypeWeaponId id in PrototypeInfantryRoles.AllowedPrimaryWeapons(role))
                {
                    Click("weapon-" + weapons[(int)id]);
                    Check(view.Loadout.primary == id && preview.ShownWeapon == id, "Equipment click updates actual loadout and held weapon: " + role + "/" + id);
                    Check(PrototypeLoadout.Load().primary == id, "Actual saved selection follows weapon callback");
                    Check(Root.Q<Label>("primary-stat-damage").text == PrototypeInfantryRoles.GetWeapon(id).Damage.ToString("0"), "Overview stats follow primary definition");
                }
                for (int frame = 0; frame < 6; frame++) yield return null;
                Capture("picker-" + roles[(int)role]); yield return null;
                view.HandleLoadoutBack();
                for (int frame = 0; frame < 5; frame++) yield return null;
                Check(!Visible(Root.Q<VisualElement>("loadout-picker")) && Visible(Root.Q<VisualElement>("page-loadout")), "Escape closes picker before leaving loadout");
                Click("slot-secondary");
                for (int frame = 0; frame < 5; frame++) yield return null;
                Check(Visible(Root.Q<Button>("weapon-pistol")) && !Visible(Root.Q<Button>("weapon-carbine")), "Secondary only exposes P8");
                Click("weapon-pistol");
                Check(preview.ShownWeapon == PrototypeWeaponId.Pistol && Root.Q<Label>("stat-damage").text == "29", "P8 model and secondary stats update together");
                Check(Root.Q<Label>("primary-stat-damage").text == PrototypeInfantryRoles.GetWeapon(view.Loadout.primary).Damage.ToString("0"), "Secondary inspection leaves overview primary stats unchanged");
                view.HandleLoadoutAccept();
                Click("slot-gadget");
                for (int frame = 0; frame < 5; frame++) yield return null;
                Check(Visible(Root.Q<Label>("gadget-info")) && !Visible(Root.Q<VisualElement>("weapon-options")), "Class equipment opens the correct explanatory panel");
                Capture("gadget-" + roles[(int)role]); yield return null;
                Click("loadout-picker-close");
            }
            Click("slot-throwable");
            for (int frame = 0; frame < 5; frame++) yield return null;
            Click("throwable-smoke");
            Check(view.Loadout.throwable == PrototypeThrowableId.Smoke && Root.Q<Label>("throwable-name").text == "烟雾弹", "Smoke selection updates saved loadout and bottom card");
            for (int frame = 0; frame < 5; frame++) yield return null;
            Capture("picker-throwable"); yield return null;
            Click("throwable-frag"); Click("loadout-picker-done");
            Click("loadout-complete");
            for (int frame = 0; frame < 8; frame++) yield return null;
            Check(Visible(Root.Q<VisualElement>("page-lobby")) && !view.DeployRequested && !game.MatchStarted,
                "Complete saves and returns to lobby without starting the game");
            Check(preview.ShownClass == PrototypeInfantryClass.Engineer && preview.ShownWeapon == view.Loadout.primary,
                "Lobby carries selected class and actual primary weapon");
            ValidateLobby();
            Click("tab-loadout");
            for (int frame = 0; frame < 5; frame++) yield return null;
            view.HandleLoadoutBack();
            Check(!view.DeployRequested && !game.MatchStarted, "Overview Escape returns without deployment");
            Click("tab-loadout"); view.HandleLoadoutAccept();
            Check(!view.DeployRequested && !game.MatchStarted, "Overview Enter completes without deployment");
            Click("tab-loadout"); Click("class-assault"); Click("slot-primary"); Click("weapon-carbine"); Click("loadout-picker-done");
            Click("loadout-complete"); Click("tab-settings");
            for (int i = 0; i < 8; i++) yield return null;
            Check(Visible(Root.Q<VisualElement>("page-settings")) && Root.Q<Slider>("volume-slider") != null,
                "Original settings controls remain visible");
            Check(Mathf.Approximately(AudioListener.volume, originalVolume) && Mathf.Approximately(game.Music.MusicVolume, originalMusic) &&
                Mathf.Approximately(game.MouseSensitivity, originalSensitivity) && Mathf.Approximately(game.CrosshairSize, originalCrosshair),
                "Page navigation does not alter audio, sensitivity or crosshair preferences");
            Capture("settings-existing"); yield return null;
            Click("tab-lobby");
            for (int i = 0; i < 8; i++) yield return null;
            Click("lan-button");
            for (int i = 0; i < 8; i++) yield return null;
            if (!Application.isBatchMode)
                Check((bool)Field(game, "roomMenuOpen"), "Real LAN button enters original room screen through frontend lifecycle");
            else Check(view.LanRequested, "LAN callback requests original room screen");
            Capture("lan-existing"); yield return null;
            typeof(PrototypeRuntime).GetField("roomMenuOpen", Fields).SetValue(game, false);
            game.Frontend.Open(); Acquire();
            for (int i = 0; i < 8; i++) yield return null;
            Click("browse-operation"); ClickMode("mode-conquest"); Click("operation-back");
            for (int i = 0; i < 8; i++) yield return null;
            Click("solo-button");
            for (int i = 0; i < 8; i++) yield return null;
            if (!Application.isBatchMode)
                Check(game.MatchStarted && !game.Frontend.IsOpen && game.SoloTeamSize == 8,
                    "Real start button enters original 8v8 solo match and closes all lobby layers");
            else Check(view.DeployRequested, "Start callback requests original match lifecycle");
            Capture("solo-existing"); yield return null; yield return null;
        }

        private VisualElement Root => view.Root;
        private void Acquire()
        {
            view = (PrototypeStartMenuView)Field(game.Frontend, "startMenu");
            Check(view != null, "Actual UI Toolkit menu initializes without fallback");
            preview = (PrototypeMenuPreview)Field(view, "preview");
        }
        private void ValidateLobby()
        {
            VisualElement root = Root, screen = root.Q<VisualElement>("screen"); Rect bounds = screen.worldBound;
            ValidateTypography();
            Check(root.Q<Button>("tab-operations") == null && root.Q<Button>("tab-lobby") != null &&
                root.Q<Button>("tab-loadout") != null && root.Q<Button>("tab-settings") != null, "Top navigation has only lobby/loadout/settings");
            Check(!Visible(root.Q<VisualElement>(className: "operator-caption")) && !Visible(root.Q<Label>("squad-class")),
                "Lobby displays no class caption or class selection");
            VisualElement mission = root.Q<VisualElement>(className: "lobby-mission-card");
            VisualElement card = root.Q<VisualElement>("identity-card");
            Button start = root.Q<Button>("solo-button"), lan = root.Q<Button>("lan-button");
            Check(mission.worldBound.xMin > bounds.width * .64f && Mathf.Abs(mission.worldBound.xMin - start.worldBound.xMin) < 2f &&
                start.worldBound.yMin >= mission.worldBound.yMax && lan.worldBound.yMin >= start.worldBound.yMax,
                "Mission, start and LAN form one aligned right column");
            Check(card.worldBound.xMax < bounds.width * .4f && card.worldBound.yMin > bounds.height * .72f,
                "Avatar/name/calling-card sits at bottom left");
            Check(root.Q<Image>("player-avatar").image == preview.Portrait && Visible(root.Q<Image>("player-avatar")),
                "Calling card shows actual model portrait");
            Check(root.Q<Image>("hangar-image").image == preview.LobbyBackgroundTexture && preview.LobbyBackgroundTexture != preview.BackgroundTexture,
                "Lobby background is independently replaceable; original pages keep old background");
            Check(Mathf.Abs(preview.OperatorViewportPoint.x - .5f) < .012f && !preview.IsLoadoutComposition,
                "Actual soldier is centered at 50 percent of viewport");
            foreach (string id in new[] { "tab-lobby", "tab-loadout", "tab-settings", "browse-operation", "solo-button", "lan-button", "identity-card", "player-name" })
            {
                VisualElement element = root.Q<VisualElement>(id); Rect box = element.worldBound;
                Check(Within(box, bounds), id + " stays within " + Screen.width + "x" + Screen.height);
                VisualElement hit = root.panel.Pick(box.center);
                Check(hit != null && (hit == element || element.Contains(hit)), id + " has unobstructed actual picking");
                layout.Add(Screen.width + "x" + Screen.height + "," + id + "," + box.x.ToString("F2") + "," + box.y.ToString("F2") + "," + box.width.ToString("F2") + "," + box.height.ToString("F2"));
            }
            RenderTexture foreground = (RenderTexture)preview.Texture;
            Texture2D pixels = ReadTexture(foreground); int left = pixels.width, right = -1, top = -1, bottom = pixels.height;
            Color32[] rgba = pixels.GetPixels32();
            for (int y = 0; y < pixels.height; y++) for (int x = 0; x < pixels.width; x++)
                if (rgba[y * pixels.width + x].a > 160)
                { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
            Object.Destroy(pixels);
            Check(right > left && top > bottom, "Real model foreground contains opaque pixels");
            Rect image = root.Q<Image>("operator-image").worldBound;
            float scale = Mathf.Min(image.width / foreground.width, image.height / foreground.height);
            Rect fit = new Rect(image.center.x - foreground.width * scale / 2, image.center.y - foreground.height * scale / 2, foreground.width * scale, foreground.height * scale);
            Rect character = Rect.MinMaxRect(fit.xMin + left * scale, fit.yMax - top * scale, fit.xMin + right * scale, fit.yMax - bottom * scale);
            Check(Within(character, bounds) && character.yMin > root.Q<VisualElement>(className: "top-bar").worldBound.yMax &&
                !character.Overlaps(mission.worldBound) && !character.Overlaps(start.worldBound) && !character.Overlaps(card.worldBound),
                "Rendered silhouette stays clear of navigation, mission, play action and calling card");
        }
        private void ValidateLoadout()
        {
            Rect screenBounds = Root.Q<VisualElement>("screen").worldBound;
            Check(Visible(Root.Q<VisualElement>("page-loadout")) && preview.IsLoadoutComposition, "B overview and close operator composition are active");
            Check(!Visible(Root.Q<VisualElement>("deployment-actions")) && !Visible(Root.Q<VisualElement>("identity-card")), "Overview contains equipment actions only");
            CheckFont(Root.Q<Label>("loadout-class-title"), "LobbyHeavy");
            CheckFont(Root.Q<Label>("loadout-role-equipment"), "LobbyHeavy");
            CheckFont(Root.Q<Button>("loadout-complete"), "LobbyHeavy");
            CheckFont(Root.Q<Label>("primary-name"), "LobbyMedium");
            CheckFont(Root.Q<Label>("primary-stat-rate"), "LobbyRegular");
            Rect previous = Rect.zero;
            foreach (string key in new[] { "primary", "secondary", "gadget", "throwable" })
            {
                Button card = Root.Q<Button>("slot-" + key); Rect box = card.worldBound;
                Check(Within(box, screenBounds) && box.width > 150 && box.height > 125 && box.yMin > screenBounds.height * .55f,
                    "Equipment card is readable along bottom: " + key);
                Check(previous.width == 0 || (!box.Overlaps(previous) && Mathf.Abs(box.yMin - previous.yMin) < 1), "Equipment cards align without overlap");
                VisualElement picked = Root.panel.Pick(box.center);
                Check(picked == card || card.Contains(picked), "Actual picking reaches equipment card: " + key);
                Image icon = Root.Q<Image>("icon-" + key);
                Check(icon.image != null && icon.image.width >= 320 && Within(icon.worldBound, box), "Card uses high-resolution real equipment thumbnail: " + key);
                CheckFont(Root.Q<Label>(key + "-name"), "LobbyMedium");
                previous = box;
                layout.Add(Screen.width + "x" + Screen.height + ",slot-" + key + "," + box.x + "," + box.y + "," + box.width + "," + box.height);
            }
            Rect intro = Root.Q<VisualElement>("loadout-intro").worldBound;
            Check(Within(intro, screenBounds) && intro.yMax < previous.yMin - 4, "Class description stays clear of bottom equipment row");
            foreach (string id in new[] { "class-assault", "class-medic", "class-recon", "class-engineer", "loadout-back", "loadout-complete" })
            {
                VisualElement item = Root.Q<VisualElement>(id); VisualElement picked = Root.panel.Pick(item.worldBound.center);
                Check(Within(item.worldBound, screenBounds) && (picked == item || item.Contains(picked)), id + " is visible and receives actual picking");
            }
            Texture2D pixels = ReadTexture((RenderTexture)preview.Texture); int left = pixels.width, right = -1, top = -1;
            Color32[] rgba = pixels.GetPixels32();
            for (int y = pixels.height / 2; y < pixels.height; y++) for (int x = 0; x < pixels.width; x++)
                if (rgba[y * pixels.width + x].a > 160) { left = Mathf.Min(left, x); right = Mathf.Max(right, x); top = Mathf.Max(top, y); }
            Object.Destroy(pixels);
            Image image = Root.Q<Image>("operator-image");
            float scale = Mathf.Min(image.worldBound.width / preview.Texture.width, image.worldBound.height / preview.Texture.height);
            float imageLeft = image.worldBound.center.x - preview.Texture.width * scale / 2;
            float imageBottom = image.worldBound.center.y + preview.Texture.height * scale / 2;
            Check(right > left && imageLeft + left * scale > intro.xMax && imageBottom - top * scale > Root.Q<VisualElement>(className: "top-bar").worldBound.yMax,
                "Rendered upper body stays clear of class copy and top navigation");
        }

        private void ValidateTypography()
        {
            CheckFont(Root.Q<Label>(className: "hero-title"), "LobbyHeavy");
            CheckFont(Root.Q<Label>(className: "operation-title"), "LobbyHeavy");
            CheckFont(Root.Q<Button>("solo-button"), "LobbyHeavy");
            CheckFont(Root.Q<Label>(className: "brand-title"), "LobbyStencil");
            CheckFont(Root.Q<Button>("tab-lobby"), "LobbyMedium");
            CheckFont(Root.Q<Button>("browse-operation"), "LobbyMedium");
            CheckFont(Root.Q<Button>("lan-button"), "LobbyMedium");
            CheckFont(Root.Q<Label>("operation-summary"), "LobbyRegular");
            var nameFont = Root.Q<TextField>("player-name").resolvedStyle.unityFontDefinition.fontAsset;
            Check(nameFont != null && nameFont.name == "LobbyRegular" &&
                nameFont.TryAddCharacters("铁锋测试玩家龘", out string missing),
                "Editable Chinese player names use the embedded dynamic font");
        }
        private void CheckFont(TextElement text, string expected)
        {
            var font = text.resolvedStyle.unityFontDefinition.fontAsset;
            Check(font != null && font.name == expected && font.sourceFontFile != null,
                text.text + " resolves to embedded " + expected);
            // Unity's advanced text backend does not populate the legacy character table.
            // Test the embedded face directly; an empty runtime cache is not a missing glyph.
            Check(font.TryAddCharacters(text.text, out string missing), text.text + " has all glyphs in its embedded face: " + missing);
            Check(text.resolvedStyle.unityFontStyleAndWeight == FontStyle.Normal, "Actual font face is used without synthetic bold");
            Vector2 measured = text.MeasureTextSize(text.text, 0, VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined);
            Check(measured.x <= text.contentRect.width + 2f, text.text + " fits its text area at " + Screen.width + "x" + Screen.height);
        }
        private static bool Within(Rect a, Rect b) => a.width > 1 && a.height > 1 && a.xMin >= b.xMin - 1 && a.yMin >= b.yMin - 1 && a.xMax <= b.xMax + 1 && a.yMax <= b.yMax + 1;
        private static bool Visible(VisualElement element)
        {
            for (VisualElement item = element; item != null; item = item.parent)
                if (item.resolvedStyle.display == DisplayStyle.None || item.resolvedStyle.visibility == Visibility.Hidden) return false;
            return element != null;
        }
        private void Click(string id)
        {
            Button button = Root.Q<Button>(id); Check(button != null, "Button exists: " + id);
            typeof(Clickable).GetMethod("Invoke", Fields).Invoke(button.clickable, new object[] { null });
        }
        private void ClickMode(string id)
        {
            using (ClickEvent click = ClickEvent.GetPooled()) { click.target = Root.Q<VisualElement>(id); Root.Q<VisualElement>(id).SendEvent(click); }
        }
        private static object Field(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
        private void Capture(string name) { if (!Application.isBatchMode) ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); }
        private static Texture2D ReadTexture(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            try { RenderTexture.active = texture; var result = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false); result.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); result.Apply(); return result; }
            finally { RenderTexture.active = previous; }
        }
        private void SaveTexture(RenderTexture texture, string name) { Texture2D image = ReadTexture(texture); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); Object.Destroy(image); }
        private void Check(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); checks.Add("PASS: " + text); }
        private void Observe(string condition, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void RestoreString(string key, bool had, string value) { if (had) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
    }
}
#endif
