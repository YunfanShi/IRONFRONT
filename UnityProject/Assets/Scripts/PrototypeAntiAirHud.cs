using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Draw-only seeker overlay: feedback audio is emitted by the lock's update
    // transitions, never by the multiple OnGUI passes used to render this HUD.
    public static class PrototypeAntiAirHud
    {
        private static GUIStyle title, detail, centered;
        private static readonly Color Search = new Color(.53f, .89f, .66f);
        private static readonly Color Acquiring = new Color(1f, .80f, .18f);
        private static readonly Color Locked = new Color(1f, .23f, .18f);

        public static void Draw(PrototypePlayer player, float width, float height)
        {
            if (player == null || !player.CanUseHeldEquipment ||
                player.HeldEquipment != PrototypeHeldEquipment.AntiAirLauncher ||
                player.ViewCamera == null || Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            bool aiming = player.IsAiming;
            IPrototypeVehicle target = aiming ? player.AntiAirLockTarget : null;
            bool ready = target != null && player.AntiAirLocked;
            Color color = ready ? Locked : target != null ? Acquiring : Search;
            if (aiming)
            {
                float focalPixels = height * .5f / Mathf.Tan(player.ViewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
                float retainRadius = focalPixels * Mathf.Tan(PrototypeAntiAirLock.RetentionHalfAngle * Mathf.Deg2Rad);
                float captureRadius = focalPixels * Mathf.Tan(PrototypeAntiAirLock.AimHalfAngle * Mathf.Deg2Rad);
                Vector2 middle = new Vector2(width * .5f, height * .5f);
                Color guide = Search; guide.a = .20f;
                Circle(middle, retainRadius, guide);
                guide.a = .32f;
                Corners(new Rect(middle.x - captureRadius, middle.y - captureRadius,
                    captureRadius * 2f, captureRadius * 2f), guide, 1f, 12f);
                if (target == null)
                {
                    guide.a = .12f;
                    float y = middle.y - captureRadius + Mathf.Repeat(Time.time * .5f, 1f) * captureRadius * 2f;
                    Fill(new Rect(middle.x - captureRadius * .76f, y, captureRadius * 1.52f, 1f), guide);
                }
                else if (TryTargetRect(player, width, height, out Rect frame, out float distance))
                {
                    Color pulse = color;
                    pulse.a = ready ? 1f : .74f + Mathf.Sin(Time.time * 7f) * .17f;
                    Corners(frame, pulse, 2f, Mathf.Min(22f, frame.width * .24f));
                    if (!ready)
                    {
                        pulse.a = .24f;
                        float scan = frame.y + Mathf.Repeat(Time.time * .85f, 1f) * frame.height;
                        Fill(new Rect(frame.x + 4f, scan, frame.width - 8f, 1f), pulse);
                    }
                    float flash = Mathf.Clamp01(1f - (Time.time - player.AntiAirLockCompletedAt) / .65f);
                    if (ready && flash > 0f)
                    {
                        pulse = Locked; pulse.a = flash * .7f;
                        Rect outside = frame; float expansion = flash * 12f;
                        outside.xMin -= expansion; outside.xMax += expansion;
                        outside.yMin -= expansion; outside.yMax += expansion;
                        Corners(outside, pulse, 2f, 17f);
                    }
                    Fill(new Rect(frame.x, frame.yMax + 5f, frame.width, 2f), new Color(color.r, color.g, color.b, .18f));
                    Fill(new Rect(frame.x, frame.yMax + 5f, frame.width * player.AntiAirLockProgress, 2f), color);
                    centered.normal.textColor = color;
                    float labelWidth = Mathf.Max(150f, frame.width);
                    GUI.Label(new Rect(frame.center.x - labelWidth * .5f, frame.yMax + 9f, labelWidth, 20f),
                        Mathf.RoundToInt(distance) + " m  ·  " + (ready ? "LOCKED" :
                            (player.AntiAirLockProgress * PrototypeAntiAirLock.RequiredSeconds).ToString("0.0") + " / 5.0 s"), centered);
                }
            }
            // Leave the central target view and earned-score burst clear.
            // The compact panel sits above the lower-right weapon cards.
            float panelX = width - 354f, panelY = height - 244f;
            Fill(new Rect(panelX, panelY, 340f, 72f), new Color(.012f, .03f, .026f, .66f));
            Fill(new Rect(panelX, panelY, 340f, 2f), color);
            title.normal.textColor = color; detail.normal.textColor = new Color(.88f, .94f, .89f);
            GUI.Label(new Rect(panelX + 12f, panelY + 7f, 220f, 21f), ready ? "锁定完成 / LOCKED" :
                target != null ? "正在锁定 / ACQUIRING" : "寻敌 / SEARCH", title);
            GUI.Label(new Rect(panelX + 232f, panelY + 7f, 103f, 21f),
                player.AntiAirZoom.ToString("0.#") + "×  [ T ]", title);
            GUI.Label(new Rect(panelX + 12f, panelY + 30f, 316f, 18f), target != null ? target.VehicleName :
                aiming ? "空中敌机 · 420 m · 右键持续瞄准 5 秒" : "右键瞄准 · T 切换 1.5× / 3×", detail);
            GUI.Label(new Rect(panelX + 12f, panelY + 50f, 316f, 18f), ready ?
                "左键 / P 发射  ·  " + player.AntiAirMissileCount + " 枚导弹" :
                "遮挡或热诱弹会中断锁定  ·  " + player.AntiAirMissileCount + " 枚导弹", detail);
        }

        // Viewport projection is converted directly to the Runtime's GUI canvas;
        // only the vehicle's actual hull collider defines bounds, never trails,
        // exhaust, radar effects or released flares.
        public static bool TryTargetRect(PrototypePlayer player, float width, float height,
            out Rect frame, out float distance)
        {
            frame = default; distance = 0f;
            IPrototypeVehicle target = player?.AntiAirLockTarget;
            Camera camera = player?.ViewCamera;
            if (target == null || !target.Alive || target.VehicleTransform == null || camera == null) return false;
            Collider hull = target.VehicleTransform.GetComponent<Collider>();
            Bounds bounds = hull != null ? hull.bounds : new Bounds(target.VehicleTransform.position + Vector3.up * 1.7f,
                new Vector3(3f, 2.5f, 5f));
            Vector3 center = camera.WorldToViewportPoint(bounds.center);
            if (center.z <= camera.nearClipPlane || center.x < 0f || center.x > 1f || center.y < 0f || center.y > 1f) return false;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 viewport = camera.WorldToViewportPoint(point);
                if (viewport.z <= camera.nearClipPlane) continue;
                Vector2 screen = new Vector2(viewport.x * width, (1f - viewport.y) * height);
                minimum = Vector2.Min(minimum, screen); maximum = Vector2.Max(maximum, screen);
            }
            if (float.IsInfinity(minimum.x)) return false;
            float boxWidth = Mathf.Clamp(maximum.x - minimum.x + 16f, 56f, 190f);
            float boxHeight = Mathf.Clamp(maximum.y - minimum.y + 16f, 44f, 138f);
            frame = new Rect(center.x * width - boxWidth * .5f,
                (1f - center.y) * height - boxHeight * .5f, boxWidth, boxHeight);
            distance = Vector3.Distance(camera.transform.position, bounds.center);
            return true;
        }

        private static void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            detail = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            centered = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
        private static void Circle(Vector2 middle, float radius, Color color)
        {
            const int segments = 64;
            Vector2 previous = middle + Vector2.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 next = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Line(previous, next, 1f, color); previous = next;
            }
        }
        private static void Corners(Rect box, Color color, float thickness, float arm)
        {
            Fill(new Rect(box.x, box.y, arm, thickness), color);
            Fill(new Rect(box.x, box.y, thickness, arm), color);
            Fill(new Rect(box.xMax - arm, box.y, arm, thickness), color);
            Fill(new Rect(box.xMax - thickness, box.y, thickness, arm), color);
            Fill(new Rect(box.x, box.yMax - thickness, arm, thickness), color);
            Fill(new Rect(box.x, box.yMax - arm, thickness, arm), color);
            Fill(new Rect(box.xMax - arm, box.yMax - thickness, arm, thickness), color);
            Fill(new Rect(box.xMax - thickness, box.yMax - arm, thickness, arm), color);
        }
        private static void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            Matrix4x4 old = GUI.matrix;
            Vector2 delta = b - a;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            // Pivot stays in logical canvas coordinates; the Runtime's scale
            // is applied once after this local rotation rather than feeding a
            // scaled pivot through GUIUtility.RotateAroundPivot again.
            GUI.matrix = old * Matrix4x4.Translate(new Vector3(a.x, a.y, 0f)) *
                Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, angle)) *
                Matrix4x4.Translate(new Vector3(-a.x, -a.y, 0f));
            Fill(new Rect(a.x, a.y - width * .5f, delta.magnitude, width), color);
            GUI.matrix = old;
        }
        private static void Fill(Rect box, Color color)
        {
            Color old = GUI.color; GUI.color = color;
            GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = old;
        }
    }
}
