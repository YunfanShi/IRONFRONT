using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ironfront.UnityPrototype
{
    // UI Toolkit owns the start screen. Combat, room and respawn HUDs remain in IMGUI.
    public sealed class PrototypeStartMenuView : IDisposable
    {
        private static readonly string[] PageNames = { "lobby", "operations", "loadout", "settings" };
        private static readonly string[] ClassNames = { "突击兵", "医疗兵", "侦察兵", "工程兵" };
        private static readonly string[] ClassCodes = { "ASSAULT", "MEDIC", "RECON", "ENGINEER" };

        private readonly GameObject host;
        private readonly UIDocument document;
        private readonly PrototypeMenuPreview preview;
        private readonly Texture2D veil;
        private readonly Texture2D map;
        private readonly Button[] pageButtons = new Button[4];
        private readonly VisualElement[] pages = new VisualElement[4];
        private readonly Button[] classButtons = new Button[4];
        private readonly TextField nameField;
        private readonly Label classCaption;
        private readonly Label operatorTitle;
        private readonly Label squadClass;
        private readonly Slider volumeSlider;
        private readonly Label volumeValue;
        private readonly VisualElement screen;
        private int selectedPage;

        public string PlayerName => nameField.value;
        public PrototypeInfantryClass SelectedClass { get; private set; }
        public bool DeployRequested { get; private set; }
        public bool LanRequested { get; private set; }

        public static bool AssetsAvailable =>
            Resources.Load<VisualTreeAsset>("Menu/StartMenu") != null &&
            Resources.Load<PanelSettings>("Menu/StartMenuPanel") != null;

        public PrototypeStartMenuView(PrototypeRuntime runtime, string playerName,
            PrototypeInfantryClass soldierClass, float volume)
        {
            VisualTreeAsset tree = Resources.Load<VisualTreeAsset>("Menu/StartMenu");
            StyleSheet sheet = Resources.Load<StyleSheet>("Menu/StartMenuTheme");
            PanelSettings settings = Resources.Load<PanelSettings>("Menu/StartMenuPanel");
            if (tree == null || sheet == null || settings == null)
                throw new InvalidOperationException("The editable start menu UI assets are missing.");

            host = new GameObject("IRONFRONT Start Menu UI Toolkit");
            host.SetActive(false);
            document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = tree;
            host.SetActive(true);
            VisualElement root = document.rootVisualElement;
            root.styleSheets.Add(sheet);
            screen = Require<VisualElement>(root, "screen");

            preview = new PrototypeMenuPreview(runtime);
            Image hangar = Require<Image>(root, "hangar-image");
            hangar.image = preview.Texture;
            hangar.scaleMode = ScaleMode.StretchToFill;
            veil = MakeVeil();
            Require<VisualElement>(root, "scene-veil").style.backgroundImage =
                new StyleBackground(veil);

            map = PrototypeFrontend.BuildTacticalMapTexture();
            Image thumbnail = Require<Image>(root, "map-thumb");
            thumbnail.image = map;
            thumbnail.scaleMode = ScaleMode.StretchToFill;
            Require<Label>(root, "version").text = Application.version;

            for (int i = 0; i < PageNames.Length; i++)
            {
                int page = i;
                pageButtons[i] = Require<Button>(root, "tab-" + PageNames[i]);
                pages[i] = Require<VisualElement>(root, "page-" + PageNames[i]);
                pageButtons[i].clicked += () => SetPage(page);
            }
            Require<Button>(root, "browse-operation").clicked += () => SetPage(1);
            Require<Button>(root, "solo-button").clicked += () => DeployRequested = true;
            Require<Button>(root, "lan-button").clicked += RequestLan;
            nameField = Require<TextField>(root, "player-name");
            nameField.value = playerName;
            classCaption = Require<Label>(root, "operator-class");
            operatorTitle = Require<Label>(root, "operator-title");
            squadClass = Require<Label>(root, "squad-class");
            string[] roleKeys = { "assault", "medic", "recon", "engineer" };
            for (int i = 0; i < roleKeys.Length; i++)
            {
                int role = i;
                classButtons[i] = Require<Button>(root, "class-" + roleKeys[i]);
                classButtons[i].clicked += () => SetClass((PrototypeInfantryClass)role);
            }
            volumeSlider = Require<Slider>(root, "volume-slider");
            volumeValue = Require<Label>(root, "volume-value");
            volumeSlider.value = volume;
            volumeValue.text = Mathf.RoundToInt(volume * 100f) + "%";
            volumeSlider.RegisterValueChangedCallback(change =>
            {
                AudioListener.volume = change.newValue;
                volumeValue.text = Mathf.RoundToInt(change.newValue * 100f) + "%";
                PlayerPrefs.SetFloat("ironfront.unity.masterVolume", change.newValue);
                PlayerPrefs.Save();
            });
            host.AddComponent<PrototypeStartMenuInput>().View = this;
            SetClass(soldierClass);
            SetPage(0);
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T result = root.Q<T>(name);
            if (result == null) throw new InvalidOperationException("Start menu element missing: " + name);
            return result;
        }

        public void RequestLan()
        {
            LanRequested = true;
            screen.style.display = DisplayStyle.None;
            preview.SetVisible(false);
        }

        public void HandleKeyboard()
        {
            if (IsEditingName()) return;
            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.F1 + i))) SetPage(i);
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    SetClass((PrototypeInfantryClass)i);
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                DeployRequested = true;
            if (Input.GetKeyDown(KeyCode.L)) RequestLan();
        }

        private bool IsEditingName()
        {
            if (nameField.panel == null) return false;
            VisualElement focused = nameField.panel.focusController.focusedElement as VisualElement;
            return focused != null && (focused == nameField || nameField.Contains(focused));
        }

        private void SetPage(int page)
        {
            selectedPage = page;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].style.display = i == selectedPage ? DisplayStyle.Flex : DisplayStyle.None;
                pageButtons[i].EnableInClassList("active", i == selectedPage);
            }
        }

        private void SetClass(PrototypeInfantryClass role)
        {
            int index = (int)role;
            if (index < 0 || index >= ClassNames.Length) return;
            SelectedClass = role;
            preview.SetClass(role);
            for (int i = 0; i < classButtons.Length; i++)
                classButtons[i].EnableInClassList("selected", i == index);
            classCaption.text = "当前兵种：" + ClassNames[index];
            operatorTitle.text = "OPERATOR  /  " + ClassCodes[index];
            squadClass.text = ClassNames[index];
            PrototypeWeaponDefinition weapon = PrototypeInfantryRoles.GetWeapon(
                PrototypeInfantryRoles.DefaultWeapon(role));
            VisualElement root = document.rootVisualElement;
            Require<Label>(root, "weapon-name").text = weapon.ChineseName;
            Require<Label>(root, "stat-damage").text = weapon.Damage.ToString("0");
            Require<Label>(root, "stat-rate").text = Mathf.RoundToInt(60f / weapon.FireInterval).ToString();
            Require<Label>(root, "stat-magazine").text = weapon.Magazine.ToString();
            Require<Label>(root, "stat-range").text = weapon.EffectiveRange.ToString("0") + "m";
        }

        private static Texture2D MakeVeil()
        {
            var texture = new Texture2D(256, 1, TextureFormat.RGBA32, false)
            {
                name = "Start menu horizontal veil",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                float opacity = Mathf.Lerp(.94f, .16f,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .75f)));
                texture.SetPixel(i, 0, new Color(.005f, .014f, .019f, opacity));
            }
            texture.Apply();
            return texture;
        }

        public void Dispose()
        {
            preview.Dispose();
            UnityEngine.Object.Destroy(veil);
            UnityEngine.Object.Destroy(map);
            UnityEngine.Object.Destroy(host);
        }
    }

    public sealed class PrototypeStartMenuInput : MonoBehaviour
    {
        public PrototypeStartMenuView View { get; set; }
        private void Update() => View?.HandleKeyboard();
    }
}
