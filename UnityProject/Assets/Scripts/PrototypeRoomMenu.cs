using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeRoomActionKind
    {
        None, Create, Join, Leave, Ready, Unready, Start, Blue, Red, Kick, MoveBlue, MoveRed,
        SetTickets
    }

    public struct PrototypeRoomAction
    {
        public PrototypeRoomActionKind Kind;
        public int TargetId;
        public PrototypeRoomAction(PrototypeRoomActionKind kind, int targetId = -1)
        {
            Kind = kind;
            TargetId = targetId;
        }
    }

    // The transport and the server both enforce permissions. This view only
    // exposes host controls to the host so the distinction is obvious to players.
    public sealed class PrototypeRoomMenu
    {
        public string Address = "http://127.0.0.1:7878";
        public string JoinCode = "";

        public PrototypeRoomAction Draw(float width, float height, bool connected,
            bool isHost, bool ready, string phase, string roomCode, string status,
            int localId, string[] roster, int[] rosterIds, bool[] rosterBlue, int tickets)
        {
            DrawRect(new Rect(0f, 0f, width, height),
                new Color(0.025f, 0.055f, 0.09f, 0.96f));
            float x = (width - 840f) * 0.5f;
            float y = (height - 540f) * 0.5f;
            DrawRect(new Rect(x, y, 840f, 540f),
                new Color(0.065f, 0.11f, 0.16f, 1f));
            DrawRect(new Rect(x, y, 840f, 4f),
                new Color(0.29f, 0.82f, 0.85f, 1f));
            DrawRect(new Rect(x + 24f, y + 92f, 792f, 1f),
                new Color(0.20f, 0.32f, 0.39f, 1f));
            var title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            var body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            title.normal.textColor = Color.white;
            body.normal.textColor = Color.white;
            GUI.Label(new Rect(x + 24f, y + 18f, 700f, 40f), "IRONFRONT  /  UNITY LAN ROOM", title);
            GUI.Label(new Rect(x + 25f, y + 58f, 770f, 26f),
                "HOST: start, settings, kick.  PLAYERS: own infantry.  LAN KIT: ASSAULT.", body);

            if (!connected)
            {
                GUI.Label(new Rect(x + 28f, y + 110f, 710f, 24f),
                    "LAN server address  /  Run npm run lan on the host computer", body);
                Address = GUI.TextField(new Rect(x + 28f, y + 138f, 620f, 31f), Address, 180);
                if (GUI.Button(new Rect(x + 28f, y + 188f, 240f, 42f), "CREATE ROOM"))
                    return new PrototypeRoomAction(PrototypeRoomActionKind.Create);
                GUI.Label(new Rect(x + 28f, y + 251f, 270f, 25f), "Six character room code", body);
                JoinCode = GUI.TextField(new Rect(x + 28f, y + 278f, 240f, 31f), JoinCode, 6)
                    .ToUpperInvariant();
                if (GUI.Button(new Rect(x + 286f, y + 275f, 210f, 37f), "JOIN ROOM"))
                    return new PrototypeRoomAction(PrototypeRoomActionKind.Join);
            }
            else
            {
                GUI.Label(new Rect(x + 28f, y + 108f, 700f, 30f),
                    "CODE  " + roomCode + "     PHASE  " + phase.ToUpperInvariant() +
                    "     ROLE  " + (isHost ? "HOST" : "PLAYER"), title);
                GUI.Label(new Rect(x + 28f, y + 148f, 380f, 24f),
                    "Connected players  /  host may move or remove players", body);
                int rows = roster == null ? 0 : Mathf.Min(roster.Length, 8);
                for (int i = 0; i < rows; i++)
                {
                    float rowY = y + 180f + i * 28f;
                    int id = rosterIds != null && i < rosterIds.Length ? rosterIds[i] : -1;
                    DrawRect(new Rect(x + 24f, rowY - 2f, 640f, 26f),
                        i % 2 == 0 ? new Color(0.10f, 0.17f, 0.23f, 1f) :
                        new Color(0.08f, 0.14f, 0.19f, 1f));
                    GUI.Label(new Rect(x + 29f, rowY, 360f, 25f), roster[i], body);
                    if (!isHost || id == localId || phase != "preparation") continue;
                    bool blue = rosterBlue != null && i < rosterBlue.Length && rosterBlue[i];
                    if (GUI.Button(new Rect(x + 430f, rowY, 130f, 24f), blue ? "MOVE RED" : "MOVE BLUE"))
                        return new PrototypeRoomAction(blue ? PrototypeRoomActionKind.MoveRed :
                            PrototypeRoomActionKind.MoveBlue, id);
                    if (GUI.Button(new Rect(x + 575f, rowY, 85f, 24f), "KICK"))
                        return new PrototypeRoomAction(PrototypeRoomActionKind.Kick, id);
                }
                GUI.Label(new Rect(x + 28f, y + 408f, 250f, 24f),
                    "MATCH TICKETS  " + tickets + "    /    AI SOLDIERS  8", body);
                if (isHost && phase == "preparation")
                {
                    int[] choices = { 100, 300, 500, 800 };
                    for (int i = 0; i < choices.Length; i++)
                        if (GUI.Button(new Rect(x + 370f + i * 83f, y + 405f, 76f, 27f),
                            choices[i].ToString()))
                            return new PrototypeRoomAction(PrototypeRoomActionKind.SetTickets,
                                choices[i]);
                }
                if (phase == "preparation")
                {
                    if (GUI.Button(new Rect(x + 28f, y + 452f, 115f, 38f), "BLUE"))
                        return new PrototypeRoomAction(PrototypeRoomActionKind.Blue);
                    if (GUI.Button(new Rect(x + 151f, y + 452f, 115f, 38f), "RED"))
                        return new PrototypeRoomAction(PrototypeRoomActionKind.Red);
                    if (GUI.Button(new Rect(x + 276f, y + 452f, 150f, 38f),
                        ready ? "UNREADY" : "READY"))
                        return new PrototypeRoomAction(ready ? PrototypeRoomActionKind.Unready :
                            PrototypeRoomActionKind.Ready);
                    if (isHost && GUI.Button(new Rect(x + 441f, y + 452f, 170f, 38f), "START MATCH"))
                        return new PrototypeRoomAction(PrototypeRoomActionKind.Start);
                }
            }
            if (GUI.Button(new Rect(x + 690f, y + 452f, 120f, 38f), "BACK"))
                return new PrototypeRoomAction(PrototypeRoomActionKind.Leave);
            GUI.Label(new Rect(x + 28f, y + 506f, 780f, 23f),
                string.IsNullOrEmpty(status) ? "Waiting for room action." : status, body);
            return new PrototypeRoomAction(PrototypeRoomActionKind.None);
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
