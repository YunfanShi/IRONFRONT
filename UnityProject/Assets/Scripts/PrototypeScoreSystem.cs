using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeScoreNoticeKind
    { Damage, Kill, Assist, VehicleDestroyed, VehicleAssist, Capture, CaptureProgress, Spend, Refund }
    // The host records real combat and objective contributions. Total score survives
    // spending and respawn; Credits is the personal support budget for this match.
    public sealed class PrototypeScoreSystem
    {
        public const int KillPoints = 100;
        public const int VehicleDestroyPoints = 200;
        public const int AssistPoints = 50;
        public const int VehicleAssistPoints = 100;
        public const float HealthPerDamagePoint = 10f;
        public const int CapturePoints = 150;
        public const int CaptureProgressPoints = 20;
        public const float CaptureProgressInterval = 5f;
        public const float AssistWindow = 12f;
        public const float AssistDamageThreshold = 20f;

        public sealed class Entry
        {
            public int Total { get; internal set; }
            public int TotalEarned => Total;
            public int Credits { get; internal set; }
            public int Kills { get; internal set; }
            public int VehiclesDestroyed { get; internal set; }
            public int Assists { get; internal set; }
            public int VehicleAssists { get; internal set; }
            public int DamagePoints { get; internal set; }
            public int Captures { get; internal set; }
            public int CaptureTicks { get; internal set; }
            internal int Spent;
        }

        public readonly struct Notice
        {
            public readonly object Actor;
            public readonly string Reason;
            public readonly int Points;
            public readonly float Time;
            public readonly PrototypeScoreNoticeKind Kind;
            public readonly object Victim;
            public readonly int SettlementPoints;
            public readonly long Sequence;
            public Notice(object actor, string reason, int points, float time,
                PrototypeScoreNoticeKind kind, object victim, int settlementPoints, long sequence)
            { Actor = actor; Reason = reason; Points = points; Time = time; Kind = kind;
                Victim = victim; SettlementPoints = settlementPoints; Sequence = sequence; }
        }

        // Separate from integer scoring: even a one-health glancing hit needs
        // immediate confirmation, and a healed target may exhaust its score budget.
        public readonly struct DamageEvent
        {
            public readonly object Actor, Target;
            public readonly PrototypeTeam SourceTeam;
            public readonly float Damage, Time;
            public readonly long Sequence;
            public DamageEvent(object actor, object target, PrototypeTeam sourceTeam,
                float damage, float time, long sequence)
            { Actor = actor; Target = target; SourceTeam = sourceTeam; Damage = damage; Time = time; Sequence = sequence; }
        }

        private readonly struct DamageSample
        {
            public readonly float Damage;
            public readonly float Time;
            public readonly int Credits;
            public DamageSample(float damage, float time, int credits)
            { Damage = damage; Time = time; Credits = credits; }
        }

        private sealed class Contribution
        {
            public PrototypeTeam Team;
            public float FractionalHealth;
            public readonly Queue<DamageSample> Samples = new Queue<DamageSample>();
            public float RecentDamage(float now)
            {
                while (Samples.Count > 0 && now - Samples.Peek().Time > AssistWindow)
                    Samples.Dequeue();
                float damage = 0f;
                foreach (DamageSample sample in Samples) damage += sample.Damage;
                return damage;
            }
            public int RecentCredits(float now)
            {
                RecentDamage(now);
                int credits = 0;
                foreach (DamageSample sample in Samples) credits += sample.Credits;
                return credits;
            }
        }

        private sealed class VictimLife
        {
            public bool Defeated;
            public float HealthCreditRemaining;
            public readonly Dictionary<object, Contribution> Contributors =
                new Dictionary<object, Contribution>();
        }

        private readonly struct Defeat
        {
            public readonly object Target;
            public readonly PrototypeTeam Team;
            public Defeat(object target, PrototypeTeam team) { Target = target; Team = team; }
        }

        private sealed class DamageContext
        {
            public object Source;
            public PrototypeTeam Team;
            public bool Allowed;
            public readonly List<Defeat> Defeats = new List<Defeat>();
        }

        private readonly PrototypeRuntime runtime;
        private readonly Func<float> clock;
        private readonly Dictionary<object, Entry> entries = new Dictionary<object, Entry>();
        private readonly Dictionary<object, VictimLife> lives = new Dictionary<object, VictimLife>();
        private readonly Dictionary<PrototypeCapturePoint, Dictionary<object, float>> captureTimers =
            new Dictionary<PrototypeCapturePoint, Dictionary<object, float>>();
        private readonly List<Notice> notices = new List<Notice>();
        private readonly Stack<DamageContext> damageContexts = new Stack<DamageContext>();
        private readonly List<object> staleActors = new List<object>();
        private long nextSequence;
        private long nextDamageSequence;
        public IReadOnlyList<Notice> Notices => notices;
        public event Action<Notice> Awarded;
        public event Action<DamageEvent> DamageDealt;

        public PrototypeScoreSystem(PrototypeRuntime game = null, Func<float> time = null)
        { runtime = game; clock = time ?? (() => UnityEngine.Time.time); }

        private bool Authority => runtime == null || !runtime.IsNetworkReplica;
        private bool CanAward => Authority && (runtime == null ||
            (runtime.MatchStarted && runtime.Match != null && !runtime.Match.Winner.HasValue));

        public Entry Get(object actor)
        {
            actor = CanonicalActor(actor);
            if (actor == null) return new Entry();
            if (!entries.TryGetValue(actor, out Entry entry))
                entries.Add(actor, entry = new Entry());
            return entry;
        }

        // Inspecting a scoreboard must not create phantom scoring accounts.
        public bool TryGet(object actor, out Entry entry)
        {
            actor = CanonicalActor(actor);
            entry = null;
            return actor != null && entries.TryGetValue(actor, out entry);
        }

        public void Reset()
        {
            entries.Clear(); lives.Clear(); captureTimers.Clear(); notices.Clear();
            // Reset belongs to a new battle, never to a weapon callback.
            damageContexts.Clear();
        }

        public void ResetCaptureProgress() => captureTimers.Clear();

        public void BeginLife(object actor)
        {
            actor = CanonicalActor(actor);
            if (actor == null) return;
            lives.Remove(actor);
            foreach (Dictionary<object, float> timers in captureTimers.Values) timers.Remove(actor);
            // Personal totals and support credits deliberately survive a new life.
        }

        public bool TrySpend(object actor, int amount, string reason = null)
        {
            actor = CanonicalActor(actor);
            if (!CanAward || actor == null || amount <= 0) return false;
            Entry entry = Get(actor);
            if (entry.Credits < amount) return false;
            entry.Credits -= amount; entry.Spent += amount;
            Publish(actor, -amount, reason ?? "支援调用", PrototypeScoreNoticeKind.Spend, null, 0);
            return true;
        }

        public bool Refund(object actor, int amount, string reason = null)
        {
            actor = CanonicalActor(actor);
            if (actor == null || amount <= 0) return false;
            Entry entry = Get(actor);
            if (entry.Spent < amount) return false;
            entry.Spent -= amount; entry.Credits += amount;
            Publish(actor, amount, reason ?? "支援退款", PrototypeScoreNoticeKind.Refund, null, 0);
            return true;
        }

        // Capture the returned actor when the projectile is launched, so changing
        // seats or leaving a vehicle before impact cannot transfer the reward.
        public static object ActorForShooter(Transform shooter)
        {
            if (shooter == null) return null;
            PrototypePlayer player = shooter.GetComponentInParent<PrototypePlayer>();
            if (player != null) return player;
            PrototypeBot bot = shooter.GetComponentInParent<PrototypeBot>();
            if (bot != null) return bot;
            PrototypeRemotePlayer remote = shooter.GetComponentInParent<PrototypeRemotePlayer>();
            if (remote != null) return remote;
            foreach (MonoBehaviour component in shooter.GetComponentsInParent<MonoBehaviour>())
                if (component is IPrototypeVehicle vehicle)
                    return vehicle.Occupant != null ? (object)vehicle.Occupant :
                        vehicle.Driver != null ? (object)vehicle.Driver : vehicle;
            return null;
        }

        public static object CanonicalActor(object actor)
        {
            if (actor is PrototypePlayer || actor is PrototypeBot ||
                actor is PrototypeRemotePlayer || actor is IPrototypeVehicle) return actor;
            Transform transform = actor is GameObject gameObject ? gameObject.transform :
                actor is Component component ? component.transform : null;
            if (transform == null) return actor is UnityEngine.Object ? null : actor;
            PrototypePlayer player = transform.GetComponentInParent<PrototypePlayer>();
            if (player != null) return player;
            PrototypeBot bot = transform.GetComponentInParent<PrototypeBot>();
            if (bot != null) return bot;
            PrototypeRemotePlayer remote = transform.GetComponentInParent<PrototypeRemotePlayer>();
            if (remote != null) return remote;
            foreach (MonoBehaviour parent in transform.GetComponentsInParent<MonoBehaviour>())
                if (parent is IPrototypeVehicle vehicle) return vehicle;
            return actor;
        }

        public void ApplyDamage(object target, object sourceActor, PrototypeTeam sourceTeam,
            float amount, Action applyActualDamage)
        {
            if (applyActualDamage == null) return;
            target = CanonicalActor(target);
            float before = HealthOf(target);
            if (target != null) Life(target);
            var context = new DamageContext
            {
                Source = CanonicalActor(sourceActor), Team = sourceTeam,
                Allowed = CanAward && IsFinite(amount) && amount > 0f && before > 0f
            };
            damageContexts.Push(context);
            try
            {
                applyActualDamage();
                if (context.Allowed)
                {
                    float actualDamage = Mathf.Max(0f, before - HealthOf(target));
                    RecordDamageInternal(target, context.Source, sourceTeam, actualDamage);
                    // This fallback keeps old vehicle implementations authoritative
                    // even if they have not acquired an explicit death hook yet.
                    if (before > 0f && HealthOf(target) <= 0f && TryTeam(target, out PrototypeTeam team))
                        context.Defeats.Add(new Defeat(target, team));
                    foreach (Defeat defeat in context.Defeats)
                        ResolveNow(defeat.Target, defeat.Team, context.Source, context.Team);
                }
            }
            finally { damageContexts.Pop(); }
        }

        public void RecordDamage(object target, object sourceActor, PrototypeTeam sourceTeam,
            float actualDamage)
        {
            if (!CanAward) return;
            RecordDamageInternal(CanonicalActor(target), CanonicalActor(sourceActor), sourceTeam,
                actualDamage);
        }

        private void RecordDamageInternal(object target, object source, PrototypeTeam sourceTeam,
            float damage)
        {
            if (target == null || source == null || ReferenceEquals(target, source) ||
                !IsFinite(damage) || damage <= 0f || !TryTeam(target, out PrototypeTeam targetTeam) ||
                targetTeam == sourceTeam) return;
            VictimLife life = Life(target);
            if (life.Defeated) return;
            if (!life.Contributors.TryGetValue(source, out Contribution contribution))
                life.Contributors.Add(source, contribution = new Contribution { Team = sourceTeam });
            float now = clock();
            DamageDealt?.Invoke(new DamageEvent(source, target, sourceTeam, damage, now, ++nextDamageSequence));
            contribution.RecentDamage(now);
            // Repaired health can still qualify a real recent assist, but only
            // one maximum-health budget per victim life can mint damage credits.
            float eligible = Mathf.Min(damage, life.HealthCreditRemaining);
            life.HealthCreditRemaining = Mathf.Max(0f, life.HealthCreditRemaining - eligible);
            contribution.FractionalHealth += eligible;
            int points = Mathf.FloorToInt((contribution.FractionalHealth + .0001f) / HealthPerDamagePoint);
            contribution.FractionalHealth = Mathf.Max(0f, contribution.FractionalHealth - points * HealthPerDamagePoint);
            int credited = points > 0 ? Award(source, points, "伤害", PrototypeScoreNoticeKind.Damage, target) : 0;
            Get(source).DamagePoints += credited;
            contribution.Samples.Enqueue(new DamageSample(damage, now, credited));
        }

        public void ResolveDefeat(object target, PrototypeTeam targetTeam)
        {
            target = CanonicalActor(target);
            if (target == null || HealthOf(target) > 0f) return;
            if (damageContexts.Count > 0)
            {
                DamageContext context = damageContexts.Peek();
                if (context.Allowed) context.Defeats.Add(new Defeat(target, targetTeam));
                return;
            }
            if (CanAward) ResolveNow(target, targetTeam, null, targetTeam);
        }

        private void ResolveNow(object target, PrototypeTeam targetTeam, object killer,
            PrototypeTeam killerTeam)
        {
            VictimLife life = Life(target);
            if (life.Defeated) return;
            life.Defeated = true;
            float now = clock();
            bool validKiller = killer != null && !ReferenceEquals(killer, target) && killerTeam != targetTeam;
            if (validKiller)
            {
                if (target is IPrototypeVehicle)
                {
                    Get(killer).VehiclesDestroyed++;
                    int previous = life.Contributors.TryGetValue(killer, out Contribution own) ? own.RecentCredits(now) : 0;
                    Award(killer, Mathf.Max(0, VehicleDestroyPoints - previous), "摧毁载具",
                        PrototypeScoreNoticeKind.VehicleDestroyed, target, VehicleDestroyPoints);
                }
                else
                {
                    Get(killer).Kills++;
                    int previous = life.Contributors.TryGetValue(killer, out Contribution own) ? own.RecentCredits(now) : 0;
                    Award(killer, Mathf.Max(0, KillPoints - previous), "击杀",
                        PrototypeScoreNoticeKind.Kill, target, KillPoints);
                }
            }
            foreach (KeyValuePair<object, Contribution> pair in life.Contributors)
            {
                if ((validKiller && ReferenceEquals(pair.Key, killer)) || pair.Value.Team == targetTeam ||
                    pair.Value.RecentDamage(now) < AssistDamageThreshold) continue;
                if (target is IPrototypeVehicle)
                {
                    Get(pair.Key).VehicleAssists++;
                    Award(pair.Key, VehicleAssistPoints, "载具助攻", PrototypeScoreNoticeKind.VehicleAssist, target, VehicleAssistPoints);
                }
                else
                {
                    Get(pair.Key).Assists++;
                    Award(pair.Key, AssistPoints, "助攻", PrototypeScoreNoticeKind.Assist, target, AssistPoints);
                }
            }
            life.Contributors.Clear();
        }

        public void RecordCaptureProgress(PrototypeCapturePoint point, PrototypeTeam team,
            IList<object> contributors, float seconds, bool captured)
        {
            if (!CanAward || point == null || !IsFinite(seconds) || seconds <= 0f ||
                contributors == null || contributors.Count == 0 ||
                point.Contested)
            { ClearCaptureProgress(point); return; }
            if (!captureTimers.TryGetValue(point, out Dictionary<object, float> timers))
                captureTimers.Add(point, timers = new Dictionary<object, float>());
            staleActors.Clear();
            foreach (object actor in timers.Keys)
                if (!ContainsActor(contributors, actor)) staleActors.Add(actor);
            foreach (object actor in staleActors) timers.Remove(actor);
            foreach (object contributor in contributors)
            {
                object actor = CanonicalActor(contributor);
                if (actor == null || HealthOf(actor) <= 0f || !TryTeam(actor, out PrototypeTeam actorTeam) ||
                    actorTeam != team) continue;
                timers.TryGetValue(actor, out float elapsed);
                elapsed += seconds;
                while (elapsed + .0001f >= CaptureProgressInterval)
                {
                    elapsed -= CaptureProgressInterval;
                    Get(actor).CaptureTicks++;
                    Award(actor, CaptureProgressPoints, "占领推进", PrototypeScoreNoticeKind.CaptureProgress, point);
                }
                timers[actor] = Mathf.Max(0f, elapsed);
                if (captured)
                {
                    Get(actor).Captures++;
                    Award(actor, CapturePoints, "占领 " + point.Definition.Id, PrototypeScoreNoticeKind.Capture, point);
                }
            }
            if (captured) captureTimers.Remove(point);
        }

        public void ClearCaptureProgress(PrototypeCapturePoint point)
        { if (point != null) captureTimers.Remove(point); }

        private static bool ContainsActor(IList<object> actors, object target)
        {
            foreach (object actor in actors)
                if (ReferenceEquals(CanonicalActor(actor), target)) return true;
            return false;
        }

        private int Award(object actor, int points, string reason, PrototypeScoreNoticeKind kind,
            object victim = null, int settlementPoints = 0)
        {
            Entry entry = Get(actor);
            int reward = Math.Min(points, int.MaxValue - entry.Total);
            entry.Total += reward; entry.Credits += reward;
            Publish(actor, reward, reason, kind, victim, settlementPoints);
            return reward;
        }

        private void Publish(object actor, int points, string reason, PrototypeScoreNoticeKind kind,
            object victim, int settlementPoints)
        {
            var notice = new Notice(actor, reason, points, clock(), kind, victim, settlementPoints, ++nextSequence);
            notices.Insert(0, notice);
            if (notices.Count > 12) notices.RemoveAt(notices.Count - 1);
            Awarded?.Invoke(notice);
        }

        private VictimLife Life(object target)
        {
            if (!lives.TryGetValue(target, out VictimLife life))
                lives.Add(target, life = new VictimLife { HealthCreditRemaining = MaximumHealthOf(target) });
            return life;
        }

        private static float HealthOf(object actor) =>
            actor is PrototypePlayer player ? player.Health :
            actor is PrototypeBot bot ? bot.Health :
            actor is PrototypeRemotePlayer remote ? remote.Health :
            actor is IPrototypeVehicle vehicle ? vehicle.Health : 0f;

        private static float MaximumHealthOf(object actor) =>
            actor is IPrototypeArmoredVehicle vehicle ? vehicle.MaximumHealth :
            actor is PrototypePlayer || actor is PrototypeBot || actor is PrototypeRemotePlayer ? 100f : HealthOf(actor);

        private static bool TryTeam(object actor, out PrototypeTeam team)
        {
            if (actor is PrototypePlayer player) { team = player.Team; return true; }
            if (actor is PrototypeBot bot) { team = bot.Team; return true; }
            if (actor is PrototypeRemotePlayer remote) { team = remote.Team; return true; }
            if (actor is IPrototypeVehicle vehicle) { team = vehicle.Team; return true; }
            team = default; return false;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
