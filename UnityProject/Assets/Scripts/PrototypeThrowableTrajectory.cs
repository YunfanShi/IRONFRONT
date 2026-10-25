using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Prediction uses the same fixed-step gravity and radius as the physical
    // throwable. The dotted line stops at the first contact, before any bounce.
    public sealed class PrototypeThrowableTrajectory : MonoBehaviour
    {
        public const float ThrowSpeed = 17f;
        public const float UpwardBoost = 5f;
        public const float FuseSeconds = 1.8f;
        public const float CollisionRadius = .13f;
        private const int MaximumDashes = 180;
        private readonly List<Vector3> points = new List<Vector3>(192);
        private readonly List<LineRenderer> dashes = new List<LineRenderer>(MaximumDashes);
        private Transform owner;
        private Material material;
        public bool IsVisible => gameObject.activeSelf && points.Count > 1;
        public int PointCount => points.Count;
        public IReadOnlyList<Vector3> Points => points;
        public bool ContactPredicted { get; private set; }
        public Vector3 EndPoint => points.Count == 0 ? Vector3.zero : points[points.Count - 1];

        public void Initialize(Transform shooter, Material lineMaterial)
        {
            owner = shooter;
            material = lineMaterial;
            gameObject.layer = 2;
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                points.Clear();
                ContactPredicted = false;
                foreach (LineRenderer dash in dashes) dash.enabled = false;
            }
            gameObject.SetActive(visible);
        }

        public void UpdatePath(Vector3 origin, Vector3 velocity, float duration = FuseSeconds)
        {
            SetVisible(true);
            ContactPredicted = Predict(origin, velocity, Physics.gravity,
                Time.fixedDeltaTime, duration, owner, points);
            int used = 0;
            float travelled = 0f;
            const float period = .72f;
            const float length = .36f;
            for (int i = 1; i < points.Count && used < MaximumDashes; i++)
            {
                Vector3 start = points[i - 1];
                Vector3 delta = points[i] - start;
                float distance = delta.magnitude;
                if (distance < .00001f) continue;
                float cursor = 0f;
                while (cursor < distance && used < MaximumDashes)
                {
                    float phase = Mathf.Repeat(travelled + cursor, period);
                    bool draw = phase < length;
                    float amount = Mathf.Min(distance - cursor,
                        (draw ? length : period) - phase);
                    if (amount < .00001f) amount = Mathf.Min(.00001f, distance - cursor);
                    if (draw)
                    {
                        LineRenderer dash = GetDash(used++);
                        dash.enabled = true;
                        dash.SetPosition(0, start + delta * (cursor / distance));
                        dash.SetPosition(1, start + delta * ((cursor + amount) / distance));
                    }
                    cursor += amount;
                }
                travelled += distance;
            }
            for (int i = used; i < dashes.Count; i++) dashes[i].enabled = false;
        }

        private LineRenderer GetDash(int index)
        {
            while (dashes.Count <= index)
            {
                var holder = new GameObject("Trajectory dash");
                holder.transform.SetParent(transform, false);
                holder.layer = 2;
                LineRenderer dash = holder.AddComponent<LineRenderer>();
                dash.sharedMaterial = material;
                dash.positionCount = 2;
                dash.useWorldSpace = true;
                dash.startWidth = dash.endWidth = .028f;
                dash.numCapVertices = 2;
                dash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dash.receiveShadows = false;
                dashes.Add(dash);
            }
            return dashes[index];
        }

        public static bool Predict(Vector3 origin, Vector3 velocity, Vector3 gravity,
            float fixedStep, float duration, Transform shooter, List<Vector3> output)
        {
            output.Clear();
            output.Add(origin);
            fixedStep = Mathf.Clamp(fixedStep, .001f, .1f);
            Physics.SyncTransforms();
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float dt = Mathf.Min(fixedStep, duration - elapsed);
                velocity += gravity * dt;
                Vector3 next = origin + velocity * dt;
                Vector3 delta = next - origin;
                float distance = delta.magnitude;
                float nearest = distance;
                bool contact = false;
                if (distance > .000001f)
                    foreach (RaycastHit hit in Physics.SphereCastAll(origin, CollisionRadius,
                        delta / distance, distance, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    {
                        if (shooter != null && hit.collider.transform.IsChildOf(shooter) ||
                            hit.distance > nearest) continue;
                        nearest = hit.distance;
                        contact = true;
                    }
                if (contact)
                {
                    output.Add(origin + delta.normalized * nearest);
                    return true;
                }
                origin = next;
                output.Add(origin);
                elapsed += dt;
            }
            return false;
        }
    }
}
