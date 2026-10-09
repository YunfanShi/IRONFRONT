using UnityEngine;

namespace Ironfront.UnityPrototype
{
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

        public bool IsOpen { get; private set; }
        public PrototypeInfantryClass SelectedClass { get; private set; } = PrototypeInfantryClass.Assault;
        public string SelectedClassName => ClassNames[ClassIndex(SelectedClass)];

        public void Open(bool isRespawn = false)
        {
            redeploy = isRespawn;
            IsOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            IsOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
                "LOCAL CONQUEST  |  BLUE TEAM", labelStyle);

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
            GUI.Label(new Rect(rightX, y + 272f, 258f, 143f),
                "WASD  Move\n" +
                "MOUSE  Aim\n" +
                "LMB  Fire\n" +
                "R  Reload\n" +
                "SHIFT  Sprint\n" +
                "SPACE  Jump\n" +
                "X  Class ability\n" +
                "ESC  Unlock cursor",
                smallStyle);

            Rect deployButton = new Rect(rightX, y + 438f, 260f, 52f);
            DrawRect(deployButton, Accent);
            string buttonLabel = redeploy ? "REDEPLOY  >" : "DEPLOY  >";
            GUI.Label(deployButton, buttonLabel, buttonStyle);
            bool deploy = GUI.Button(deployButton, GUIContent.none, GUIStyle.none);
            HandleKeyboard(ref deploy);

            GUI.Label(new Rect(x + 25f, y + 504f, 845f, 20f),
                "SOLO PROTOTYPE   //   More maps, vehicles and modes are in development.", smallStyle);

            if (!deploy) return false;
            Close();
            return true;
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
