using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeMission { Capture, Defend, Reinforce }

    public sealed class PrototypeSquad
    {
        public readonly int Id;
        public readonly PrototypeTeam Team;
        public readonly PrototypeBot[] Members;
        public string ObjectiveId { get; internal set; }
        public PrototypeMission Mission { get; internal set; }
        public string Reason { get; internal set; }
        public Vector2 LastKnownEnemy { get; private set; }
        public float LastSeenAt { get; private set; } = -100f;
        internal float AssignedAt;
        internal readonly Dictionary<string, float> BlockedUntil = new Dictionary<string, float>();

        public PrototypeSquad(int id, PrototypeTeam team, PrototypeBot first, PrototypeBot second)
        {
            Id = id;
            Team = team;
            Members = new[] { first, second };
            ObjectiveId = "-";
            Reason = "Awaiting orders";
        }

        public int LivingMembers
        {
            get
            {
                int count = 0;
                foreach (PrototypeBot bot in Members) if (bot.Alive) count++;
                return count;
            }
        }

        public Vector2 Position
        {
            get
            {
                Vector2 total = Vector2.zero;
                int count = 0;
                foreach (PrototypeBot bot in Members)
                    if (bot.Alive) { total += bot.MapPosition; count++; }
                return count > 0 ? total / count : Members[0].SpawnPosition;
            }
        }

        public void ReportContact(Vector2 position, float now)
        {
            LastKnownEnemy = position;
            LastSeenAt = now;
        }

        public bool TryRecentContact(float now, out Vector2 position)
        {
            position = LastKnownEnemy;
            return now - LastSeenAt < 7f;
        }

        public void ClearContact() { LastSeenAt = -100f; }
    }

    // Receives value-only observations from friendly soldiers. Enemy objects never enter this class.
    public sealed class PrototypeCommander
    {
        private struct Contact
        {
            public Vector2 Position;
            public float SeenAt;
        }

        private readonly List<Contact> contacts = new List<Contact>();
        private readonly List<PrototypeSquad> squads = new List<PrototypeSquad>();
        private readonly PrototypeTeam team;
        private float nextDecision;

        public IReadOnlyList<PrototypeSquad> Squads => squads;

        public PrototypeCommander(PrototypeTeam team) { this.team = team; }

        public PrototypeSquad Register(PrototypeBot first, PrototypeBot second)
        {
            var squad = new PrototypeSquad(squads.Count + 1, team, first, second);
            squads.Add(squad);
            first.JoinSquad(this, squad, 0);
            second.JoinSquad(this, squad, 1);
            return squad;
        }

        public void ReportEnemy(Vector2 position, float now)
        {
            // Nearby repeated sightings refresh one contact instead of growing the report list every frame.
            for (int i = 0; i < contacts.Count; i++)
                if (Vector2.Distance(contacts[i].Position, position) < 14f)
                {
                    contacts[i] = new Contact { Position = position, SeenAt = now };
                    return;
                }
            if (contacts.Count == 16) contacts.RemoveAt(0);
            contacts.Add(new Contact { Position = position, SeenAt = now });
        }

        public void ReportRouteFailure(PrototypeSquad squad, float now)
        {
            if (squad == null || squad.Team != team || squad.ObjectiveId == "-") return;
            squad.BlockedUntil[squad.ObjectiveId] = now + 18f;
            nextDecision = 0f;
        }

        public void Tick(float now, PrototypeMatch match)
        {
            if (match.Winner.HasValue || now < nextDecision) return;
            nextDecision = now + 4f;
            for (int i = contacts.Count - 1; i >= 0; i--)
                if (now - contacts[i].SeenAt > 24f) contacts.RemoveAt(i);

            var assigned = new int[match.Points.Count];
            foreach (PrototypeSquad squad in squads)
            {
                PrototypeCapturePoint previous = FindPoint(match, squad.ObjectiveId);
                bool blocked = previous != null && IsBlocked(squad, previous.Definition.Id, now);
                bool urgent = previous != null && previous.Owner == team && previous.Contested;
                PrototypeCapturePoint best = null;
                float bestScore = float.NegativeInfinity;
                for (int i = 0; i < match.Points.Count; i++)
                {
                    PrototypeCapturePoint point = match.Points[i];
                    bool own = point.Owner == team;
                    float need = own ? (point.Contested ? 8f : -1.5f) :
                        point.Owner.HasValue ? 6f : 4.5f;
                    float score = need - Vector2.Distance(squad.Position, point.Definition.Position) * 0.012f;
                    score -= assigned[i] * 3f;
                    if (point == previous) score += 1.25f;
                    if (IsBlocked(squad, point.Definition.Id, now))
                        score -= 12f;
                    float pressure = KnownPressure(point.Definition.Position, now);
                    if (own) score += pressure * 1.5f;
                    else score -= pressure * 0.25f;
                    if (squad.LivingMembers < 2 && own) score += 1.5f;
                    if (score <= bestScore) continue;
                    best = point;
                    bestScore = score;
                }

                // Keep a valid order for a short window; actual pressure or a failed route can interrupt it.
                if (previous != null && now - squad.AssignedAt < 12f && !blocked && !urgent)
                    best = previous;
                if (best == null) continue;
                PrototypeMission mission = best.Owner == team ?
                    (best.Contested ? PrototypeMission.Reinforce : PrototypeMission.Defend) :
                    PrototypeMission.Capture;
                bool changed = squad.ObjectiveId != best.Definition.Id || squad.Mission != mission;
                if (changed)
                {
                    squad.ObjectiveId = best.Definition.Id;
                    squad.Mission = mission;
                    squad.AssignedAt = now;
                    squad.Reason = blocked ? "Route blocked; redirected" :
                        mission == PrototypeMission.Reinforce ? "Friendly point contested" :
                        mission == PrototypeMission.Defend ? "Hold captured point" :
                        "Capture for ticket advantage";
                }
                for (int member = 0; member < squad.Members.Length; member++)
                    squad.Members[member].SetObjective(
                        Slot(best.Definition.Position, squad.Id, member), best.Definition.Id);
                assigned[match.Points.IndexOf(best)]++;
            }
        }

        private Vector2 Slot(Vector2 center, int squadId, int member)
        {
            Vector2 basePoint = team == PrototypeTeam.Blue ? PrototypeLayout.BlueBase : PrototypeLayout.RedBase;
            Vector2 forward = (center - basePoint).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x);
            Vector2 candidate = center + side * ((member == 0 ? -1f : 1f) * (3f + squadId * 2f)) -
                forward * (squadId * 2f);
            return PrototypeLayout.Collides(candidate, 0.8f) ? center : candidate;
        }

        private float KnownPressure(Vector2 objective, float now)
        {
            float pressure = 0f;
            foreach (Contact contact in contacts)
                if (Vector2.Distance(contact.Position, objective) < 60f)
                    pressure += Mathf.Clamp01(1f - (now - contact.SeenAt) / 24f);
            return Mathf.Min(2f, pressure);
        }

        private static bool IsBlocked(PrototypeSquad squad, string id, float now) =>
            squad.BlockedUntil.TryGetValue(id, out float until) && now < until;

        private static PrototypeCapturePoint FindPoint(PrototypeMatch match, string id)
        {
            foreach (PrototypeCapturePoint point in match.Points)
                if (point.Definition.Id == id) return point;
            return null;
        }
    }
}
