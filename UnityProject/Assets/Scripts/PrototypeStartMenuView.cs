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
        private readonly Texture2D missionShade;
        private readonly Texture2D buttonGrain;
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
        private readonly Label operationSummary;
        private readonly VisualElement conquestCard;
        private readonly VisualElement breakthroughCard;
        private readonly Slider volumeSlider;
        private readonly Label volumeValue;
        private readonly Slider musicSlider;
        private readonly Label musicValue;
        private readonly Slider sensitivitySlider;
        private readonly Label sensitivityValue;
        private readonly Slider crosshairSlider;
        private readonly Label crosshairValue;
        private readonly VisualElement crosshairPreview;
        private readonly VisualElement screen;
        private readonly Label selectionTitle;
        private readonly Label previewEquipment;
        private readonly VisualElement weaponStats;
        private readonly PrototypeLoadoutSymbol roleSymbol;
        private bool pickerOpen;
        private int lastLoadoutKeyFrame = -1;
        private int selectedPage;
        private int selectedSlot;

        public string PlayerName => nameField.value;
        public PrototypeInfantryClass SelectedClass { get; private set; }
        public PrototypeGameMode SelectedMode { get; private set; }
        public int SelectedTeamSize { get; private set; } = 8;
        public PrototypeLoadout Loadout => loadout.Copy();
        public bool DeployRequested { get; private set; }
        public bool LanRequested { get; private set; }
        public VisualElement Root => document.rootVisualElement;

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
            StyleSheet lobbySheet = Resources.Load<StyleSheet>("Menu/LobbyTheme");
            if (lobbySheet != null) root.styleSheets.Add(lobbySheet);
            StyleSheet loadoutSheet = Resources.Load<StyleSheet>("Menu/LoadoutTheme");
            if (loadoutSheet != null) root.styleSheets.Add(loadoutSheet);
            screen = Require<VisualElement>(root, "screen");
            screen.RegisterCallback<GeometryChangedEvent>(change =>
            {
                screen.EnableInClassList("compact", change.newRect.height < 585f);
                screen.EnableInClassList("short-panel", change.newRect.height < 520f);
            });

            preview = new PrototypeMenuPreview(runtime);
            Image hangar = Require<Image>(root, "hangar-image");
            hangar.image = preview.BackgroundTexture;
            hangar.scaleMode = ScaleMode.ScaleAndCrop;
            Image operatorImage = Require<Image>(root, "operator-image");
            operatorImage.image = preview.Texture;
            operatorImage.scaleMode = ScaleMode.ScaleToFit;
            veil = MakeVeil();
            Require<VisualElement>(root, "scene-veil").style.backgroundImage =
                new StyleBackground(veil);

            map = PrototypeFrontend.BuildTacticalMapTexture();
            Image thumbnail = Require<Image>(root, "map-thumb");
            thumbnail.image = map;
            thumbnail.scaleMode = ScaleMode.StretchToFill;
            Image lobbyArt = Require<Image>(root, "lobby-map-art");
            lobbyArt.image = Resources.Load<Texture2D>("Menu/IndustrialFrontline061") ?? map;
            lobbyArt.scaleMode = ScaleMode.ScaleAndCrop;
            missionShade = PrototypeLobbyDecoration.MakeMissionShade();
            Require<VisualElement>(root, "lobby-map-shade").style.backgroundImage = new StyleBackground(missionShade);
            buttonGrain = PrototypeLobbyDecoration.MakeButtonGrain();
            Require<VisualElement>(root, "calling-card").Add(new PrototypeLobbyDecoration());
            Require<Image>(root, "player-avatar").image = preview.Portrait;
            Require<Image>(root, "player-avatar").scaleMode = ScaleMode.ScaleToFit;
            Require<Label>(root, "version").text = Application.version;
            operationSummary = Require<Label>(root, "operation-summary");
            conquestCard = Require<VisualElement>(root, "mode-conquest");
            breakthroughCard = Require<VisualElement>(root, "mode-breakthrough");
            conquestCard.RegisterCallback<ClickEvent>(_ => SelectMode(PrototypeGameMode.Conquest));
            breakthroughCard.RegisterCallback<ClickEvent>(_ => SelectMode(PrototypeGameMode.Breakthrough));
            Require<Button>(root, "size-8v8").clicked += () => SelectedTeamSize = 8;
            SelectMode(PlayerPrefs.GetInt("ironfront.unity.soloMode", 0) == 1 ?
                PrototypeGameMode.Breakthrough : PrototypeGameMode.Conquest);

            for (int i = 0; i < PageNames.Length; i++)
            {
                int page = i;
                pageButtons[i] = root.Q<Button>("tab-" + PageNames[i]);
                pages[i] = Require<VisualElement>(root, "page-" + PageNames[i]);
                if (pageButtons[i] != null) pageButtons[i].clicked += () => SetPage(page);
            }
            Require<Button>(root, "browse-operation").clicked += () => SetPage(1);
            Require<Button>(root, "operation-back").clicked += () => SetPage(0);
            Require<Button>(root, "solo-button").clicked += () => DeployRequested = true;
            Require<Button>(root, "lan-button").clicked += RequestLan;
            Require<Button>(root, "loadout-complete").clicked += CompleteLoadout;
            Require<Button>(root, "loadout-back").clicked += CompleteLoadout;
            Require<Button>(root, "loadout-picker-close").clicked += ClosePicker;
            Require<Button>(root, "loadout-picker-done").clicked += ClosePicker;
            Require<Button>(root, "loadout-complete").style.backgroundImage = new StyleBackground(buttonGrain);
            // Let Toolkit dispatch Enter to focused buttons. Handle the overview's
            // Enter and Escape once, then suppress the global start-game shortcut.
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (selectedPage != 2) return;
                if (evt.keyCode == KeyCode.Escape || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    lastLoadoutKeyFrame = Time.frameCount;
                    if (evt.keyCode == KeyCode.Escape) { HandleLoadoutBack(); evt.StopImmediatePropagation(); }
                    else if (!(root.focusController.focusedElement is Button))
                    { HandleLoadoutAccept(); evt.StopImmediatePropagation(); }
                }
            }, TrickleDown.TrickleDown);
            roleSymbol = new PrototypeLoadoutSymbol(loadout.role, true);
            Require<VisualElement>(root, "loadout-role-symbol").Add(roleSymbol);
            nameField = Require<TextField>(root, "player-name");
            nameField.value = playerName;
            classCaption = Require<Label>(root, "operator-class");
            operatorTitle = Require<Label>(root, "operator-title");
            previewEquipment = Require<Label>(root, "preview-equipment");
            squadClass = Require<Label>(root, "squad-class");
            string[] roleKeys = { "assault", "medic", "recon", "engineer" };
            for (int i = 0; i < roleKeys.Length; i++)
            {
                int role = i;
                classButtons[i] = Require<Button>(root, "class-" + roleKeys[i]);
                classButtons[i].clicked += () => SetClass((PrototypeInfantryClass)role);
                Require<VisualElement>(root, "class-symbol-" + roleKeys[i]).Add(new PrototypeLoadoutSymbol((PrototypeInfantryClass)i));
            }
            string[] slotKeys = { "primary", "secondary", "gadget", "throwable" };
            for (int i = 0; i < slotKeys.Length; i++)
            {
                int slot = i;
                slotButtons[i] = Require<Button>(root, "slot-" + slotKeys[i]);
                slotButtons[i].clicked += () => { SelectSlot(slot); OpenPicker(); };
            }
            weaponOptions = Require<VisualElement>(root, "weapon-options");
            throwableOptions = Require<VisualElement>(root, "throwable-options");
            gadgetInfo = Require<Label>(root, "gadget-info");
            selectionTitle = Require<Label>(root, "selection-title");
            weaponStats = Require<VisualElement>(root, "weapon-stats");
            string[] weaponKeys = { "carbine", "marksman", "smg", "lmg", "battlerifle", "sniper", "pistol" };
            for (int i = 0; i < weaponKeys.Length; i++)
            {
                int weapon = i;
                weaponButtons[i] = Require<Button>(root, "weapon-" + weaponKeys[i]);
                weaponButtons[i].clicked += () => SelectWeapon((PrototypeWeaponId)weapon);
                Image weaponImage = Require<Image>(root, "weapon-icon-" + weaponKeys[i]);
                weaponImage.image = preview.WeaponThumbnail((PrototypeWeaponId)i);
                weaponImage.scaleMode = ScaleMode.ScaleToFit;
            }
            string[] throwableKeys = { "frag", "smoke" };
            for (int i = 0; i < throwableKeys.Length; i++)
            {
                int throwable = i;
                throwableButtons[i] = Require<Button>(root, "throwable-" + throwableKeys[i]);
                throwableButtons[i].clicked += () => SelectThrowable((PrototypeThrowableId)throwable);
                Require<Image>(root, "throwable-icon-" + throwableKeys[i]).image =
                    preview.ThrowableThumbnail((PrototypeThrowableId)i);
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
            musicSlider = Require<Slider>(root, "music-slider");
            musicValue = Require<Label>(root, "music-value");
            musicSlider.value = runtime.Music != null ? runtime.Music.MusicVolume : .55f;
            musicValue.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
            musicSlider.RegisterValueChangedCallback(change =>
            {
                runtime.Music?.SetMusicVolume(change.newValue);
                musicValue.text = Mathf.RoundToInt(change.newValue * 100f) + "%";
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
            crosshairSlider = Require<Slider>(root, "crosshair-slider");
            crosshairValue = Require<Label>(root, "crosshair-value");
            crosshairPreview = Require<VisualElement>(root, "crosshair-preview");
            crosshairSlider.SetValueWithoutNotify(runtime.CrosshairSize);
            UpdateCrosshairPreview(runtime.CrosshairSize);
            crosshairSlider.RegisterValueChangedCallback(change =>
            {
                runtime.SetCrosshairSize(change.newValue);
                UpdateCrosshairPreview(runtime.CrosshairSize);
            });
            host.AddComponent<PrototypeStartMenuInput>().View = this;
            SetClass(loadout.role);
            SelectSlot(0);
            SetPage(0);
        }

        private void UpdateCrosshairPreview(float size)
        {
            crosshairValue.text = Mathf.RoundToInt(size * 100f) + "%";
            crosshairPreview.Clear();
            float center = 31f, gap = 6f * size, arm = 7f * size, thickness = 2f * size;
            AddPreviewBar(new Rect(center - gap - arm, center - size, arm, thickness));
            AddPreviewBar(new Rect(center + gap, center - size, arm, thickness));
            AddPreviewBar(new Rect(center - size, center - gap - arm, thickness, arm));
            AddPreviewBar(new Rect(center - size, center + gap, thickness, arm));
            AddPreviewBar(new Rect(center - size, center - size, thickness, thickness));
        }

        private void AddPreviewBar(Rect rectangle)
        {
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.position = Position.Absolute;
            bar.style.left = rectangle.x; bar.style.top = rectangle.y;
            bar.style.width = rectangle.width; bar.style.height = rectangle.height;
            bar.style.backgroundColor = new Color(.96f, .98f, .95f);
            crosshairPreview.Add(bar);
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
            // Loadout keyboard events belong to its Toolkit controls, never deployment.
            if (selectedPage == 2 || lastLoadoutKeyFrame == Time.frameCount)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.F1 + i))) SetPage(i);
                    if (!pickerOpen && Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SetClass((PrototypeInfantryClass)i);
                }
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.F1 + i))) SetPage(i);
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    SetClass((PrototypeInfantryClass)i);
            }
            if (Input.GetKeyDown(KeyCode.M))
            {
                SelectMode(SelectedMode == PrototypeGameMode.Conquest ?
                    PrototypeGameMode.Breakthrough : PrototypeGameMode.Conquest);
                SetPage(1);
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
            if (page < 0 || page >= pages.Length) return;
            lastLoadoutKeyFrame = Time.frameCount;
            if (pickerOpen) ClosePicker();
            selectedPage = page;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].style.display = i == selectedPage ? DisplayStyle.Flex : DisplayStyle.None;
                if (pageButtons[i] != null) pageButtons[i].EnableInClassList("active", i == selectedPage || (i == 0 && selectedPage == 1));
                screen.EnableInClassList(PageNames[i] + "-view", i == selectedPage);
            }
            VisualElement actions = Require<VisualElement>(Root, "deployment-actions");
            Button start = Require<Button>(Root, "solo-button");
            if (selectedPage == 0)
            {
                Require<VisualElement>(Root, "lobby-deployment-slot").Add(actions);
                actions.Insert(0, start);
                start.style.backgroundImage = new StyleBackground(buttonGrain);
            }
            else
            {
                screen.Add(actions);
                actions.Add(start);
                start.style.backgroundImage = StyleKeyword.None;
            }
            Require<Image>(Root, "hangar-image").image = selectedPage == 0 ? preview.LobbyBackgroundTexture : preview.BackgroundTexture;
            preview.SetComposition(selectedPage == 2, selectedPage == 0);
            RefreshPreview();
        }

        private void SelectMode(PrototypeGameMode mode)
        {
            SelectedMode = mode;
            conquestCard.EnableInClassList("selected", mode == PrototypeGameMode.Conquest);
            breakthroughCard.EnableInClassList("selected", mode == PrototypeGameMode.Breakthrough);
            operationSummary.text = mode == PrototypeGameMode.Conquest ?
                "征服 · 8v8 · 五据点" : "攻防 · 8v8 · 逐点进攻";
            PlayerPrefs.SetInt("ironfront.unity.soloMode", (int)mode);
            PlayerPrefs.Save();
        }

        public void SelectClass(PrototypeInfantryClass role) => SetClass(role);

        private void SetClass(PrototypeInfantryClass role)
        {
            int index = (int)role;
            if (index < 0 || index >= ClassNames.Length) return;
            SelectedClass = role;
            loadout.role = role;
            loadout.Validate();
            loadout.Save();
            for (int i = 0; i < classButtons.Length; i++)
                classButtons[i].EnableInClassList("selected", i == index);
            classCaption.text = ClassNames[index];
            operatorTitle.text = ClassCodes[index];
            squadClass.text = ClassNames[index];
            roleSymbol.SetRole(role);
            Require<Label>(Root, "loadout-class-title").text = ClassNames[index];
            Require<Label>(Root, "loadout-class-tagline").text = new[] {
                "前线推进 · 弹药补给", "战地救护 · 近距支援", "远程侦察 · 前沿部署", "载具维护 · 反装甲支援" }[index];
            Require<Label>(Root, "loadout-role-equipment").text = GadgetName(role);
            Require<Label>(Root, "loadout-role-description").text = new[] {
                "为自己与队友补充弹药", "恢复自己或附近友军生命", "部署一次性的复活信标", "修复载具，提供反装甲支援" }[index];
            Require<Label>(Root, "loadout-role-weapons").text = "适用武器：" + new[] {
                "卡宾枪 / 战斗步枪", "冲锋枪 / 卡宾枪", "射手步枪 / 狙击步枪", "战斗步枪 / 轻机枪" }[index];
            RefreshLoadout();
        }

        public void HandleLoadoutBack()
        {
            lastLoadoutKeyFrame = Time.frameCount;
            if (pickerOpen) ClosePicker(); else if (selectedPage == 2) CompleteLoadout();
        }

        public void HandleLoadoutAccept()
        {
            lastLoadoutKeyFrame = Time.frameCount;
            if (pickerOpen) ClosePicker(); else if (selectedPage == 2) CompleteLoadout();
        }

        private void CompleteLoadout()
        {
            lastLoadoutKeyFrame = Time.frameCount;
            loadout.Save(); SetPage(0);
        }

        private void OpenPicker()
        {
            pickerOpen = true;
            Require<VisualElement>(Root, "loadout-picker").style.display = DisplayStyle.Flex;
            Require<VisualElement>(Root, "class-rail").SetEnabled(false);
            Require<VisualElement>(Root, "loadout-workbench").SetEnabled(false);
            Root.Q<VisualElement>(className: "loadout-footer").SetEnabled(false);
            Require<Button>(Root, "loadout-picker-done").Focus();
        }

        private void ClosePicker()
        {
            pickerOpen = false;
            Require<VisualElement>(Root, "loadout-picker").style.display = DisplayStyle.None;
            Require<VisualElement>(Root, "class-rail").SetEnabled(true);
            Require<VisualElement>(Root, "loadout-workbench").SetEnabled(true);
            Root.Q<VisualElement>(className: "loadout-footer").SetEnabled(true);
            slotButtons[selectedSlot].Focus();
            RefreshPreview();
        }

        private void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= slotButtons.Length) return;
            selectedSlot = slot;
            for (int i = 0; i < slotButtons.Length; i++)
                slotButtons[i].EnableInClassList("selected", i == slot);
            weaponOptions.style.display = slot <= 1 ? DisplayStyle.Flex : DisplayStyle.None;
            throwableOptions.style.display = slot == 3 ? DisplayStyle.Flex : DisplayStyle.None;
            gadgetInfo.style.display = slot == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            weaponStats.style.display = slot <= 1 ? DisplayStyle.Flex : DisplayStyle.None;
            Require<Label>(Root, "loadout-picker-hint").text = slot == 2 ?
                "兵种装备随兵种自动配置" : slot == 1 ? "通用副武器 · P8 手枪" : "选择装备，立即应用于当前配装";
            RefreshLoadout();
        }

        private void SelectWeapon(PrototypeWeaponId weapon)
        {
            if (selectedSlot == 0 ? !PrototypeInfantryRoles.IsPrimaryWeaponAllowed(loadout.role, weapon) :
                selectedSlot != 1 || !PrototypeInfantryRoles.IsSecondaryWeaponAllowed(weapon)) return;
            if (selectedSlot == 0) loadout.primary = weapon;
            else if (selectedSlot == 1) loadout.secondary = weapon;
            loadout.Validate();
            loadout.Save();
            RefreshLoadout();
        }

        private void SelectThrowable(PrototypeThrowableId throwable)
        {
            if (!Enum.IsDefined(typeof(PrototypeThrowableId), throwable)) return;
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
            Image primary = Require<Image>(root, "icon-primary");
            primary.image = preview.WeaponThumbnail(loadout.primary);
            primary.scaleMode = ScaleMode.ScaleToFit;
            Image secondary = Require<Image>(root, "icon-secondary");
            secondary.image = preview.WeaponThumbnail(loadout.secondary);
            secondary.scaleMode = ScaleMode.ScaleToFit;
            Require<Image>(root, "icon-gadget").image = preview.GadgetThumbnail(loadout.role);
            Require<Image>(root, "icon-throwable").image = preview.ThrowableThumbnail(loadout.throwable);
            Require<Image>(root, "icon-gadget").scaleMode = ScaleMode.ScaleToFit;
            Require<Image>(root, "icon-throwable").scaleMode = ScaleMode.ScaleToFit;
            for (int i = 0; i < weaponButtons.Length; i++)
            {
                bool allowed = selectedSlot == 0 ?
                    PrototypeInfantryRoles.IsPrimaryWeaponAllowed(loadout.role, (PrototypeWeaponId)i) :
                    PrototypeInfantryRoles.IsSecondaryWeaponAllowed((PrototypeWeaponId)i);
                weaponButtons[i].style.display = allowed ? DisplayStyle.Flex : DisplayStyle.None;
                weaponButtons[i].EnableInClassList("selected", i == (int)(selectedSlot == 0 ? loadout.primary : loadout.secondary));
            }
            if (selectedSlot == 0)
                foreach (PrototypeWeaponId allowed in PrototypeInfantryRoles.AllowedPrimaryWeapons(loadout.role))
                    weaponOptions.Add(weaponButtons[(int)allowed]);
            selectionTitle.text = selectedSlot == 0 ? ClassNames[(int)loadout.role] + " · 主武器" :
                selectedSlot == 1 ? "副武器 · P8 手枪" : selectedSlot == 2 ? "兵种装备" : "投掷物";
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
            PrototypeWeaponDefinition primaryStats = PrototypeInfantryRoles.GetWeapon(loadout.primary);
            Require<Label>(root, "primary-stat-damage").text = primaryStats.Damage.ToString("0");
            Require<Label>(root, "primary-stat-rate").text = Mathf.RoundToInt(60f / primaryStats.FireInterval) + " RPM";
            Require<Label>(root, "primary-stat-magazine").text = primaryStats.Magazine.ToString();
            Require<Label>(root, "primary-stat-range").text = primaryStats.EffectiveRange.ToString("0") + " m";
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            bool secondary = selectedPage == 2 && selectedSlot == 1;
            PrototypeWeaponId weapon = secondary ? loadout.secondary : loadout.primary;
            preview.SetClass(loadout.role, weapon);
            previewEquipment.text = secondary ? "展示副武器" : "展示主武器";
        }

        public static string GadgetName(PrototypeInfantryClass role)
        {
            switch (role)
            {
                case PrototypeInfantryClass.Medic: return "战地治疗";
                case PrototypeInfantryClass.Recon: return "复活信标";
                case PrototypeInfantryClass.Engineer: return "修理工具";
                default: return "弹药包";
            }
        }

        private static string GadgetDescription(PrototypeInfantryClass role)
        {
            switch (role)
            {
                case PrototypeInfantryClass.Medic: return "X 战地治疗：恢复自己或附近友军生命值。";
                case PrototypeInfantryClass.Recon: return "X 复活信标：阵亡后可选择一次性部署。";
                case PrototypeInfantryClass.Engineer: return "X 修理 · 4/Z 反装甲 · 5 防空（各 2 发）。防空瞄准 5 秒锁定，左键/P 发射。";
                default: return "X 弹药包 · 6 中立补给箱（90 秒冷却 / 最多 2 个）。箱旁 E 补给。";
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
                float opacity = Mathf.Lerp(.32f, .025f,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .75f)));
                texture.SetPixel(i, 0, new Color(.015f, .019f, .018f, opacity));
            }
            texture.Apply();
            return texture;
        }

        public void Dispose()
        {
            preview.Dispose();
            UnityEngine.Object.Destroy(veil);
            UnityEngine.Object.Destroy(map);
            UnityEngine.Object.Destroy(missionShade);
            UnityEngine.Object.Destroy(buttonGrain);
            UnityEngine.Object.Destroy(host);
        }
    }

    public sealed class PrototypeStartMenuInput : MonoBehaviour
    {
        public PrototypeStartMenuView View { get; set; }
        private void Update() => View?.HandleKeyboard();
    }
}
