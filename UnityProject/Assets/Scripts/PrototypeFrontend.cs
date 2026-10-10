using UnityEngine;
using System.Collections.Generic;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeDownedAction { None, Rescue, GiveUp }

    // IMGUI deploy screen. PrototypeRuntime owns the match lifecycle and calls Draw
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

        private GUIStyle logoStyle;
        private GUIStyle headingStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle cardDescriptionStyle;
        private GUIStyle buttonStyle;

        private bool redeploy;
        private string selectedLocation = "BASE";

        public bool IsOpen { get; private set; }
        public bool IsDownedScreen { get; private set; }
        public bool LanRequested { get; private set; }
        public PrototypeInfantryClass SelectedClass { get; private set; } = PrototypeInfantryClass.Assault;
        public string SelectedClassName => ClassNames[ClassIndex(SelectedClass)];

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

            DrawRect(new Rect(0f, 0f, canvasWidth, canvasHeight), Background);

            const float width = 920f;
            const float height = 530f;
            float x = (canvasWidth - width) * 0.5f;
            float y = (canvasHeight - height) * 0.5f;
            DrawRect(new Rect(x, y, width, height), Panel);
            DrawRect(new Rect(x, y, width, 4f), Accent);
            DrawRect(new Rect(x + 606f, y + 105f, 1f, 391f), Divider);
            DrawRect(new Rect(x + 20f, y + 95f, width - 40f, 1f), Divider);

            GUI.Label(new Rect(x + 24f, y + 17f, 425f, 49f), "IRONFRONT", logoStyle);
            GUI.Label(new Rect(x + 25f, y + 65f, 510f, 22f),
                "UNITY PROTOTYPE   /   FRONTLINE DEPLOYMENT", smallStyle);
            GUI.Label(new Rect(x + 644f, y + 39f, 248f, 27f),
                "SOLO CONQUEST  |  UNITY LAN", labelStyle);

            GUI.Label(new Rect(x + 25f, y + 111f, 400f, 28f),
                "SELECT YOUR CLASS", headingStyle);
            GUI.Label(new Rect(x + 25f, y + 414f, 560f, 42f),
                "Your role changes what you bring to the battlefield.\nPick a class, then deploy with your squad.", smallStyle);

            for (int i = 0; i < Classes.Length; i++)
            {
                float cardX = x + 25f + (i % 2) * 282f;
                float cardY = y + 148f + (i / 2) * 132f;
                DrawClassCard(new Rect(cardX, cardY, 270f, 119f), i);
            }

            float rightX = x + 628f;
            GUI.Label(new Rect(rightX, y + 113f, 260f, 31f), "MISSION BRIEF", headingStyle);
            GUI.Label(new Rect(rightX, y + 154f, 258f, 60f),
                "Capture the objectives.\nHold more sectors to drain enemy tickets.", labelStyle);
            DrawRect(new Rect(rightX, y + 227f, 260f, 1f), Divider);
            GUI.Label(new Rect(rightX, y + 240f, 258f, 26f), "FIELD CONTROLS", headingStyle);
            GUI.Label(new Rect(rightX, y + 272f, 258f, 158f),
                "WASD  Move\n" +
                "MOUSE  Aim\n" +
                "LMB  Fire\n" +
                "R  Reload\n" +
                "SHIFT  Sprint\n" +
                "SPACE  Jump\n" +
                "X  Class ability\n" +
                "E  Enter / exit vehicle\n" +
                "F1 / F2  Rover driver / gunner\n" +
                "T90  WASD drive / LMB cannon\n" +
                "ESC  Unlock cursor",
                smallStyle);

            Rect deployButton = new Rect(rightX, y + 438f, 260f, 52f);
            DrawRect(deployButton, Accent);
            string buttonLabel = redeploy ? "REDEPLOY  >" : "DEPLOY  >";
            GUI.Label(deployButton, buttonLabel, buttonStyle);
            bool deploy = GUI.Button(deployButton, GUIContent.none, GUIStyle.none);
            if (!redeploy && GUI.Button(new Rect(x + 25f, y + 463f, 270f, 37f),
                "LAN ROOM  /  CREATE OR JOIN  [L]")) LanRequested = true;
            HandleKeyboard(ref deploy);

            GUI.Label(new Rect(x + 25f, y + 504f, 845f, 20f),
                "SOLO OR UNITY LAN   //   LAN uses ASSAULT; the host controls the match.", smallStyle);

            if (!deploy) return false;
            Close();
            return true;
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
