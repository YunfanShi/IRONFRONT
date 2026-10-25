using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Owned by the current Runtime, so reloads discard the entire pool. Inactive
    // lines are bounded; a burst may grow active lines without dropping shots.
    public sealed class PrototypeTracerPool : MonoBehaviour
    {
        private const int WarmCount = 64;
        private const int RetainedLimit = 256;
        private static readonly ProfilerMarker SpawnMarker = new ProfilerMarker("IRONFRONT.Tracer.Spawn");
        private readonly Stack<LineRenderer> available = new Stack<LineRenderer>(RetainedLimit);
        private readonly List<ActiveLine> active = new List<ActiveLine>(WarmCount);
        public int ActiveCount => active.Count;
        public int RetainedCount => available.Count;
        private struct ActiveLine
        {
            public LineRenderer Line;
            public float Expires;
        }

        private void Awake()
        {
            for (int i = 0; i < WarmCount; i++) available.Push(Create());
        }

        private LineRenderer Create()
        {
            var item = new GameObject("Tracer");
            item.transform.SetParent(transform, false);
            var line = item.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = .035f;
            line.endWidth = .012f;
            line.numCapVertices = 2;
            item.SetActive(false);
            return line;
        }

        public void Show(Vector3 from, Vector3 to, Material material)
        {
            using (SpawnMarker.Auto())
            {
                LineRenderer line = null;
                while (available.Count > 0 && line == null) line = available.Pop();
                if (line == null) line = Create();
                line.sharedMaterial = material;
                line.SetPosition(0, from); line.SetPosition(1, to);
                line.gameObject.SetActive(true);
                active.Add(new ActiveLine { Line = line, Expires = Time.time + .09f });
            }
        }

        private void LateUpdate()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ActiveLine item = active[i];
                if (item.Line != null && Time.time < item.Expires) continue;
                int last = active.Count - 1;
                active[i] = active[last]; active.RemoveAt(last);
                if (item.Line == null) continue;
                item.Line.gameObject.SetActive(false);
                if (available.Count < RetainedLimit) available.Push(item.Line);
                else Destroy(item.Line.gameObject);
            }
        }
    }
}
