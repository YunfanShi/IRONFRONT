using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Port of the browser game's 16 m pedestrian navigation grid.
    public sealed class PrototypeNavigation
    {
        private const int Step = 16;
        private const int Count = 45;
        private const int CellCount = Count * Count;
        private const float Clearance = 1.12f;
        private readonly bool[] walkable = new bool[CellCount];
        private readonly Dictionary<long, bool> edgeCache = new Dictionary<long, bool>();
        private static readonly int[] Dx = { 1, -1, 0, 0, 1, -1, 1, -1 };
        private static readonly int[] Dz = { 0, 0, 1, -1, 1, 1, -1, -1 };

        private readonly struct OpenNode
        {
            public readonly int Index;
            public readonly float Score;
            public OpenNode(int index, float score) { Index = index; Score = score; }
        }

        public PrototypeNavigation()
        {
            for (int i = 0; i < CellCount; i++)
                walkable[i] = !PrototypeLayout.Collides(Point(i), Clearance);
        }

        private static Vector2 Point(int index) =>
            new Vector2(((index % Count) + 0.5f) * Step - 360f,
                        ((index / Count) + 0.5f) * Step - 360f);

        private static bool Clear(Vector2 a, Vector2 b) =>
            !PrototypeLayout.Collides(a, Clearance) &&
            !PrototypeLayout.Collides(b, Clearance) &&
            !PrototypeLayout.LineBlocked(a, b, Clearance);

        private int VisibleNode(Vector2 from)
        {
            int nearest = -1;
            float nearestDistance = 80f * 80f;
            for (int i = 0; i < CellCount; i++)
            {
                if (!walkable[i]) continue;
                Vector2 point = Point(i);
                float distance = (point - from).sqrMagnitude;
                if (distance >= nearestDistance || !Clear(from, point)) continue;
                nearest = i;
                nearestDistance = distance;
            }
            return nearest;
        }

        private bool EdgeClear(int a, int b)
        {
            long key = (long)Mathf.Min(a, b) * CellCount + Mathf.Max(a, b);
            if (edgeCache.TryGetValue(key, out bool cached)) return cached;
            bool clear = Clear(Point(a), Point(b));
            edgeCache[key] = clear;
            return clear;
        }

        private static float Heuristic(int a, int b) =>
            Mathf.Sqrt(Mathf.Pow(a % Count - b % Count, 2f) +
                       Mathf.Pow(a / Count - b / Count, 2f));

        public List<Vector2> Find(Vector2 from, Vector2 to)
        {
            if (Clear(from, to)) return new List<Vector2> { to };
            int start = VisibleNode(from);
            int goal = VisibleNode(to);
            if (start < 0 || goal < 0) return new List<Vector2>();

            var cost = new float[CellCount];
            var prior = new int[CellCount];
            var closed = new bool[CellCount];
            for (int i = 0; i < CellCount; i++) { cost[i] = float.PositiveInfinity; prior[i] = -1; }
            cost[start] = 0f;
            var open = new List<OpenNode> { new OpenNode(start, Heuristic(start, goal)) };

            int scanned = 0;
            while (open.Count > 0 && scanned++ < 2200)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++)
                    if (open[i].Score < open[best].Score) best = i;
                int current = open[best].Index;
                open.RemoveAt(best);
                if (closed[current]) continue;
                closed[current] = true;
                if (current == goal) break;

                int cx = current % Count;
                int cz = current / Count;
                for (int i = 0; i < Dx.Length; i++)
                {
                    int nx = cx + Dx[i];
                    int nz = cz + Dz[i];
                    if (nx < 0 || nz < 0 || nx >= Count || nz >= Count) continue;
                    int next = nz * Count + nx;
                    if (!walkable[next] || closed[next] || !EdgeClear(current, next)) continue;
                    if (Dx[i] != 0 && Dz[i] != 0 &&
                        (!walkable[cz * Count + nx] || !walkable[nz * Count + cx])) continue;
                    float alternative = cost[current] + new Vector2(Dx[i], Dz[i]).magnitude;
                    if (alternative >= cost[next]) continue;
                    cost[next] = alternative;
                    prior[next] = current;
                    open.Add(new OpenNode(next, alternative + Heuristic(next, goal)));
                }
            }

            if (start != goal && prior[goal] < 0) return new List<Vector2>();
            var raw = new List<Vector2>();
            for (int at = goal; at != start && at >= 0; at = prior[at]) raw.Add(Point(at));
            raw.Reverse();
            raw.Insert(0, Point(start));
            raw.Add(to);

            var result = new List<Vector2>();
            Vector2 position = from;
            for (int index = 0; index < raw.Count;)
            {
                int furthest = -1;
                for (int candidate = raw.Count - 1; candidate >= index; candidate--)
                    if (Clear(position, raw[candidate])) { furthest = candidate; break; }
                if (furthest < 0) return new List<Vector2>();
                position = raw[furthest];
                result.Add(position);
                index = furthest + 1;
            }
            return result;
        }
    }
}
