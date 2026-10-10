using UnityEngine;
using System.Collections.Generic;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeDownedAction { None, Rescue, GiveUp }

    // IMGUI start and deployment screens. PrototypeRuntime owns the match lifecycle and calls Draw
    // after applying the same logical 960x600 GUI matrix used by the in-game HUD.
    public sealed class PrototypeFrontend
    {
        private static readonly PrototypeInfantryClass[] Classes =
        {
            PrototypeInfantryClass.Assault,
            PrototypeInfantryClass.Medic,
            PrototypeInfantryClass.Recon,
            PrototypeInfantryClass.Engineer
        };

        private static readonly string[] ClassNames =
            { "ASSAULT", "MEDIC", "RECON", "ENGINEER" };

        private static readonly string[] ClassDescriptions =
        {
            "Lead the push  /  supply ammunition",
            "Hold the line  /  heal friendly soldiers",
            "Find the opening  /  scout enemy positions",
            "Fortify allies  /  issue armor plates"
        };

        private static readonly string[] ClassNamesChinese =
            { "突击兵", "医疗兵", "侦察兵", "工程兵" };
        private static readonly string[] ClassAbilities =
        {
            "弹药补给  /  持续推进前线",
            "治疗友军  /  优先救援队友",
            "侦测敌情  /  扩大视野范围",
            "装甲补给  /  加固友军防线"
        };

        private enum MenuTab { Lobby, Operations, Loadout, Settings }
        private static readonly string[] MenuTabs =
            { "大厅", "作战", "配装", "设置" };

        private static readonly Color Background = new Color(0.025f, 0.055f, 0.09f, 0.96f);
        private static readonly Color Panel = new Color(0.065f, 0.11f, 0.16f, 1f);
        private static readonly Color Card = new Color(0.10f, 0.17f, 0.23f, 1f);
        private static readonly Color SelectedCard = new Color(0.12f, 0.29f, 0.37f, 1f);
        private static readonly Color Accent = new Color(0.29f, 0.82f, 0.85f, 1f);
        private static readonly Color Muted = new Color(0.62f, 0.72f, 0.78f, 1f);
        private static readonly Color Divider = new Color(0.20f, 0.32f, 0.39f, 1f);
        private static readonly Color StartBackground = new Color(.025f, .045f, .050f, 1f);
        private static readonly Color MenuPanel = new Color(.006f, .020f, .026f, .88f);
        private static readonly Color MenuCard = new Color(.018f, .043f, .052f, .87f);
        private static readonly Color MenuSelected = new Color(.035f, .095f, .110f, .92f);
        private static readonly Color MenuAccent = new Color(.48f, .77f, .80f, 1f);
        private static readonly Color MenuDivider = new Color(.018f, .036f, .043f, 1f);
        private static readonly Color WarmAccent = new Color(0.95f, 0.53f, 0.30f, 1f);
        private const string PlayerNameKey = "ironfront.unity.playerName";
        private const string VolumeKey = "ironfront.unity.masterVolume";

        private GUIStyle logoStyle;
        private GUIStyle headingStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle cardDescriptionStyle;
        private GUIStyle buttonStyle;
        private GUIStyle heroStyle;
        private GUIStyle nameFieldStyle;
        private GUIStyle darkButtonStyle;
        private GUIStyle lightButtonStyle;
        private GUIStyle menuTitleStyle;
        private GUIStyle menuSubtitleStyle;
        private GUIStyle navStyle;
        private GUIStyle navSelectedStyle;
        private GUIStyle statStyle;

        private readonly PrototypeRuntime runtime;
        private PrototypeStartMenuView startMenu;
        private PrototypeMenuPreview menuPreview;
        private Texture2D menuVeil;
        private Texture2D tacticalMapTexture;
        private MenuTab menuTab;
        private float volume;

        private bool redeploy;
        private string selectedLocation = "BASE";

        public bool IsOpen { get; private set; }
        public bool IsDownedScreen { get; private set; }
        public bool LanRequested { get; private set; }
        public PrototypeInfantryClass SelectedClass { get; private set; } = PrototypeInfantryClass.Assault;
        public string SelectedClassName => ClassNames[ClassIndex(SelectedClass)];
        public string PlayerName { get; private set; } = "Player";

        public PrototypeFrontend(PrototypeRuntime game)
        {
            runtime = game;
            PlayerName = CleanPlayerName(PlayerPrefs.GetString(PlayerNameKey, "Player"));
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            AudioListener.volume = volume;
        }

        public void Open(bool isRespawn = false)
        {
            if (startMenu != null)
            {
                startMenu.Dispose();
                startMenu = null;
            }
            redeploy = isRespawn;
            IsDownedScreen = false;
            if (isRespawn) selectedLocation = "BASE";
            LanRequested = false;
            IsOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!isRespawn && PrototypeStartMenuView.AssetsAvailable)
            {
                try { startMenu = new PrototypeStartMenuView(runtime, PlayerName, SelectedClass, volume); }
                catch (System.Exception error)
                {
                    Debug.LogError("Start menu UI Toolkit could not initialize: " + error);
                    startMenu = null;
                }
            }
        }

        public void Close()
        {
            IsOpen = false;
            IsDownedScreen = false;
            if (startMenu != null)
            {
                startMenu.Dispose();
                startMenu = null;
            }
            DisposePreview();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Dispose()
        {
            if (startMenu != null)
            {
                startMenu.Dispose();
                startMenu = null;
            }
            DisposePreview();
            if (menuVeil != null) Object.Destroy(menuVeil);
            if (tacticalMapTexture != null) Object.Destroy(tacticalMapTexture);
            menuVeil = null;
            tacticalMapTexture = null;
        }

        public void OpenDowned()
        {
            redeploy = false;
            IsDownedScreen = true;
            IsOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public PrototypeDownedAction DrawDowned(float canvasWidth, float canvasHeight,
            float seconds, bool rescueCalled)
        {
            if (!IsOpen || !IsDownedScreen) return PrototypeDownedAction.None;
            EnsureStyles();
            DrawRect(new Rect(0f, 0f, canvasWidth, canvasHeight), Background);
            float x = (canvasWidth - 510f) * .5f;
            float y = (canvasHeight - 300f) * .5f;
            DrawRect(new Rect(x, y, 510f, 300f), Panel);
            DrawRect(new Rect(x, y, 510f, 4f), Accent);
            GUI.Label(new Rect(x + 28f, y + 28f, 455f, 48f),
                "濒死 / DOWNED", logoStyle);
            GUI.Label(new Rect(x + 28f, y + 88f, 455f, 30f),
                "等待救援  " + Mathf.Max(0f, seconds).ToString("0.0") + " s", headingStyle);
            GUI.Label(new Rect(x + 28f, y + 124f, 455f, 44f),
                "医疗兵可以将你救起。按住空格可加快放弃救援。", labelStyle);
            Rect rescue = new Rect(x + 28f, y + 185f, 220f, 51f);
            Rect giveUp = new Rect(x + 263f, y + 185f, 220f, 51f);
            DrawRect(rescue, SelectedCard);
            DrawRect(giveUp, Card);
            GUI.Label(rescue, rescueCalled ? "救援已呼叫" : "呼叫救援 / H", buttonStyle);
            GUI.Label(giveUp, "放弃救援", buttonStyle);
            if (GUI.Button(rescue, GUIContent.none, GUIStyle.none))
                return PrototypeDownedAction.Rescue;
            if (GUI.Button(giveUp, GUIContent.none, GUIStyle.none))
                return PrototypeDownedAction.GiveUp;
            return PrototypeDownedAction.None;
        }

        public void SelectClass(PrototypeInfantryClass soldierClass)
        {
            if (ClassIndex(soldierClass) < Classes.Length) SelectedClass = soldierClass;
        }

        // Returns true once when the user deploys. The owner can then apply
        // SelectedClass and start or respawn the player in the same GUI event.
        public bool Draw(float canvasWidth, float canvasHeight)
        {
            if (!IsOpen) return false;
            if (!redeploy && startMenu != null)
            {
                PlayerName = startMenu.PlayerName;
                SelectedClass = startMenu.SelectedClass;
                if (startMenu.LanRequested)
                {
                    LanRequested = true;
                    SavePlayerName();
                    return false;
                }
                if (!startMenu.DeployRequested) return false;
                SavePlayerName();
                Close();
                return true;
            }
            EnsureStyles();
            Event inputEvent = Event.current;
            if (inputEvent != null && inputEvent.type == EventType.KeyDown &&
                (inputEvent.keyCode == KeyCode.Return || inputEvent.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "PlayerName")
            {
                GUI.FocusControl(null);
                inputEvent.Use();
            }
            float leftX = 48f;
            float leftW = Mathf.Min(492f, canvasWidth * .54f);
            DrawRect(new Rect(0f, 0f, canvasWidth, canvasHeight), StartBackground);
            DrawMenuPreview(canvasWidth, canvasHeight);
            DrawMenuVeil(canvasWidth, canvasHeight);
            if (menuTab != MenuTab.Lobby)
            {
                DrawRect(new Rect(30f, 88f, leftW + 36f, canvasHeight - 188f),
                    new Color(.003f, .015f, .022f, .77f));
                DrawRect(new Rect(30f, 88f, leftW + 36f, 2f), MenuAccent);
            }
            DrawMenuHeader(canvasWidth);
            switch (menuTab)
            {
                case MenuTab.Lobby: DrawLobby(leftX, leftW); break;
                case MenuTab.Operations: DrawOperations(leftX, leftW); break;
                case MenuTab.Loadout: DrawLoadout(leftX, leftW); break;
                case MenuTab.Settings: DrawSettings(leftX, leftW); break;
            }
            DrawMenuCaption(canvasWidth, canvasHeight);
            bool deploy = DrawMenuFooter(canvasWidth, canvasHeight);
            HandleKeyboard(ref deploy);
            if (deploy || LanRequested) SavePlayerName();
            if (!deploy) return false;
            Close();
            return true;
        }

        private void DrawMenuHeader(float canvasWidth)
        {
            DrawRect(new Rect(0f, 0f, canvasWidth, 66f),
                new Color(.003f, .008f, .011f, 1f));
            DrawRect(new Rect(0f, 65f, canvasWidth, 1f), MenuDivider);
            GUI.Label(new Rect(40f, 19f, 260f, 35f), "IRONFRONT", headingStyle);
            DrawRect(new Rect(165f, 24f, 28f, 2f), MenuAccent);
            float tabsX = canvasWidth * .5f - 170f;
            for (int i = 0; i < MenuTabs.Length; i++)
            {
                Rect tab = new Rect(tabsX + i * 85f, 10f, 80f, 50f);
                bool selected = (int)menuTab == i;
                GUI.Label(tab, MenuTabs[i], selected ? navSelectedStyle : navStyle);
                if (selected) DrawRect(new Rect(tab.x + 13f, 62f, 54f, 3f), MenuAccent);
                if (GUI.Button(tab, GUIContent.none, GUIStyle.none)) menuTab = (MenuTab)i;
            }
            GUI.Label(new Rect(canvasWidth - 168f, 24f, 143f, 20f),
                "UNITY  /  " + Application.version, smallStyle);
        }

        private void DrawMenuPreview(float canvasWidth, float canvasHeight)
        {
            if (menuPreview == null && runtime != null)
                menuPreview = new PrototypeMenuPreview(runtime);
            if (menuPreview != null)
            {
                menuPreview.SetVisible(true);
                menuPreview.SetClass(SelectedClass);
                GUI.DrawTexture(new Rect(0f, 0f, canvasWidth, canvasHeight),
                    menuPreview.Texture, ScaleMode.StretchToFill, false);
            }
        }

        private void DrawMenuVeil(float canvasWidth, float canvasHeight)
        {
            if (menuVeil == null)
            {
                menuVeil = new Texture2D(256, 1, TextureFormat.RGBA32, false);
                menuVeil.wrapMode = TextureWrapMode.Clamp;
                menuVeil.filterMode = FilterMode.Bilinear;
                for (int i = 0; i < 256; i++)
                {
                    float t = i / 255f;
                    float opacity = Mathf.Lerp(.94f, .16f,
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .75f)));
                    menuVeil.SetPixel(i, 0, new Color(.005f, .014f, .019f, opacity));
                }
                menuVeil.Apply();
            }
            GUI.DrawTexture(new Rect(0f, 66f, canvasWidth, canvasHeight - 66f),
                menuVeil, ScaleMode.StretchToFill, true);
        }

        private void DrawMenuCaption(float canvasWidth, float canvasHeight)
        {
            float x = canvasWidth - 198f;
            float y = canvasHeight - 156f;
            DrawRect(new Rect(x, y, 116f, 2f), MenuAccent);
            GUI.Label(new Rect(x, y + 11f, 190f, 21f),
                "OPERATOR  /  " + ClassNames[(int)SelectedClass], smallStyle);
            GUI.Label(new Rect(x, y + 33f, 190f, 20f),
                "当前兵种：" + ClassNamesChinese[(int)SelectedClass], labelStyle);
        }

        private void DrawLobby(float x, float width)
        {
            GUI.Label(new Rect(x, 105f, width, 22f),
                "联合特遣队  /  TASK FORCE", smallStyle);
            GUI.Label(new Rect(x - 4f, 133f, width + 12f, 74f),
                "准备行动", heroStyle);
            GUI.Label(new Rect(x, 210f, width, 38f),
                "选择任务，与 AI 队友一起进入战场。", menuSubtitleStyle);

            Rect operation = new Rect(x, 248f, width, 126f);
            DrawRect(operation, MenuPanel);
            DrawRect(new Rect(x, 248f, 3f, 126f), MenuAccent);
            GUI.Label(new Rect(x + 20f, 262f, width - 40f, 20f),
                "当前行动  /  OPERATION 01", smallStyle);
            GUI.Label(new Rect(x + 20f, 290f, width - 40f, 36f),
                "工业前线", menuTitleStyle);
            GUI.Label(new Rect(x + 20f, 332f, width - 40f, 22f),
                "征服模式  ·  五个据点  ·  AI 战场", labelStyle);
            if (GUI.Button(operation, GUIContent.none, GUIStyle.none))
                menuTab = MenuTab.Operations;

            GUI.Label(new Rect(x, 419f, width, 20f), "小队部署  /  SQUAD", smallStyle);
            float cardW = (width - 18f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                Rect card = new Rect(x + i * (cardW + 6f), 443f, cardW, 45f);
                DrawRect(card, i == 0 ? MenuSelected : MenuCard);
                GUI.Label(new Rect(card.x + 10f, card.y + 5f, cardW - 12f, 17f),
                    (i + 1).ToString("00") + (i == 0 ? " / YOU" : " / AI"), smallStyle);
                GUI.Label(new Rect(card.x + 10f, card.y + 23f, cardW - 12f, 19f),
                    i == 0 ? ClassNamesChinese[(int)SelectedClass] : "队友", labelStyle);
            }
        }

        private void DrawOperations(float x, float width)
        {
            GUI.Label(new Rect(x, 100f, width, 21f), "选择战场  /  OPERATIONS", smallStyle);
            GUI.Label(new Rect(x, 127f, width, 48f), "工业前线", menuTitleStyle);
            GUI.Label(new Rect(x, 180f, width, 23f),
                "现有可游玩战场  ·  720 × 720  ·  五个据点", labelStyle);

            float cardW = (width - 12f) / 2f;
            Rect liveMap = new Rect(x, 218f, cardW, 121f);
            Rect futureMap = new Rect(x + cardW + 12f, 218f, cardW, 121f);
            DrawRect(liveMap, MenuSelected);
            DrawRect(new Rect(liveMap.x, liveMap.y, liveMap.width, 2f), MenuAccent);
            DrawTacticalMap(new Rect(liveMap.x + 11f, liveMap.y + 11f,
                liveMap.width - 22f, 69f));
            GUI.Label(new Rect(liveMap.x + 11f, liveMap.y + 87f, cardW - 22f, 24f),
                "工业前线  /  可游玩", labelStyle);
            DrawRect(futureMap, MenuCard);
            GUI.Label(new Rect(futureMap.x + 14f, futureMap.y + 24f,
                cardW - 26f, 26f), "赤砂走廊", headingStyle);
            GUI.Label(new Rect(futureMap.x + 14f, futureMap.y + 62f,
                cardW - 26f, 43f), "地形与场景概念已保留\n开发中，暂不可选择", smallStyle);

            GUI.Label(new Rect(x, 356f, width, 20f), "模式  /  GAME MODE", smallStyle);
            Rect mode = new Rect(x, 380f, cardW, 68f);
            Rect futureMode = new Rect(x + cardW + 12f, 380f, cardW, 68f);
            DrawRect(mode, MenuSelected);
            DrawRect(new Rect(mode.x, mode.y, 3f, mode.height), MenuAccent);
            GUI.Label(new Rect(mode.x + 14f, mode.y + 10f, cardW - 25f, 24f),
                "征服  /  CONQUEST", headingStyle);
            GUI.Label(new Rect(mode.x + 14f, mode.y + 38f, cardW - 25f, 20f),
                "争夺五处据点，耗尽敌方兵力", smallStyle);
            DrawRect(futureMode, MenuCard);
            GUI.Label(new Rect(futureMode.x + 14f, futureMode.y + 10f,
                cardW - 25f, 24f), "攻防  /  BREAKTHROUGH", labelStyle);
            GUI.Label(new Rect(futureMode.x + 14f, futureMode.y + 38f,
                cardW - 25f, 20f), "开发中，暂不可选择", smallStyle);
            GUI.Label(new Rect(x, 465f, width, 22f),
                "当前选择：工业前线 · 征服模式", smallStyle);
        }

        private void DrawLoadout(float x, float width)
        {
            GUI.Label(new Rect(x, 100f, width, 21f), "兵种与配装  /  LOADOUT", smallStyle);
            GUI.Label(new Rect(x, 126f, width, 42f), "选择你的兵种", menuTitleStyle);
            GUI.Label(new Rect(x, 165f, width, 21f),
                "点击兵种，预览当前默认武器与战场能力。", smallStyle);
            float cardW = (width - 10f) / 2f;
            for (int i = 0; i < Classes.Length; i++)
            {
                Rect card = new Rect(x + (i % 2) * (cardW + 10f),
                    197f + (i / 2) * 74f, cardW, 66f);
                bool selected = SelectedClass == Classes[i];
                DrawRect(card, selected ? MenuSelected : MenuCard);
                if (selected) DrawRect(new Rect(card.x, card.y, 3f, card.height), MenuAccent);
                GUI.Label(new Rect(card.x + 13f, card.y + 5f, cardW - 20f, 30f),
                    (i + 1).ToString("00") + "  " + ClassNamesChinese[i], headingStyle);
                GUI.Label(new Rect(card.x + 13f, card.y + 36f, cardW - 20f, 24f),
                    ClassAbilities[i], smallStyle);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) SelectedClass = Classes[i];
            }

            PrototypeWeaponDefinition weapon = PrototypeInfantryRoles.GetWeapon(
                PrototypeInfantryRoles.DefaultWeapon(SelectedClass));
            Rect equipment = new Rect(x, 356f, width, 123f);
            DrawRect(equipment, MenuPanel);
            DrawRect(new Rect(x, 356f, width, 2f), MenuAccent);
            GUI.Label(new Rect(x + 16f, 367f, width - 32f, 20f),
                "当前默认装备  /  PRIMARY WEAPON", smallStyle);
            GUI.Label(new Rect(x + 16f, 392f, width - 32f, 29f),
                weapon.ChineseName, headingStyle);
            string stats = "伤害 " + weapon.Damage.ToString("0") +
                "     射速 " + Mathf.RoundToInt(60f / weapon.FireInterval) + " RPM" +
                "     弹匣 " + weapon.Magazine +
                "     有效射程 " + weapon.EffectiveRange.ToString("0") + "m";
            GUI.Label(new Rect(x + 16f, 441f, width - 32f, 25f), stats, statStyle);
        }

        private void DrawSettings(float x, float width)
        {
            GUI.Label(new Rect(x, 100f, width, 21f), "系统设置  /  SETTINGS", smallStyle);
            GUI.Label(new Rect(x, 128f, width, 43f), "出击前设置", menuTitleStyle);
            DrawRect(new Rect(x, 188f, width, 110f), MenuPanel);
            GUI.Label(new Rect(x + 18f, 206f, width - 36f, 23f),
                "主音量  /  MASTER VOLUME", headingStyle);
            float nextVolume = GUI.HorizontalSlider(
                new Rect(x + 20f, 252f, width - 110f, 18f), volume, 0f, 1f);
            GUI.Label(new Rect(x + width - 74f, 244f, 55f, 25f),
                Mathf.RoundToInt(volume * 100f) + "%", labelStyle);
            if (!Mathf.Approximately(nextVolume, volume))
            {
                volume = nextVolume;
                AudioListener.volume = volume;
                PlayerPrefs.SetFloat(VolumeKey, volume);
                PlayerPrefs.Save();
            }
            DrawRect(new Rect(x, 319f, width, 160f), MenuPanel);
            GUI.Label(new Rect(x + 18f, 335f, width - 36f, 23f),
                "操作提示  /  CONTROLS", headingStyle);
            GUI.Label(new Rect(x + 18f, 375f, width - 36f, 95f),
                "W A S D  移动       鼠标  瞄准 / 射击\n" +
                "F1–F4  切换页面       1–4  快速选兵种\n" +
                "ENTER  开始游戏       L  打开联机房间", labelStyle);
        }

        private bool DrawMenuFooter(float canvasWidth, float canvasHeight)
        {
            float y = canvasHeight - 96f;
            DrawRect(new Rect(0f, y, canvasWidth, 96f),
                new Color(.003f, .010f, .014f, .90f));
            DrawRect(new Rect(0f, y, canvasWidth, 1f), MenuDivider);
            GUI.Label(new Rect(40f, y + 12f, 220f, 19f),
                "玩家代号  /  PLAYER NAME", smallStyle);
            Rect nameRect = new Rect(40f, y + 36f, 215f, 42f);
            DrawRect(nameRect, MenuCard);
            GUI.SetNextControlName("PlayerName");
            PlayerName = GUI.TextField(new Rect(nameRect.x + 12f, nameRect.y + 7f,
                nameRect.width - 24f, 29f), PlayerName, 20, nameFieldStyle);

            Rect solo = new Rect(274f, y + 26f, 225f, 52f);
            DrawRect(solo, MenuAccent);
            GUI.Label(solo, "单人游戏  /  SOLO  ↗", darkButtonStyle);
            bool deploy = GUI.Button(solo, GUIContent.none, GUIStyle.none);
            Rect lanButton = new Rect(510f, y + 26f, 220f, 52f);
            DrawRect(lanButton, MenuCard);
            GUI.Label(lanButton, "联机房间  /  LAN ROOM", lightButtonStyle);
            if (GUI.Button(lanButton, GUIContent.none, GUIStyle.none))
            {
                LanRequested = true;
                if (menuPreview != null) menuPreview.SetVisible(false);
            }
            GUI.Label(new Rect(750f, y + 34f, canvasWidth - 767f, 42f),
                "单人模式无需联网\n按 ENTER 直接出击", smallStyle);
            return deploy;
        }

        private void DrawTacticalMap(Rect map)
        {
            if (tacticalMapTexture == null) tacticalMapTexture = BuildTacticalMapTexture();
            GUI.DrawTexture(map, tacticalMapTexture, ScaleMode.StretchToFill, false);
            DrawRect(new Rect(map.x, map.y, map.width, 1f), MenuDivider);
            DrawRect(new Rect(map.x, map.y + map.height - 1f, map.width, 1f), MenuDivider);
            foreach (PrototypeLayout.Objective objective in PrototypeLayout.Objectives)
            {
                float mx = map.x + (objective.Position.x / PrototypeLayout.MapSize + .5f) *
                    map.width;
                float my = map.y + (objective.Position.y / PrototypeLayout.MapSize + .5f) *
                    map.height;
                DrawRect(new Rect(mx - 7f, my - 8f, 14f, 16f), MenuAccent);
                GUI.Label(new Rect(mx - 5f, my - 8f, 16f, 19f), objective.Id,
                    new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter });
            }
        }

        internal static Texture2D BuildTacticalMapTexture()
        {
            const int textureWidth = 256;
            const int textureHeight = 128;
            var pixels = new Color[textureWidth * textureHeight];
            for (int y = 0; y < textureHeight; y++)
            {
                float wz = (y / (float)(textureHeight - 1) - .5f) * PrototypeLayout.MapSize;
                for (int x = 0; x < textureWidth; x++)
                {
                    float wx = (x / (float)(textureWidth - 1) - .5f) * PrototypeLayout.MapSize;
                    float noise = Mathf.PerlinNoise(x * .055f + .8f, y * .11f + 1.7f);
                    Color ground = Color.Lerp(new Color(.075f, .115f, .075f),
                        new Color(.17f, .22f, .14f), noise);
                    float highway = Mathf.Abs(wz - wx * .62f - 6f);
                    float crossroad = Mathf.Abs(wx + wz * .35f + 58f);
                    if (highway < 11f || crossroad < 8f)
                        ground = new Color(.30f, .32f, .26f);
                    else if (highway < 16f || crossroad < 13f)
                        ground = new Color(.19f, .23f, .19f);
                    if (noise > .69f && highway > 19f && crossroad > 16f)
                        ground = new Color(.052f, .12f, .075f);
                    pixels[y * textureWidth + x] = ground;
                }
            }
            foreach (PrototypeLayout.Block block in PrototypeLayout.Blocks)
            {
                int minX = Mathf.Clamp(Mathf.FloorToInt((block.Position.x - block.Width / 2f) /
                    PrototypeLayout.MapSize * textureWidth + textureWidth / 2f), 0, textureWidth - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt((block.Position.x + block.Width / 2f) /
                    PrototypeLayout.MapSize * textureWidth + textureWidth / 2f), 0, textureWidth - 1);
                int minY = Mathf.Clamp(Mathf.FloorToInt((block.Position.y - block.Depth / 2f) /
                    PrototypeLayout.MapSize * textureHeight + textureHeight / 2f), 0, textureHeight - 1);
                int maxY = Mathf.Clamp(Mathf.CeilToInt((block.Position.y + block.Depth / 2f) /
                    PrototypeLayout.MapSize * textureHeight + textureHeight / 2f), 0, textureHeight - 1);
                Color roof = block.Kind == "factory" || block.Kind == "barracks"
                    ? new Color(.24f, .29f, .25f) : new Color(.33f, .34f, .27f);
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                        pixels[y * textureWidth + x] = roof;
            }
            var result = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false)
            {
                name = "Industrial Frontline menu map",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        private void DisposePreview()
        {
            if (menuPreview == null) return;
            menuPreview.Dispose();
            menuPreview = null;
        }

        private void SavePlayerName()
        {
            PlayerName = CleanPlayerName(PlayerName);
            PlayerPrefs.SetString(PlayerNameKey, PlayerName);
            PlayerPrefs.Save();
        }

        private static string CleanPlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Player";
            var result = new System.Text.StringBuilder(20);
            foreach (char value in name.Trim())
            {
                if (result.Length >= 20) break;
                if (!char.IsControl(value)) result.Append(value);
            }
            return result.Length == 0 ? "Player" : result.ToString();
        }

        // Returns a requested location. The runtime/host validates it again
        // when the deployment button is pressed.
        public string DrawDeployment(float canvasWidth, float canvasHeight,
            PrototypeRuntime game, bool includeVehicles, float waiting)
        {
            if (!IsOpen || IsDownedScreen || !redeploy || game == null) return null;
            EnsureStyles();
            List<PrototypeDeployment.Location> locations =
                PrototypeDeployment.GetLocations(game, game.Player.Team, includeVehicles);
            PrototypeDeployment.Location? selected = null;
            foreach (PrototypeDeployment.Location location in locations)
                if (location.Id == selectedLocation) { selected = location; break; }
            if (!selected.HasValue) selectedLocation = "BASE";

            DrawRect(new Rect(0f, 0f, canvasWidth, canvasHeight), Background);
            float x = (canvasWidth - 920f) * .5f;
            float y = (canvasHeight - 530f) * .5f;
            DrawRect(new Rect(x, y, 920f, 530f), Panel);
            DrawRect(new Rect(x, y, 920f, 4f), Accent);
            GUI.Label(new Rect(x + 24f, y + 15f, 570f, 44f),
                "TACTICAL DEPLOYMENT", logoStyle);
            GUI.Label(new Rect(x + 25f, y + 62f, 700f, 25f),
                "阵亡后选择部署位置  /  仅己方据点可用；交战据点在外围部署", smallStyle);

            Rect map = new Rect(x + 25f, y + 100f, 435f, 345f);
            DrawRect(map, Card);
            for (int i = 1; i < 6; i++)
            {
                DrawRect(new Rect(map.x + i * map.width / 6f, map.y,
                    1f, map.height), Divider);
                DrawRect(new Rect(map.x, map.y + i * map.height / 6f,
                    map.width, 1f), Divider);
            }
            foreach (PrototypeDeployment.Location location in locations)
            {
                if (location.Kind == "vehicle") continue;
                float mx = map.x + (location.Center.x / PrototypeLayout.MapSize + .5f) *
                    map.width;
                float my = map.y + (location.Center.y / PrototypeLayout.MapSize + .5f) *
                    map.height;
                Rect marker = new Rect(mx - 12f, my - 12f, 24f, 24f);
                DrawRect(marker, location.Id == selectedLocation ? Accent :
                    location.Available ? new Color(.24f, .62f, .70f) :
                    new Color(.43f, .38f, .38f));
                if (GUI.Button(marker, location.Id == "BASE" ? "⌂" :
                    location.Kind == "squad" ? "S" : location.Id))
                    selectedLocation = location.Id;
            }
            float listX = x + 485f;
            GUI.Label(new Rect(listX, y + 101f, 400f, 26f),
                "部署地点  /  SPAWN LOCATIONS", headingStyle);
            for (int i = 0; i < locations.Count && i < 11; i++)
            {
                PrototypeDeployment.Location location = locations[i];
                float rowY = y + 132f + i * 28f;
                Rect row = new Rect(listX, rowY, 405f, 26f);
                DrawRect(row, location.Id == selectedLocation ? SelectedCard :
                    location.Available ? Card : Panel);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                    selectedLocation = location.Id;
                GUI.Label(new Rect(row.x + 8f, row.y + 2f, 255f, 22f),
                    location.Name, smallStyle);
                GUI.Label(new Rect(row.x + 268f, row.y + 2f, 133f, 22f),
                    location.Reason, smallStyle);
            }
            foreach (PrototypeDeployment.Location location in locations)
                if (location.Id == selectedLocation) { selected = location; break; }
            DrawRect(new Rect(x + 25f, y + 476f, 866f, 1f), Divider);
            GUI.Label(new Rect(x + 25f, y + 451f, 860f, 22f),
                selected.HasValue ? selected.Value.Name + "  ·  " +
                    selected.Value.Reason : "请选择部署位置", smallStyle);

            if (includeVehicles)
            {
                GUI.Label(new Rect(x + 25f, y + 486f, 115f, 26f), "兵种", labelStyle);
                for (int i = 0; i < Classes.Length; i++)
                {
                    Rect role = new Rect(x + 85f + i * 98f, y + 484f, 94f, 32f);
                    DrawRect(role, SelectedClass == Classes[i] ? SelectedCard : Card);
                    if (GUI.Button(role, ClassNames[i])) SelectedClass = Classes[i];
                }
            }
            else
                GUI.Label(new Rect(x + 25f, y + 486f, 470f, 26f),
                    "LAN 玩家由房主验证部署；当前使用突击兵", smallStyle);
            Rect deploy = new Rect(x + 624f, y + 481f, 266f, 39f);
            bool ready = waiting <= 0f && selected.HasValue && selected.Value.Available;
            DrawRect(deploy, ready ? Accent : Divider);
            GUI.Label(deploy, waiting > 0f ?
                "复活准备  " + waiting.ToString("0.0") + " s" :
                "部署 / DEPLOY", buttonStyle);
            return ready && GUI.Button(deploy, GUIContent.none, GUIStyle.none) ?
                selectedLocation : null;
        }

        private void DrawClassCard(Rect rect, int index)
        {
            bool selected = SelectedClass == Classes[index];
            DrawRect(rect, selected ? SelectedCard : Card);
            if (selected)
            {
                DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), Accent);
                DrawRect(new Rect(rect.x + 4f, rect.y, rect.width - 4f, 1f), Accent);
            }
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) SelectedClass = Classes[index];

            GUI.Label(new Rect(rect.x + 18f, rect.y + 11f, 48f, 23f),
                (index + 1).ToString("00"), smallStyle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 35f, rect.width - 35f, 31f),
                ClassNames[index], cardTitleStyle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 77f, rect.width - 31f, 35f),
                ClassDescriptions[index], cardDescriptionStyle);
            if (selected) DrawRect(new Rect(rect.x + rect.width - 21f, rect.y + 17f, 8f, 8f), Accent);
        }

        private void HandleKeyboard(ref bool deploy)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown) return;
            if (GUI.GetNameOfFocusedControl() == "PlayerName") return;
            if (!redeploy && current.keyCode >= KeyCode.F1 && current.keyCode <= KeyCode.F4)
            {
                menuTab = (MenuTab)((int)current.keyCode - (int)KeyCode.F1);
                current.Use();
                return;
            }
            if (!redeploy && current.keyCode == KeyCode.L)
            {
                LanRequested = true;
                current.Use();
                return;
            }
            if (current.keyCode >= KeyCode.Alpha1 && current.keyCode <= KeyCode.Alpha4)
            {
                SelectedClass = Classes[(int)current.keyCode - (int)KeyCode.Alpha1];
                current.Use();
            }
            else if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                deploy = true;
                current.Use();
            }
        }

        private void EnsureStyles()
        {
            if (logoStyle != null) return;
            logoStyle = MakeStyle(39, FontStyle.Bold, Color.white);
            headingStyle = MakeStyle(19, FontStyle.Bold, Color.white);
            labelStyle = MakeStyle(15, FontStyle.Normal, Color.white);
            smallStyle = MakeStyle(13, FontStyle.Normal, Muted);
            smallStyle.richText = false;
            smallStyle.wordWrap = true;
            cardTitleStyle = MakeStyle(22, FontStyle.Bold, Color.white);
            cardDescriptionStyle = MakeStyle(13, FontStyle.Normal, Muted);
            cardDescriptionStyle.wordWrap = true;
            buttonStyle = MakeStyle(18, FontStyle.Bold, new Color(0.03f, 0.12f, 0.15f, 1f));
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            heroStyle = MakeStyle(48, FontStyle.Bold, Color.white);
            nameFieldStyle = new GUIStyle(GUIStyle.none)
            {
                fontSize = 19,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            nameFieldStyle.normal.textColor = Color.white;
            nameFieldStyle.focused.textColor = Color.white;
            darkButtonStyle = MakeStyle(19, FontStyle.Bold, StartBackground);
            darkButtonStyle.alignment = TextAnchor.MiddleCenter;
            darkButtonStyle.hover.textColor = StartBackground;
            darkButtonStyle.active.textColor = StartBackground;
            darkButtonStyle.focused.textColor = StartBackground;
            lightButtonStyle = MakeStyle(17, FontStyle.Bold, Color.white);
            lightButtonStyle.alignment = TextAnchor.MiddleCenter;
            menuTitleStyle = MakeStyle(31, FontStyle.Bold, Color.white);
            menuSubtitleStyle = MakeStyle(17, FontStyle.Normal, Muted);
            navStyle = MakeStyle(17, FontStyle.Normal, Muted);
            navStyle.alignment = TextAnchor.MiddleCenter;
            navSelectedStyle = MakeStyle(17, FontStyle.Bold, Color.white);
            navSelectedStyle.alignment = TextAnchor.MiddleCenter;
            statStyle = MakeStyle(13, FontStyle.Bold, MenuAccent);
        }

        private static GUIStyle MakeStyle(int size, FontStyle weight, Color textColor)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = weight,
                alignment = TextAnchor.UpperLeft
            };
            style.normal.textColor = textColor;
            return style;
        }

        private static int ClassIndex(PrototypeInfantryClass soldierClass)
        {
            for (int i = 0; i < Classes.Length; i++)
                if (Classes[i] == soldierClass) return i;
            return Classes.Length;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
