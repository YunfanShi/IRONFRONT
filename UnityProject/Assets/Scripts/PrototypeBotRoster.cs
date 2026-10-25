using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public static class PrototypeBotRoster
    {
        private static readonly string[] Blue =
            { "Falcon", "Atlas", "Raven", "Scout", "Echo", "Wolf", "Orion", "Hawk" };
        private static readonly string[] Red =
            { "Viper", "Cobra", "Lynx", "Rook", "Flint", "Vega", "Fox", "Shade" };

        public static string NameFor(PrototypeTeam team, int ordinal)
        {
            ordinal = Mathf.Max(0, ordinal);
            string[] names = team == PrototypeTeam.Blue ? Blue : Red;
            return names[ordinal % names.Length] +
                (ordinal < names.Length ? "" : " " + (ordinal / names.Length + 1));
        }
    }

    public readonly struct PrototypeBotDownNotice
    {
        public readonly string Name;
        public readonly PrototypeTeam Team;
        public readonly float ExpiresAt;
        public PrototypeBotDownNotice(string name, PrototypeTeam team, float expiresAt)
        { Name = name; Team = team; ExpiresAt = expiresAt; }
    }

    public sealed class PrototypeBotDownFeed
    {
        public const int Capacity = 4;
        public const float Duration = 8f;
        private readonly List<PrototypeBotDownNotice> entries = new List<PrototypeBotDownNotice>();
        public IReadOnlyList<PrototypeBotDownNotice> Entries => entries;

        public void Add(string name, PrototypeTeam team, float now)
        {
            Expire(now);
            if (string.IsNullOrWhiteSpace(name)) return;
            entries.Insert(0, new PrototypeBotDownNotice(name, team, now + Duration));
            if (entries.Count > Capacity) entries.RemoveAt(entries.Count - 1);
        }

        public void Expire(float now)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
                if (now >= entries[i].ExpiresAt) entries.RemoveAt(i);
        }

        public void Clear() => entries.Clear();
    }
}
