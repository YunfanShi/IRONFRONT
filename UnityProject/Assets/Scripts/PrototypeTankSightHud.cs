using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Pure observation of the current camera and actual cannon. This overlay
    // never steers the turret or supplies a substitute projectile origin.
    public static class PrototypeTankSightHud
    {
        public struct Reading
        {
            public float Distance, Range, SpeedKph, Elevation, Heading, ReadyIn;
            public bool HasRange;
            public Vector3 BoreViewport;
        }

        private static GUIStyle number, caption, status;
        private static readonly Color Ink = new Color(.83f, .91f, .84f, .88f);
        private static readonly Color Guide = new Color(.64f, .77f, .67f, .38f);

        public static Rect StatusPanel(float width, float height) =>
            new Rect(width * .5f - 125f,
                Mathf.Min(height * .72f + 5f, PrototypePlayerHealthHud.VehiclePanel(width, height).y - 28f), 250f, 22f);

        public static bool TryRead(PrototypeRuntime runtime, out Reading reading)
        {
            reading = default;
            PrototypePlayer player = runtime?.Player;
            if (player == null || !player.Alive || !player.IsFirstPerson ||
                !(player.CurrentVehicle is PrototypeTankVehicle tank) || tank.PlayerSeat != 0 ||
                !tank.Alive || player.ViewCamera == null || tank.WeaponMuzzle == null) return false;
            Camera camera = player.ViewCamera;
            Transform muzzle = tank.WeaponMuzzle;
            reading.Range = tank.WeaponRange;
            reading.Distance = reading.Range;
            foreach (RaycastHit hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward,
                reading.Range, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(tank.transform) || hit.collider.transform.IsChildOf(player.transform)) continue;
                if (hit.distance >= reading.Distance) continue;
                reading.HasRange = true; reading.Distance = hit.distance;
            }
            reading.SpeedKph = tank.Speed * 3.6f;
            reading.Elevation = Mathf.Asin(Mathf.Clamp(muzzle.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            reading.Heading = Mathf.Repeat(Mathf.Atan2(muzzle.forward.x, muzzle.forward.z) * Mathf.Rad2Deg, 360f);
            reading.ReadyIn = tank.CannonReadyIn;
            // A separate small marker shows where the real bore points while
            // the turret traverses toward the central optical sight line.
            reading.BoreViewport = camera.WorldToViewportPoint(muzzle.position + muzzle.forward * Mathf.Max(15f, reading.Distance));
            return true;
        }

        public static void Draw(PrototypeRuntime runtime, float width, float height)
        {
            if (runtime == null || Event.current == null || Event.current.type != EventType.Repaint || !runtime.MatchStarted ||
                runtime.PauseOpen || runtime.SupportMenuOpen || runtime.ScoreboardOpen || runtime.Match.Winner.HasValue ||
                runtime.Frontend != null && runtime.Frontend.IsOpen || !TryRead(runtime, out Reading value)) return;
            EnsureStyles();
            float cx = width * .5f, cy = height * .5f;
            float left = width * .30f, right = width * .70f;
            float top = height * .28f, bottom = height * .72f;
            string distance = value.HasRange ? Mathf.RoundToInt(value.Distance) + " m" : ">" + Mathf.RoundToInt(value.Range) + " m";
            GUI.Label(new Rect(cx - 90f, top - 29f, 180f, 26f), distance, number);
            GUI.Label(new Rect(cx - 90f, top - 6f, 180f, 18f), "距离 / RANGE", caption);
            Fill(new Rect(left, top - 8f, Mathf.Max(0f, cx - left - 100f), 1f), Guide);
            Fill(new Rect(cx + 100f, top - 8f, Mathf.Max(0f, right - cx - 100f), 1f), Guide);
            GUI.Label(new Rect(left - 60f, top + 34f, 120f, 24f), Mathf.Abs(value.SpeedKph).ToString("0") + "", number);
            GUI.Label(new Rect(left - 60f, top + 58f, 120f, 18f), value.SpeedKph < -.5f ? "KM/H · 倒车" : "KM/H", caption);
            GUI.Label(new Rect(right - 60f, top + 34f, 120f, 24f), value.Elevation.ToString("+0.0;-0.0;0.0") + "°", number);
            GUI.Label(new Rect(right - 60f, top + 58f, 120f, 18f), "炮管俯仰 / ELEV", caption);
            Ruler(left, top + 94f, bottom, Mathf.Clamp01(Mathf.Abs(value.SpeedKph) / 36f), true);
            Ruler(right, top + 94f, bottom, Mathf.InverseLerp(-10f, 25f, value.Elevation), false);
            // The main optical cross is stationary. Four separated arms leave
            // small targets visible and match the surrounding observation marks.
            Fill(new Rect(cx - 34f, cy, 24f, 1f), Ink); Fill(new Rect(cx + 10f, cy, 24f, 1f), Ink);
            Fill(new Rect(cx, cy - 22f, 1f, 12f), Ink); Fill(new Rect(cx, cy + 10f, 1f, 12f), Ink);
            Vector3 point = value.BoreViewport;
            if (point.z > 0f && point.x > .08f && point.x < .92f && point.y > .08f && point.y < .92f)
            {
                float x = point.x * width, y = (1f - point.y) * height;
                Fill(new Rect(x - 4f, y - 4f, 8f, 1f), Ink); Fill(new Rect(x - 4f, y + 3f, 8f, 1f), Ink);
                Fill(new Rect(x - 4f, y - 4f, 1f, 8f), Ink); Fill(new Rect(x + 3f, y - 4f, 1f, 8f), Ink);
            }
            GUI.Label(StatusPanel(width, height),
                "方位 " + value.Heading.ToString("000") + "°  ·  " +
                (value.ReadyIn > 0f ? "装填 " + value.ReadyIn.ToString("0.0") + " s" : "主炮就绪"), status);
        }

        private static void Ruler(float x, float top, float bottom, float value, bool left)
        {
            float height = Mathf.Max(12f, bottom - top);
            Fill(new Rect(x, top, 1f, height), Guide);
            for (int tick = 0; tick <= 6; tick++)
                Fill(new Rect(left ? x - 6f : x, top + height * tick / 6f, 6f, 1f), Guide);
            Fill(new Rect(x - 3f, bottom - Mathf.Clamp01(value) * height - 2f, 7f, 4f), Ink);
        }
        private static void EnsureStyles()
        {
            if (number != null) return;
            number = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            caption = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            status = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            number.normal.textColor = caption.normal.textColor = status.normal.textColor = Ink;
        }
        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }
    }
}
