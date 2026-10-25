using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // White, antialiased silhouettes remain legible over the weapon and smoke.
    public static class PrototypeScoreIcons
    {
        private static readonly Dictionary<PrototypeScoreNoticeKind, Texture2D> Cache =
            new Dictionary<PrototypeScoreNoticeKind, Texture2D>();

        public static Texture2D For(PrototypeScoreNoticeKind kind)
        {
            if (Cache.TryGetValue(kind, out Texture2D existing) && existing != null) return existing;
            var texture = new Texture2D(80, 80, TextureFormat.RGBA32, false)
            { name = "IRONFRONT score " + kind, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[6400];
            for (int y = 0; y < 80; y++) for (int x = 0; x < 80; x++)
            {
                int samples = 0;
                for (int sy = 0; sy < 2; sy++) for (int sx = 0; sx < 2; sx++)
                    if (Inside(kind, new Vector2((x + .25f + sx * .5f) / 40f - 1f,
                        (y + .25f + sy * .5f) / 40f - 1f))) samples++;
                pixels[y * 80 + x] = new Color(1f, 1f, 1f, samples * .25f);
            }
            texture.SetPixels(pixels); texture.Apply(false, true); Cache[kind] = texture;
            return texture;
        }

        private static bool Ellipse(Vector2 p, float x, float y, float rx, float ry) =>
            Mathf.Pow((p.x - x) / rx, 2f) + Mathf.Pow((p.y - y) / ry, 2f) <= 1f;
        private static bool Box(Vector2 p, float x, float y, float w, float h) =>
            Mathf.Abs(p.x - x) <= w * .5f && Mathf.Abs(p.y - y) <= h * .5f;
        private static bool Line(Vector2 p, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 d = b - a;
            return Vector2.Distance(p, a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude)) <= thickness;
        }

        private static bool Inside(PrototypeScoreNoticeKind kind, Vector2 p)
        {
            if (kind == PrototypeScoreNoticeKind.Kill)
            {
                bool shape = Ellipse(p, 0f, .18f, .58f, .59f) || Box(p, 0f, -.35f, .76f, .34f);
                if (Ellipse(p, -.23f, .13f, .145f, .14f) || Ellipse(p, .23f, .13f, .145f, .14f) ||
                    p.y < -.02f && p.y > -.22f && Mathf.Abs(p.x) < .08f ||
                    p.y < -.37f && (Mathf.Abs(p.x - .15f) < .03f || Mathf.Abs(p.x + .15f) < .03f)) return false;
                return shape;
            }
            if (kind == PrototypeScoreNoticeKind.Assist)
            {
                return Ellipse(p, -.30f, .38f, .18f, .20f) || Ellipse(p, .30f, .38f, .18f, .20f) ||
                    Box(p, -.3f, -.06f, .33f, .47f) || Box(p, .3f, -.06f, .33f, .47f) ||
                    Line(p, new Vector2(-.68f, -.45f), new Vector2(.52f, -.45f), .055f) ||
                    Line(p, new Vector2(.33f, -.26f), new Vector2(.54f, -.45f), .055f) ||
                    Line(p, new Vector2(.33f, -.64f), new Vector2(.54f, -.45f), .055f);
            }
            if (kind == PrototypeScoreNoticeKind.VehicleDestroyed || kind == PrototypeScoreNoticeKind.VehicleAssist)
            {
                Vector2 tank = p + new Vector2(0f, .21f);
                bool shape = Box(tank, 0f, -.11f, 1.35f, .32f) || Box(tank, -.1f, .16f, .64f, .29f) ||
                    Box(tank, .4f, .24f, .62f, .08f) ||
                    Ellipse(tank, -.45f, -.34f, .16f, .10f) || Ellipse(tank, 0f, -.34f, .16f, .10f) ||
                    Ellipse(tank, .45f, -.34f, .16f, .10f);
                if (kind == PrototypeScoreNoticeKind.VehicleDestroyed)
                    return shape || Line(p, new Vector2(-.32f, .40f), new Vector2(.32f, .78f), .055f) ||
                        Line(p, new Vector2(.32f, .40f), new Vector2(-.32f, .78f), .055f) ||
                        Line(p, new Vector2(0f, .35f), new Vector2(0f, .89f), .04f);
                return shape || Line(p, new Vector2(-.62f, .49f), new Vector2(-.18f, .72f), .055f) ||
                    Line(p, new Vector2(-.18f, .72f), new Vector2(-.18f, .48f), .055f) ||
                    Line(p, new Vector2(.62f, .49f), new Vector2(.18f, .72f), .055f) ||
                    Line(p, new Vector2(.18f, .72f), new Vector2(.18f, .48f), .055f);
            }
            if (kind == PrototypeScoreNoticeKind.Capture || kind == PrototypeScoreNoticeKind.CaptureProgress)
                return Box(p, -.33f, 0f, .09f, 1.35f) || Box(p, .08f, .36f, .73f, .45f);
            float diamond = Mathf.Abs(p.x) + Mathf.Abs(p.y);
            return diamond > .34f && diamond < .49f ||
                Box(p, 0f, 0f, .10f, .24f) || Box(p, 0f, 0f, .24f, .10f);
        }
    }
}
