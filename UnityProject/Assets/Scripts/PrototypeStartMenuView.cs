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
        private readonly Button[] slotButtons = new Button[4];
        private readonly Button[] weaponButtons = new Button[7];
        private readonly Button[] throwableButtons = new Button[2];
        private readonly VisualElement weaponOptions;
        private readonly VisualElement throwableOptions;
        private readonly Label gadgetInfo;
        private readonly PrototypeLoadout loadout;
        private readonly TextField nameField;
        private readonly Label classCaption;
        private readonly Label operatorTitle;
        private readonly Label squadClass;
        private readonly Slider volumeSlider;
        private readonly Label volumeValue;
        private readonly Slider sensitivitySlider;
        private readonly Label sensitivityValue;
        private readonly VisualElement screen;
        private int selectedPage;
        private int selectedSlot;

        public string PlayerName => nameField.value;
        public PrototypeInfantryClass SelectedClass { get; private set; }
        public PrototypeLoadout Loadout => loadout.Copy();
        public bool DeployRequested { get; private set; }
        public bool LanRequested { get; private set; }

        public static bool AssetsAvailable =>
            Resources.Load<VisualTreeAsset>("Menu/StartMenu") != null &&
            Resources.Load<PanelSettings>("Menu/StartMenuPanel") != null;

        public PrototypeStartMenuView(PrototypeRuntime runtime, string playerName,
            PrototypeLoadout selection, float volume)
        {
            loadout = selection?.Copy() ?? new PrototypeLoadout();
            loadout.Validate();
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
            string[] slotKeys = { "primary", "secondary", "gadget", "throwable" };
            for (int i = 0; i < slotKeys.Length; i++)
            {
                int slot = i;
                slotButtons[i] = Require<Button>(root, "slot-" + slotKeys[i]);
                slotButtons[i].clicked += () => SelectSlot(slot);
            }
            weaponOptions = Require<VisualElement>(root, "weapon-options");
            throwableOptions = Require<VisualElement>(root, "throwable-options");
            gadgetInfo = Require<Label>(root, "gadget-info");
            string[] weaponKeys = { "carbine", "marksman", "smg", "lmg", "battlerifle", "sniper", "pistol" };
            for (int i = 0; i < weaponKeys.Length; i++)
            {
                int weapon = i;
                weaponButtons[i] = Require<Button>(root, "weapon-" + weaponKeys[i]);
                weaponButtons[i].clicked += () => SelectWeapon((PrototypeWeaponId)weapon);
                Require<Image>(root, "weapon-icon-" + weaponKeys[i]).image =
                    PrototypeEquipmentIcons.Weapon((PrototypeWeaponId)i);
            }
            string[] throwableKeys = { "frag", "smoke" };
            for (int i = 0; i < throwableKeys.Length; i++)
            {
                int throwable = i;
                throwableButtons[i] = Require<Button>(root, "throwable-" + throwableKeys[i]);
                throwableButtons[i].clicked += () => SelectThrowable((PrototypeThrowableId)throwable);
                Require<Image>(root, "throwable-icon-" + throwableKeys[i]).image =
                    PrototypeEquipmentIcons.Throwable((PrototypeThrowableId)i);
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
            sensitivitySlider = Require<Slider>(root, "sensitivity-slider");
            sensitivityValue = Require<Label>(root, "sensitivity-value");
            sensitivitySlider.value = runtime.MouseSensitivity;
            sensitivityValue.text = runtime.MouseSensitivity.ToString("0.0");
            sensitivitySlider.RegisterValueChangedCallback(change =>
            {
                runtime.SetMouseSensitivity(change.newValue);
                sensitivityValue.text = runtime.MouseSensitivity.ToString("0.0");
            });
            host.AddComponent<PrototypeStartMenuInput>().View = this;
            SetClass(loadout.role);
            SelectSlot(0);
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
            loadout.role = role;
            loadout.Validate();
            loadout.Save();
            preview.SetClass(role, loadout.primary);
            for (int i = 0; i < classButtons.Length; i++)
                classButtons[i].EnableInClassList("selected", i == index);
            classCaption.text = "当前兵种：" + ClassNames[index];
            operatorTitle.text = "OPERATOR  /  " + ClassCodes[index];
            squadClass.text = ClassNames[index];
            RefreshLoadout();
        }

        private void SelectSlot(int slot)
        {
            selectedSlot = slot;
            for (int i = 0; i < slotButtons.Length; i++)
                slotButtons[i].EnableInClassList("selected", i == slot);
            weaponOptions.style.display = slot <= 1 ? DisplayStyle.Flex : DisplayStyle.None;
            throwableOptions.style.display = slot == 3 ? DisplayStyle.Flex : DisplayStyle.None;
            gadgetInfo.style.display = slot == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshLoadout();
        }

        private void SelectWeapon(PrototypeWeaponId weapon)
        {
            if (selectedSlot == 0 && weapon == PrototypeWeaponId.Pistol) return;
            if (selectedSlot == 1 && weapon != PrototypeWeaponId.Pistol &&
                weapon != PrototypeWeaponId.Marksman) return;
            if (selectedSlot == 1 && loadout.primary == PrototypeWeaponId.Marksman &&
                weapon != PrototypeWeaponId.Pistol) return;
            if (selectedSlot == 0) loadout.primary = weapon;
            else if (selectedSlot == 1) loadout.secondary = weapon;
            loadout.Validate();
            loadout.Save();
            preview.SetClass(loadout.role, loadout.primary);
            RefreshLoadout();
        }

        private void SelectThrowable(PrototypeThrowableId throwable)
        {
            loadout.throwable = throwable;
            loadout.Save();
            RefreshLoadout();
        }

        private void RefreshLoadout()
        {
            VisualElement root = document.rootVisualElement;
            Require<Label>(root, "primary-name").text = ShortWeaponName(loadout.primary);
            Require<Label>(root, "secondary-name").text = ShortWeaponName(loadout.secondary);
            Require<Label>(root, "gadget-name").text = GadgetName(loadout.role);
            gadgetInfo.text = GadgetDescription(loadout.role);
            Require<Label>(root, "throwable-name").text = loadout.throwable == PrototypeThrowableId.Frag ? "破片手雷" : "烟雾弹";
            Require<Image>(root, "icon-primary").image = PrototypeEquipmentIcons.Weapon(loadout.primary);
            Require<Image>(root, "icon-secondary").image = PrototypeEquipmentIcons.Weapon(loadout.secondary);
            Require<Image>(root, "icon-gadget").image = PrototypeEquipmentIcons.Gadget(loadout.role);
            Require<Image>(root, "icon-throwable").image = PrototypeEquipmentIcons.Throwable(loadout.throwable);
            for (int i = 0; i < weaponButtons.Length; i++)
            {
                bool allowed = selectedSlot == 0 ? i != (int)PrototypeWeaponId.Pistol :
                    (i == (int)PrototypeWeaponId.Pistol ||
                     (i == (int)PrototypeWeaponId.Marksman && loadout.primary != PrototypeWeaponId.Marksman));
                weaponButtons[i].style.display = allowed ? DisplayStyle.Flex : DisplayStyle.None;
                weaponButtons[i].EnableInClassList("selected", i == (int)(selectedSlot == 0 ? loadout.primary : loadout.secondary));
            }
            for (int i = 0; i < throwableButtons.Length; i++)
                throwableButtons[i].EnableInClassList("selected", i == (int)loadout.throwable);
            PrototypeWeaponDefinition weapon = PrototypeInfantryRoles.GetWeapon(
                selectedSlot == 1 ? loadout.secondary : loadout.primary);
            Require<Label>(root, "stat-heading").text = selectedSlot == 1 ?
                "当前副武器  /  SECONDARY WEAPON" : "当前主武器  /  PRIMARY WEAPON";
            Require<Label>(root, "weapon-name").text = weapon.ChineseName;
            Require<Label>(root, "stat-damage").text = weapon.Damage.ToString("0");
            Require<Label>(root, "stat-rate").text = Mathf.RoundToInt(60f / weapon.FireInterval).ToString();
            Require<Label>(root, "stat-magazine").text = weapon.Magazine.ToString();
            Require<Label>(root, "stat-range").text = weapon.EffectiveRange.ToString("0") + "m";
        }

        public static string GadgetName(PrototypeInfantryClass role)
        {
            switch (role)
            {
                case PrototypeInfantryClass.Medic: return "战地治疗";
                case PrototypeInfantryClass.Recon: return "复活信标";
                case PrototypeInfantryClass.Engineer: return "修理工具";
                default: return "弹药补给";
            }
        }

        private static string GadgetDescription(PrototypeInfantryClass role)
        {
            switch (role)
            {
                case PrototypeInfantryClass.Medic: return "战地治疗 · 给自己或附近友军恢复生命值。战斗中按 X 使用。";
                case PrototypeInfantryClass.Recon: return "复活信标 · 按 X 放置信标，阵亡后可选择一次性部署。";
                case PrototypeInfantryClass.Engineer: return "修理工具 · 按 X 修理己方载具；按 Z 发射反装甲火箭（2 发）。";
                default: return "弹药补给 · 为自己或附近友军补充弹药。战斗中按 X 使用。";
            }
        }

        private static string ShortWeaponName(PrototypeWeaponId id)
        {
            switch (id)
            {
                case PrototypeWeaponId.Carbine: return "IF-27 卡宾枪";
                case PrototypeWeaponId.Marksman: return "M89 射手步枪";
                case PrototypeWeaponId.Smg: return "VX-9 冲锋枪";
                case PrototypeWeaponId.Lmg: return "H60 轻机枪";
                case PrototypeWeaponId.BattleRifle: return "BR-44 战斗步枪";
                case PrototypeWeaponId.Sniper: return "S12 狙击步枪";
                default: return "P8 手枪";
            }
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
