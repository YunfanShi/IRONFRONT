using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ironfront.UnityPrototype
{
    [Serializable]
    public sealed class PrototypeIncomingDamage
    {
        public int sequence;
        public float amount, x, y, z, age;
        public bool hasSource;
        [NonSerialized] private float recordedAt;
        public Vector3? Source => hasSource && float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z) ?
            new Vector3(x, y, z) : (Vector3?)null;

        public static PrototypeIncomingDamage Record(PrototypeIncomingDamage previous, float amount, Vector3? source)
        {
            Vector3 p = source ?? Vector3.zero;
            return new PrototypeIncomingDamage { sequence = (previous?.sequence ?? 0) + 1,
                amount = amount, hasSource = source.HasValue, x = p.x, y = p.y, z = p.z, recordedAt = Time.time };
        }

        public PrototypeIncomingDamage Snapshot() => new PrototypeIncomingDamage {
            sequence = sequence, amount = amount, hasSource = hasSource, x = x, y = y, z = z,
            age = Mathf.Max(0f, Time.time - recordedAt) };
    }

    [DefaultExecutionOrder(200)]
    public sealed class PrototypeDamageFeedback : MonoBehaviour, IDisposable
    {
        public const float FlashSeconds = .48f;
        public const float DirectionSeconds = 1.1f;
        private struct Impact { public Vector3 direction; public float at; public bool used; }
        private readonly Impact[] impacts = new Impact[4];
        private PrototypeRuntime runtime;
        private PrototypePlayer player;
        private Func<float> clock;
        private GameObject host;
        private PanelSettings panel;
        private DamageOverlay overlay;
        private float flashAt, flashPeak;
        private int networkSequence;
        private bool networkInitialized, drawn;
        public int HitsObserved { get; private set; }
        private float Now => clock != null ? clock() : Time.unscaledTime;
        private bool Active => runtime != null && player != null && player.Alive && runtime.MatchStarted &&
            !runtime.PauseOpen && !runtime.SupportMenuOpen && !runtime.Match.Winner.HasValue &&
            (runtime.Frontend == null || !runtime.Frontend.IsOpen);
        public float FlashOpacity => Active ? flashPeak * Mathf.Pow(Mathf.Clamp01(1f - (Now - flashAt) / FlashSeconds), 2f) : 0f;
        public int DirectionCount { get { int n = 0; for (int i = 0; i < impacts.Length; i++) if (DirectionAlpha(i) > 0f) n++; return n; } }
        public VisualElement Root => overlay;

        public void Initialize(PrototypeRuntime game, PrototypePlayer recipient, Func<float> time = null)
        {
            Dispose(); runtime = game; player = recipient; clock = time;
            networkSequence = 0; networkInitialized = false; HitsObserved = 0;
            panel = Instantiate(Resources.Load<PanelSettings>("Menu/StartMenuPanel"));
            panel.name = "Incoming damage overlay panel"; panel.sortingOrder = 80;
            host = new GameObject("Incoming damage overlay"); host.transform.SetParent(transform, false);
            host.SetActive(false);
            var document = host.AddComponent<UIDocument>(); document.panelSettings = panel; document.sortingOrder = 80;
            host.SetActive(true);
            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.styleSheets.Add(Resources.Load<StyleSheet>("Menu/DamageFeedback"));
            root.AddToClassList("damage-overlay");
            overlay = new DamageOverlay(this); overlay.AddToClassList("damage-overlay"); root.Add(overlay);
        }

        public void Notify(float healthLost, Vector3? source)
        {
            if (!Active || !float.IsFinite(healthLost) || healthLost <= 0f) return;
            HitsObserved++;
            flashPeak = Mathf.Min(.68f, Mathf.Max(FlashOpacity, .30f + Mathf.Clamp01(healthLost / 40f) * .24f) + FlashOpacity * .18f);
            flashAt = Now;
            if (!source.HasValue) return;
            Vector3 direction = source.Value - player.transform.position; direction.y = 0f;
            if (!float.IsFinite(direction.x) || !float.IsFinite(direction.z) || direction.sqrMagnitude < .04f) return;
            direction.Normalize();
            int slot = -1; float oldest = float.PositiveInfinity;
            for (int i = 0; i < impacts.Length; i++)
            {
                if (DirectionAlpha(i) > 0f && Vector3.Dot(direction, impacts[i].direction) > .92f) { slot = i; break; }
                if (!impacts[i].used || Now - impacts[i].at >= DirectionSeconds) { slot = i; oldest = float.NegativeInfinity; }
                else if (impacts[i].at < oldest) { oldest = impacts[i].at; slot = i; }
            }
            impacts[slot] = new Impact { direction = direction, at = Now, used = true };
        }

        // Optional host data: old hosts still get an undirected flash from HP loss.
        // Seed the first snapshot, consume suppressed events and never replay one packet.
        public void ReceiveSnapshot(PrototypeIncomingDamage damage, float previousHealth, float health)
        {
            int sequence = damage?.sequence ?? 0;
            bool fresh = sequence > networkSequence;
            networkSequence = Mathf.Max(networkSequence, sequence);
            if (!networkInitialized || previousHealth <= 0f || health <= 0f || health > previousHealth)
            { networkInitialized = true; Clear(); return; }
            if (health >= previousHealth) return;
            if (damage != null && sequence > 0 && !fresh) return;
            bool known = fresh && damage != null && float.IsFinite(damage.age) && damage.age >= 0f && damage.age < DirectionSeconds;
            Notify(previousHealth - health, known ? damage.Source : null);
        }

        public float DirectionAlpha(int index)
        {
            if (!Active || index < 0 || index >= impacts.Length || !impacts[index].used) return 0f;
            return Mathf.Clamp01((DirectionSeconds - (Now - impacts[index].at)) / .65f);
        }

        public float DirectionAngle(int index)
        {
            Vector3 forward = player.ViewCamera.transform.forward; forward.y = 0f;
            if (forward.sqrMagnitude < .001f) forward = player.transform.forward;
            forward.Normalize(); Vector3 right = Vector3.Cross(Vector3.up, forward);
            return Mathf.Atan2(Vector3.Dot(impacts[index].direction, right), Vector3.Dot(impacts[index].direction, forward)) * Mathf.Rad2Deg;
        }

        private void LateUpdate() => Tick();
        public void Tick()
        {
            if (!Active) Clear();
            bool visible = FlashOpacity > .001f || DirectionCount > 0;
            if ((visible || drawn) && overlay != null) overlay.MarkDirtyRepaint();
            drawn = visible;
        }

        public void Clear()
        {
            flashPeak = 0f;
            for (int i = 0; i < impacts.Length; i++) impacts[i] = default;
            if (drawn && overlay != null) overlay.MarkDirtyRepaint();
            drawn = false;
        }
        public void Dispose()
        {
            Clear();
            if (host != null) { host.SetActive(false); Destroy(host); }
            if (panel != null) Destroy(panel);
            host = null; panel = null; overlay = null; runtime = null; player = null;
        }
        private void OnDisable() => Clear();
        private void OnDestroy() => Dispose();

        private sealed class DamageOverlay : VisualElement
        {
            private readonly PrototypeDamageFeedback feedback;
            public DamageOverlay(PrototypeDamageFeedback owner)
            { feedback = owner; pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }

            private void Draw(MeshGenerationContext context)
            {
                float w = contentRect.width, h = contentRect.height;
                if (w < 1f || h < 1f) return;
                float flash = feedback.FlashOpacity;
                if (flash > .001f)
                {
                    const int nx = 32, ny = 24;
                    MeshWriteData mesh = context.Allocate((nx + 1) * (ny + 1), nx * ny * 6);
                    for (int y = 0; y <= ny; y++) for (int x = 0; x <= nx; x++)
                    {
                        float u = (float)x / nx, v = (float)y / ny;
                        float edge = Mathf.Pow(Mathf.Pow(Mathf.Abs(u * 2f - 1f), 6f) + Mathf.Pow(Mathf.Abs(v * 2f - 1f), 6f), 1f / 6f);
                        float alpha = flash * Mathf.Pow(Mathf.Clamp01((edge - .64f) / .36f), 2f);
                        mesh.SetNextVertex(new Vertex { position = new Vector3(u * w, v * h, Vertex.nearZ), tint = new Color(.85f, .025f, .025f, alpha) });
                    }
                    for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
                    {
                        ushort a = (ushort)(y * (nx + 1) + x), b = (ushort)(a + 1), c = (ushort)(a + nx + 1), d = (ushort)(c + 1);
                        mesh.SetNextIndex(a); mesh.SetNextIndex(b); mesh.SetNextIndex(c);
                        mesh.SetNextIndex(b); mesh.SetNextIndex(d); mesh.SetNextIndex(c);
                    }
                }
                Painter2D p = context.painter2D;
                Vector2 center = new Vector2(w * .5f, h * .5f);
                float scale = Mathf.Min(w / 960f, h / 640f), radius = 92f * scale;
                for (int i = 0; i < feedback.impacts.Length; i++)
                {
                    float alpha = feedback.DirectionAlpha(i); if (alpha <= 0f) continue;
                    float angle = feedback.DirectionAngle(i) - 90f;
                    p.lineWidth = 8f * scale; p.strokeColor = new Color(.12f, 0f, 0f, alpha * .8f);
                    p.BeginPath(); p.Arc(center, radius, Angle.Degrees(angle - 20f), Angle.Degrees(angle + 20f)); p.Stroke();
                    p.lineWidth = 4f * scale; p.strokeColor = new Color(1f, .16f, .10f, alpha);
                    p.BeginPath(); p.Arc(center, radius, Angle.Degrees(angle - 20f), Angle.Degrees(angle + 20f)); p.Stroke();
                    float radians = angle * Mathf.Deg2Rad;
                    Vector2 outward = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    Vector2 tangent = new Vector2(-outward.y, outward.x);
                    p.fillColor = new Color(1f, .22f, .14f, alpha);
                    p.BeginPath(); p.MoveTo(center + outward * (radius + 11f * scale));
                    p.LineTo(center + outward * (radius + 3f * scale) + tangent * (5f * scale));
                    p.LineTo(center + outward * (radius + 3f * scale) - tangent * (5f * scale)); p.ClosePath(); p.Fill();
                }
            }
        }
    }
}
