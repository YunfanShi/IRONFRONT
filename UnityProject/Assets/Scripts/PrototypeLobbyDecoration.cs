using UnityEngine;
using UnityEngine.UIElements;

namespace Ironfront.UnityPrototype
{
    public sealed class PrototypeLobbyDecoration : VisualElement
    {
        public PrototypeLobbyDecoration()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("calling-card-art");
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = contentRect.width, height = contentRect.height;
            if (width < 1f || height < 1f) return;
            Painter2D p = context.painter2D;
            p.lineWidth = .55f;
            p.strokeColor = new Color(.72f, .53f, .22f, .23f);
            for (int line = 0; line < 18; line++)
            {
                p.BeginPath();
                for (int step = 0; step <= 60; step++)
                {
                    float x = width * step / 60f;
                    float y = line * 7f - 28f + Mathf.Sin(x * .044f + line * .32f) * 10f + Mathf.Sin(x * .09f - line * .4f) * 4f;
                    Vector2 point = new Vector2(x, y);
                    if (step == 0) p.MoveTo(point); else p.LineTo(point);
                }
                p.Stroke();
            }
            p.fillColor = new Color(.79f, .65f, .35f, .24f);
            p.BeginPath(); p.MoveTo(new Vector2(width - 52f, height * .25f));
            p.LineTo(new Vector2(width - 17f, height * .25f));
            p.LineTo(new Vector2(width - 34f, height * .78f)); p.ClosePath(); p.Fill();
            p.strokeColor = new Color(.9f, .77f, .5f, .32f); p.lineWidth = .8f;
            p.BeginPath(); p.MoveTo(new Vector2(width - 35f, height * .25f));
            p.LineTo(new Vector2(width - 35f, height * .78f)); p.Stroke();
        }

        public static Texture2D MakeMissionShade()
        {
            var texture = new Texture2D(1, 128, TextureFormat.RGBA32, false)
            { name = "Lobby mission text legibility gradient", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < texture.height; y++)
                texture.SetPixel(0, y, new Color(.025f, .035f, .035f, Mathf.Lerp(.97f, .04f, Mathf.SmoothStep(0f, 1f, y / 105f))));
            texture.Apply(false, true); return texture;
        }

        public static Texture2D MakeButtonGrain()
        {
            var texture = new Texture2D(256, 64, TextureFormat.RGBA32, false)
            { name = "Lobby powder coat surface", wrapMode = TextureWrapMode.Clamp };
            var random = new System.Random(610);
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                    texture.SetPixel(x, y, new Color(1f, .98f, .90f, (float)random.NextDouble() * .045f + y / 63f * .045f));
            texture.Apply(false, true); return texture;
        }
    }
}
