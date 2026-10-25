using System;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Four original diagonal strokes confirm observed enemy health loss. Audio
    // is dispatched from Update, never from the repeating OnGUI render passes.
    public sealed class PrototypeHitFeedback : MonoBehaviour, IDisposable
    {
        public const float HitSeconds = .22f;
        public const float KillSeconds = .30f;
        public const float AudioMergeSeconds = .045f;
        private PrototypeScoreSystem scores;
        private PrototypePlayer recipient;
        private PrototypeRuntime runtime;
        private Func<float> clock;
        private float hitUntil, lethalUntil, impactAt, cueAt;
        private bool pendingCue, pendingKill, disposed;
        private long lastDamageSequence, lastAwardSequence;
        public int HitsObserved { get; private set; }
        public int KillsObserved { get; private set; }
        public int CueRequests { get; private set; }
        public PrototypePlayerCue LastCueRequested { get; private set; }
        private float Now => clock != null ? clock() : Time.time;
        public bool Visible => !disposed && Now < Mathf.Max(hitUntil, lethalUntil);
        public bool IsLethal => !disposed && Now < lethalUntil;
        public bool HasPendingCue => pendingCue;
        private bool Active => !disposed && recipient != null && recipient.Alive &&
            (runtime == null || runtime.MatchStarted && !runtime.PauseOpen && !runtime.SupportMenuOpen &&
                !runtime.IsNetworkReplica && !runtime.Match.Winner.HasValue &&
                (runtime.Frontend == null || !runtime.Frontend.IsOpen));

        public void Initialize(PrototypeScoreSystem source, PrototypePlayer player, Func<float> time = null)
        {
            Dispose(); disposed = false; scores = source; recipient = player;
            runtime = player != null ? player.GetComponentInParent<PrototypeRuntime>() : GetComponentInParent<PrototypeRuntime>();
            clock = time ?? (() => Time.time); Reset();
            if (scores == null) return;
            scores.DamageDealt += OnDamage; scores.Awarded += OnAwarded;
        }

        private void OnDamage(PrototypeScoreSystem.DamageEvent damage)
        {
            if (!Active || !ReferenceEquals(damage.Actor, recipient) || damage.Damage <= 0f ||
                damage.Sequence <= lastDamageSequence) return;
            lastDamageSequence = damage.Sequence; HitsObserved++;
            impactAt = Now; hitUntil = Now + HitSeconds;
            QueueCue(false);
        }

        private void OnAwarded(PrototypeScoreSystem.Notice notice)
        {
            if (!Active || !ReferenceEquals(notice.Actor, recipient) || notice.Sequence <= lastAwardSequence ||
                notice.Kind != PrototypeScoreNoticeKind.Kill && notice.Kind != PrototypeScoreNoticeKind.VehicleDestroyed) return;
            lastAwardSequence = notice.Sequence; KillsObserved++;
            impactAt = Now; lethalUntil = Now + KillSeconds;
            QueueCue(true);
        }

        private void QueueCue(bool lethal)
        {
            if (!pendingCue) { cueAt = Now + AudioMergeSeconds; pendingCue = true; }
            pendingKill |= lethal;
        }

        private void Update() => Tick();
        public void Tick()
        {
            if (!Active) { Reset(); return; }
            if (disposed || !pendingCue || Now < cueAt) return;
            PrototypePlayerCue cue = pendingKill ? PrototypePlayerCue.Kill : PrototypePlayerCue.Hit;
            pendingCue = pendingKill = false;
            LastCueRequested = cue; CueRequests++;
            PrototypePlayerAudio.PlayCue(cue);
        }

        public void Draw(float width, float height)
        {
            if (!Active || !Visible) return;
            float now = Now, end = IsLethal ? lethalUntil : hitUntil;
            float alpha = Mathf.Clamp01((end - now) / .09f);
            float pop = Mathf.Exp(-Mathf.Max(0f, now - impactAt) * 18f);
            float gap = 7f + pop * 3f, length = (IsLethal ? 12f : 8.5f) + pop * 2f;
            float thickness = IsLethal ? 2.6f : 2f;
            Vector2 center = new Vector2(width * .5f, height * .5f);
            Matrix4x4 originalMatrix = GUI.matrix; Color originalColor = GUI.color;
            for (int i = 0; i < 4; i++)
            {
                GUI.matrix = StrokeMatrix(originalMatrix, center, 45f + i * 90f);
                GUI.color = new Color(0f, 0f, 0f, alpha * .6f);
                GUI.DrawTexture(new Rect(center.x + gap - 1f, center.y - thickness * .5f - 1f,
                    length + 2f, thickness + 2f), Texture2D.whiteTexture);
                GUI.color = IsLethal ? new Color(1f, .22f, .17f, alpha) : new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(center.x + gap, center.y - thickness * .5f, length, thickness), Texture2D.whiteTexture);
            }
            GUI.matrix = originalMatrix; GUI.color = originalColor;
        }

        private static Matrix4x4 StrokeMatrix(Matrix4x4 original, Vector2 center, float angle) =>
            original * Matrix4x4.Translate(new Vector3(center.x, center.y, 0f)) *
            Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, angle)) *
            Matrix4x4.Translate(new Vector3(-center.x, -center.y, 0f));

        public void Reset()
        {
            hitUntil = lethalUntil = impactAt = cueAt = 0f;
            pendingCue = pendingKill = false;
            lastDamageSequence = lastAwardSequence = 0;
            HitsObserved = KillsObserved = CueRequests = 0;
        }

        public void Dispose()
        {
            if (scores != null) { scores.DamageDealt -= OnDamage; scores.Awarded -= OnAwarded; }
            scores = null; recipient = null; runtime = null; disposed = true; Reset();
        }
        private void OnDisable() => Reset();
        private void OnDestroy() => Dispose();
    }
}
