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

        private static readonly Color Background = new Color(0.025f, 0.055f, 0.09f, 0.96f);
        private static readonly Color Panel = new Color(0.065f, 0.11f, 0.16f, 1f);
        private static readonly Color Card = new Color(0.10f, 0.17f, 0.23f, 1f);
        private static readonly Color SelectedCard = new Color(0.12f, 0.29f, 0.37f, 1f);
        private static readonly Color Accent = new Color(0.29f, 0.82f, 0.85f, 1f);
        private static readonly Color Muted = new Color(0.62f, 0.72f, 0.78f, 1f);
        private static readonly Color Divider = new Color(0.20f, 0.32f, 0.39f, 1f);
        private static readonly Color StartBackground = new Color(0.025f, 0.045f, 0.065f, 1f);
        private static readonly Color WarmAccent = new Color(0.95f, 0.53f, 0.30f, 1f);
        private const string PlayerNameKey = "ironfront.unity.playerName";

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

        private bool redeploy;
        private string selectedLocation = "BASE";

        public bool IsOpen { get; private set; }
        public bool IsDownedScreen { get; private set; }
        public bool LanRequested { get; private set; }
        public PrototypeInfantryClass SelectedClass { get; private set; } = PrototypeInfantryClass.Assault;
        public string SelectedClassName => ClassNames[ClassIndex(SelectedClass)];
        public string PlayerName { get; private set; } = "Player";

        public PrototypeFrontend()
        {
            PlayerName = CleanPlayerName(PlayerPrefs.GetString(PlayerNameKey, "Player"));
        }

        public void Open(bool isRespawn = false)
        {
            redeploy = isRespawn;
            IsDownedScreen = false;
            if (isRespawn) selectedLocation = "BASE";
            LanRequested = false;
            IsOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            IsOpen = false;
            IsDownedScreen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
            EnsureStyles();
            Event inputEvent = Event.current;
            if (inputEvent != null && inputEvent.type == EventType.KeyDown &&
                (inputEvent.keyCode == KeyCode.Return || inputEvent.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "PlayerName")
            {
                GUI.FocusControl(null);
                inputEvent.Use();
            }
            // Both columns and the background use the logical canvas bounds, so the
            // opening screen covers every aspect ratio rather than a centered card.
            DrawRect(new Rect(0f, 0f, canvasWidth, canvasHeight), StartBackground);
            float panelX = canvasWidth - 390f;
            float panelY = 72f;
            float panelH = canvasHeight - 124f;
            DrawRect(new Rect(0f, 0f, canvasWidth, 5f), Accent);
            DrawRect(new Rect(panelX - 21f, 0f, canvasWidth - panelX + 21f, canvasHeight),
                new Color(.045f, .075f, .10f, 1f));
            DrawRect(new Rect(0f, canvasHeight - 56f, canvasWidth, 1f), Divider);
            GUI.Label(new Rect(38f, 23f, 310f, 30f), "IF  /  IRONFRONT", headingStyle);
            GUI.Label(new Rect(canvasWidth - 315f, 27f, 280f, 20f),
                "UNITY EDITION     //     0.31.0", smallStyle);

            float heroW = panelX - 76f;
            GUI.Label(new Rect(39f, 109f, heroW, 25f),
                "OPERATION 01   /   FRONTLINE", smallStyle);
            GUI.Label(new Rect(34f, 145f, heroW + 28f, 93f), "IRONFRONT", heroStyle);
            DrawRect(new Rect(39f, 242f, 70f, 4f), WarmAccent);
            GUI.Label(new Rect(39f, 265f, heroW, 68f),
                "五个据点，一条战线。\n选择你的身份，进入持续争夺的战场。", labelStyle);

            // A quiet tactical map provides atmosphere without loading extra art.
            float mapY = canvasHeight - 236f;
            float mapH = 155f;
            DrawRect(new Rect(39f, mapY, heroW, mapH),
                new Color(.055f, .10f, .13f, 1f));
            for (int i = 1; i < 6; i++)
                DrawRect(new Rect(39f + i * heroW / 6f, mapY, 1f, mapH), Divider);
            for (int i = 1; i < 4; i++)
                DrawRect(new Rect(39f, mapY + i * mapH / 4f, heroW, 1f), Divider);
            float routeY = mapY + mapH * .52f;
            DrawRect(new Rect(66f, routeY, heroW - 54f, 2f), Accent);
            for (int i = 0; i < 5; i++)
            {
                float markerX = 71f + i * (heroW - 63f) / 4f;
                DrawRect(new Rect(markerX - 8f, routeY - 7f, 17f, 17f),
                    i == 2 ? WarmAccent : Accent);
                GUI.Label(new Rect(markerX - 7f, routeY - 29f, 32f, 20f),
                    ((char)('A' + i)).ToString(), smallStyle);
            }
            GUI.Label(new Rect(54f, mapY + 11f, heroW - 30f, 21f),
                "TACTICAL THEATER  /  05 CONTROL SECTORS", smallStyle);
            GUI.Label(new Rect(39f, canvasHeight - 42f, heroW, 20f),
                "SOLO  /  AI BATTLEFIELD          LAN  /  HOST CONTROLLED", smallStyle);

            float contentX = panelX + 20f;
            float contentW = 342f;
            DrawRect(new Rect(panelX, panelY, 382f, panelH), Panel);
            DrawRect(new Rect(panelX, panelY, 382f, 3f), WarmAccent);
            GUI.Label(new Rect(contentX, panelY + 20f, contentW, 28f),
                "准备出击", headingStyle);
            GUI.Label(new Rect(contentX, panelY + 54f, contentW, 42f),
                "输入玩家代号，选择单人兵种；\n联机房间中将显示你的代号。", smallStyle);

            GUI.Label(new Rect(contentX, panelY + 101f, contentW, 23f),
                "PLAYER NAME  /  玩家代号", smallStyle);
            Rect nameRect = new Rect(contentX, panelY + 126f, contentW, 42f);
            DrawRect(nameRect, Card);
            GUI.SetNextControlName("PlayerName");
            PlayerName = GUI.TextField(new Rect(nameRect.x + 12f, nameRect.y + 7f,
                nameRect.width - 24f, 29f), PlayerName, 20, nameFieldStyle);
            GUI.Label(new Rect(contentX, panelY + 181f, contentW, 24f),
                "SOLO CLASS  /  单人兵种", smallStyle);
            float roleY = panelY + 210f;
            for (int i = 0; i < Classes.Length; i++)
            {
                Rect role = new Rect(contentX + (i % 2) * 173f,
                    roleY + (i / 2) * 51f, 169f, 45f);
                DrawRect(role, SelectedClass == Classes[i] ? SelectedCard : Card);
                if (SelectedClass == Classes[i])
                    DrawRect(new Rect(role.x, role.y, 3f, role.height), Accent);
                GUI.Label(new Rect(role.x + 12f, role.y + 9f, 150f, 29f),
                    (i + 1).ToString("00") + "  " + ClassNames[i], labelStyle);
                if (GUI.Button(role, GUIContent.none, GUIStyle.none)) SelectedClass = Classes[i];
            }

            float actionY = panelY + panelH - 112f;
            Rect start = new Rect(contentX, actionY, contentW, 48f);
            DrawRect(start, Accent);
            GUI.Label(start, "开始游戏   /   SOLO  →", darkButtonStyle);
            bool deploy = GUI.Button(start, GUIContent.none, GUIStyle.none);
            Rect lanButton = new Rect(contentX, actionY + 55f, contentW, 43f);
            DrawRect(lanButton, Card);
            GUI.Label(lanButton, "联机房间   /   CREATE OR JOIN  →", lightButtonStyle);
            if (GUI.Button(lanButton, GUIContent.none, GUIStyle.none)) LanRequested = true;
            HandleKeyboard(ref deploy);
            if (deploy || LanRequested) SavePlayerName();
            if (!deploy) return false;
            Close();
            return true;
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
            heroStyle = MakeStyle(66, FontStyle.Bold, Color.white);
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
