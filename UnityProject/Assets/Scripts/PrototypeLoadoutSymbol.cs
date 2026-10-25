using UnityEngine;
using UnityEngine.UIElements;

namespace Ironfront.UnityPrototype
{
    // Resolution-independent insignia; avoids platform emoji/fallback glyphs.
    public sealed class PrototypeLoadoutSymbol : VisualElement
    {
        private PrototypeInfantryClass role;
        private readonly bool equipment;
        public PrototypeLoadoutSymbol(PrototypeInfantryClass value, bool equipment = false)
        {
            role = value; this.equipment = equipment;
            pickingMode = PickingMode.Ignore; AddToClassList("loadout-symbol-drawing");
            generateVisualContent += Draw;
        }
        public void SetRole(PrototypeInfantryClass value) { role = value; MarkDirtyRepaint(); }
        private void Draw(MeshGenerationContext context)
        {
            if (contentRect.width < 1 || contentRect.height < 1) return;
            Painter2D p = context.painter2D;
            Color ink = equipment ? new Color(.92f, .92f, .86f) : resolvedStyle.color;
            p.fillColor = ink; p.strokeColor = ink; p.lineWidth = Mathf.Min(contentRect.width, contentRect.height) / 15f;
            Vector2 P(float x, float y) => new Vector2(x * contentRect.width, y * contentRect.height);
            void Rect(float x, float y, float w, float h)
            { p.BeginPath(); p.MoveTo(P(x,y)); p.LineTo(P(x+w,y)); p.LineTo(P(x+w,y+h)); p.LineTo(P(x,y+h)); p.ClosePath(); p.Fill(); }
            void Line(float x, float y, float x2, float y2)
            { p.BeginPath(); p.MoveTo(P(x,y)); p.LineTo(P(x2,y2)); p.Stroke(); }
            switch (role)
            {
                case PrototypeInfantryClass.Medic:
                    Rect(.36f,.08f,.28f,.84f); Rect(.08f,.36f,.84f,.28f); break;
                case PrototypeInfantryClass.Recon:
                    p.BeginPath(); p.Arc(P(.5f,.5f), Mathf.Min(contentRect.width,contentRect.height)*.29f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                    Line(.5f,.03f,.5f,.97f); Line(.03f,.5f,.97f,.5f); break;
                case PrototypeInfantryClass.Engineer:
                    p.lineWidth *= 2.2f;
                    Line(.18f,.86f,.75f,.29f); Line(.20f,.22f,.79f,.81f);
                    p.lineWidth *= .65f;
                    Line(.57f,.13f,.57f,.35f); Line(.57f,.35f,.80f,.37f); Line(.80f,.37f,.94f,.22f);
                    Line(.08f,.34f,.34f,.08f); break;
                default:
                    if (equipment)
                    {
                        Rect(.12f,.27f,.76f,.62f); Rect(.18f,.12f,.64f,.10f);
                        p.fillColor = new Color(.12f,.14f,.13f);
                        for(int i=0;i<3;i++) Rect(.29f+i*.16f,.46f,.09f,.28f);
                    }
                    else
                    {
                        Rect(.09f,.1f,.82f,.1f);
                        for(int i=0;i<4;i++) Rect(.09f+i*.24f,.16f,.1f,.76f);
                    }
                    break;
            }
        }
    }
}
