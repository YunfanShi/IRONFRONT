using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Selects a reachable position physically screened by the existing map geometry.
    // No damage reduction is granted: the cover only works when it blocks line of sight.
    public static class PrototypeTactics
    {
        private readonly struct Candidate
        {
            public readonly Vector2 Position;
            public readonly float Score;
            public Candidate(Vector2 position, float score) { Position = position; Score = score; }
        }

        public static bool TryFindCover(Vector2 from, Vector2 threat, Vector2 objective,
            Vector2 teammate, PrototypeNavigation navigation, out Vector2 cover)
        {
            cover = Vector2.zero;
            var candidates = new List<Candidate>();
            foreach (PrototypeLayout.Block block in PrototypeLayout.Blocks)
            {
                if (Vector2.Distance(from, block.Position) > 42f) continue;
                float x = block.Width * 0.5f + 1.6f;
                float z = block.Depth * 0.5f + 1.6f;
                Consider(block.Position + new Vector2(x, 0f));
                Consider(block.Position + new Vector2(-x, 0f));
                Consider(block.Position + new Vector2(0f, z));
                Consider(block.Position + new Vector2(0f, -z));
            }

            candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
            for (int i = 0; i < Mathf.Min(6, candidates.Count); i++)
            {
                Vector2 candidate = candidates[i].Position;
                if (navigation.Find(from, candidate).Count == 0) continue;
                cover = candidate;
                return true;
            }
            return false;

            void Consider(Vector2 candidate)
            {
                float distance = Vector2.Distance(from, candidate);
                if (distance < 2f || distance > 28f) return;
                if (Vector2.Distance(candidate, teammate) < 3f) return;
                if (PrototypeLayout.Collides(candidate, 0.8f)) return;
                if (!PrototypeLayout.LineBlocked(candidate, threat, 0.1f)) return;
                float score = distance + Vector2.Distance(candidate, objective) * 0.04f;
                candidates.Add(new Candidate(candidate, score));
            }
        }
    }
}
